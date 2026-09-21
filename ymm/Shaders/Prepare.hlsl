#define D2D_ENTRY main
#include <d2d1effecthelpers.hlsli>
#include "Color.hlsli"
float gamma, pivot, linearLight, reserved;
D2D_PS_ENTRY(main)
{
    float4 c = D2DGetInput(0);
    if (c.a <= 0) return 0;
    if (linearLight > .5) c.rgb = decode3(c.rgb / c.a) * c.a;
    if (gamma > 1.0001) c.rgb = pivot * pow(max(c.rgb / max(pivot, .05), 0), gamma);
    return c;
}
