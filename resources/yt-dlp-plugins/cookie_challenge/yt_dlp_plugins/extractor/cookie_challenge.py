"""Handle bounded arithmetic cookie challenges without executing JavaScript."""
import math
import re
from urllib.parse import urlsplit

from yt_dlp.extractor.common import InfoExtractor
from yt_dlp.extractor.pornhub import PornHubIE
from yt_dlp.utils import ExtractorError

__all__ = []


def solve_cookie(page):
    # Accept one known grammar only. Never eval JavaScript or page expressions.
    if len(page) > 65536:
        return None
    page = re.sub(r'/\*.*?\*/', '', page, flags=re.S)
    start = re.search(r'function\s+go\(\)\s*\{\s*var p=(\d{1,15});\s*'
                      r'var s=(\d{1,10});\s*var n;', page)
    if not start:
        return None
    p, s = map(int, start.groups())
    if s > 0xffffffff or not 1 < p <= 10**14:
        return None
    code = page[start.end():]
    operation = r'p\s*([+-])=\s*(\d{1,15})\s*(?:\*\s*(\d{1,15}))?\s*;'
    conditional = re.compile(r'\s*if\s*\(\(s\s*>>\s*(\d{1,2})\)\s*&\s*1\)\s*'
                             + operation + r'\s*else\s*' + operation)
    plain = re.compile(r'\s*' + operation)
    for _ in range(128):
        match = conditional.match(code)
        if match:
            shift, a_sign, a, a_factor, b_sign, b, b_factor = match.groups()
            if int(shift) > 31:
                return None
            # JavaScript shifts convert the left operand to a signed int32.
            signed_s = s if s < 2**31 else s - 2**32
            sign, value, factor = ((a_sign, a, a_factor) if (signed_s >> int(shift)) & 1
                                   else (b_sign, b, b_factor))
        else:
            match = plain.match(code)
            if not match:
                break
            sign, value, factor = match.groups()
        delta = int(value) * int(factor or 1)
        if delta > 10**14:
            return None
        p += delta if sign == '+' else -delta
        if not 1 < p <= 10**14:
            return None
        code = code[match.end():]
    else:
        return None
    tail = re.match(
        r'\s*n=leastFactor\(p\);\s*\{\s*document\.cookie="(KEY|RNKEY)="'
        r'\+n\+"\*"\+p/n\+":"\+s\+"(:\d{1,12}:\d{1,12});path=/;";\s*'
        r'document\.location\.reload\(true\);\s*\}\s*\}', code)
    if not tail:
        return None
    # The bounded trial count also caps CPU for prime/adversarial inputs.
    factor = 2 if p % 2 == 0 else 3
    limit = math.isqrt(p)
    for _ in range(1000000):
        if p % factor == 0 or factor > limit:
            factor = min(factor, p) if p % factor == 0 else p
            return tail[1], f'{factor}*{p // factor}:{s}{tail[2]}'
        factor += 2
    return None


class PornHubChallengeIE(PornHubIE, plugin_name='beardl_cookie_challenge'):
    def _download_webpage_handle(self, *args, **kwargs):
        # Replace the upstream base method's PhantomJS fallback only. Requests
        # still use yt-dlp's cookies, proxy, TLS and browser impersonation.
        result = InfoExtractor._download_webpage_handle(self, *args, **kwargs)
        if not result:
            return result
        page, handle = result
        if not re.search(r'<body\b[^>]+\bonload=["\']go\(\)', page):
            return result
        cookie = solve_cookie(page)
        if cookie is None:
            raise ExtractorError('The site returned an unsupported verification challenge', expected=True)
        host = urlsplit(handle.url).hostname
        # Never attach a challenge cookie to a redirect outside the extractor's site.
        if not host or not re.fullmatch(r'(?:[\w-]+\.)*pornhub(?:premium)?\.(?:com|net|org)', host):
            raise ExtractorError('Unexpected verification host', expected=True)
        self._set_cookie(host, cookie[0], cookie[1])
        result = InfoExtractor._download_webpage_handle(self, *args, **kwargs)
        if result and re.search(r'<body\b[^>]+\bonload=["\']go\(\)', result[0]):
            raise ExtractorError('Site verification did not accept the challenge cookie', expected=True)
        return result
