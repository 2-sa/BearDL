const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const { test } = require('node:test');

for (const target of ['chrome', 'firefox']) {
  test(`${target}: routes to the clicked tab after asynchronous storage`, async () => {
    const listeners = {};
    const event = name => ({ addListener: handler => { listeners[name] = handler; } });
    let finishStorage;
    const calls = [];
    let menuCreates = 0;
    let menuRemovals = 0;
    let storedMenu = { showContextMenu: true };
    const api = {
      storage: {
        sync: {
          get: keys => {
            if (keys === 'showContextMenu') {
              return Promise.resolve(storedMenu);
            }
            return new Promise(resolve => { finishStorage = resolve; });
          }
        },
        onChanged: event('storage')
      },
      tabs: { update: async (id, options) => calls.push({ id, url: options.url }), query: async () => [{ id: 4, url: 'https://example.com/keyboard' }] },
      runtime: { onInstalled: event('installed'), onStartup: event('startup'), openOptionsPage: async () => {} },
      contextMenus: { onClicked: event('context'), removeAll: async () => { menuRemovals++; }, create: () => { menuCreates++; } },
      action: { onClicked: event('action') },
      commands: { onCommand: event('command') }
    };
    const context = vm.createContext({ chrome: api, browser: api, URL, console, importScripts: () => {} });
    const folder = path.join(__dirname, '../../extension', target);
    vm.runInContext(fs.readFileSync(path.join(folder, 'utils.js'), 'utf8'), context);
    vm.runInContext(fs.readFileSync(path.join(folder, 'background.js'), 'utf8'), context);

    await listeners.startup();
    assert.equal(menuRemovals, 1);
    assert.equal(menuCreates, 1);
    storedMenu = { showContextMenu: false };
    await listeners.startup();
    assert.equal(menuRemovals, 2);
    assert.equal(menuCreates, 1);

    listeners.action({ id: 17, url: 'https://example.com/watch?v=123&x=%26#part' });
    finishStorage({ trimPlaylist: false });
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(calls.pop(), { id: 17, url: 'parabolic://example.com/watch?v=123&x=%26#part' });

    listeners.action({ id: 18, url: 'http://example.com/plain/video?id=9' });
    finishStorage({ trimPlaylist: false });
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(calls.pop(), { id: 18, url: 'parabolic://http/example.com/plain/video?id=9' });

    listeners.context({ menuItemId: 'openParabolicLink', linkUrl: 'https://youtube.com/watch?v=abc&list=def&index=2' }, { id: 8 });
    finishStorage({ trimPlaylist: true });
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(calls.pop(), { id: 8, url: 'parabolic://youtube.com/watch?v=abc' });

    listeners.action({ id: 9, url: 'chrome://extensions' });
    listeners.action({ id: 9, url: 'about:config' });
    listeners.action({ id: 9 });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(calls.length, 0);

    for (let i = 0; i < 2; i++) {
      listeners.action({ id: 17, url: 'https://example.com/repeat' });
      finishStorage({});
      await new Promise(resolve => setImmediate(resolve));
    }
    assert.equal(calls.length, 2, 'Repeated clicks must not silently drop the same URL');
  });

  test(`${target}: options page and manifest use updated settings API and BearDL homepage`, async () => {
    const folder = path.join(__dirname, '../../extension', target);
    const manifest = JSON.parse(fs.readFileSync(path.join(folder, 'manifest.json'), 'utf8'));
    assert.equal(manifest.homepage_url, 'https://github.com/2-sa/BearDL');

    const docListeners = {};
    const elements = {
      trimPlaylist: { checked: false, listeners: {}, addEventListener(evt, fn) { this.listeners[evt] = fn; } },
      contextMenuToggle: { checked: false, listeners: {}, addEventListener(evt, fn) { this.listeners[evt] = fn; } }
    };
    const synced = { trimPlaylist: true, showContextMenu: false };
    const written = [];
    const storageApi = {
      storage: {
        sync: {
          get: async () => ({ ...synced }),
          set: async patch => { written.push(patch); Object.assign(synced, patch); }
        }
      }
    };
    const globals = {
      console,
      document: {
        addEventListener: (evt, fn) => { docListeners[evt] = fn; },
        getElementById: id => elements[id]
      }
    };
    globals[target === 'chrome' ? 'chrome' : 'browser'] = storageApi;
    const context = vm.createContext(globals);
    vm.runInContext(fs.readFileSync(path.join(folder, 'options.js'), 'utf8'), context);

    await docListeners.DOMContentLoaded();
    assert.equal(elements.trimPlaylist.checked, true);
    assert.equal(elements.contextMenuToggle.checked, false);

    await elements.trimPlaylist.listeners.change({ target: { checked: false } });
    await elements.contextMenuToggle.listeners.change({ target: { checked: true } });
    assert.deepEqual(JSON.parse(JSON.stringify(written)), [{ trimPlaylist: false }, { showContextMenu: true }]);
    assert.deepEqual(synced, { trimPlaylist: false, showContextMenu: true });
  });
}
