#define D2D_REQUIRES_SCENE_POSITION
#define D2D_ENTRY main
#include <d2d1effecthelpers.hlsli>

float factor, reserved0, reserved1, reserved2;
float4 sourceBounds, outputBounds;

float4 sourceAt(float2 p, float2 q)
{
    float4 uv = D2DGetInputCoordinate(0);
    return InputTexture0.SampleLevel(InputSampler0, uv.xy + uv.zw * (q - p), 0);
}

D2D_PS_ENTRY(main)
{
    float2 p = D2DGetScenePosition().xy;
    int f = (int)clamp(factor, 1, 4);
    int2 cell = (int2)floor(p - outputBounds.xy);
    int width = (int)(sourceBounds.z - sourceBounds.x);
    int height = (int)(sourceBounds.w - sourceBounds.y);
    int baseX = cell.x * f;
    int baseY = cell.y * f;
    float4 sum = 0;
    float count = 0;
    [unroll] for (int y = 0; y < 4; ++y)
    {
        if (y >= f || baseY + y >= height) break;
        [unroll] for (int x = 0; x < 4; ++x)
        {
            if (x >= f || baseX + x >= width) break;
            float2 q = sourceBounds.xy + float2(baseX + x + .5, baseY + y + .5);
            sum += sourceAt(p, q);
            count += 1;
        }
    }
    return count > 0 ? sum / count : 0;
}
