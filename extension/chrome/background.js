importScripts('utils.js');
// Shared helpers are loaded by the manifest (Firefox) or importScripts (Chrome).
async function openParabolicUrl(url, tabId) {
  try {
    if (typeof url !== 'string' || !/^https?:\/\//i.test(url) || !Number.isInteger(tabId)) {
      return;
    }
    let settings = {};
    try {
      settings = await chrome.storage.sync.get(['trimPlaylist']);
    } catch (error) {
      console.warn('Unable to read extension settings:', error);
    }
    const processedUrl = settings.trimPlaylist ? trimPlaylistFromUrl(url) : url;
    const schemeUrl = /^http:\/\//i.test(processedUrl)
      ? `parabolic://http/${processedUrl.slice('http://'.length)}`
      : `parabolic://${processedUrl.replace(/^https:\/\//i, '')}`;
    // Keep the original tab: the user may switch tabs while storage is loading.
    await chrome.tabs.update(tabId, { url: schemeUrl });
  } catch (error) {
    console.error('Unable to send link to BearDL:', error);
  }
}

async function updateContextMenu() {
  try {
    await chrome.contextMenus.removeAll();
    const settings = await chrome.storage.sync.get('showContextMenu');
    if (settings.showContextMenu !== false) {
      chrome.contextMenus.create({ id: 'openParabolicLink', title: 'Open link in BearDL', contexts: ['link', 'page'] });
    }
  } catch (error) {
    console.error('Unable to update context menu:', error);
  }
}

chrome.runtime.onInstalled.addListener(updateContextMenu);
chrome.runtime.onStartup.addListener(updateContextMenu);
chrome.storage.onChanged.addListener((changes, namespace) => {
  if (namespace === 'sync' && changes.showContextMenu) {
    void updateContextMenu();
  }
});
chrome.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId === 'openParabolicLink') {
    void openParabolicUrl(info.linkUrl || info.pageUrl, tab?.id);
  }
});
chrome.action.onClicked.addListener(tab => { void openParabolicUrl(tab.url, tab.id); });
chrome.commands.onCommand.addListener(async command => {
  try {
    if (command === 'open-parabolic-current-tab') {
      const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
      if (tabs.length > 0) {
        await openParabolicUrl(tabs[0].url, tabs[0].id);
      }
    } else if (command === 'open-parabolic-options') {
      await chrome.runtime.openOptionsPage();
    }
  } catch (error) {
    console.error('Unable to handle extension command:', error);
  }
});
