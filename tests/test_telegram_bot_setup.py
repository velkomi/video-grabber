import contextlib
import importlib.util
import io
import pathlib
import unittest
from unittest.mock import patch

SOURCE = pathlib.Path(__file__).resolve().parents[1] / 'deploy/platform/configure_telegram_bot.py'
SPEC = importlib.util.spec_from_file_location('telegram_setup', SOURCE)
SETUP = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SETUP)


class TelegramCommandMenuTests(unittest.TestCase):
    def run_setup(self, apply=False, configured=False):
        calls = []
        current = {'commands': [], 'menu': {'type': 'web_app'}}
        expected_names = ['start', 'menu', 'link', 'download', 'jobs', 'account', 'settings', 'subscription', 'help', 'hide']
        if configured and hasattr(SETUP, 'BOT_COMMANDS'):
            current['commands'] = SETUP.BOT_COMMANDS
            current['menu'] = {'type': 'commands'}

        def fake_api(token, method, payload=None):
            calls.append((method, payload))
            if method == 'getMe':
                return {'ok': True, 'result': {'username': 'VideoGra_bot'}}
            if method == 'getMyCommands':
                return {'ok': True, 'result': current['commands']}
            if method == 'getChatMenuButton':
                return {'ok': True, 'result': current['menu']}
            if method == 'setMyCommands':
                current['commands'] = payload['commands']
                return {'ok': True, 'result': True}
            if method == 'setChatMenuButton':
                current['menu'] = payload['menu_button']
                return {'ok': True, 'result': True}
            raise AssertionError('Unexpected Telegram mutation: ' + method)

        argv = ['configure_telegram_bot.py', '--commands-only'] + (['--apply'] if apply else [])
        with patch.object(SETUP, 'api', fake_api), patch.object(SETUP, 'secret', return_value='synthetic-token') as secrets, patch('sys.argv', argv), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, SETUP.main())
        self.assertEqual([('VG_TELEGRAM_BOT_TOKEN', 'VG_TELEGRAM_BOT_TOKEN_FILE')], [call.args for call in secrets.call_args_list])
        if apply:
            self.assertEqual(expected_names, [item['command'] for item in current['commands']])
            self.assertEqual('commands', current['menu']['type'])
        return calls

    def test_dry_run_never_changes_settings_or_reads_webhook_secret(self):
        calls = self.run_setup()
        self.assertEqual(['getMe'], [method for method, _ in calls])

    def test_commands_only_registers_commands_and_menu_without_touching_webhook(self):
        calls = self.run_setup(apply=True)
        self.assertIn('setMyCommands', [method for method, _ in calls])
        self.assertIn('setChatMenuButton', [method for method, _ in calls])

    def test_matching_registration_does_not_repeat_mutations(self):
        calls = self.run_setup(apply=True, configured=True)
        self.assertFalse(any(method.startswith('set') for method, _ in calls))


if __name__ == '__main__':
    unittest.main()
