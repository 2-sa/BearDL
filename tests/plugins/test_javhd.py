import base64
import json
import unittest
from unittest.mock import MagicMock, patch

from plugin_paths import add_plugin_paths

add_plugin_paths()

from yt_dlp.utils import ExtractorError
from yt_dlp_plugins.extractor.javhd import JavHDTodayIE


class JavHDTodayTests(unittest.TestCase):
    def test_valid_urls(self):
        ie = JavHDTodayIE()
        valid_cases = [
            ('https://javhd.today/373612/mosaic-xrl-026-collar-dog/', '373612'),
            ('https://javhd.today/373612/', '373612'),
            ('https://javhd.today/373612', '373612'),
            ('https://javhd.today/embed/373612/', '373612'),
            ('https://javhd.today/cn/373612/test-slug/', '373612'),
            ('https://javhdz.today/373612/', '373612'),
        ]
        for url, expected_id in valid_cases:
            with self.subTest(url=url):
                m = ie._match_valid_url(url)
                self.assertIsNotNone(m)
                self.assertEqual(m.group('id'), expected_id)

    def test_invalid_urls(self):
        ie = JavHDTodayIE()
        invalid_cases = [
            'https://javhd.today/',
            'https://javhd.today/categories/',
            'https://javhd.today/tag/deep-throating/',
            'https://example.com/373612/',
        ]
        for url in invalid_cases:
            with self.subTest(url=url):
                self.assertIsNone(ie._match_valid_url(url))

    def test_extraction_with_mocked_network(self):
        ie = JavHDTodayIE()

        embed_urls = ['https://stream.javhdz.today/embed_server10.php?p=testtoken123']
        embeds_b64 = base64.b64encode(json.dumps(embed_urls).encode('utf-8')).decode('utf-8')

        video_page_html = f'''
        <!DOCTYPE html>
        <html>
        <head>
            <meta property="og:title" content="Test JAV Video Title">
            <meta property="og:description" content="Test description content">
            <meta property="og:image" content="https://pics.javhd.today/thumb.jpg">
            <meta property="og:video:duration" content="7200">
            <meta property="og:video:release_date" content="2026-10-23T12:00:00+00:00">
            <meta property="video:tag" content="tag1">
            <meta property="video:tag" content="tag2">
        </head>
        <body>
            <button class="button_choice_server" data-name="Fastserver" data-embeds="{embeds_b64}"></button>
        </body>
        </html>
        '''

        embed_page_html = '''
        <!DOCTYPE html>
        <html>
        <head><title>Player</title></head>
        <body>
            <script>
            var FIRST = {"type":"f4scdn","src":"/universal-stream-hls/12345/playlist.m3u8","title":"Test"};
            </script>
        </body>
        </html>
        '''

        dummy_formats = [{
            'format_id': 'Fastserver',
            'url': 'https://stream.javhdz.today/universal-stream-hls/12345/playlist.m3u8',
            'ext': 'mp4',
            'protocol': 'm3u8_native',
        }]

        def mock_download_webpage(url, video_id, *args, **kwargs):
            if 'javhd.today' in url:
                return video_page_html
            if 'embed_server10.php' in url:
                return embed_page_html
            return None

        with patch.object(ie, '_download_webpage', side_effect=mock_download_webpage), \
             patch.object(ie, '_extract_m3u8_formats', return_value=dummy_formats):
            info = ie._real_extract('https://javhd.today/373612/test-video/')

        self.assertEqual(info['id'], '373612')
        self.assertEqual(info['title'], 'Test JAV Video Title')
        self.assertEqual(info['description'], 'Test description content')
        self.assertEqual(info['thumbnail'], 'https://pics.javhd.today/thumb.jpg')
        self.assertEqual(info['duration'], 7200)
        self.assertEqual(info['tags'], ['tag1', 'tag2'])
        self.assertEqual(info['formats'], dummy_formats)

    def test_no_formats_raises_extractor_error(self):
        ie = JavHDTodayIE()
        empty_page = '<html><head><title>No Video</title></head><body></body></html>'

        with patch.object(ie, '_download_webpage', return_value=empty_page):
            with self.assertRaises(ExtractorError):
                ie._real_extract('https://javhd.today/373612/')


if __name__ == '__main__':
    unittest.main()
