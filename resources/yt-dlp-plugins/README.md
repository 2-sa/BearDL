# yt-dlp-plugins

Adapted from:

- https://github.com/bindestriche/srt_fix

## Arithmetic verification fallback

`cookie_challenge` replaces the video extractor's PhantomJS fallback for the
observed `go()` arithmetic cookie challenge. It parses a fixed arithmetic grammar
without evaluating JavaScript or launching a browser. Only KEY/RNKEY cookies for
the extractor's own host are accepted, with one retry. Page size, integer sizes,
operation count and factorization work are bounded. Unknown challenges fail
explicitly; this is not a general browser, login or regional-access solution.
The plugin is copied to all platforms by the existing plugin packaging targets.
Tests in `tests/plugins/test_cookie_challenge.py` run with the existing checks.

## BearDL subtitle extraction

`xhamster_subtitles` extends the built-in yt-dlp extractor using player metadata
already downloaded during discovery. Original tracks appear under `subtitles`;
generated tracks appear under `automatic_captions` and require the app's
"Include Auto-Generated Subtitles" setting. Select the desired languages in the
download dialog. Fast Download uses the subtitle languages selected previously.
The app's existing subtitle format and embedding settings still apply.

The plugin is bundled on all platforms by `Nickvision.Common.targets`. It keeps
the built-in video extractor and needs no separate Python installation in the
app. Tracks unavailable on the source page cannot be downloaded by this plugin.

Regression checks run on GitHub Actions against bundled and latest yt-dlp:
`python -m unittest discover -s tests/plugins -v`.
