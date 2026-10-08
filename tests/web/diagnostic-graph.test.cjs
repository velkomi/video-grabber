const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
test('the graph joins browser requests and worker events only by explicit job links',()=>{
  const source=fs.readFileSync(__dirname+'/../../scripts/Build-DiagnosticsReport.py','utf8');
  const fn=source.slice(source.indexOf('function linkedEvents('),source.indexOf('\nfunction draw()'));
  const context={}; vm.runInNewContext(fn,context);
  const page='a'.repeat(32),request='b'.repeat(32),job='c'.repeat(32),other='d'.repeat(32);
  const events=[{traceId:page,service:'web'},{traceId:request,parentTraceId:page,jobId:job,service:'api'},
    {traceId:job,jobId:job,service:'worker'},{traceId:other,jobId:other,service:'worker'}];
  const linked=context.linkedEvents(events,page);
  assert.equal(linked.length,3);
  assert.equal(linked[2].service,'worker');
  assert.ok(!linked.some(e=>e.traceId===other));
});
