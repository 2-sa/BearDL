// Load saved settings when options opens
document.addEventListener('DOMContentLoaded', async () => {
  try {
    const result = await chrome.storage.sync.get(['trimPlaylist', 'showContextMenu']);
    document.getElementById('trimPlaylist').checked = Boolean(result && result.trimPlaylist);
    document.getElementById('contextMenuToggle').checked = result && typeof result.showContextMenu !== 'undefined' ? result.showContextMenu : true;
  } catch (error) {
    console.error('Storage error:', error);
  }
});

// Save/show Trim Playlist setting
document.getElementById('trimPlaylist').addEventListener('change', async (e) => {
  try {
    await chrome.storage.sync.set({ trimPlaylist: e.target.checked });
  } catch (error) {
    console.error('Storage error:', error);
  }
});

// Save/show context menu setting
document.getElementById('contextMenuToggle').addEventListener('change', async (e) => {
  try {
    await chrome.storage.sync.set({ showContextMenu: e.target.checked });
  } catch (error) {
    console.error('Storage error:', error);
  }
});
