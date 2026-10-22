const { PNG } = require('pngjs');
const pixelmatch = require('pixelmatch').default;

// Chromium can vary a few rounded-edge RGB values by one quantization level
// even for identical HTML. Larger differences must be classified as antialiasing
// by pixelmatch at zero color threshold. Never mask regions or accept alpha/layout changes.
function compareScreenshots(actual, expected) {
  const a = PNG.sync.read(actual);
  const b = PNG.sync.read(expected);
  if (a.width !== b.width || a.height !== b.height) return { equivalent: false, reason: 'dimensions' };
  let changedPixels = 0;
  let maxRgbDelta = 0;
  let alphaChanged = false;
  for (let i = 0; i < a.data.length; i += 4) {
    const delta = Math.max(Math.abs(a.data[i] - b.data[i]), Math.abs(a.data[i + 1] - b.data[i + 1]), Math.abs(a.data[i + 2] - b.data[i + 2]));
    const alpha = a.data[i + 3] !== b.data[i + 3];
    if (delta || alpha) changedPixels++;
    maxRgbDelta = Math.max(maxRgbDelta, delta);
    alphaChanged ||= alpha;
  }
  let materialChanges = 0;
  if (!alphaChanged && changedPixels <= 32 && maxRgbDelta > 1) {
    const diff = Buffer.alloc(a.data.length);
    pixelmatch(a.data, b.data, diff, a.width, a.height, { threshold: 0, includeAA: false, diffMask: true });
    for (let i = 0; i < a.data.length; i += 4) {
      if (diff[i + 3] && [0, 1, 2].some(channel => Math.abs(a.data[i + channel] - b.data[i + channel]) > 1)) materialChanges++;
    }
  }
  return { equivalent: !alphaChanged && changedPixels <= 32 && materialChanges === 0, changedPixels, maxRgbDelta, alphaChanged, materialChanges };
}

module.exports = { compareScreenshots };
