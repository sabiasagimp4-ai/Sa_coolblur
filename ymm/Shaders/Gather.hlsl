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
float sampleCount, reserved0, reserved1, reserved2;
float4 innerColor, middleColor, outerColor;
float4 bounds, depthBounds;

float focusAmount(float2 p)
{
    float2 center = bounds.xy + (bounds.zw - bounds.xy) * float2(centerX, centerY) / 100;
    float2 d = p - center;
    float a = 1;
    if (mode < 1.5) a = saturate((abs(-sin(angle) * d.x + cos(angle) * d.y) - zone * .5) / max(feather, 1e-6));
    else if (mode < 2.5) a = saturate((length(d) - zone) / max(feather, 1e-6));
    else if (mode < 3.5)
    {
        if (hasDepth < .5) return 0;
        float2 uv = (p - bounds.xy) / max(bounds.zw - bounds.xy, 1);
        float2 dp = clamp(depthBounds.xy + uv * (depthBounds.zw - depthBounds.xy), depthBounds.xy + .5, depthBounds.zw - .5);
        float4 dc = D2DSampleInputAtPosition(1, dp);
        float depth = dot(dc.rgb, float3(.2126, .7152, .0722));
        a = saturate((abs(depth - focusDistance) - focusRange * .5) / max(depthFeather, 1e-6));
    }
    return invertFocus > .5 ? 1 - a : a;
}
float weight(float rn, float rn2)
{
    return edge >= 0 ? 1 - edge + edge * (.15 + .85 * rn * rn2) : 1 + edge - edge * exp(-2 * rn2);
}
float4 fetch(float2 p, float2 offset)
{
    float2 q = p + offset;
    if (repeatEdge > .5) q = clamp(q, bounds.xy + .5, bounds.zw - .5);
    // Outside the half-pixel border, all bilinear contributors are zero.
    else if (any(q <= bounds.xy - .5) || any(q >= bounds.zw + .5)) return 0;
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
    if (radius < .5) return fetch(p, 0);
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
        int count = (int)sampleCount;
        [loop] for (int i = 0; i < count; ++i)
        {
            float4 s;
            if (count <= 128) s = samples128[i];
            else if (count <= 256) s = samples256[i];
            else s = samples512[i];
            float w = weight(s.z, s.w);
            sum += fetch(p, s.xy * axes) * w;
            total += w;
        }
    }
    return total > 0 ? sum / total : fetch(p, 0);
}
D2D_PS_ENTRY(main)
{
    float2 p = D2DGetScenePosition().xy;
    float4 original = D2DGetInput(2);
    float amount = focusAmount(p);
    if (showMap > .5) return float4(amount.xxx * original.a, original.a);
    if (amount <= 0 || maxRadius < .5) return original;
    float radius = amount * maxRadius;
    float4 mid = gather(p, radius);
    float3 c0 = innerColor.rgb, c1 = middleColor.rgb, c2 = outerColor.rgb;
    if (dot(c0 + c1 + c2, float3(1,1,1)) < 1e-8)
    { c0 = float3(1,0,0); c1 = float3(0,1,0); c2 = float3(0,0,1); }
    float3 denom = max(c0 + c1 + c2, 1e-8);
    float3 result;
    if (abs(dispersion) < 1e-6) result = mid.rgb * (c0+c1+c2) / denom;
    else
    {
        float3 inside = dot(c0,c0) > 0 ? gather(p, max(radius * (1 - dispersion), 0)).rgb : 0;
        float3 outside = dot(c2,c2) > 0 ? gather(p, max(radius * (1 + dispersion), 0)).rgb : 0;
        result = (inside * c0 + mid.rgb * c1 + outside * c2) / denom;
    }
    if (gamma > 1.0001) result = pivot * pow(max(result / max(pivot, .05), 0), 1 / gamma);
    float a = saturate(mid.a);
    if (a <= 1e-6) return 0;
    if (linearLight > .5) result = encode3(result / a) * a;
    // D2D requires premultiplied output. Spectral channels may otherwise exceed
    // the mid-radius alpha at a transparent edge and brighten later composites.
    return float4(clamp(result, 0, a), a);
}
