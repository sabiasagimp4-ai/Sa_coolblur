#define D2D_REQUIRES_SCENE_POSITION
#define D2D_ENTRY main
#include <d2d1effecthelpers.hlsli>
#include "Color.hlsli"
#include "Samples.hlsli"
float centerX, centerY, zone, feather;
float angle, mode, invertFocus, showMap;
float maxRadius, dispersion, edge, anamorphic;
float gamma, pivot, linearLight, repeatEdge;
float focusDistance, focusRange, depthFeather, hasDepth;
float sampleCount, downsampleScale, reserved1, reserved2;
float4 innerColor, middleColor, outerColor;
float4 bounds, depthBounds;
float4 focusBounds;

float focusAmount(float2 p)
{
    float2 center = bounds.xy + (bounds.zw - bounds.xy) * float2(centerX, centerY) / 100;
    // CUDA's reduced-resolution path divides the source-space center by the
    // downsample factor; it does not re-normalize it against the rounded-up
    // low-resolution image dimensions.  The fourth input supplies the source
    // rectangle so the same calculation survives odd-sized frames.
    if (downsampleScale > 1.5)
        center = bounds.xy + (focusBounds.zw - focusBounds.xy) * float2(centerX, centerY) / (100 * downsampleScale);
    float2 d = p - center;
    float a = 1;
    if (mode < 1.5) a = saturate((abs(-sin(angle) * d.x + cos(angle) * d.y) - zone * .5) / max(feather, 1e-6));
    else if (mode < 2.5) a = saturate((length(d) - zone) / max(feather, 1e-6));
    else if (mode < 3.5)
    {

        // BuildDepthMap in the source plugin maps mismatched sizes with
        // sx=floor(x*depthWidth/sourceWidth), not centre-based resampling.
        float2 sourceCell = floor(p - bounds.xy);
        float2 sourceSize = max(bounds.zw - bounds.xy, 1);
        float2 depthSize = max(depthBounds.zw - depthBounds.xy, 1);
        float2 depthCell = floor(sourceCell * depthSize / sourceSize);
        float2 dp = clamp(depthBounds.xy + depthCell + .5, depthBounds.xy + .5, depthBounds.zw - .5);
        float4 duv = D2DGetInputCoordinate(1);
        float4 dc = InputTexture1.SampleLevel(InputSampler1, duv.xy + duv.zw * (dp - p), 0);
        float depth = dot(dc.rgb, float3(.2126, .7152, .0722));
        a = saturate((abs(depth - focusDistance) - focusRange * .5) / max(depthFeather, 1e-6));
    }
    float result = invertFocus > .5 ? 1 - a : a;
    return mode >= 2.5 && mode < 3.5 && hasDepth < .5 ? 0 : result;
}
float weight(float rn, float rn2)
{
    return edge >= 0 ? 1 - edge + edge * (.15 + .85 * rn * rn2) : 1 + edge - edge * exp(-2 * rn2);
}
float4 fetch(float2 p, float2 offset)
{
    float2 q = p + offset;
    if (repeatEdge > .5) q = clamp(q, bounds.xy + .5, bounds.zw - .5);
    float coverage = 1;
    if (repeatEdge < .5)
    {
        float2 low = saturate(q - bounds.xy + .5);
        float2 high = saturate(bounds.zw + .5 - q);
        coverage = low.x * low.y * high.x * high.y;
        q = clamp(q, bounds.xy + .5, bounds.zw - .5);
    }
    float4 uv = D2DGetInputCoordinate(0);
    return InputTexture0.SampleLevel(InputSampler0, uv.xy + uv.zw * (q - p), 0) * coverage;
}
float4 gather(float2 p, float radius)
{
    float4 result = fetch(p, 0);
    if (radius >= .5)
    {
    float aspect = sqrt(clamp(anamorphic, .25, 4));
    float2 axes = max(float2(radius / aspect, radius * aspect), .5);
    int2 ir = (int2)ceil(axes);
    float4 sum = 0;
    float total = 0;
    if ((2 * ir.x + 1) * (2 * ir.y + 1) <= 169)
    {
        [loop] for (int y = -ir.y; y <= ir.y; ++y)
        [loop] for (int x = -ir.x; x <= ir.x; ++x)
        {
            float2 n = float2(x, y) / axes;
            float rn2 = dot(n, n);
            if (rn2 > 1) continue;
            float rn = sqrt(rn2);
            float w = weight(rn, rn2) * saturate((1 - rn) * min(axes.x, axes.y));
            sum += fetch(p, float2(x, y)) * w;
            total += w;
        }
    }
    else
    {
        // Match the original plugin's hybrid CUDA gather.  The quality
        // setting is only used for genuinely large radii; small/medium
        // radii use the same fixed 64/128-point tiers as the AE plugin.
        // Keep the loop bound tied to the uniform quality setting.  FXC
        // rejects a [loop] whose bound is first rewritten to a compile-time
        // tier; the early break below preserves the same runtime tier choice
        // without changing the shader's instruction shape.
        int count = (int)sampleCount;
        [loop] for (int i = 0; i < count; ++i)
        {
            if (radius <= 16 && i >= 64) break;
            if (radius > 16 && radius <= 40 && i >= 128) break;
            float4 s = 0;
            if (radius <= 16) s = samples64[i];
            else if (radius <= 40) s = samples128[i];
            else if (count <= 128) s = samples128[i];
            else if (count <= 256) s = samples256[i];
            else s = samples512[i];
            float w = weight(s.z, s.w);
            sum += fetch(p, s.xy * axes) * w;
            total += w;
        }
    }
    if (total > 0) result = sum / total;
    }
    return result;
}
D2D_PS_ENTRY(main)
{
    float2 p = D2DGetScenePosition().xy;
    float4 original = D2DGetInput(2);
    float amount = focusAmount(p);
    // The source CPU diagnostic writes the raw amount to RGB and preserves
    // alpha independently, including RGB values greater than alpha.
    if (showMap > .5) return float4(amount.xxx, original.a);
    if (amount <= 0 || maxRadius < .5) return original;
    float radius = amount * maxRadius;
    float4 mid = gather(p, radius);
    float3 c0 = innerColor.rgb, c1 = middleColor.rgb, c2 = outerColor.rgb;
    if (dot(c0 + c1 + c2, float3(1,1,1)) < 1e-8)
    { c0 = float3(1,0,0); c1 = float3(0,1,0); c2 = float3(0,0,1); }
    float3 denom = max(c0 + c1 + c2, 1e-8);
    float3 result;
    if (abs(dispersion) < 1e-4) result = mid.rgb * (c0+c1+c2) / denom;
    else
    {
        float3 inside = dot(c0,c0) > 0 ? gather(p, max(radius * (1 - dispersion), 0)).rgb : 0;
        float3 outside = dot(c2,c2) > 0 ? gather(p, max(radius * (1 + dispersion), 0)).rgb : 0;
        result = (inside * c0 + mid.rgb * c1 + outside * c2) / denom;
    }
    if (gamma > 1.0001) result = pivot * pow(max(result / max(pivot, .05), 0), 1 / gamma);
    float a = mid.a;
    if (a <= 1e-6) return 0;
    if (linearLight > .5) result = encode3(result / a) * a;
    // Keep the original plugin's premultiplied values.  Do not clamp RGB to
    // alpha here: the AE GPU path writes the finite post-processed channels
    // unchanged, and spectral taps may legitimately exceed the mid alpha.
    return float4(result, a);
}
