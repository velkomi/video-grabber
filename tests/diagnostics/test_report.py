import importlib.util
from datetime import datetime, timezone, timedelta
import json
from pathlib import Path
import tempfile
import unittest

spec=importlib.util.spec_from_file_location('report',Path(__file__).resolve().parents[2]/'scripts/Build-DiagnosticsReport.py')
report=importlib.util.module_from_spec(spec); spec.loader.exec_module(report)

class ReportTests(unittest.TestCase):
    def test_rotated_log_produces_incomplete_report_instead_of_crashing(self):
        class Rotated:
            def is_symlink(self):
                return False
            def stat(self):
                raise FileNotFoundError('private-path')
        data=report.aggregate([Rotated()])
        self.assertEqual('incomplete',data['status']); self.assertEqual(1,data['skippedFiles'])
        self.assertNotIn('private-path',json.dumps(data))
    def test_no_data_does_not_claim_success(self):
        self.assertEqual('no_data',report.aggregate([])['status'])

    def test_report_drops_messages_secrets_old_events_and_reports_incomplete_coverage(self):
        now=datetime.now(timezone.utc)
        item=dict(timestamp=now.isoformat(),event='auth.desktop.consume',outcome='failed',service='windows',
                  traceId='a'*32,durationMs=42,message='token=private',url='https://private.example')
        with tempfile.TemporaryDirectory(prefix='vg-report-fixture-') as directory:
            path=Path(directory)/'fixture.jsonl'
            path.write_text(json.dumps(item)+'\n'+json.dumps({**item,'timestamp':(now-timedelta(days=31)).isoformat()})+'\n{invalid\n',encoding='utf-8')
            data=report.aggregate([path],now)
        self.assertEqual('incomplete',data['status']); self.assertEqual(1,data['eventCount'])
        self.assertEqual(1,data['expiredEvents']); self.assertEqual(1,data['malformedLines'])
        self.assertNotIn('private',json.dumps(data))

    def test_structured_server_log_and_untrusted_fields_are_sanitized(self):
        item=dict(timestamp=datetime.now(timezone.utc).isoformat(),event='<script>',outcome='succeeded',
                  service='api',traceId='email@example.com',message='secret')
        event,_=report.normalize({'Message':'VG_TELEMETRY '+json.dumps(item)})
        self.assertEqual('unknown',event['event']); self.assertEqual('unlinked',event['traceId'])
        self.assertNotIn('secret',json.dumps(event))

    def test_graph_keeps_known_button_names_but_drops_arbitrary_input(self):
        base=dict(timestamp=datetime.now(timezone.utc).isoformat(),event='ui.action',outcome='activated',service='web')
        event,_=report.normalize({**base,'action':'plan-full_course','value':'private-email'})
        self.assertEqual('plan-full_course',event['action']); self.assertNotIn('private-email',json.dumps(event))
        unknown,_=report.normalize({**base,'action':'private-email'})
        self.assertNotIn('action',unknown)

if __name__=='__main__':
    unittest.main()
