const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

const images = [{ naturalWidth: 500, src: '/cached' }, { naturalWidth: 80, src: '/pending' }, { naturalWidth: 0, src: '/failed' }];
const listeners = {};
let callback, cancelled = false;
const context = {
  window: {}, escapeHtml: value => String(value),
  document: { addEventListener: (event, listener) => { listeners[event] = listener; }, querySelectorAll: () => images },
  setTimeout: fn => { callback = fn; return 1; },
  clearTimeout: () => { cancelled = true; }, clearInterval: () => {}
};
const source = fs.readFileSync(path.join(__dirname, '../../src/Presentation/wwwroot/discovery.js'), 'utf8');
vm.runInNewContext(source.replace('  return { get enabled()', '  window.artworkTest = { coverImage, refreshArtwork, leave };\n  return { get enabled()'), context);
const { coverImage, refreshArtwork, leave } = context.window.artworkTest;
const mbid = 'e97f805a-ab48-4c52-855e-07049142113d';
assert.ok(coverImage(mbid).includes('/api/artwork/recordings/' + mbid));
assert.ok(coverImage(null).includes('00000000-0000-0000-0000-000000000000'));
assert.ok(!coverImage('"><script>').includes('<script>'));
assert.ok(coverImage(mbid).includes('alt=""'));
refreshArtwork();
callback();
assert.equal(images[0].src, '/cached', 'Successful thumbnails must not be downloaded again');
assert.equal(images[1].src, '/pending?refresh=1');
assert.equal(images[2].src, '/failed?refresh=1');
const image = { matches: () => true, style: {} };
listeners.error({ target: image });
assert.equal(image.style.visibility, 'hidden');
listeners.load({ target: image });
assert.equal(image.style.visibility, '');
cancelled = false;
leave();
assert.ok(cancelled, 'Leaving discovery must cancel artwork refresh');
console.log('PASS artwork: safe identities, placeholders, bounded refresh, cached thumbnails, errors and navigation');
