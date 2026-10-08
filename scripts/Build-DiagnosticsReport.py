"""Read-only, bounded aggregation of sanitized VideoGrabber JSONL; no cleanup or commands."""
import argparse
from collections import Counter
from datetime import datetime, timedelta, timezone
import json
import math
from pathlib import Path
import re

LABEL = re.compile(r'^[a-zA-Z0-9_.-]{1,80}$')
TRACE = re.compile(r'^[a-fA-F0-9-]{32,36}$')
ACTIONS = {'header-login','google-login','email-login','logout','submit-download','refresh-jobs',
           'operation','download-target','device-select','plan-dialog-action','plan-dialog-close','plan-dialog-x',
           'plan-free','plan-start','plan-unlimited_video','plan-full_course',
           'navigate:#how','navigate:#app','navigate:#pricing','navigate:#download',
           'navigate:/download/windows','navigate:/download/windows/portable','navigate:/miniapp/'}
ACTIONS.update('scene:'+feature for feature in ('workflow-url','workflow-format','workflow-download',
               'pricing-free','pricing-start','pricing-unlimited_video','pricing-full_course',
               'device-windows','device-web','device-telegram','windows-app'))
ACTIONS.update(('guide:link','guide:format','guide:file','info-dialog-x','info-dialog-close','info-dialog-action'))
ACTIONS.update(('dialog:info','dialog:plan'))

def normalize(item):
    # Accept raw app JSONL and the structured console/Docker envelope without copying messages.
    if not isinstance(item, dict):
        raise ValueError('object_required')
    nested = item.get('State', {}).get('DiagnosticEvent') if isinstance(item.get('State'), dict) else None
    message = nested or item.get('log') or item.get('Message')
    if isinstance(message, str):
        position = message.find('VG_TELEMETRY ')
        if position >= 0:
            item = json.JSONDecoder().raw_decode(message[position + 13:].lstrip())[0]
        elif nested:
            item = json.loads(nested)
    stamp = datetime.fromisoformat(str(item.get('timestamp','')).replace('Z','+00:00'))
    if stamp.tzinfo is None:
        raise ValueError('utc_required')
    event = item.get('event', item.get('stage','unknown'))
    outcome = item.get('outcome', item.get('status','unknown'))
    clean = {'timestamp':stamp.astimezone(timezone.utc).isoformat(), 'service':item.get('service','windows'),
             'event':event, 'outcome':outcome}
    for name in ('service','event','outcome'):
        if not isinstance(clean[name],str) or not LABEL.fullmatch(clean[name]):
            clean[name] = 'unknown'
    trace = item.get('traceId', item.get('jobId',''))
    clean['traceId'] = trace if isinstance(trace,str) and TRACE.fullmatch(trace) else 'unlinked'
    action = item.get('action')
    if isinstance(action,str) and action in ACTIONS:
        clean['action'] = action
    if item.get('state') in ('hero','workflow','sync','pricing','windows'):
        clean['state'] = item['state']
    parent = item.get('parentTraceId')
    if isinstance(parent,str) and TRACE.fullmatch(parent):
        clean['parentTraceId'] = parent
    job = item.get('jobId')
    if isinstance(job,str) and TRACE.fullmatch(job):
        clean['jobId'] = job.replace('-','').lower()
    duration = item.get('durationMs')
    if isinstance(duration,(int,float)) and not isinstance(duration,bool) and math.isfinite(duration) and 0 <= duration <= 86_400_000:
        clean['durationMs'] = round(duration,2)
    if isinstance(item.get('status'),int) and 100 <= item['status'] <= 599:
        clean['httpStatus'] = item['status']
    return clean, stamp

def aggregate(paths, now=None):
    now = now or datetime.now(timezone.utc)
    events = []; malformed = skipped = expired = dropped = 0; total = 0
    for index, path in enumerate(paths):
        try:
            if path.is_symlink():
                skipped += 1; continue
            size = path.stat().st_size
            stream = path.open(encoding='utf-8-sig',errors='replace')
        except OSError:
            skipped += 1; continue
        if index >= 512 or size > 4*1024*1024 or total+size > 64*1024*1024:
            stream.close()
            skipped += 1; continue
        total += size
        with stream:
            while True:
                try:
                    line = stream.readline()
                except OSError:
                    skipped += 1; break
                if not line:
                    break
                if not line.strip():
                    continue
                if len(line) > 65536:
                    malformed += 1; continue
                try:
                    item = json.loads(line)
                    if isinstance(item,dict) and item.get('event')=='diagnostics.coverage':
                        dropped += max(0, int(item.get('dropped',0)))
                        continue
                    event, stamp = normalize(item)
                    if stamp < now-timedelta(days=30):
                        expired += 1; continue
                    if stamp > now+timedelta(minutes=5):
                        malformed += 1; continue
                    if len(events) >= 10000:
                        dropped += 1; continue
                    events.append(event)
                except (ValueError,TypeError,AttributeError,OverflowError):
                    malformed += 1
    events.sort(key=lambda item:item['timestamp'])
    failures = sum(e['outcome'] in ('failed','error','review_required') for e in events)
    status = 'incomplete' if malformed or skipped or dropped else 'no_data' if not events else 'issues_found' if failures else 'no_errors_observed'
    return dict(schemaVersion=1, generatedUtc=now.isoformat(), status=status, eventCount=len(events),
                failureEvents=failures, malformedLines=malformed, skippedFiles=skipped,
                expiredEvents=expired, droppedEvents=dropped, windowDays=30,
                services=dict(Counter(e['service'] for e in events)), events=events)

HTML = '''<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>VideoGrabber · диагностика</title><style>
body{font:16px system-ui;background:#07101f;color:#eef5ff;margin:0;padding:32px}main{max-width:1200px;margin:auto}h1{font-size:36px}p{color:#b1c4da;line-height:1.6}section{background:#0c1a2d;border:1px solid #294663;border-radius:18px;padding:24px;margin:24px 0;overflow-x:auto;box-sizing:border-box}button,select{background:#142d4b;color:#fff;border:1px solid #4e759c;border-radius:8px;padding:10px;font:inherit;max-width:100%}output{display:block;margin:16px 0;overflow-wrap:anywhere}.graph{display:flex;gap:10px;overflow:auto;padding:16px 0}.node{min-width:150px;max-width:220px;padding:14px;border:1px solid #457aa5;border-radius:12px;overflow-wrap:anywhere}.node.failed{border-color:#ff7e8b}.node small{display:block;color:#a8bfd6;margin-top:8px}table{width:100%;border-collapse:collapse}td,th{text-align:left;padding:12px;border-bottom:1px solid #294663}.bar{background:#5bb8ff;height:12px;min-width:1px;border-radius:6px}.arrow{align-self:center;color:#7498bd}@media(max-width:600px){body{padding:16px}section{padding:16px}h1{font-size:28px}}
</style><main><h1>VideoGrabber · диагностика</h1><p>Сводка фактически собранных событий. Отсутствие ошибок в журнале не доказывает исправность всех функций.</p>
<section><h2>Охват</h2><output id="coverage"></output><p>Исходные сообщения, URL, поля форм и секреты не включаются. Чтение журналов не изменяет и не удаляет файлы.</p></section>
<section><h2>Сервисы и события</h2><table><thead><tr><th>Сервис</th><th>События</th><th>Ошибки</th><th>Доля событий</th></tr></thead><tbody id="services"></tbody></table></section>
<section><h2>Граф наблюдаемого пути</h2><select id="traces" aria-label="Выбрать trace"></select><div id="graph" class="graph"></div><p>Стрелки показывают порядок событий по времени внутри trace, а не доказанную причинность. Связанные browser/API trace включаются по явно записанному parentTraceId и UUID задания jobId. Показаны до 100 событий выбранного пути.</p></section>
<section><h2>Длительность этапов</h2><table><thead><tr><th>Этап</th><th>Завершений с длительностью</th><th>p50, мс</th><th>p95, мс</th></tr></thead><tbody id="durations"></tbody></table><p>Процентили относятся к этому небольшому набору событий; это не нагрузочный тест.</p></section></main>
<script>const data=__DATA__;
document.querySelector('#coverage').textContent=`${data.status} · ${data.eventCount} событий · ${data.failureEvents} событий ошибок · ${data.malformedLines} повреждённых строк · ${data.skippedFiles} пропущенных файлов · ${data.droppedEvents} вытесненных событий · окно ${data.windowDays} дней`;
for(const [service,count] of Object.entries(data.services)){const row=document.createElement('tr');for(const value of [service,count,data.events.filter(e=>e.service===service&&['failed','error','review_required'].includes(e.outcome)).length]){const cell=document.createElement('td');cell.textContent=value;row.append(cell)}const cell=document.createElement('td'),bar=document.createElement('div');bar.className='bar';bar.style.width=(count/Math.max(1,data.eventCount)*100)+'%';cell.append(bar);row.append(cell);document.querySelector('#services').append(row)}
const selector=document.querySelector('#traces');for(const trace of [...new Set(data.events.map(e=>e.traceId))].slice(0,300)){const option=document.createElement('option');option.value=trace;option.textContent=trace;selector.append(option)}
function linkedEvents(events,trace){
  const related=new Set([trace]);
  for(const e of events)if(e.parentTraceId===trace)related.add(e.traceId);
  const jobs=new Set(events.filter(e=>related.has(e.traceId)&&e.jobId).map(e=>e.jobId));
  for(const e of events)if(jobs.has(e.jobId)||jobs.has(e.traceId.replaceAll('-','').toLowerCase()))related.add(e.traceId);
  return events.filter(e=>related.has(e.traceId));
}
function draw(){const graph=document.querySelector('#graph');graph.replaceChildren();const events=linkedEvents(data.events,selector.value).slice(0,100);for(const [index,e] of events.entries()){if(index){const arrow=document.createElement('span');arrow.className='arrow';arrow.textContent='→';graph.append(arrow)}const node=document.createElement('div');node.className='node'+(['failed','error','review_required'].includes(e.outcome)?' failed':'');node.textContent=e.service+' · '+e.event+(e.action?' · '+e.action:e.state?' · '+e.state:'');const detail=document.createElement('small');detail.textContent=e.outcome+' · '+e.timestamp.slice(11,23)+(e.durationMs!==undefined?' · '+e.durationMs+' ms':'')+(e.httpStatus?' · HTTP '+e.httpStatus:'');node.append(detail);graph.append(node)}}selector.addEventListener('change',draw);draw();
const groups={};for(const e of data.events){if(e.durationMs!==undefined&&e.outcome!=='started'){const key=e.service+'.'+e.event;(groups[key]??=[]).push(e.durationMs)}}for(const [name,values] of Object.entries(groups)){values.sort((a,b)=>a-b);const q=p=>values[Math.max(0,Math.ceil(values.length*p)-1)];const row=document.createElement('tr');for(const value of [name,values.length,q(.5),q(.95)]){const cell=document.createElement('td');cell.textContent=value;row.append(cell)}document.querySelector('#durations').append(row)}
</script></html>'''

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('inputs', nargs='+', type=Path, help='JSONL files or explicit directory (nonrecursive)')
    parser.add_argument('--output', type=Path, required=True)
    args=parser.parse_args()
    paths=[]
    for path in args.inputs:
        if path.is_dir():
            paths.extend(sorted(path.glob('*.jsonl')))
        elif path.is_file():
            paths.append(path)
        else:
            parser.error('Input path does not exist')
    paths=list(dict.fromkeys(paths))
    data=aggregate(paths)
    args.output.mkdir(parents=True,exist_ok=False)
    serialized=json.dumps(data,ensure_ascii=False,allow_nan=False)
    (args.output/'report.json').write_text(serialized,encoding='utf-8')
    (args.output/'index.html').write_text(HTML.replace('__DATA__',serialized.replace('<','\\u003c')),encoding='utf-8')
    print(json.dumps({key:data[key] for key in ('status','eventCount','failureEvents','malformedLines','skippedFiles','droppedEvents')}))

if __name__=='__main__':
    main()
