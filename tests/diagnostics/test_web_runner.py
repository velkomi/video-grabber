import importlib.util
from pathlib import Path
import unittest

spec=importlib.util.spec_from_file_location('runner',Path(__file__).resolve().parents[2]/'scripts/Test-Web.py')
runner=importlib.util.module_from_spec(spec); spec.loader.exec_module(runner)

class RunnerTests(unittest.TestCase):
    def test_missing_resource_cannot_be_a_green_e2e(self):
        with self.assertRaises(AssertionError):
            runner.check_diagnostics([dict(schemaVersion=1,event='resource.error',outcome='failed')],0)

    def test_dropped_errors_cannot_be_a_green_e2e(self):
        with self.assertRaises(AssertionError):
            runner.check_diagnostics([dict(schemaVersion=1,event='page.ready',outcome='succeeded')],1)

if __name__=='__main__':
    unittest.main()
