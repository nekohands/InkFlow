const { test } = require('node:test');
const assert = require('node:assert/strict');
const { PNG } = require('pngjs');
const { compareScreenshots } = require('./compare-screenshots.cjs');

function fixture(width = 10, changes = 0, delta = 1, channel = 0) {
  const png = new PNG({ width, height: 10 });
  png.data.fill(128);
  for (let pixel = 0; pixel < changes; pixel++) png.data[pixel * 4 + channel] += delta;
  return PNG.sync.write(png);
}

test('identical screenshots have zero changed pixels', () => {
  assert.deepEqual(compareScreenshots(fixture(), fixture()), { equivalent: true, changedPixels: 0, maxRgbDelta: 0, alphaChanged: false, materialChanges: 0 });
});
test('only bounded one-level RGB quantization is accepted', () => {
  assert.equal(compareScreenshots(fixture(10, 32), fixture()).equivalent, true);
  assert.equal(compareScreenshots(fixture(10, 33), fixture()).equivalent, false);
  assert.equal(compareScreenshots(fixture(10, 1, 2), fixture()).equivalent, false);
});
test('dimensions and alpha changes always fail', () => {
  assert.equal(compareScreenshots(fixture(11), fixture()).equivalent, false);
  assert.equal(compareScreenshots(fixture(10, 1, 1, 3), fixture()).equivalent, false);
});
