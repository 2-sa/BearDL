# yt-dlp-plugins

Adapted from:

- https://github.com/bindestriche/srt_fix

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
