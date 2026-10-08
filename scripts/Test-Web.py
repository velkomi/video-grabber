"""Repeatable public UI smoke/E2E. No login, payment, download or real customer job."""
import argparse
from datetime import datetime, timezone
import functools
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import threading
import sys
import time
import urllib.error
import urllib.request
from urllib.parse import urlsplit
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
WWW = ROOT / 'src/VideoGrabber.Platform.Api/wwwroot'

class Preview(SimpleHTTPRequestHandler):
    def do_GET(self):
        if self.path.startswith('/v1/'):
            self.send_response(503); self.send_header('Content-Type', 'application/json'); self.end_headers()
            self.wfile.write(b'{"code":"preview_backend_unavailable"}')
        else:
            super().do_GET()
    def do_POST(self):
        self.send_error(405)
    def log_message(self, *_):
        pass

class FixtureServer(ThreadingHTTPServer):
    def handle_error(self, request, client_address):
        if isinstance(sys.exception(), (ConnectionResetError, BrokenPipeError)):
            return  # A fresh browser closing its keep-alive connection is normal fixture teardown.
        super().handle_error(request, client_address)

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args):
        return None

def check_diagnostics(data, dropped):
    assert data, 'No diagnostics were captured'
    assert dropped == 0, 'Diagnostics coverage is incomplete'
    assert all(e.get('schemaVersion') == 1 for e in data)
    assert not any((e['event'].startswith('javascript.') or e['event']=='resource.error')
                   and e['outcome']=='failed' for e in data), 'Browser or resource failure'

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mode', choices=['smoke', 'e2e', 'all'], default='smoke')
    parser.add_argument('--base-url', default='http://127.0.0.1:8892')
    parser.add_argument('--serve', action='store_true', help='Start isolated static fixture on a free loopback port')
    parser.add_argument('--health', action='store_true', help='Read real API readiness, explicitly separate from UI fixture')
    parser.add_argument('--allow-remote-smoke', action='store_true')
    parser.add_argument('--edge-driver', help='Existing driver path; no driver downloads are initiated')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    server = None
    if args.serve:
        if args.health:
            parser.error('--health cannot check a static fixture')
        server = FixtureServer(('127.0.0.1', 0), functools.partial(Preview, directory=str(WWW)))
        threading.Thread(target=server.serve_forever, daemon=True).start()
        args.base_url = f'http://127.0.0.1:{server.server_port}'
    target = urlsplit(args.base_url)
    if target.scheme not in ('http', 'https') or target.username or target.password or target.query or target.fragment or target.path not in ('', '/'):
        parser.error('Use a plain origin without credentials, path, query or fragment')
    local = target.hostname in ('localhost', '127.0.0.1', '::1')
    if not local and (args.mode != 'smoke' or not args.allow_remote_smoke):
        parser.error('Remote targets require --mode smoke --allow-remote-smoke. E2E is loopback only.')
    run_id = uuid.uuid4().hex
    out = args.output or ROOT / 'artifacts/qa' / (datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ') + '-' + run_id[:8])
    out.mkdir(parents=True, exist_ok=False)
    trace = out / 'steps.jsonl'
    results = []

    def emit(name, outcome, **fields):
        event = dict(schemaVersion=1, timestamp=datetime.now(timezone.utc).isoformat(), service='e2e',
                     environment='isolated-static' if args.serve else 'local' if local else 'remote-readonly',
                     revision='UNKNOWN', event=name, outcome=outcome, traceId=run_id, **fields)
        with trace.open('a', encoding='utf-8') as stream:
            stream.write(json.dumps(event, ensure_ascii=False) + '\n')

    def step(name, action):
        emit(name, 'started')
        start = time.monotonic()
        try:
            action()
            result = dict(name=name, status='passed', durationMs=round((time.monotonic()-start)*1000))
            emit(name, 'succeeded', durationMs=result['durationMs'])
        except Exception as error:
            result = dict(name=name, status='failed', durationMs=round((time.monotonic()-start)*1000), errorType=type(error).__name__)
            if driver:
                driver.save_screenshot(str(out/('failed-'+name+'.png')))
                if name.startswith(('e2e.controls.','e2e.responsive_scenes.')):
                    result['observed'] = driver.execute_script("return {scene:document.querySelector('#hero-three').dataset.storyState,clipped:document.querySelector('#hero-three').dataset.controlsClipped,geometryClipped:document.querySelector('#hero-three').dataset.geometryClipped,visible:[...document.querySelectorAll('.scene-control')].filter(n=>!n.hidden).map(n=>n.dataset.feature)}")
            emit(name, 'failed', durationMs=result['durationMs'], errorType=result['errorType'])
        results.append(result)
        return result['status'] == 'passed'

    opener = urllib.request.build_opener(NoRedirect)
    def smoke(path, expected, marker=None):
        with opener.open(args.base_url.rstrip('/') + path, timeout=15) as response:
            assert response.status == expected
            content = response.read(1_048_577)
            assert len(content) <= 1_048_576
            if marker:
                assert marker.encode() in content
    driver = None
    try:
        if args.mode in ('smoke', 'all'):
            for path, marker in [('/web/', 'diagnostics.js'), ('/web/styles.css', '.pricing-grid'),
                                 ('/web/app.js', 'setupStoryStage'), ('/web/diagnostics.js', 'VGDiagnostics'),
                                 ('/web/hero-three.bundle.js', 'videograbber:studio-model')]:
                step('smoke.' + path.split('/')[-1].replace('.', '_') if path != '/web/' else 'smoke.html',
                     lambda p=path, m=marker: smoke(p, 200, m))
            if args.health:
                def health():
                    with opener.open(args.base_url.rstrip('/') + '/health/ready', timeout=15) as response:
                        assert response.status == 200
                        payload = json.loads(response.read(65536))
                        assert payload.get('status') == 'ready'
                step('smoke.api_ready', health)
        if args.mode in ('e2e', 'all'):
            def start_browser():
                nonlocal driver
                if not args.edge_driver or not Path(args.edge_driver).is_file():
                    raise FileNotFoundError('Existing Edge driver is required')
                from selenium import webdriver
                from selenium.webdriver.edge.service import Service
                options = webdriver.EdgeOptions()
                options.add_argument('--headless=new'); options.add_argument('--window-size=1440,900'); options.add_argument('--no-first-run')
                driver = webdriver.Edge(service=Service(args.edge_driver), options=options)
                driver.execute_cdp_cmd('Network.enable', {})
                # The tested controls stay on loopback; external HTTPS and download redirects are blocked.
                driver.execute_cdp_cmd('Network.setBlockedURLs', {'urls':['https://*']})
                driver.execute_cdp_cmd('Emulation.setDeviceMetricsOverride', {'width':1440,'height':900,'deviceScaleFactor':1,'mobile':False})
                driver.execute_cdp_cmd('Emulation.setEmulatedMedia', {'features':[{'name':'prefers-reduced-motion','value':'no-preference'}]})
            if step('e2e.browser', start_browser):
                from selenium.webdriver.support.ui import WebDriverWait
                wait = WebDriverWait(driver, 20)
                def loaded():
                    driver.get(args.base_url.rstrip('/') + '/web/')
                    wait.until(lambda d: d.execute_script("return document.querySelector('#hero-visual').classList.contains('three-ready')"))
                    assert driver.execute_script('return !!window.VGDiagnostics')
                    assert driver.execute_script("return document.fonts.check('700 18px Manrope','Ссылка MP4') && document.fonts.check('500 15px Onest','Подробнее') && document.querySelector('#hero-three').dataset.geometryClipped==='0'")
                    assert driver.execute_script("return ['Manrope','Onest'].every(name=>[...document.fonts].some(face=>face.family.replaceAll(String.fromCharCode(34),'').replaceAll(String.fromCharCode(39),'')===name&&face.status==='loaded'))")
                    driver.save_screenshot(str(out/'hero.png'))
                step('e2e.hero', loaded)
                def mesh_click(button):
                    x=float(button.get_attribute('data-mesh-x')); y=float(button.get_attribute('data-mesh-y'))
                    assert driver.execute_script("return !document.elementFromPoint(arguments[0],arguments[1]).closest('button,a')",x,y)
                    for event in ['mouseMoved','mousePressed','mouseReleased']:
                        driver.execute_cdp_cmd('Input.dispatchMouseEvent',{'type':event,'x':x,'y':y,
                            'button':'none' if event=='mouseMoved' else 'left','clickCount':1})
                def guide(button, feature, close_mode='escape', mesh=False, keyboard=False):
                    if mesh:
                        mesh_click(button)
                    elif keyboard:
                        from selenium.webdriver.common.keys import Keys
                        button.send_keys(Keys.ENTER)
                    else:
                        button.click()
                    wait.until(lambda d: d.execute_script("return document.querySelector('#info-dialog').open"))
                    key=button.get_attribute('data-feature') or button.get_attribute('data-info')
                    expected={'workflow-url':'Шаг 1. Скопируйте ссылку','link':'Шаг 1. Скопируйте ссылку',
                        'workflow-format':'Шаг 2. Выберите формат','format':'Шаг 2. Выберите формат',
                        'workflow-download':'Шаг 3. Сохраните файл','file':'Шаг 3. Сохраните файл',
                        'device-windows':'VideoGrabber для Windows','windows-app':'VideoGrabber для Windows',
                        'device-web':'Один аккаунт — везде','device-telegram':'VideoGrabber в Telegram'}
                    assert driver.find_element('id','info-dialog-title').text==expected[key]
                    content=driver.find_element('id','info-dialog-content')
                    assert len(content.find_elements('css selector','section'))>=3
                    assert len(content.text)>200
                    assert 'VPS' not in content.text and 'Managed' not in content.text
                    if key in ('workflow-format','device-telegram'):
                        width=driver.execute_script('return innerWidth')
                        driver.save_screenshot(str(out/f'guide-{width}-{key}.png'))
                    if close_mode=='escape':
                        from selenium.webdriver.common.keys import Keys
                        driver.switch_to.active_element.send_keys(Keys.ESCAPE)
                    else:
                        driver.find_element('id','info-dialog-'+close_mode).click()
                    wait.until(lambda d: not d.execute_script("return document.querySelector('#info-dialog').open"))
                    assert driver.execute_script('return document.activeElement===arguments[0]',button)
                for scene, anchor in [('workflow','how'), ('sync','app'), ('pricing','pricing'), ('windows','download')]:
                    def navigate(s=scene, a=anchor):
                        driver.find_element('css selector', f'.topbar nav a[href="#{a}"]').click()
                        wait.until(lambda d: d.execute_script("return document.querySelector('#hero-three').dataset.storyState===arguments[0] && document.querySelector('#hero-three').dataset.transition==='stable'", s))
                        assert driver.execute_script("return document.querySelector('#'+arguments[0]).getBoundingClientRect().top < innerHeight", a)
                        driver.save_screenshot(str(out/(s+'.png')))
                    step('e2e.navigate.'+scene, navigate)
                    def scene_controls(s=scene):
                        controls=driver.find_elements('css selector',f'.scene-control[data-scene="{s}"]')
                        expected={'workflow':3,'sync':3,'pricing':4,'windows':1}[s]
                        assert sum(c.is_displayed() for c in controls)==expected
                        assert driver.execute_script("return document.querySelector('#hero-three').dataset.controlsClipped==='0' && document.querySelector('#hero-three').dataset.geometryClipped==='0'")
                        if s!='pricing':
                            for index,control in enumerate(controls):
                                guide(control,control.get_attribute('data-feature'),['x','escape','close'][index%3])
                        else:
                            for control in controls:
                                control.click()
                                wait.until(lambda d:d.execute_script("return document.querySelector('#plan-dialog').open"))
                                plan=control.get_attribute('data-feature').removeprefix('pricing-')
                                assert driver.find_element('id','plan-dialog-title').text=={'free':'Free','start':'Start','unlimited_video':'Unlimited Video','full_course':'Full Course'}[plan]
                                driver.find_element('id','plan-dialog-x').click()
                    step('e2e.controls.'+scene,scene_controls)
                    if scene in ('workflow','sync','windows'):
                        step('e2e.mesh.'+scene,lambda s=scene:guide(driver.find_element('css selector',f'.scene-control[data-scene="{s}"]'),s,mesh=True))
                    else:
                        def mesh_plan():
                            mesh_click(driver.find_element('css selector','.scene-control[data-feature="pricing-full_course"]'))
                            wait.until(lambda d:d.execute_script("return document.querySelector('#plan-dialog').open"))
                            assert driver.find_element('id','plan-dialog-title').text=='Full Course'
                            driver.find_element('id','plan-dialog-x').click()
                        step('e2e.mesh.pricing',mesh_plan)
                for plan, title, amount in [('free','Free','0'),('start','Start','1 500'),('unlimited_video','Unlimited Video','2 500'),('full_course','Full Course','5 000')]:
                    def dialog(p=plan, t=title, price=amount):
                        driver.find_element('css selector', f'.price-card[data-plan="{p}"] .plan-action').click()
                        wait.until(lambda d: d.execute_script("return document.querySelector('#plan-dialog').open"))
                        assert driver.find_element('id','plan-dialog-title').text == t
                        assert price in driver.find_element('id','plan-dialog-price').text
                        from selenium.webdriver.common.keys import Keys
                        driver.switch_to.active_element.send_keys(Keys.ESCAPE)
                        wait.until(lambda d: not d.execute_script("return document.querySelector('#plan-dialog').open"))
                        assert driver.execute_script("return document.activeElement.matches('.plan-action')")
                    step('e2e.plan.'+plan, dialog)
                def invalid_email():
                    field = driver.find_element('id','login-email')
                    field.send_keys('invalid-address')
                    assert not driver.execute_script('return arguments[0].checkValidity()', field)
                    field.clear()
                step('e2e.email.validation', invalid_email)
                def circles():
                    driver.find_element('css selector','.topbar nav a[href="#how"]').click()
                    wait.until(lambda d:d.execute_script("return document.querySelector('#hero-three').dataset.storyState==='workflow' && document.querySelector('#hero-three').dataset.transition==='stable'"))
                    for index,button in enumerate(driver.find_elements('css selector','.step-icon[data-info]')):
                        guide(button,button.get_attribute('data-info'),'x',keyboard=index==0)
                step('e2e.workflow_circles',circles)
                if args.serve:
                    def local_google():
                        driver.find_element('id','header-login').click()
                        wait.until(lambda d:d.execute_script("return document.querySelector('#hero-three').dataset.storyState==='sync' && document.querySelector('#hero-three').dataset.transition==='stable'"))
                        driver.find_element('id','google-login').click()
                        wait.until(lambda d:'локальный просмотр' in d.find_element('id','auth-status').text)
                        assert driver.find_element('id','auth-preview-link').is_displayed()
                        assert urlsplit(driver.current_url).hostname=='127.0.0.1'
                        driver.save_screenshot(str(out/'preview-google-message.png'))
                    step('e2e.preview_google_reason',local_google)
                def desktop_diagnostics():
                    data = driver.execute_script('return window.VGDiagnostics.snapshot()')
                    check_diagnostics(data, driver.execute_script('return window.VGDiagnostics.dropped'))
                    assert any(e['event']=='ui.action' and e.get('action')=='plan-full_course' for e in data)
                    assert any(e['event']=='scene.state' and e.get('state')=='pricing' for e in data)
                    assert driver.execute_script("return [...document.querySelectorAll('img')].filter(i=>i.getBoundingClientRect().top<innerHeight&&i.getBoundingClientRect().bottom>0).every(i=>i.complete&&i.naturalWidth>0)")
                    (out/'browser-desktop.jsonl').write_text(''.join(json.dumps(e,ensure_ascii=False)+'\n' for e in data),encoding='utf-8')
                step('e2e.desktop_diagnostics', desktop_diagnostics)
                def mobile():
                    driver.execute_cdp_cmd('Emulation.setDeviceMetricsOverride', {'width':390,'height':844,'deviceScaleFactor':1,'mobile':False})
                    driver.get(args.base_url.rstrip('/') + '/web/')
                    wait.until(lambda d: d.execute_script("return document.querySelector('#hero-visual').classList.contains('three-ready')"))
                    assert driver.execute_script('return document.documentElement.scrollWidth===document.documentElement.clientWidth')
                    driver.save_screenshot(str(out/'mobile.png'))
                step('e2e.mobile', mobile)
                def responsive_scenes(width):
                    driver.execute_cdp_cmd('Emulation.setDeviceMetricsOverride',{'width':width,'height':844,'deviceScaleFactor':1,'mobile':False})
                    for scene,anchor in [('workflow','how'),('sync','app'),('pricing','pricing'),('windows','download')]:
                        driver.execute_script("document.querySelector('#'+arguments[0]).scrollIntoView({block:'start',behavior:'instant'})",anchor)
                        wait.until(lambda d:d.execute_script("return document.querySelector('#hero-three').dataset.storyState===arguments[0] && document.querySelector('#hero-three').dataset.transition==='stable'",scene))
                        # On narrow screens the model follows the whole sign-in form. Bring its
                        # reserved slot into view before real clicks, as a person scrolling would.
                        driver.execute_script("document.querySelector('.story-slot-'+arguments[0]).scrollIntoView({block:'center',behavior:'instant'})",scene)
                        wait.until(lambda d:d.execute_script("return document.querySelector('#hero-three').dataset.storyState===arguments[0] && document.querySelector('#hero-three').dataset.transition==='stable'",scene))
                        assert driver.execute_script("return document.documentElement.scrollWidth===document.documentElement.clientWidth && document.querySelector('#hero-three').dataset.controlsClipped==='0' && document.querySelector('#hero-three').dataset.geometryClipped==='0'")
                        controls=driver.find_elements('css selector',f'.scene-control[data-scene="{scene}"]')
                        assert all(c.is_displayed() for c in controls)
                        for control in controls:
                            if scene!='pricing':
                                guide(control,control.get_attribute('data-feature'),'x')
                            else:
                                control.click()
                                wait.until(lambda d:d.execute_script("return document.querySelector('#plan-dialog').open"))
                                driver.find_element('id','plan-dialog-x').click()
                        driver.save_screenshot(str(out/f'{width}-{scene}.png'))
                    driver.execute_script("window.scrollTo({top:0,behavior:'instant'})")
                    wait.until(lambda d:d.execute_script("return document.querySelector('#hero-three').dataset.storyState==='hero'"))
                for width in [390,700,1050]:
                    step('e2e.responsive_scenes.'+str(width),lambda w=width:responsive_scenes(w))
                def reduced():
                    phase = 'reduce'
                    try:
                        for cycle in range(3):
                            phase = 'reduce-' + str(cycle)
                            driver.execute_cdp_cmd('Emulation.setEmulatedMedia', {'features':[{'name':'prefers-reduced-motion','value':'reduce'}]})
                            wait.until(lambda d: d.execute_script("return matchMedia('(prefers-reduced-motion: reduce)').matches && document.querySelector('#hero-three').hidden && !document.querySelector('#hero-visual').classList.contains('three-ready')"))
                            driver.save_screenshot(str(out/'reduced-motion.png'))
                            phase = 'restore-' + str(cycle)
                            driver.execute_cdp_cmd('Emulation.setEmulatedMedia', {'features':[{'name':'prefers-reduced-motion','value':'no-preference'}]})
                            wait.until(lambda d: d.execute_script("return !matchMedia('(prefers-reduced-motion: reduce)').matches && document.querySelector('#hero-visual').classList.contains('three-ready')"))
                    except Exception:
                        state=driver.execute_script("return {reduced:matchMedia('(prefers-reduced-motion: reduce)').matches,hidden:document.querySelector('#hero-three').hidden,ready:document.querySelector('#hero-visual').classList.contains('three-ready'),dataset:{...document.querySelector('#hero-three').dataset}}")
                        (out/'reduced-motion-failure.json').write_text(json.dumps({'phase':phase,'state':state},indent=2),encoding='utf-8')
                        driver.save_screenshot(str(out/'reduced-motion-failure.png'))
                        raise
                step('e2e.reduced_motion_restore', reduced)
                def context_recovery():
                    driver.execute_cdp_cmd('Emulation.setDeviceMetricsOverride',{'width':1440,'height':900,'deviceScaleFactor':1,'mobile':False})
                    driver.execute_script("window.scrollTo({top:0,behavior:'instant'})")
                    wait.until(lambda d:d.execute_script("return document.querySelector('#hero-three').dataset.context==='ready'"))
                    for cycle in range(2):
                        driver.execute_script("window.vgTestLoss=document.querySelector('#hero-three').getContext('webgl2').getExtension('WEBGL_lose_context');if(!vgTestLoss)throw Error('lose_context_unavailable');vgTestLoss.loseContext();")
                        wait.until(lambda d:d.execute_script("return document.querySelector('#hero-three').dataset.context==='lost'"))
                        driver.execute_script("document.querySelector('#hero-visual').dispatchEvent(new CustomEvent('videograbber:story-visibility',{detail:{visible:true,reducedMotion:false}}));window.dispatchEvent(new Event('resize'));document.dispatchEvent(new Event('visibilitychange'));")
                        assert driver.execute_script("const c=document.querySelector('#hero-three');return c.hidden&&!document.querySelector('#hero-visual').classList.contains('three-ready')&&getComputedStyle(document.querySelector('.hero-art')).opacity==='1'")
                        if cycle==0:driver.save_screenshot(str(out/'webgl-safe-fallback.png'))
                        driver.execute_script('vgTestLoss.restoreContext();')
                        wait.until(lambda d:d.execute_script("const c=document.querySelector('#hero-three');return c.dataset.context==='ready'&&!c.hidden&&document.querySelector('#hero-visual').classList.contains('three-ready')"))
                    events=driver.execute_script("return window.VGDiagnostics.snapshot().filter(e=>e.event.startsWith('webgl.'))")
                    assert [e['event'] for e in events]==['webgl.context_lost','webgl.context_restored']*2
                    driver.save_screenshot(str(out/'webgl-restored.png'))
                step('e2e.webgl_context_recovery',context_recovery)
                def diagnostics():
                    data = driver.execute_script('return window.VGDiagnostics.snapshot()')
                    check_diagnostics(data, driver.execute_script('return window.VGDiagnostics.dropped'))
                    assert driver.execute_script("return [...document.querySelectorAll('img')].filter(i=>i.getBoundingClientRect().top<innerHeight&&i.getBoundingClientRect().bottom>0).every(i=>i.complete&&i.naturalWidth>0)")
                    coverage = dict(schemaVersion=1,timestamp=datetime.now(timezone.utc).isoformat(),service='web',
                                    event='diagnostics.coverage',outcome='complete',dropped=0,maxEvents=500)
                    data.insert(0, coverage)
                    (out/'browser.jsonl').write_text(''.join(json.dumps(e,ensure_ascii=False)+'\n' for e in data),encoding='utf-8')
                step('e2e.diagnostics', diagnostics)
    finally:
        if driver:
            driver.quit()
        if server:
            server.shutdown(); server.server_close()
        skipped = ['real_oauth', 'real_payment', 'real_download', 'native_windows_sign_in', 'physical_mobile_devices']
        if not args.health:
            skipped.append('api_readiness')
        report = dict(schemaVersion=1, generatedUtc=datetime.now(timezone.utc).isoformat(), runId=run_id,
                      scope='public-web', results=results, notRun=skipped,
                      status='failed' if any(r['status']=='failed' for r in results) else 'passed' if results else 'no_data')
        (out/'results.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
        suite = ET.Element('testsuite', name='VideoGrabber.PublicWeb', tests=str(len(results)),
                           failures=str(sum(r['status']=='failed' for r in results)))
        for result in results:
            case = ET.SubElement(suite,'testcase',name=result['name'],time=str(result['durationMs']/1000))
            if result['status']=='failed':
                ET.SubElement(case,'failure',type=result['errorType'])
        ET.ElementTree(suite).write(out/'junit.xml',encoding='utf-8',xml_declaration=True)
        print(json.dumps({'status':report['status'],'passed':sum(r['status']=='passed' for r in results),
                          'failed':sum(r['status']=='failed' for r in results),'notRun':skipped,'output':str(out)}))
    return 0 if report['status']=='passed' else 1

if __name__ == '__main__':
    raise SystemExit(main())
