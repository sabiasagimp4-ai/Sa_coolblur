import { chooseDownsampleFactor, generateVogelSamples } from "./model.js";

const DEFAULTS = Object.freeze({
  previewScale: 0.5,
  quality: 256,
  downsample: 0,
  mode: 1,
  centerX: 50,
  centerY: 50,
  width: 200,
  feather: 300,
  angle: 0,
  invert: false,
  showMap: false,
  radius: 20,
  bokehEdge: 0,
  highlight: 30,
  bokehStrength: 0,
  threshold: 0.8,
  dispersion: 25,
  anamorphic: 1,
  innerColor: "#ff0000",
  middleColor: "#00ff00",
  outerColor: "#0000ff",
  edgeRepeat: true,
  linearLight: true,
});

const RANGE_GROUPS = {
  focusControls: [
    ["centerX", "中心X", -200, 300, 0.1, "%"],
    ["centerY", "中心Y", -200, 300, 0.1, "%"],
    ["width", "ピント幅", 0, 4000, 1, "px"],
    ["feather", "境界ぼかし", 0, 2000, 1, "px"],
    ["angle", "角度", -360, 360, 0.1, "°"],
  ],
  blurControls: [
    ["radius", "半径", 0, 100, 0.1, "px"],
    ["bokehEdge", "ボケの縁", -100, 100, 0.1, "%"],
    ["highlight", "ハイライト", 0, 100, 0.1, "%"],
    ["bokehStrength", "玉ボケ強調", 0, 200, 0.1, "%"],
    ["threshold", "しきい値", 0.05, 1, 0.01, ""],
  ],
  lensControls: [
    ["dispersion", "分散量", -1000, 1000, 1, "%"],
    ["anamorphic", "縦横比", 0.5, 2, 0.01, ""],
  ],
};

const PRESETS = [
  ["既定値", {}],
  ["弱い全体", { mode: 4, radius: 8, dispersion: 25, width: 0, feather: 1 }],
  ["全体ぼけ", { mode: 4, quality: 512, radius: 40, dispersion: 0, highlight: 90, bokehStrength: 100, width: 0, feather: 1 }],
  ["色分散", { mode: 4, radius: 20, dispersion: 250, width: 0, feather: 1 }],
  ["机にピント", { mode: 1, radius: 36, dispersion: 150, centerY: 76, width: 80, feather: 150 }],
  ["黒板にピント", { mode: 2, radius: 38, dispersion: 200, centerX: 61, centerY: 35, width: 100, feather: 170 }],
  ["横長ボケ", { mode: 4, radius: 24, dispersion: 200, bokehEdge: 70, highlight: 65, anamorphic: 0.5 }],
  ["縁の強いボケ", { mode: 4, radius: 24, dispersion: 100, bokehEdge: 100, highlight: 50, bokehStrength: 100 }],
];

const VERTEX_SHADER = `#version 300 es
precision highp float;
out vec2 vUv;
void main() {
  vec2 p = vec2(float((gl_VertexID << 1) & 2), float(gl_VertexID & 2));
  vUv = p * 0.5;
  gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
}`;

const PREPARE_SHADER = `#version 300 es
precision highp float;
in vec2 vUv;
uniform sampler2D uSource;
uniform float uGamma;
uniform float uPivot;
uniform bool uLinearLight;
out vec4 outColor;

float decode1(float x) {
  return x <= 0.04045 ? x / 12.92 : pow((x + 0.055) / 1.055, 2.4);
}
vec3 decode3(vec3 x) {
  return vec3(decode1(x.r), decode1(x.g), decode1(x.b));
}
void main() {
  vec4 c = texture(uSource, vUv);
  if (c.a <= 0.0) { outColor = vec4(0.0); return; }
  if (uLinearLight) c.rgb = decode3(c.rgb / c.a) * c.a;
  if (uGamma > 1.0001) c.rgb = uPivot * pow(max(c.rgb / max(uPivot, 0.05), vec3(0.0)), vec3(uGamma));
  outColor = c;
}`;

const DOWNSAMPLE_SHADER = `#version 300 es
precision highp float;
uniform sampler2D uInput;
uniform ivec2 uSourceSize;
uniform int uFactor;
out vec4 outColor;
void main() {
  ivec2 cell = ivec2(gl_FragCoord.xy);
  ivec2 base = cell * uFactor;
  vec4 sum = vec4(0.0);
  float count = 0.0;
  for (int y = 0; y < 4; ++y) {
    if (y >= uFactor || base.y + y >= uSourceSize.y) break;
    for (int x = 0; x < 4; ++x) {
      if (x >= uFactor || base.x + x >= uSourceSize.x) break;
      sum += texelFetch(uInput, base + ivec2(x, y), 0);
      count += 1.0;
    }
  }
  outColor = count > 0.0 ? sum / count : vec4(0.0);
}`;

const GATHER_SHADER = `#version 300 es
precision highp float;
precision highp int;
uniform sampler2D uPrepared;
uniform sampler2D uOriginal;
uniform sampler2D uSamples;
uniform vec2 uSize;
uniform vec2 uFocusSize;
uniform float uCenterX;
uniform float uCenterY;
uniform float uZone;
uniform float uFeather;
uniform float uAngle;
uniform float uMode;
uniform bool uInvert;
uniform bool uShowMap;
uniform float uRadius;
uniform float uDispersion;
uniform float uEdge;
uniform float uAnamorphic;
uniform float uGamma;
uniform float uPivot;
uniform float uBokehStrength;
uniform bool uLinearLight;
uniform bool uRepeatEdge;
uniform int uQuality;
uniform vec3 uInnerColor;
uniform vec3 uMiddleColor;
uniform vec3 uOuterColor;
out vec4 outColor;

float saturate1(float x) { return clamp(x, 0.0, 1.0); }
float encode1(float x) {
  return x <= 0.0031308 ? x * 12.92 : 1.055 * pow(max(x, 0.0), 1.0 / 2.4) - 0.055;
}
vec3 encode3(vec3 x) { return vec3(encode1(x.r), encode1(x.g), encode1(x.b)); }

vec2 toUv(vec2 p) { return vec2(p.x / uSize.x, 1.0 - p.y / uSize.y); }

float focusAmount(vec2 p) {
  vec2 center = uFocusSize * vec2(uCenterX, uCenterY) / 100.0;
  vec2 d = p - center;
  float a = 1.0;
  if (uMode < 1.5) {
    a = saturate1((abs(-sin(uAngle) * d.x + cos(uAngle) * d.y) - uZone * 0.5) / max(uFeather, 1e-6));
  } else if (uMode < 2.5) {
    a = saturate1((length(d) - uZone) / max(uFeather, 1e-6));
  }
  return uInvert ? 1.0 - a : a;
}

float kernelWeight(float rn, float rn2) {
  return uEdge >= 0.0
    ? 1.0 - uEdge + uEdge * (0.15 + 0.85 * rn * rn2)
    : 1.0 + uEdge - uEdge * exp(-2.0 * rn2);
}

vec4 fetchPrepared(vec2 q) {
  float coverage = 1.0;
  if (uRepeatEdge) {
    q = clamp(q, vec2(0.5), uSize - vec2(0.5));
  } else {
    vec2 low = clamp(q + vec2(0.5), 0.0, 1.0);
    vec2 high = clamp(uSize + vec2(0.5) - q, 0.0, 1.0);
    coverage = low.x * low.y * high.x * high.y;
    q = clamp(q, vec2(0.5), uSize - vec2(0.5));
  }
  return texture(uPrepared, toUv(q)) * coverage;
}

vec3 isolateHighlight(vec4 c) {
  if (c.a <= 1e-6) return vec3(0.0);
  float luminance = dot(c.rgb / c.a, vec3(0.2126, 0.7152, 0.0722));
  float start = max(uPivot, 0.05);
  float mask = saturate1((luminance - start) / max(1.0 - start, 0.05));
  mask = mask * mask * (3.0 - 2.0 * mask);
  return c.rgb * mask;
}

struct GatherResult {
  vec4 color;
  vec3 highlight;
};

GatherResult gatherColor(vec2 p, float radius) {
  GatherResult result;
  vec4 base = fetchPrepared(p);
  result.color = base;
  result.highlight = uBokehStrength > 1e-6 ? isolateHighlight(base) : vec3(0.0);
  if (radius < 0.5) return result;
  float aspect = sqrt(clamp(uAnamorphic, 0.25, 4.0));
  vec2 axes = max(vec2(radius / aspect, radius * aspect), vec2(0.5));
  ivec2 ir = ivec2(ceil(axes));
  vec4 sum = vec4(0.0);
  vec3 highlightSum = vec3(0.0);
  float total = 0.0;
  if ((2 * ir.x + 1) * (2 * ir.y + 1) <= 169) {
    for (int y = -12; y <= 12; ++y) {
      if (abs(y) > ir.y) continue;
      for (int x = -12; x <= 12; ++x) {
        if (abs(x) > ir.x) continue;
        vec2 n = vec2(float(x), float(y)) / axes;
        float rn2 = dot(n, n);
        if (rn2 > 1.0) continue;
        float rn = sqrt(rn2);
        float w = kernelWeight(rn, rn2) * saturate1((1.0 - rn) * min(axes.x, axes.y));
        vec4 tap = fetchPrepared(p + vec2(float(x), float(y)));
        sum += tap * w;
        if (uBokehStrength > 1e-6) highlightSum += isolateHighlight(tap) * w;
        total += w;
      }
    }
  } else {
    int count;
    int offset;
    bool enhanced = uBokehStrength > 1e-6;
    if (!enhanced && radius <= 16.0) { count = 64; offset = 0; }
    else if (!enhanced && radius <= 40.0) { count = 128; offset = 64; }
    else if (uQuality <= 128) { count = 128; offset = 64; }
    else if (uQuality <= 256) { count = 256; offset = 192; }
    else { count = 512; offset = 448; }
    for (int i = 0; i < 512; ++i) {
      if (i >= count) break;
      vec4 s = texelFetch(uSamples, ivec2(offset + i, 0), 0);
      float w = kernelWeight(s.z, s.w);
      vec4 tap = fetchPrepared(p + s.xy * axes);
      sum += tap * w;
      if (enhanced) highlightSum += isolateHighlight(tap) * w;
      total += w;
    }
  }
  if (total > 0.0) {
    result.color = sum / total;
    if (uBokehStrength > 1e-6) result.highlight = highlightSum / total;
  }
  return result;
}

void main() {
  vec2 p = vec2(gl_FragCoord.x, uSize.y - gl_FragCoord.y);
  vec4 original = texelFetch(uOriginal, ivec2(gl_FragCoord.xy), 0);
  float amount = focusAmount(p);
  if (uShowMap) {
    outColor = vec4(vec3(amount), original.a);
    return;
  }
  if (amount <= 0.0 || uRadius < 0.5) { outColor = original; return; }
  float radius = amount * uRadius;
  GatherResult midSample = gatherColor(p, radius);
  vec4 mid = midSample.color;
  vec3 c0 = uInnerColor;
  vec3 c1 = uMiddleColor;
  vec3 c2 = uOuterColor;
  if (dot(c0 + c1 + c2, vec3(1.0)) < 1e-8) {
    c0 = vec3(1.0, 0.0, 0.0);
    c1 = vec3(0.0, 1.0, 0.0);
    c2 = vec3(0.0, 0.0, 1.0);
  }
  vec3 denom = max(c0 + c1 + c2, vec3(1e-8));
  vec3 result;
  if (abs(uDispersion) < 1e-4) {
    result = mid.rgb * (c0 + c1 + c2) / denom;
  } else {
    vec3 inside = vec3(0.0);
    vec3 outside = vec3(0.0);
    if (dot(c0, c0) > 0.0) {
      GatherResult insideSample = gatherColor(p, max(radius * (1.0 - uDispersion), 0.0));
      inside = insideSample.color.rgb;
    }
    if (dot(c2, c2) > 0.0) {
      GatherResult outsideSample = gatherColor(p, max(radius * (1.0 + uDispersion), 0.0));
      outside = outsideSample.color.rgb;
    }
    result = (inside * c0 + mid.rgb * c1 + outside * c2) / denom;
  }
  vec3 bokeh = midSample.highlight;
  if (uGamma > 1.0001) {
    result = uPivot * pow(max(result / max(uPivot, 0.05), vec3(0.0)), vec3(1.0 / uGamma));
    if (uBokehStrength > 1e-6) bokeh = uPivot * pow(max(bokeh / max(uPivot, 0.05), vec3(0.0)), vec3(1.0 / uGamma));
  }
  float a = mid.a;
  if (a <= 1e-6) { outColor = vec4(0.0); return; }
  if (uBokehStrength > 1e-6) {
    vec3 baseStraight = result / a;
    vec3 bokehStraight = clamp(bokeh * uBokehStrength / a, 0.0, 1.0);
    result = (vec3(1.0) - (vec3(1.0) - baseStraight) * (vec3(1.0) - bokehStraight)) * a;
  }
  if (uLinearLight) result = encode3(result / a) * a;
  outColor = vec4(result, a);
}`;

const COMPOSITE_SHADER = `#version 300 es
precision highp float;
uniform sampler2D uRaw;
uniform sampler2D uBlur;
uniform vec2 uRawSize;
uniform vec2 uBlurSize;
uniform float uCenterX;
uniform float uCenterY;
uniform float uZone;
uniform float uFeather;
uniform float uAngle;
uniform float uMode;
uniform bool uInvert;
uniform int uFactor;
uniform float uMaxRadius;
out vec4 outColor;
float focusAmount(vec2 p) {
  vec2 center = uRawSize * vec2(uCenterX, uCenterY) / 100.0;
  vec2 d = p - center;
  float a = 1.0;
  if (uMode < 1.5) a = clamp((abs(-sin(uAngle) * d.x + cos(uAngle) * d.y) - uZone * 0.5) / max(uFeather, 1e-6), 0.0, 1.0);
  else if (uMode < 2.5) a = clamp((length(d) - uZone) / max(uFeather, 1e-6), 0.0, 1.0);
  return uInvert ? 1.0 - a : a;
}
void main() {
  vec2 p = vec2(gl_FragCoord.x, uRawSize.y - gl_FragCoord.y);
  vec4 raw = texelFetch(uRaw, ivec2(gl_FragCoord.xy), 0);
  float f = float(max(uFactor, 1));
  vec2 q = p / f;
  vec2 uv = vec2(q.x / uBlurSize.x, 1.0 - q.y / uBlurSize.y);
  vec4 blurred = texture(uBlur, uv);
  float amount = focusAmount(p);
  float e1 = uMaxRadius > 1.0 ? 4.0 / uMaxRadius : 1.0;
  float blend = uFactor == 1 ? 1.0 : (amount <= 0.0 ? 0.0 : (amount >= e1 ? 1.0 : amount / e1));
  outColor = mix(raw, blurred, blend);
}`;

class CoolBlurRenderer {
  constructor(canvas) {
    this.canvas = canvas;
    this.gl = canvas.getContext("webgl2", {
      alpha: false,
      antialias: false,
      premultipliedAlpha: true,
      preserveDrawingBuffer: true,
    });
    if (!this.gl) throw new Error("このブラウザはWebGL2に対応していません。");
    const gl = this.gl;
    if ("drawingBufferColorSpace" in gl) gl.drawingBufferColorSpace = "srgb";
    const floatRenderTarget = Boolean(gl.getExtension("EXT_color_buffer_float"));
    const floatLinearFiltering = Boolean(gl.getExtension("OES_texture_float_linear"));
    this.floatTarget = floatRenderTarget && floatLinearFiltering;
    this.programs = {
      prepare: this.createProgram(PREPARE_SHADER),
      downsample: this.createProgram(DOWNSAMPLE_SHADER),
      gather: this.createProgram(GATHER_SHADER),
      composite: this.createProgram(COMPOSITE_SHADER),
    };
    this.vao = gl.createVertexArray();
    gl.bindVertexArray(this.vao);
    this.sourceTexture = this.createTexture(false);
    this.sampleTexture = this.createSampleTexture();
    this.targets = {};
    gl.disable(gl.BLEND);
    gl.disable(gl.DEPTH_TEST);
  }

  createProgram(fragmentSource) {
    const gl = this.gl;
    const compile = (type, source) => {
      const shader = gl.createShader(type);
      gl.shaderSource(shader, source);
      gl.compileShader(shader);
      if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        const log = gl.getShaderInfoLog(shader);
        gl.deleteShader(shader);
        throw new Error(`シェーダーのコンパイルに失敗しました: ${log}`);
      }
      return shader;
    };
    const vertex = compile(gl.VERTEX_SHADER, VERTEX_SHADER);
    const fragment = compile(gl.FRAGMENT_SHADER, fragmentSource);
    const program = gl.createProgram();
    gl.attachShader(program, vertex);
    gl.attachShader(program, fragment);
    gl.linkProgram(program);
    gl.deleteShader(vertex);
    gl.deleteShader(fragment);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
      throw new Error(`シェーダーのリンクに失敗しました: ${gl.getProgramInfoLog(program)}`);
    }
    return program;
  }

  createTexture(linear = true) {
    const gl = this.gl;
    const texture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, linear ? gl.LINEAR : gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, linear ? gl.LINEAR : gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    return texture;
  }

  createSampleTexture() {
    const gl = this.gl;
    const data = generateVogelSamples();
    const cursor = data.length / 4;
    const texture = this.createTexture(false);
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA32F, cursor, 1, 0, gl.RGBA, gl.FLOAT, data);
    return texture;
  }

  createTarget(width, height, linear = true) {
    const gl = this.gl;
    const texture = this.createTexture(linear);
    gl.bindTexture(gl.TEXTURE_2D, texture);
    if (this.floatTarget) {
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA16F, width, height, 0, gl.RGBA, gl.HALF_FLOAT, null);
    } else {
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, width, height, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
    }
    const framebuffer = gl.createFramebuffer();
    gl.bindFramebuffer(gl.FRAMEBUFFER, framebuffer);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, texture, 0);
    if (gl.checkFramebufferStatus(gl.FRAMEBUFFER) !== gl.FRAMEBUFFER_COMPLETE) {
      throw new Error("描画バッファを作成できませんでした。");
    }
    return { texture, framebuffer, width, height };
  }

  disposeTargets() {
    const gl = this.gl;
    for (const target of Object.values(this.targets)) {
      if (!target) continue;
      gl.deleteTexture(target.texture);
      gl.deleteFramebuffer(target.framebuffer);
    }
    this.targets = {};
  }

  ensureTargets(width, height, factor) {
    const lowWidth = Math.ceil(width / factor);
    const lowHeight = Math.ceil(height / factor);
    const key = `${width}x${height}:${factor}:${this.floatTarget}`;
    if (this.targetKey === key) return;
    this.disposeTargets();
    this.targets.prepare = this.createTarget(width, height, true);
    this.targets.downsample = this.createTarget(lowWidth, lowHeight, true);
    this.targets.gatherFull = this.createTarget(width, height, true);
    this.targets.gatherLow = this.createTarget(lowWidth, lowHeight, true);
    this.targetKey = key;
  }

  uploadImage(image, selectedScale) {
    const maxDimension = 2048;
    const cap = Math.min(1, maxDimension / Math.max(image.naturalWidth, image.naturalHeight));
    const scale = Math.min(selectedScale, cap);
    const width = Math.max(1, Math.round(image.naturalWidth * scale));
    const height = Math.max(1, Math.round(image.naturalHeight * scale));
    const staging = document.createElement("canvas");
    staging.width = width;
    staging.height = height;
    const context = staging.getContext("2d", { alpha: true });
    context.imageSmoothingEnabled = true;
    context.imageSmoothingQuality = "high";
    context.drawImage(image, 0, 0, width, height);
    const gl = this.gl;
    gl.bindTexture(gl.TEXTURE_2D, this.sourceTexture);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
    gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, true);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, gl.RGBA, gl.UNSIGNED_BYTE, staging);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
    this.canvas.width = width;
    this.canvas.height = height;
    this.canvas.style.aspectRatio = `${width} / ${height}`;
    this.imageInfo = { width, height, scale, capped: scale < selectedScale };
    this.targetKey = "";
    return this.imageInfo;
  }

  bindTexture(unit, texture, program, name) {
    const gl = this.gl;
    gl.activeTexture(gl.TEXTURE0 + unit);
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.uniform1i(gl.getUniformLocation(program, name), unit);
  }

  setUniform(program, name, value) {
    const gl = this.gl;
    const location = gl.getUniformLocation(program, name);
    if (location === null) return;
    if (typeof value === "boolean") gl.uniform1i(location, value ? 1 : 0);
    else if (name === "uFactor" || name === "uQuality") gl.uniform1i(location, value);
    else if (name === "uSourceSize") gl.uniform2i(location, value[0], value[1]);
    else if (typeof value === "number") gl.uniform1f(location, value);
    else if (value.length === 2) gl.uniform2f(location, value[0], value[1]);
    else if (value.length === 3) gl.uniform3f(location, value[0], value[1], value[2]);
  }

  draw(program, target, width, height) {
    const gl = this.gl;
    gl.bindFramebuffer(gl.FRAMEBUFFER, target?.framebuffer ?? null);
    gl.viewport(0, 0, width, height);
    gl.useProgram(program);
    gl.bindVertexArray(this.vao);
    gl.drawArrays(gl.TRIANGLES, 0, 3);
  }

  render(params) {
    if (!this.imageInfo) return null;
    const gl = this.gl;
    const { width, height, scale } = this.imageInfo;
    // AE scales pixel-valued parameters to the current render resolution
    // before selecting the CUDA downsample factor.
    const effectiveRadius = params.radius * scale;
    const factor = chooseDownsampleFactor(params.mode, effectiveRadius, params.downsample, params.showMap);
    this.ensureTargets(width, height, factor);
    const lowWidth = Math.ceil(width / factor);
    const lowHeight = Math.ceil(height / factor);
    const gamma = 1 + params.highlight * 0.04;
    const angle = params.angle * Math.PI / 180;

    let program = this.programs.prepare;
    gl.useProgram(program);
    this.bindTexture(0, this.sourceTexture, program, "uSource");
    this.setUniform(program, "uGamma", gamma);
    this.setUniform(program, "uPivot", params.threshold);
    this.setUniform(program, "uLinearLight", params.linearLight);
    this.draw(program, this.targets.prepare, width, height);

    let preparedTexture = this.targets.prepare.texture;
    let gatherWidth = width;
    let gatherHeight = height;
    let originalTexture = this.sourceTexture;
    let gatherTarget = this.targets.gatherFull;

    if (factor > 1) {
      program = this.programs.downsample;
      gl.useProgram(program);
      this.bindTexture(0, preparedTexture, program, "uInput");
      this.setUniform(program, "uSourceSize", [width, height]);
      this.setUniform(program, "uFactor", factor);
      this.draw(program, this.targets.downsample, lowWidth, lowHeight);
      preparedTexture = this.targets.downsample.texture;
      originalTexture = this.targets.downsample.texture;
      gatherWidth = lowWidth;
      gatherHeight = lowHeight;
      gatherTarget = this.targets.gatherLow;
    }

    program = this.programs.gather;
    gl.useProgram(program);
    this.bindTexture(0, preparedTexture, program, "uPrepared");
    this.bindTexture(1, originalTexture, program, "uOriginal");
    this.bindTexture(2, this.sampleTexture, program, "uSamples");
    const spatialScale = scale / factor;
    this.setUniform(program, "uSize", [gatherWidth, gatherHeight]);
    this.setUniform(program, "uFocusSize", [width / factor, height / factor]);
    this.setUniform(program, "uCenterX", params.centerX);
    this.setUniform(program, "uCenterY", params.centerY);
    this.setUniform(program, "uZone", params.width * spatialScale);
    this.setUniform(program, "uFeather", params.feather * spatialScale);
    this.setUniform(program, "uAngle", angle);
    this.setUniform(program, "uMode", Number(params.mode));
    this.setUniform(program, "uInvert", params.invert);
    this.setUniform(program, "uShowMap", params.showMap);
    this.setUniform(program, "uRadius", params.radius * spatialScale);
    this.setUniform(program, "uDispersion", params.dispersion * 0.003);
    this.setUniform(program, "uEdge", params.bokehEdge * 0.01);
    this.setUniform(program, "uAnamorphic", params.anamorphic);
    this.setUniform(program, "uGamma", gamma);
    this.setUniform(program, "uPivot", params.threshold);
    this.setUniform(program, "uBokehStrength", params.bokehStrength * 0.01);
    this.setUniform(program, "uLinearLight", params.linearLight);
    this.setUniform(program, "uRepeatEdge", params.edgeRepeat);
    this.setUniform(program, "uQuality", Number(params.quality));
    this.setUniform(program, "uInnerColor", hexToRgb(params.innerColor));
    this.setUniform(program, "uMiddleColor", hexToRgb(params.middleColor));
    this.setUniform(program, "uOuterColor", hexToRgb(params.outerColor));
    this.draw(program, gatherTarget, gatherWidth, gatherHeight);

    program = this.programs.composite;
    gl.useProgram(program);
    this.bindTexture(0, this.sourceTexture, program, "uRaw");
    this.bindTexture(1, gatherTarget.texture, program, "uBlur");
    this.setUniform(program, "uRawSize", [width, height]);
    this.setUniform(program, "uBlurSize", [gatherWidth, gatherHeight]);
    this.setUniform(program, "uCenterX", params.centerX);
    this.setUniform(program, "uCenterY", params.centerY);
    this.setUniform(program, "uZone", params.width * scale);
    this.setUniform(program, "uFeather", params.feather * scale);
    this.setUniform(program, "uAngle", angle);
    this.setUniform(program, "uMode", Number(params.mode));
    this.setUniform(program, "uInvert", params.invert);
    this.setUniform(program, "uFactor", factor);
    this.setUniform(program, "uMaxRadius", effectiveRadius);
    this.draw(program, null, width, height);
    gl.finish();
    return { factor, width, height, scale, floatTarget: this.floatTarget };
  }
}

function hexToRgb(hex) {
  const value = Number.parseInt(hex.slice(1), 16);
  return [((value >> 16) & 255) / 255, ((value >> 8) & 255) / 255, (value & 255) / 255];
}

function createRangeControls() {
  for (const [containerId, controls] of Object.entries(RANGE_GROUPS)) {
    const container = document.getElementById(containerId);
    for (const [id, label, min, max, step, unit] of controls) {
      const row = document.createElement("label");
      row.className = "range-row";
      row.innerHTML = `<span>${label}</span><input id="${id}" data-param="${id}" type="range" min="${min}" max="${max}" step="${step}"><output class="range-value" for="${id}"></output>`;
      row.dataset.unit = unit;
      container.appendChild(row);
    }
  }
}

function setControls(values) {
  for (const [key, value] of Object.entries(values)) {
    const input = document.querySelector(`[data-param="${key}"]`);
    if (!input) continue;
    if (input.type === "checkbox") input.checked = Boolean(value);
    else input.value = String(value);
  }
  updateValueLabels();
}

function updateValueLabels() {
  document.querySelectorAll(".range-row").forEach((row) => {
    const input = row.querySelector("input");
    const output = row.querySelector("output");
    output.value = `${input.value}${row.dataset.unit}`;
  });
}

function readControls() {
  const values = {};
  document.querySelectorAll("[data-param]").forEach((input) => {
    if (input.type === "checkbox") values[input.dataset.param] = input.checked;
    else if (input.type === "color") values[input.dataset.param] = input.value;
    else values[input.dataset.param] = Number(input.value);
  });
  return values;
}

function serializeState(values) {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) {
    if (value !== DEFAULTS[key]) query.set(key, typeof value === "boolean" ? (value ? "1" : "0") : String(value));
  }
  return query.toString();
}

function stateFromHash() {
  const values = { ...DEFAULTS };
  const query = new URLSearchParams(location.hash.slice(1));
  for (const [key, raw] of query) {
    if (!(key in DEFAULTS)) continue;
    if (typeof DEFAULTS[key] === "boolean") values[key] = raw === "1" || raw === "true";
    else if (typeof DEFAULTS[key] === "number") {
      const number = Number(raw);
      if (Number.isFinite(number)) values[key] = number;
    } else values[key] = raw;
  }
  return values;
}

function loadImage(src) {
  return new Promise((resolve, reject) => {
    const image = new Image();
    image.decoding = "async";
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error("画像を読み込めませんでした。"));
    image.src = src;
  });
}

createRangeControls();
setControls(stateFromHash());

const canvas = document.getElementById("preview");
const loading = document.getElementById("loading");
const message = document.getElementById("message");
const resolution = document.getElementById("resolution");
const factorLabel = document.getElementById("factor");
const timing = document.getElementById("timing");
const precision = document.getElementById("precision");
let renderer;
let sourceImage;
let sourceUrl;
let renderTimer;
let imageScale = -1;

function setMessage(text, error = false) {
  message.textContent = text;
  message.classList.toggle("error", error);
}

function requestRender(immediate = false) {
  clearTimeout(renderTimer);
  renderTimer = setTimeout(renderNow, immediate ? 0 : 55);
}

function renderNow() {
  if (!renderer || !sourceImage) return;
  try {
    const params = readControls();
    if (imageScale !== params.previewScale) {
      const info = renderer.uploadImage(sourceImage, params.previewScale);
      imageScale = params.previewScale;
      if (info.capped) setMessage("大きな画像のため、長辺2048px以下に制限しています。");
      else if (params.previewScale < 1) setMessage("軽量プレビューです。100%表示で元のピクセル半径になります。");
      else setMessage("");
    }
    const start = performance.now();
    const result = renderer.render(params);
    const elapsed = performance.now() - start;
    resolution.textContent = `${result.width} × ${result.height}`;
    factorLabel.textContent = `${params.downsample === 0 ? "Auto" : "固定"}: ${result.factor}×`;
    timing.textContent = `${elapsed.toFixed(1)} ms`;
    precision.textContent = result.floatTarget ? "16-bit float" : "8-bit fallback";
    loading.classList.add("hidden");
  } catch (error) {
    setMessage(error.message, true);
    loading.classList.add("hidden");
    console.error(error);
  }
}

document.querySelectorAll("[data-param]").forEach((input) => {
  input.addEventListener("input", () => {
    updateValueLabels();
    requestRender();
  });
  input.addEventListener("change", () => requestRender(true));
});

const presetContainer = document.getElementById("presets");
for (const [label, values] of PRESETS) {
  const button = document.createElement("button");
  button.type = "button";
  button.className = "preset";
  button.textContent = label;
  button.addEventListener("click", () => {
    setControls({ ...DEFAULTS, ...values, previewScale: readControls().previewScale });
    requestRender(true);
  });
  presetContainer.appendChild(button);
}

document.getElementById("resetAll").addEventListener("click", () => {
  setControls(DEFAULTS);
  imageScale = -1;
  requestRender(true);
});

document.getElementById("saveImage").addEventListener("click", () => {
  canvas.toBlob((blob) => {
    if (!blob) return;
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "sa_coolblur_preview.png";
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }, "image/png");
});

document.getElementById("copyUrl").addEventListener("click", async () => {
  const url = `${location.origin}${location.pathname}#${serializeState(readControls())}`;
  try {
    await navigator.clipboard.writeText(url);
    setMessage("設定URLをコピーしました。");
  } catch {
    prompt("このURLをコピーしてください", url);
  }
});

document.getElementById("imageFile").addEventListener("change", async (event) => {
  const file = event.target.files?.[0];
  if (!file) return;
  try {
    if (sourceUrl) URL.revokeObjectURL(sourceUrl);
    sourceUrl = URL.createObjectURL(file);
    sourceImage = await loadImage(sourceUrl);
    imageScale = -1;
    requestRender(true);
  } catch (error) {
    setMessage(error.message, true);
  }
});

window.addEventListener("hashchange", () => {
  setControls(stateFromHash());
  imageScale = -1;
  requestRender(true);
});

async function start() {
  try {
    renderer = new CoolBlurRenderer(canvas);
    sourceImage = await loadImage("source_16x9.jpg");
    requestRender(true);
  } catch (error) {
    loading.textContent = "起動できませんでした";
    loading.classList.add("hidden");
    precision.textContent = "—";
    setMessage(error.message, true);
    console.error(error);
  }
}

start();
