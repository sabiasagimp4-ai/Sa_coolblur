#define D2D_REQUIRES_SCENE_POSITION
#define D2D_ENTRY main
#include <d2d1effecthelpers.hlsli>

float centerX, centerY, zone, feather;
float angle, mode, invertFocus, factor;
float maxRadius, reserved0, reserved1, reserved2;
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
    // This is the same pixel-center mapping as CUDA's
    // gx = (x + .5) / factor - .5 followed by bilinear sampling.
    float f = max(factor, 1);
    float2 q = lowBounds.xy + (p - rawBounds.xy) / f;
    float4 low = lowAt(p, q);
    // With factor==1 input 0 is the normal full-resolution Gather output;
    // returning it through the same sample path avoids an FXC false-positive
    // in D2DGetInput(0)'s generated helper implementation.
    float amount = focusAmount(p);
    // CoolBlurCompositeCUDA keeps the full-resolution source while the
    // effective radius is below 4 px, then switches completely to the
    // upsampled low-resolution blur.  This avoids softening the focus band.
    float e1 = maxRadius > 1 ? 4 / maxRadius : 1;
    float blend = factor < 1.5 ? 1 : (amount <= 0 ? 0 : (amount >= e1 ? 1 : amount / e1));
    return lerp(raw, low, blend);
}
