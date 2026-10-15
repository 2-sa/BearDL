"""Register all plugin roots before yt-dlp builds its namespace search path."""
import sys
from pathlib import Path


def add_plugin_paths():
    root = Path(__file__).resolve().parents[2] / 'resources/yt-dlp-plugins'
    for directory in sorted(root.iterdir()):
        if (directory / 'yt_dlp_plugins').is_dir() and str(directory) not in sys.path:
            sys.path.insert(0, str(directory))
