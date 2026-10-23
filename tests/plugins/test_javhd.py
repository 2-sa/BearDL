import base64
import json
import unittest
from unittest.mock import patch

from plugin_paths import add_plugin_paths

add_plugin_paths()

from yt_dlp import YoutubeDL
from yt_dlp.utils import ExtractorError
from yt_dlp_plugins.extractor.javhd import JavHDTodayIE


class FakeYDL(YoutubeDL):
    def __init__(self):
        super().__init__({'quiet': True, 'no_warnings': True, 'skip_download': True})

    def report_warning(self, *args, **kwargs):
        pass

    def to_screen(self, *args, **kwargs):
        pass


class JavHDTodayTests(unittest.TestCase):
    def setUp(self):
        self.ydl = FakeYDL()
        self.ie = JavHDTodayIE(self.ydl)

    def test_valid_urls(self):
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
                m = self.ie._match_valid_url(url)
                self.assertIsNotNone(m)
                self.assertEqual(m.group('id'), expected_id)

    def test_invalid_urls(self):
        invalid_cases = [
            'https://javhd.today/',
            'https://javhd.today/categories/',
            'https://javhd.today/tag/deep-throating/',
            'https://example.com/373612/',
        ]
        for url in invalid_cases:
            with self.subTest(url=url):
                self.assertIsNone(self.ie._match_valid_url(url))

    def test_extraction_with_mocked_network(self):
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
        }, {
            'format_id': 'Fastserver-audio',
            'url': 'https://stream.javhdz.today/audio/playlist.m3u8',
            'ext': 'm4a',
            'protocol': 'm3u8_native',
            'http_headers': {'X-Test': 'preserved'},
        }]

        def mock_download_webpage(url, video_id, *args, **kwargs):
            if 'javhd.today' in url:
                return video_page_html
            if 'embed_server10.php' in url:
                return embed_page_html
            return None

        with patch.object(self.ie, '_download_webpage', side_effect=mock_download_webpage), \
             patch.object(self.ie, '_extract_m3u8_formats', return_value=dummy_formats) as extract_hls:
            info = self.ie._real_extract('https://javhd.today/373612/test-video/')

        self.assertEqual(info['id'], '373612')
        self.assertEqual(info['title'], 'Test JAV Video Title')
        self.assertEqual(info['description'], 'Test description content')
        self.assertEqual(info['thumbnail'], 'https://pics.javhd.today/thumb.jpg')
        self.assertEqual(info['duration'], 7200)
        self.assertEqual(info['tags'], ['tag1', 'tag2'])
        self.assertEqual(info['formats'], dummy_formats)
        extract_hls.assert_called_once_with(
            'https://stream.javhdz.today/universal-stream-hls/12345/playlist.m3u8',
            '373612', 'mp4', entry_protocol='m3u8_native', m3u8_id='Fastserver',
            headers={'Referer': embed_urls[0]}, fatal=False)
        for video_format in info['formats']:
            self.assertEqual(video_format['http_headers']['Referer'], embed_urls[0])
        self.assertEqual(info['formats'][1]['http_headers']['X-Test'], 'preserved')

    def test_fallback_after_two_failed_mirrors(self):
        mirrors = [f'https://stream.example/embed_{i}.php' for i in range(3)]
        fallback = 'https://stream.example/embed_fallback.php'
        embeds = base64.b64encode(json.dumps(mirrors).encode()).decode()
        embed = base64.b64encode(fallback.encode()).decode()
        page = (f'<title>Test</title><button class="button_choice_server" '
                f'data-embeds="{embeds}" data-embed="{embed}"></button>')
        player = 'var FIRST = {"type":"mp4","src":"/video.mp4"};'
        for failed_page in (None, '<html>No source</html>'):
            with self.subTest(failed_page=failed_page):
                pages = {
                    'https://javhd.today/373612/': page,
                    mirrors[0]: failed_page,
                    mirrors[1]: failed_page,
                    fallback: player,
                }
                with patch.object(self.ie, '_download_webpage',
                                  side_effect=lambda url, *args, **kwargs: pages[url]) as download:
                    info = self.ie._real_extract('https://javhd.today/373612/')
                self.assertEqual([call.args[0] for call in download.call_args_list],
                                 ['https://javhd.today/373612/', *mirrors[:2], fallback])
                self.assertEqual(info['formats'][0]['url'], 'https://stream.example/video.mp4')
                self.assertEqual(info['formats'][0]['http_headers']['Referer'], fallback)

    def test_successful_mirror_skips_fallback(self):
        mirror = 'https://stream.example/embed_mirror.php'
        fallback = 'https://stream.example/embed_fallback.php'
        embeds = base64.b64encode(json.dumps([mirror]).encode()).decode()
        embed = base64.b64encode(fallback.encode()).decode()
        page = (f'<title>Test</title><button class="button_choice_server" '
                f'data-embeds="{embeds}" data-embed="{embed}"></button>')
        with patch.object(self.ie, '_download_webpage', side_effect=[
                page, 'var FIRST = {"type":"mp4","src":"/video.mp4"};']) as download:
            self.ie._real_extract('https://javhd.today/373612/')
        self.assertEqual([call.args[0] for call in download.call_args_list],
                         ['https://javhd.today/373612/', mirror])

    def test_duplicate_fallback_is_not_requested_again(self):
        mirrors = [f'https://stream.example/embed_{i}.php' for i in range(2)]
        embeds = base64.b64encode(json.dumps(mirrors).encode()).decode()
        embed = base64.b64encode(mirrors[0].encode()).decode()
        page = (f'<title>Test</title><button class="button_choice_server" '
                f'data-embeds="{embeds}" data-embed="{embed}"></button>')
        with patch.object(self.ie, '_download_webpage', side_effect=[page, None, None]) as download:
            with self.assertRaises(ExtractorError):
                self.ie._real_extract('https://javhd.today/373612/')
        self.assertEqual([call.args[0] for call in download.call_args_list],
                         ['https://javhd.today/373612/', *mirrors])

    def test_no_formats_raises_extractor_error(self):
        empty_page = '<html><head><title>No Video</title></head><body></body></html>'

        with patch.object(self.ie, '_download_webpage', return_value=empty_page):
            with self.assertRaises(ExtractorError):
                self.ie._real_extract('https://javhd.today/373612/')


if __name__ == '__main__':
    unittest.main()
