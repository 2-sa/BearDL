import sys
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2]
                       / 'resources/yt-dlp-plugins/cookie_challenge'))

from yt_dlp.extractor.common import InfoExtractor
from yt_dlp.utils import ExtractorError
from yt_dlp_plugins.extractor.cookie_challenge import PornHubChallengeIE, solve_cookie


class PornHubChallengeTests(unittest.TestCase):
    def page(self, operations='', tail=':123:1;path=/;'):
        return '''<script>function go() {
          var p=77; var s=5; var n;
          %s
          n=leastFactor(p);
          { document.cookie="KEY="+n+"*"+p/n+":"+s+"%s";
            document.location.reload(true); }
        }</script><body onload="go()">''' % (operations, tail)

    def test_cookie_and_comments(self):
        self.assertEqual(solve_cookie(self.page('p+=/* comment */2*7; p-=14;')),
                         ('KEY', '7*11:5:123:1'))

    def test_both_conditional_branches(self):
        self.assertEqual(solve_cookie(self.page(
            'if ((s >> 0) & 1) p+=14; else p-=2; '
            'if ((s >> 1) & 1) p-=2; else p-=14;')),
            ('KEY', '7*11:5:123:1'))

    def test_rejects_unknown_code_and_oversized_work(self):
        for operation in ('fetch("https://example.org");', 'p+=Deno.env.get("X");',
                          'while(true) {}', 'p+=999999999999999;', 'p-=77;'):
            with self.subTest(operation=operation):
                self.assertIsNone(solve_cookie(self.page(operation)))

    def test_does_not_accept_cookie_attributes_from_page(self):
        self.assertIsNone(solve_cookie(self.page(tail=';domain=example.org;path=/;')))

    def test_normal_page_is_untouched(self):
        self.assertIsNone(solve_cookie('<html>normal page</html>'))

    def test_large_numbers_and_payloads_are_rejected(self):
        self.assertIsNone(solve_cookie(self.page().replace('p=77', 'p=' + '9' * 5000)))
        self.assertIsNone(solve_cookie(' ' * 65537 + self.page()))

    def test_even_prime_and_signed_shift(self):
        self.assertEqual(solve_cookie(self.page().replace('p=77', 'p=22'))[1], '2*11:5:123:1')
        self.assertEqual(solve_cookie(self.page().replace('p=77', 'p=13'))[1], '13*1:5:123:1')
        page = self.page('if ((s >> 31) & 1) p+=0; else p-=2;').replace('s=5', 's=4294967295')
        self.assertEqual(solve_cookie(page)[1], '7*11:4294967295:123:1')

    def test_retry_sets_cookie_and_preserves_request_options(self):
        first = (self.page(), SimpleNamespace(url='https://www.pornhub.com/'))
        second = ('normal page', first[1])
        extractor = PornHubChallengeIE()
        with patch.object(InfoExtractor, '_download_webpage_handle', side_effect=[first, second]) as request, \
                patch.object(extractor, '_set_cookie') as cookie:
            self.assertEqual(extractor._download_webpage_handle('url', 'id', impersonate=True), second)
            cookie.assert_called_once_with('www.pornhub.com', 'KEY', '7*11:5:123:1')
            self.assertEqual(request.call_count, 2)
            self.assertEqual(request.call_args_list[0], request.call_args_list[1])

    def test_failed_verification_does_not_loop(self):
        first = (self.page(), SimpleNamespace(url='https://www.pornhub.com/'))
        extractor = PornHubChallengeIE()
        with patch.object(InfoExtractor, '_download_webpage_handle', return_value=first) as request, \
                patch.object(extractor, '_set_cookie'):
            with self.assertRaises(ExtractorError):
                extractor._download_webpage_handle('url', 'id')
            self.assertEqual(request.call_count, 2)

    def test_foreign_redirect_never_receives_cookie(self):
        first = (self.page(), SimpleNamespace(url='https://pornhub.com.example.org/'))
        extractor = PornHubChallengeIE()
        with patch.object(InfoExtractor, '_download_webpage_handle', return_value=first), \
                patch.object(extractor, '_set_cookie') as cookie:
            with self.assertRaises(ExtractorError):
                extractor._download_webpage_handle('url', 'id')
            cookie.assert_not_called()


if __name__ == '__main__':
    unittest.main()
