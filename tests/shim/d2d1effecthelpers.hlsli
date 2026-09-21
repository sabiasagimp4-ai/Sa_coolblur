// Test adapter only: run the production shader math through D3D11 WARP.
// Real builds always include the Windows SDK Direct2D header instead.
Texture2D<float4> InputTexture0 : register(t0);
Texture2D<float4> InputTexture1 : register(t1);
Texture2D<float4> InputTexture2 : register(t2);
Texture2D<float4> InputTexture3 : register(t3);
SamplerState InputSampler0 : register(s0);
SamplerState InputSampler1 : register(s1);
SamplerState InputSampler2 : register(s2);
SamplerState InputSampler3 : register(s3);
static float4 testPosition;
float4 D2DGetScenePosition() { return testPosition; }
float4 D2DGetInputCoordinate(uint index)
{
    uint w, h;
    if(index == 0) InputTexture0.GetDimensions(w,h);
    else if(index == 1) InputTexture1.GetDimensions(w,h);
    else if(index == 2) InputTexture2.GetDimensions(w,h);
    else InputTexture3.GetDimensions(w,h);
    return float4(testPosition.xy / float2(w,h), 1.0 / float2(w,h));
}
float4 D2DGetInput(uint index)
{
    float2 uv = D2DGetInputCoordinate(index).xy;
    if(index == 0) return InputTexture0.SampleLevel(InputSampler0, uv, 0);
    if(index == 1) return InputTexture1.SampleLevel(InputSampler1, uv, 0);
    if(index == 2) return InputTexture2.SampleLevel(InputSampler2, uv, 0);
    return InputTexture3.SampleLevel(InputSampler3, uv, 0);
}
float4 D2DSampleInputAtPosition(uint index, float2 p)
{
    uint w,h; InputTexture1.GetDimensions(w,h);
    return InputTexture1.SampleLevel(InputSampler1, p / float2(w,h), 0);
}
#define D2D_PS_ENTRY(name) float4 evaluate()
