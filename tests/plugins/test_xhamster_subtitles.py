import copy
import json
import sys
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2]
                       / 'resources/yt-dlp-plugins/xhamster_subtitles'))

from yt_dlp.extractor.xhamster import XHamsterIE
from yt_dlp_plugins.extractor.xhamster_subtitles import XHamsterSubtitlesIE


class XHamsterSubtitlesTests(unittest.TestCase):
    PAGE_URL = 'https://xhamster.com/videos/test-fixture-123456'

    def extract(self, tracks, existing=None):
        player = {'xplayerPluginSettings': {'subtitles': {'tracks': tracks}}}
        original_info = existing or {'id': '123456', 'formats': [{'url': 'https://example.org/video.mp4'}]}

        def upstream_extract(extractor, url):
            extractor._parse_json(json.dumps(player), '123456')
            return copy.deepcopy(original_info)

        with patch.object(XHamsterIE, '_real_extract', upstream_extract):
            return XHamsterSubtitlesIE()._real_extract(self.PAGE_URL)

    def test_original_and_generated_tracks_are_available_separately(self):
        info = self.extract([
            {'lang': 'en', 'isOriginal': True, 'label': 'English (original)',
             'urls': {'vtt': 'https://example.org/en.vtt'}},
            {'lang': 'ar', 'isOriginal': False, 'label': 'Arabic (auto-generated)',
             'urls': {'vtt': 'https://example.org/ar.vtt'}},
        ])
        self.assertEqual(list(info['subtitles']), ['en'])
        self.assertEqual(list(info['automatic_captions']), ['ar'])
        arabic = info['automatic_captions']['ar'][0]
        self.assertEqual(arabic['ext'], 'vtt')
        self.assertEqual(arabic['url'], 'https://example.org/ar.vtt')
        self.assertEqual(arabic['http_headers']['Referer'], self.PAGE_URL)
        self.assertEqual(info['formats'][0]['url'], 'https://example.org/video.mp4')

    def test_duplicate_and_existing_tracks_are_preserved_without_duplication(self):
        track = {'lang': 'en', 'isOriginal': True, 'urls': {'vtt': 'https://example.org/en.vtt'}}
        existing = {'id': '123456', 'subtitles': {'en': [{'url': 'https://example.org/en.vtt', 'ext': 'vtt'}]},
                    'automatic_captions': {'fr': [{'url': 'https://example.org/fr.vtt', 'ext': 'vtt'}]}}
        info = self.extract([track, track], existing)
        self.assertEqual(info, existing)

    def test_missing_tracks_do_not_change_video_results(self):
        for tracks in (None, [], {}, 'invalid'):
            with self.subTest(tracks=tracks):
                info = self.extract(tracks)
                self.assertNotIn('subtitles', info)
                self.assertNotIn('automatic_captions', info)
                self.assertEqual(info['id'], '123456')

    def test_malformed_tracks_are_ignored(self):
        info = self.extract([
            None, 'invalid', {}, {'lang': 'en', 'urls': None},
            {'lang': 1, 'urls': {'vtt': 'https://example.org/en.vtt'}},
            {'lang': '', 'urls': {'vtt': 'https://example.org/en.vtt'}},
            {'lang': 'en', 'urls': {'vtt': 'javascript:invalid'}},
        ])
        self.assertNotIn('subtitles', info)
        self.assertNotIn('automatic_captions', info)

    def test_tracks_do_not_leak_between_videos(self):
        extractor = XHamsterSubtitlesIE()
        pages = iter([
            {'xplayerPluginSettings': {'subtitles': {'tracks': [
                {'lang': 'en', 'urls': {'vtt': 'https://example.org/en.vtt'}},
            ]}}}, {},
        ])

        def upstream_extract(extractor, url):
            extractor._parse_json(json.dumps(next(pages)), '123456')
            return {'id': '123456'}

        with patch.object(XHamsterIE, '_real_extract', upstream_extract):
            self.assertIn('en', extractor._real_extract(self.PAGE_URL)['subtitles'])
            self.assertNotIn('subtitles', extractor._real_extract(self.PAGE_URL))


if __name__ == '__main__':
    unittest.main()
