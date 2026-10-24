"""Provide TikTok HTTP Error 403 blockbuster header and share-URL compatibility across yt-dlp versions."""
import random

from yt_dlp.extractor.tiktok import TikTokIE, TikTokLiveIE, TikTokUserIE

__all__ = []

_CONSONANTS = 'bcdfghjklmnpqrstvwxz'


def generate_blockbuster_headers(*, min_headers=2, max_headers=8):
    """Randomize HTTP header fingerprint using vowel-free names to avoid HTTP Error 403 blocks."""
    def random_letters(minimum, maximum):
        return ''.join(random.choices(_CONSONANTS, k=random.randint(minimum, maximum)))

    return {
        random_letters(8, 24): random_letters(16, 32)
        for _ in range(random.randint(min_headers, max_headers))
    }


def _with_blockbuster_kwargs(kwargs):
    merged = dict(kwargs)
    merged.setdefault('impersonate', True)
    headers = dict(merged.get('headers') or {})
    if not headers:
        headers.update(generate_blockbuster_headers())
    merged['headers'] = headers
    return merged


class TikTokFixIE(TikTokIE, plugin_name='beardl_tiktok'):
    _VALID_URL = r'https?://www\.tiktokv?\.com/(?:embed|(?:share|@(?P<user_id>[\w\.-]+)?)/video)/(?P<id>\d+)'
    _EMBED_REGEX = [rf'<(?:script|iframe)[^>]+\bsrc=(["\'])(?P<url>{_VALID_URL})']

    def _download_webpage_handle(self, *args, **kwargs):
        return super()._download_webpage_handle(*args, **_with_blockbuster_kwargs(kwargs))


class TikTokUserFixIE(TikTokUserIE, plugin_name='beardl_tiktok'):
    def _download_webpage_handle(self, *args, **kwargs):
        return super()._download_webpage_handle(*args, **_with_blockbuster_kwargs(kwargs))


class TikTokLiveFixIE(TikTokLiveIE, plugin_name='beardl_tiktok'):
    def _download_webpage_handle(self, *args, **kwargs):
        return super()._download_webpage_handle(*args, **_with_blockbuster_kwargs(kwargs))
