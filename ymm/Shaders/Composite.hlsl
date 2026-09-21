#define D2D_REQUIRES_SCENE_POSITION
#define D2D_ENTRY main
#include <d2d1effecthelpers.hlsli>

float centerX, centerY, zone, feather;
float angle, mode, invertFocus, factor;
float4 lowBounds, rawBounds;

float focusAmount(float2 p)
{
    float2 center = rawBounds.xy + (rawBounds.zw - rawBounds.xy) * float2(centerX, centerY) / 100;
    float2 d = p - center;
    float a = 1;
    if (mode < 1.5) a = saturate((abs(-sin(angle) * d.x + cos(angle) * d.y) - zone * .5) / max(feather, 1e-6));
    else if (mode < 2.5) a = saturate((length(d) - zone) / max(feather, 1e-6));
    return invertFocus > .5 ? 1 - a : a;
}

float4 lowAt(float2 p, float2 q)
{
    float4 uv = D2DGetInputCoordinate(0);
    return InputTexture0.SampleLevel(InputSampler0, uv.xy + uv.zw * (q - p), 0);
}

D2D_PS_ENTRY(main)
{
    float2 p = D2DGetScenePosition().xy;
    float4 raw = D2DGetInput(1);
    if (factor < 1.5) return D2DGetInput(0);
    // This is the same pixel-center mapping as CUDA's
    // gx = (x + .5) / factor - .5 followed by bilinear sampling.
    float2 q = lowBounds.xy + (p - rawBounds.xy) / factor;
    float4 low = lowAt(p, q);
    return lerp(raw, low, focusAmount(p));
}
