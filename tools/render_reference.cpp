// Deterministic CPU transcription of the CUDA GPU path used for README assets.
// It includes the source plugin's auto-downsample branch (box average, low-res
// hybrid Vogel gather, bilinear upsample, focus-aware composite). No AI assets.
#include <algorithm>
#include <array>
#include <cmath>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <vector>

struct float4 { float x, y, z, w; float4(float a, float b, float c, float d) : x(a), y(b), z(c), w(d) {} };
#include "samples_cpu.inc"

using RGB = std::array<float, 3>;

float sat(float x) { return std::clamp(x, 0.f, 1.f); }
float decode(float x) { return x <= .04045f ? x / 12.92f : std::pow((x + .055f) / 1.055f, 2.4f); }
float encode(float x) { return x <= .0031308f ? x * 12.92f : 1.055f * std::pow(x, 1.f / 2.4f) - .055f; }
float weight(float edge, float rn, float rn2)
{
    return edge >= 0 ? 1 - edge + edge * (.15f + .85f * rn * rn2) : 1 + edge - edge * std::exp(-2 * rn2);
}

struct Params
{
    int mode;
    float radius, disp, edge, boost, aspect, cx, cy, width, feather, angle;
    bool invert;
    int color;
};

int W, H;
std::vector<RGB> original, prepared;

RGB lerp(const RGB& a, const RGB& b, float t)
{
    return { a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t };
}

float bilinearPrepared(float x, float y, int c)
{
    x = std::clamp(x, 0.f, float(W - 1));
    y = std::clamp(y, 0.f, float(H - 1));
    int x0 = int(x), y0 = int(y), x1 = std::min(x0 + 1, W - 1), y1 = std::min(y0 + 1, H - 1);
    float tx = x - x0, ty = y - y0;
    float a = prepared[y0 * W + x0][c], b = prepared[y0 * W + x1][c];
    float d = prepared[y1 * W + x0][c], e = prepared[y1 * W + x1][c];
    return (a + (b - a) * tx) * (1 - ty) + (d + (e - d) * tx) * ty;
}

float gather(int x, int y, float radius, int c, const Params& p)
{
    if (radius < .5f) return prepared[y * W + x][c];
    float aspect = std::sqrt(p.aspect);
    float rx = std::max(radius / aspect, .5f), ry = std::max(radius * aspect, .5f);
    int ix = int(std::ceil(rx)), iy = int(std::ceil(ry));
    float sum = 0, total = 0;
    if ((2 * ix + 1) * (2 * iy + 1) <= 169)
    {
        for (int dy = -iy; dy <= iy; ++dy) for (int dx = -ix; dx <= ix; ++dx)
        {
            float nx = dx / rx, ny = dy / ry, r2 = nx * nx + ny * ny;
            if (r2 > 1) continue;
            float rn = std::sqrt(r2), w = weight(p.edge, rn, r2) * sat((1 - rn) * std::min(rx, ry));
            sum += bilinearPrepared(float(x + dx), float(y + dy), c) * w;
            total += w;
        }
    }
    else
    {
        int count = radius <= 16 ? 64 : radius <= 40 ? 128 : 512; // README uses High quality.
        for (int i = 0; i < count; ++i)
        {
            float4 s = count <= 64 ? samples64[i] : count <= 128 ? samples128[i] : samples512[i];
            float w = weight(p.edge, s.z, s.w);
            sum += bilinearPrepared(x + s.x * rx, y + s.y * ry, c) * w;
            total += w;
        }
    }
    return total > 0 ? sum / total : prepared[y * W + x][c];
}

float focusAmount(float px, float py, float centerX, float centerY, float width, float feather, const Params& p)
{
    float amount = 1;
    if (p.mode == 1)
    {
        float d = std::abs(-std::sin(p.angle) * (px - centerX) + std::cos(p.angle) * (py - centerY));
        amount = sat((d - width * .5f) / std::max(feather, 1e-6f));
    }
    else if (p.mode == 2)
    {
        float dx = px - centerX, dy = py - centerY;
        amount = sat((std::sqrt(dx * dx + dy * dy) - width) / std::max(feather, 1e-6f));
    }
    return p.invert ? 1 - amount : amount;
}

using Weights = std::array<std::array<float, 3>, 3>;

Weights makeWeights(int color)
{
    Weights w{{{{1, 0, 0}}, {{0, 1, 0}}, {{0, 0, 1}}}};
    if (color == 1) w = {{{{0, 1, 1}}, {{1, 0, 1}}, {{1, 1, 0}}}};
    for (int c = 0; c < 3; ++c)
    {
        float total = w[0][c] + w[1][c] + w[2][c];
        for (int k = 0; k < 3; ++k) w[k][c] /= total;
    }
    return w;
}

RGB blurPixel(int x, int y, float amount, const Params& p, const Weights& weights)
{
    if (amount <= 0 || p.radius < .5f) return prepared[y * W + x];
    float radius = amount * p.radius;
    float radii[] = { std::max(radius * (1 - p.disp), 0.f), radius, std::max(radius * (1 + p.disp), 0.f) };
    float gamma = 1 + p.boost * .04f;
    RGB out{};
    for (int c = 0; c < 3; ++c)
    {
        float value = 0;
        for (int k = 0; k < 3; ++k)
            if (weights[k][c] > 0) value += weights[k][c] * gather(x, y, radii[k], c, p);
        if (gamma > 1.0001f) value = .8f * std::pow(std::max(value / .8f, 0.f), 1 / gamma);
        out[c] = encode(value);
    }
    return out;
}

int autoDownsample(const Params& p)
{
    if (p.mode != 1 && p.mode != 2 && p.mode != 4) return 1;
    int factor = p.radius >= 96 ? 4 : p.radius >= 32 ? 2 : 1;
    if (p.radius < 8) factor = 1;
    while (factor > 1 && p.radius / factor < 4) --factor;
    return factor;
}

RGB bilinearImage(const std::vector<RGB>& image, int width, int height, float x, float y)
{
    x = std::clamp(x, 0.f, float(width - 1));
    y = std::clamp(y, 0.f, float(height - 1));
    int x0 = int(x), y0 = int(y), x1 = std::min(x0 + 1, width - 1), y1 = std::min(y0 + 1, height - 1);
    float tx = x - x0, ty = y - y0;
    return lerp(lerp(image[y0 * width + x0], image[y0 * width + x1], tx),
                lerp(image[y1 * width + x0], image[y1 * width + x1], tx), ty);
}

std::vector<RGB> render(const Params& p, const Weights& weights)
{
    const int fullW = W, fullH = H;
    const float fullCenterX = fullW * p.cx / 100, fullCenterY = fullH * p.cy / 100;
    const int factor = autoDownsample(p);
    if (factor == 1)
    {
        std::vector<RGB> out = original;
        #pragma omp parallel for schedule(dynamic, 1)
        for (int y = 0; y < H; ++y) for (int x = 0; x < W; ++x)
        {
            float amount = focusAmount(x + .5f, y + .5f, fullCenterX, fullCenterY, p.width, p.feather, p);
            if (amount > 0 && p.radius >= .5f) out[y * W + x] = blurPixel(x, y, amount, p, weights);
        }
        return out;
    }

    // CoolBlurDownsampleCUDA: average full-resolution prepared pixels into
    // packed low-resolution content.  No interpolation is used in this step.
    std::vector<RGB> fullPrepared = prepared;
    const int lowW = (fullW + factor - 1) / factor, lowH = (fullH + factor - 1) / factor;
    std::vector<RGB> lowPrepared(lowW * lowH);
    for (int ly = 0; ly < lowH; ++ly) for (int lx = 0; lx < lowW; ++lx)
    {
        RGB sum{};
        int count = 0;
        for (int sy = 0; sy < factor; ++sy) for (int sx = 0; sx < factor; ++sx)
        {
            int x = lx * factor + sx, y = ly * factor + sy;
            if (x >= fullW || y >= fullH) continue;
            const RGB& value = fullPrepared[y * fullW + x];
            for (int c = 0; c < 3; ++c) sum[c] += value[c];
            ++count;
        }
        for (int c = 0; c < 3; ++c) lowPrepared[ly * lowW + lx][c] = sum[c] / count;
    }

    // CoolBlurGatherKernelCUDA with p2 (spatial values divided by factor).
    W = lowW; H = lowH; prepared = lowPrepared;
    Params low = p;
    low.radius /= factor; low.width /= factor; low.feather /= factor;
    const float lowCenterX = fullCenterX / factor, lowCenterY = fullCenterY / factor;
    std::vector<RGB> lowBlur(lowW * lowH);
    #pragma omp parallel for schedule(dynamic, 1)
    for (int y = 0; y < lowH; ++y) for (int x = 0; x < lowW; ++x)
    {
        float amount = focusAmount(x + .5f, y + .5f, lowCenterX, lowCenterY, low.width, low.feather, low);
        lowBlur[y * lowW + x] = blurPixel(x, y, amount, low, weights);
    }

    // CoolBlurCompositeCUDA: restore full resolution and bilinear-upsample
    // with gx = (x + .5) / factor - .5, then blend by full-res focus amount.
    W = fullW; H = fullH; prepared = std::move(fullPrepared);
    std::vector<RGB> out = original;
    #pragma omp parallel for schedule(dynamic, 1)
    for (int y = 0; y < fullH; ++y) for (int x = 0; x < fullW; ++x)
    {
        float amount = focusAmount(x + .5f, y + .5f, fullCenterX, fullCenterY, p.width, p.feather, p);
        if (amount <= 0 || p.radius < .5f) continue;
        float gx = (x + .5f) / factor - .5f, gy = (y + .5f) / factor - .5f;
        out[y * fullW + x] = lerp(original[y * fullW + x], bilinearImage(lowBlur, lowW, lowH, gx, gy), amount);
    }
    return out;
}

int main(int argc, char** argv)
{
    try
    {
        if (argc != 16) throw std::runtime_error("input.ppm output.ppm mode radius dispersion edge boost aspect cx cy width feather angle invert color");
        std::ifstream f(argv[1], std::ios::binary);
        std::string magic;
        int maxval;
        f >> magic >> W >> H >> maxval;
        f.get();
        if (magic != "P6" || maxval != 255 || W < 1 || H < 1) throw std::runtime_error("Expected RGB P6 input");
        std::vector<unsigned char> bytes(W * H * 3);
        f.read((char*)bytes.data(), bytes.size());
        if (!f) throw std::runtime_error("Short input");
        Params p{std::stoi(argv[3]), std::stof(argv[4]), std::stof(argv[5]) * .003f, std::stof(argv[6]) * .01f,
                 std::stof(argv[7]), std::stof(argv[8]), std::stof(argv[9]), std::stof(argv[10]), std::stof(argv[11]),
                 std::stof(argv[12]), std::stof(argv[13]) * 3.14159265358979323846f / 180, std::stoi(argv[14]) != 0,
                 std::stoi(argv[15])};
        if (p.mode != 1 && p.mode != 2 && p.mode != 4) throw std::runtime_error("Examples support linear/radial/uniform only");
        original.resize(W * H);
        prepared.resize(W * H);
        const float gamma = 1 + p.boost * .04f;
        for (int i = 0; i < W * H; ++i) for (int c = 0; c < 3; ++c)
        {
            float value = bytes[i * 3 + c] / 255.f;
            original[i][c] = value;
            value = decode(value);
            prepared[i][c] = gamma > 1.0001f ? .8f * std::pow(value / .8f, gamma) : value;
        }
        std::vector<RGB> result = render(p, makeWeights(p.color));
        for (int i = 0; i < W * H; ++i) for (int c = 0; c < 3; ++c)
            bytes[i * 3 + c] = (unsigned char)std::lround(255 * sat(result[i][c]));
        std::ofstream out(argv[2], std::ios::binary);
        out << "P6\n" << W << " " << H << "\n255\n";
        out.write((char*)bytes.data(), bytes.size());
        if (!out) throw std::runtime_error("Write failed");
    }
    catch (const std::exception& e) { std::cerr << e.what() << "\n"; return 1; }
}
