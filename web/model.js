export const SAMPLE_TIERS = Object.freeze([64, 128, 256, 512]);

export function chooseDownsampleFactor(mode, radius, setting, showMap = false) {
  if (showMap || ![1, 2, 4].includes(Number(mode))) return 1;
  let wanted = Number(setting) === 0
    ? (radius >= 96 ? 4 : radius >= 32 ? 2 : 1)
    : Math.min(4, Math.max(1, Number(setting)));
  if (radius < 8) wanted = 1;
  while (wanted > 1 && radius / wanted < 4) wanted -= 1;
  return wanted;
}

export function generateVogelSamples() {
  const data = new Float32Array(SAMPLE_TIERS.reduce((sum, count) => sum + count, 0) * 4);
  let cursor = 0;
  for (const count of SAMPLE_TIERS) {
    const points = [];
    let meanX = 0;
    let meanY = 0;
    for (let i = 0; i < count; i += 1) {
      const radius = Math.sqrt((i + 0.5) / count);
      const angle = i * 2.39996322972865332;
      const x = radius * Math.cos(angle);
      const y = radius * Math.sin(angle);
      points.push([x, y, radius, radius * radius]);
      meanX += x;
      meanY += y;
    }
    meanX /= count;
    meanY /= count;
    for (const point of points) {
      data.set([point[0] - meanX, point[1] - meanY, point[2], point[3]], cursor * 4);
      cursor += 1;
    }
  }
  return data;
}
