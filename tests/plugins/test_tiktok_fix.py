import re
import unittest
from unittest.mock import patch

from plugin_paths import add_plugin_paths

add_plugin_paths()

from yt_dlp.extractor.common import InfoExtractor
from yt_dlp_plugins.extractor.tiktok_fix import (
    TikTokFixIE,
    TikTokLiveFixIE,
    TikTokUserFixIE,
    generate_blockbuster_headers,
)


class TikTokFixTests(unittest.TestCase):
    def test_blockbuster_headers_use_only_consonants(self):
        headers = generate_blockbuster_headers()
        self.assertGreaterEqual(len(headers), 2)
        self.assertLessEqual(len(headers), 8)
        for name, value in headers.items():
            self.assertRegex(name, r'^[bcdfghjklmnpqrstvwxz]{8,24}$')
            self.assertRegex(value, r'^[bcdfghjklmnpqrstvwxz]{16,32}$')

    def test_valid_url_matches_standard_and_share_urls(self):
        for url in (
            'https://www.tiktok.com/@leenabhushan/video/6748451240264420610',
            'https://www.tiktok.com/@/video/7683926991468367121',
            'https://www.tiktok.com/share/video/7668090902816017671/',
            'https://www.tiktokv.com/share/video/7668090902816017671/',
        ):
            with self.subTest(url=url):
                self.assertIsNotNone(re.match(TikTokFixIE._VALID_URL, url))

    def test_download_webpage_adds_impersonation_and_blockbuster_headers(self):
        for cls in (TikTokFixIE, TikTokUserFixIE, TikTokLiveFixIE):
            with self.subTest(extractor=cls.__name__):
                extractor = cls()
                with patch.object(InfoExtractor, '_download_webpage_handle', return_value=('ok', object())) as mock_dl:
                    extractor._download_webpage_handle('https://www.tiktok.com/@/video/7683926991468367121', '7683926991468367121')
                    mock_dl.assert_called_once()
                    kwargs = mock_dl.call_args.kwargs
                    self.assertTrue(kwargs.get('impersonate'))
                    self.assertIsInstance(kwargs.get('headers'), dict)
                    self.assertGreaterEqual(len(kwargs['headers']), 2)


if __name__ == '__main__':
    unittest.main()
