import base64
import json
import re
from urllib.parse import urljoin

from yt_dlp.extractor.common import InfoExtractor
from yt_dlp.utils import (
    ExtractorError,
    int_or_none,
    parse_iso8601,
)

__all__ = ['JavHDTodayIE']


class JavHDTodayIE(InfoExtractor):
    IE_NAME = 'javhd:today'
    _VALID_URL = r'https?://(?:www\.)?javhdz?\.today/(?:[a-z]{2}/)?(?:embed/)?(?P<id>\d+)(?:/(?P<display_id>[^/?#&]+)/?)?'

    _TESTS = [{
        'url': 'https://javhd.today/373612/mosaic-xrl-026-collar-dog-m-guy-breeding-obscene-mare-deep-throating-sobbing-training-2/',
        'info_dict': {
            'id': '373612',
            'ext': 'mp4',
            'title': 'Mosaic XRL-026 Collar Dog M Guy Breeding Obscene Mare Deep Throating Sobbing Training 2',
            'duration': 7680,
            'thumbnail': r're:^https?://.*\.jpg$',
        },
        'params': {
            'skip_download': True,
        },
    }]

    def _real_extract(self, url):
        mobj = self._match_valid_url(url)
        video_id = mobj.group('id')
        display_id = mobj.group('display_id')

        webpage_url = f'https://javhd.today/{video_id}/'
        webpage = self._download_webpage(
            webpage_url, video_id,
            headers={'Referer': 'https://javhd.today/'},
            fatal=False)

        if not webpage:
            webpage = self._download_webpage(
                url, video_id,
                headers={'Referer': 'https://javhd.today/'})

        title = (self._og_search_title(webpage, default=None)
                 or self._html_search_regex(
                     r'<title>([^<]+?)(?:\s*-\s*Javhd\.today)?\s*</title>',
                     webpage, 'title', default=None)
                 or display_id
                 or video_id)

        description = self._og_search_description(webpage, default=None)
        thumbnail = self._og_search_thumbnail(webpage, default=None)
        duration = int_or_none(self._html_search_meta(
            ['video:duration', 'og:video:duration'], webpage, 'duration', default=None))
        timestamp = parse_iso8601(self._html_search_meta(
            ['video:release_date', 'og:video:release_date'], webpage, 'release date', default=None))

        tags = re.findall(
            r'<meta[^>]+property=["\']video:tag["\'][^>]+content=["\']([^"\']+)["\']',
            webpage)

        formats = []
        buttons = re.findall(
            r'<button\b[^>]*class=["\'][^"\']*button_choice_server[^"\']*["\'][^>]*>',
            webpage)

        for button_html in buttons:
            server_name = self._search_regex(
                r'data-name=["\']([^"\']+)["\']', button_html, 'server name', default='server')
            data_embeds = self._search_regex(
                r'data-embeds=["\']([^"\']+)["\']', button_html, 'embeds', default=None)
            data_embed = self._search_regex(
                r'data-embed=["\']([^"\']+)["\']', button_html, 'embed', default=None)

            embed_urls = []
            if data_embeds:
                try:
                    decoded = base64.b64decode(data_embeds).decode('utf-8', errors='ignore')
                    urls = json.loads(decoded)
                    if isinstance(urls, list):
                        embed_urls.extend(urls)
                except Exception:
                    pass

            if data_embed:
                try:
                    decoded = base64.b64decode(data_embed).decode('utf-8', errors='ignore')
                    if decoded:
                        embed_urls.append(decoded)
                except Exception:
                    pass

            # Try up to 2 embed mirror URLs for this server
            server_success = False
            for embed_url in embed_urls[:2]:
                if not isinstance(embed_url, str):
                    continue

                if re.search(r'/embed.*\.php', embed_url):
                    embed_page = self._download_webpage(
                        embed_url, video_id,
                        note=f'Downloading {server_name} player page',
                        fatal=False,
                        headers={'Referer': 'https://javhd.today/'})
                    if not embed_page:
                        continue

                    first_raw = self._search_regex(
                        r'var\s+FIRST\s*=\s*(\{.+?\});',
                        embed_page, 'first source', default=None)
                    if first_raw:
                        try:
                            first_data = json.loads(first_raw)
                        except Exception:
                            first_data = None

                        if isinstance(first_data, dict):
                            src = first_data.get('src')
                            if src:
                                stream_url = urljoin(embed_url, src)
                                stream_type = first_data.get('type') or ''
                                if stream_type == 'mp4' or stream_url.endswith('.mp4'):
                                    formats.append({
                                        'url': stream_url,
                                        'format_id': f'{server_name}-mp4',
                                        'ext': 'mp4',
                                        'http_headers': {'Referer': embed_url},
                                    })
                                    server_success = True
                                    break
                                elif 'm3u8' in stream_url:
                                    m3u8_formats = self._extract_m3u8_formats(
                                        stream_url, video_id, 'mp4',
                                        entry_protocol='m3u8_native',
                                        m3u8_id=server_name,
                                        headers={'Referer': embed_url},
                                        fatal=False)
                                    if m3u8_formats:
                                        formats.extend(m3u8_formats)
                                        server_success = True
                                        break

            # If we successfully extracted formats from this server, stop searching more servers
            if server_success and formats:
                break

        if not formats:
            raise ExtractorError('No video formats found for javhd.today', expected=True)

        return {
            'id': video_id,
            'title': title,
            'description': description,
            'thumbnail': thumbnail,
            'duration': duration,
            'timestamp': timestamp,
            'tags': tags,
            'formats': formats,
        }
