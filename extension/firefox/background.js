// Shared helpers are loaded by the manifest (Firefox) or importScripts (Chrome).
async function openParabolicUrl(url, tabId) {
  try {
    if (typeof url !== 'string' || !/^https?:\/\//i.test(url) || !Number.isInteger(tabId)) {
      return;
    }
    let settings = {};
    try {
      settings = await browser.storage.sync.get(['trimPlaylist']);
    } catch (error) {
      console.warn('Unable to read extension settings:', error);
    }
    const processedUrl = settings.trimPlaylist ? trimPlaylistFromUrl(url) : url;
    const schemeUrl = /^http:\/\//i.test(processedUrl)
      ? `parabolic://http/${processedUrl.slice('http://'.length)}`
      : `parabolic://${processedUrl.replace(/^https:\/\//i, '')}`;
    // Keep the original tab: the user may switch tabs while storage is loading.
    await browser.tabs.update(tabId, { url: schemeUrl });
  } catch (error) {
    console.error('Unable to send link to BearDL:', error);
  }
}

async function updateContextMenu() {
  try {
    await browser.contextMenus.removeAll();
    const settings = await browser.storage.sync.get('showContextMenu');
    if (settings.showContextMenu !== false) {
      browser.contextMenus.create({ id: 'openParabolicLink', title: 'Open link in BearDL', contexts: ['link', 'page'] });
    }
  } catch (error) {
    console.error('Unable to update context menu:', error);
  }
}

browser.runtime.onInstalled.addListener(updateContextMenu);
browser.runtime.onStartup.addListener(updateContextMenu);
browser.storage.onChanged.addListener((changes, namespace) => {
  if (namespace === 'sync' && changes.showContextMenu) {
    void updateContextMenu();
  }
});
browser.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId === 'openParabolicLink') {
    void openParabolicUrl(info.linkUrl || info.pageUrl, tab?.id);
  }
});
browser.action.onClicked.addListener(tab => { void openParabolicUrl(tab.url, tab.id); });
browser.commands.onCommand.addListener(async command => {
  try {
    if (command === 'open-parabolic-current-tab') {
      const tabs = await browser.tabs.query({ active: true, currentWindow: true });
      if (tabs.length > 0) {
        await openParabolicUrl(tabs[0].url, tabs[0].id);
      }
    } else if (command === 'open-parabolic-options') {
      await browser.runtime.openOptionsPage();
    }
  } catch (error) {
    console.error('Unable to handle extension command:', error);
  }
});
