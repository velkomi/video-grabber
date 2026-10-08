const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const source=fs.readFileSync(__dirname+'/../../src/VideoGrabber.Platform.Api/wwwroot/web/app.js','utf8');
function setup(response){
  const context={fetch:async()=>response,Error};
  const part=source.slice(source.indexOf('function authAvailabilityError('),source.indexOf('function authRedirectUri('));
  vm.runInNewContext(part,context);
  return context;
}
test('a static preview rejection has its own actionable message and cannot be called unconfigured production auth',async()=>{
  const context=setup({ok:false,status:503,json:async()=>({code:'preview_backend_unavailable'})});
  await assert.rejects(context.getSupabaseAuthConfig(),error=>{
    assert.equal(error.code,'preview_backend_unavailable');
    assert.match(context.googleSignInMessage(error),/локальный просмотр/u);
    return true;
  });
});
test('production configuration is returned unchanged and unknown failures never display raw provider text',async()=>{
  const cfg={googleEnabled:true,url:'https://provider.test',redirectUri:'https://site.test/web/'};
  const context=setup({ok:true,status:200,json:async()=>cfg});
  assert.equal(await context.getSupabaseAuthConfig(),cfg);
  assert.doesNotMatch(context.googleSignInMessage({message:'token=secret'}),/secret/u);
});
