import assert from "node:assert/strict";
import { chooseDownsampleFactor, generateVogelSamples, SAMPLE_TIERS } from "../web/model.js";

assert.equal(chooseDownsampleFactor(1, 7.9, 0), 1);
assert.equal(chooseDownsampleFactor(1, 31.9, 0), 1);
assert.equal(chooseDownsampleFactor(1, 32, 0), 2);
assert.equal(chooseDownsampleFactor(2, 95.9, 0), 2);
assert.equal(chooseDownsampleFactor(4, 96, 0), 4);
assert.equal(chooseDownsampleFactor(4, 8, 4), 2);
assert.equal(chooseDownsampleFactor(4, 12, 3), 3);
assert.equal(chooseDownsampleFactor(4, 16, 4), 4);
assert.equal(chooseDownsampleFactor(3, 100, 0), 1);
assert.equal(chooseDownsampleFactor(1, 100, 0, true), 1);

const samples = generateVogelSamples();
assert.equal(samples.length, SAMPLE_TIERS.reduce((sum, count) => sum + count, 0) * 4);

let offset = 0;
for (const count of SAMPLE_TIERS) {
  let meanX = 0;
  let meanY = 0;
  for (let i = 0; i < count; i += 1) {
    const index = (offset + i) * 4;
    meanX += samples[index];
    meanY += samples[index + 1];
    assert.ok(samples[index + 2] > 0 && samples[index + 2] < 1);
    assert.ok(Math.abs(samples[index + 3] - samples[index + 2] ** 2) < 1e-6);
  }
  assert.ok(Math.abs(meanX / count) < 1e-7);
  assert.ok(Math.abs(meanY / count) < 1e-7);
  offset += count;
}

console.log("web model tests passed");
