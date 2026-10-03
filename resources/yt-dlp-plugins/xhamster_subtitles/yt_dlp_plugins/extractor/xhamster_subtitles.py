from yt_dlp.extractor.xhamster import XHamsterIE
from yt_dlp.utils import url_or_none

# This replaces the built-in extractor; do not register a second URL handler.
__all__ = []


class XHamsterSubtitlesIE(XHamsterIE, plugin_name='beardl_subtitles'):
    def _parse_json(self, *args, **kwargs):
        data = super()._parse_json(*args, **kwargs)
        # Reuse the player data already parsed by the built-in extractor.
        # No second page request or change to video format extraction is needed.
        if isinstance(data, dict):
            settings = data.get('xplayerPluginSettings')
            subtitles = settings.get('subtitles') if isinstance(settings, dict) else None
            tracks = subtitles.get('tracks') if isinstance(subtitles, dict) else None
            if isinstance(tracks, list):
                self._beardl_subtitle_tracks = tracks
        return data

    def _real_extract(self, url):
        self._beardl_subtitle_tracks = []
        info = super()._real_extract(url)
        for track in self._beardl_subtitle_tracks:
            if not isinstance(track, dict):
                continue
            language, urls = track.get('lang'), track.get('urls')
            if not isinstance(language, str) or not language.strip() or not isinstance(urls, dict):
                continue
            key = 'automatic_captions' if track.get('isOriginal') is False else 'subtitles'
            for extension in ('vtt', 'srt', 'ass'):
                subtitle_url = url_or_none(urls.get(extension))
                if not subtitle_url:
                    continue
                tracks = info.setdefault(key, {}).setdefault(language, [])
                if any(item.get('url') == subtitle_url for item in tracks):
                    continue
                entry = {
                    'url': subtitle_url,
                    'ext': extension,
                    'http_headers': {'Referer': url},
                }
                if isinstance(track.get('label'), str):
                    entry['name'] = track['label']
                tracks.append(entry)
        return info
