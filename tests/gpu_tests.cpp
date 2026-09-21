#include <d3d11.h>
#include <d3dcompiler.h>
#include <wrl/client.h>
#include <array>
#include <vector>
#include <fstream>
#include <sstream>
#include <iostream>
#include <cmath>
#include <stdexcept>
#include <algorithm>
using Microsoft::WRL::ComPtr;
using Pixel = std::array<float,4>;
constexpr int W=24,H=16;
void check(HRESULT hr) { if(FAILED(hr)) throw std::runtime_error("D3D error " + std::to_string(hr)); }
std::string read(const std::string& path) {
    std::ifstream f(path); if(!f) throw std::runtime_error("Missing " + path);
    return std::string(std::istreambuf_iterator<char>(f), {});
}
std::string source(const std::string& name) {
    auto s=read("ymm/Shaders/"+name+".hlsl");
    for(auto name : {"d2d1effecthelpers.hlsli","Color.hlsli","Samples.hlsli"}) {
        auto at=s.find(std::string("#include ") + (name==std::string("d2d1effecthelpers.hlsli") ? "<" : "\"") + name);
        if(at!=std::string::npos) {
            auto end=s.find('\n',at);
            auto path=(name==std::string("d2d1effecthelpers.hlsli") ? "tests/shim/" : "ymm/Shaders/");
            s.replace(at,end-at,read(std::string(path)+name));
        }
    }
    return s+"\nfloat4 main(float4 p:SV_Position):SV_Target { testPosition=p; return evaluate(); }\n";
}
ComPtr<ID3DBlob> compile(const std::string& s,const char* profile) {
    ComPtr<ID3DBlob> code,errors;
    auto hr=D3DCompile(s.data(),s.size(),nullptr,nullptr,nullptr,"main",profile,D3DCOMPILE_ENABLE_STRICTNESS,0,&code,&errors);
    if(errors) std::cerr<<(char*)errors->GetBufferPointer();
    check(hr); return code;
}
struct Gpu {
    ComPtr<ID3D11Device> dev; ComPtr<ID3D11DeviceContext> ctx;
    ComPtr<ID3D11VertexShader> vs; ComPtr<ID3D11PixelShader> prep,gather,downsample,composite;
    ComPtr<ID3D11SamplerState> sampler;
    Gpu() {
        check(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_WARP,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&dev,nullptr,&ctx));
        auto v=compile("float4 main(uint i:SV_VertexID):SV_Position { float2 p=float2((i<<1)&2,i&2); return float4(p*float2(2,-2)+float2(-1,1),0,1); }","vs_4_0");
        check(dev->CreateVertexShader(v->GetBufferPointer(),v->GetBufferSize(),nullptr,&vs));
        auto p=compile(source("Prepare"),"ps_4_0");
        check(dev->CreatePixelShader(p->GetBufferPointer(),p->GetBufferSize(),nullptr,&prep));
        p=compile(source("Gather"),"ps_4_0");
        check(dev->CreatePixelShader(p->GetBufferPointer(),p->GetBufferSize(),nullptr,&gather));
        p=compile(source("Downsample"),"ps_4_0");
        check(dev->CreatePixelShader(p->GetBufferPointer(),p->GetBufferSize(),nullptr,&downsample));
        p=compile(source("Composite"),"ps_4_0");
        check(dev->CreatePixelShader(p->GetBufferPointer(),p->GetBufferSize(),nullptr,&composite));
        D3D11_SAMPLER_DESC sd={}; sd.Filter=D3D11_FILTER_MIN_MAG_MIP_LINEAR;
        sd.AddressU=sd.AddressV=sd.AddressW=D3D11_TEXTURE_ADDRESS_CLAMP; sd.MaxLOD=D3D11_FLOAT32_MAX;
        check(dev->CreateSamplerState(&sd,&sampler));
    }
    ComPtr<ID3D11Texture2D> texture(int width,int height,const std::vector<Pixel>* pixels=nullptr) {
        D3D11_TEXTURE2D_DESC d={}; d.Width=width; d.Height=height; d.MipLevels=d.ArraySize=1;
        d.Format=DXGI_FORMAT_R32G32B32A32_FLOAT; d.SampleDesc.Count=1;
        d.BindFlags=D3D11_BIND_SHADER_RESOURCE|D3D11_BIND_RENDER_TARGET;
        D3D11_SUBRESOURCE_DATA data={}; if(pixels) {data.pSysMem=pixels->data(); data.SysMemPitch=width*sizeof(Pixel);}
        ComPtr<ID3D11Texture2D> t; check(dev->CreateTexture2D(&d,pixels?&data:nullptr,&t)); return t;
    }
    ComPtr<ID3D11Texture2D> draw(ID3D11PixelShader* ps,const std::array<ID3D11Texture2D*,4>& inputs,UINT inputCount,const float* constants,UINT bytes,int width,int height) {
        auto out=texture(width,height); ComPtr<ID3D11RenderTargetView> rtv; check(dev->CreateRenderTargetView(out.Get(),nullptr,&rtv));
        std::array<ComPtr<ID3D11ShaderResourceView>,4> views;
        ID3D11ShaderResourceView* raw[4]; ID3D11SamplerState* ss[]={sampler.Get(),sampler.Get(),sampler.Get(),sampler.Get()};
        for(int i=0;i<4;++i) {check(dev->CreateShaderResourceView(inputs[i],nullptr,&views[i])); raw[i]=views[i].Get();}
        D3D11_BUFFER_DESC bd={}; bd.ByteWidth=bytes; bd.Usage=D3D11_USAGE_IMMUTABLE; bd.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
        D3D11_SUBRESOURCE_DATA data={}; data.pSysMem=constants;
        ComPtr<ID3D11Buffer> cb; check(dev->CreateBuffer(&bd,&data,&cb));
        D3D11_VIEWPORT vp={0,0,(float)width,(float)height,0,1};
        ctx->RSSetViewports(1,&vp); ctx->OMSetRenderTargets(1,rtv.GetAddressOf(),nullptr);
        ctx->VSSetShader(vs.Get(),nullptr,0); ctx->PSSetShader(ps,nullptr,0);
        ctx->PSSetConstantBuffers(0,1,cb.GetAddressOf()); ctx->PSSetShaderResources(0,inputCount,raw); ctx->PSSetSamplers(0,inputCount,ss);
        ctx->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST); ctx->Draw(3,0);
        ID3D11ShaderResourceView* empty[4]={}; ctx->PSSetShaderResources(0,4,empty); ctx->OMSetRenderTargets(0,nullptr,nullptr);
        return out;
    }
    std::vector<Pixel> readback(ID3D11Texture2D* out,int width,int height) {
        D3D11_TEXTURE2D_DESC d; out->GetDesc(&d); d.Usage=D3D11_USAGE_STAGING; d.BindFlags=0; d.CPUAccessFlags=D3D11_CPU_ACCESS_READ;
        ComPtr<ID3D11Texture2D> stage; check(dev->CreateTexture2D(&d,nullptr,&stage)); ctx->CopyResource(stage.Get(),out);
        D3D11_MAPPED_SUBRESOURCE mapped; check(ctx->Map(stage.Get(),0,D3D11_MAP_READ,0,&mapped));
        std::vector<Pixel> result(width*height);
        for(int y=0;y<height;++y) memcpy(result.data()+y*width,(char*)mapped.pData+y*mapped.RowPitch,width*sizeof(Pixel));
        ctx->Unmap(stage.Get(),0); return result;
    }
    std::vector<Pixel> render(const std::vector<Pixel>& pixels,std::array<float,48> c,const std::vector<Pixel>* depth=nullptr) {
        auto input=texture(W,H,&pixels), dt=texture(W,H,depth?depth:&pixels);
        std::array<float,4> pc={c[12],c[13],c[14],0};
        auto prepared=draw(prep.Get(),{input.Get(),input.Get(),input.Get(),input.Get()},1,pc.data(),sizeof(pc),W,H);
        auto out=draw(gather.Get(),{prepared.Get(),dt.Get(),input.Get(),input.Get()},4,c.data(),sizeof(c),W,H);
        return readback(out.Get(),W,H);
    }
    std::vector<Pixel> renderDepth(const std::vector<Pixel>& pixels,std::array<float,48> c,const std::vector<Pixel>& depth,int depthW,int depthH) {
        auto input=texture(W,H,&pixels), dt=texture(depthW,depthH,&depth);
        std::array<float,4> pc={c[12],c[13],c[14],0};
        auto prepared=draw(prep.Get(),{input.Get(),input.Get(),input.Get(),input.Get()},1,pc.data(),sizeof(pc),W,H);
        c[40]=0;c[41]=0;c[42]=(float)depthW;c[43]=(float)depthH;
        auto out=draw(gather.Get(),{prepared.Get(),dt.Get(),input.Get(),input.Get()},4,c.data(),sizeof(c),W,H);
        return readback(out.Get(),W,H);
    }
    std::vector<Pixel> downsampleImage(const std::vector<Pixel>& pixels,int width,int height,int factor) {
        int lowW=(width+factor-1)/factor, lowH=(height+factor-1)/factor;
        auto input=texture(width,height,&pixels);
        std::array<float,12> c={(float)factor,0,0,0, 0,0,(float)width,(float)height, 0,0,(float)lowW,(float)lowH};
        auto out=draw(downsample.Get(),{input.Get(),input.Get(),input.Get(),input.Get()},1,c.data(),sizeof(c),lowW,lowH);
        return readback(out.Get(),lowW,lowH);
    }
    std::vector<Pixel> compositeImage(const std::vector<Pixel>& low,int lowW,int lowH,const std::vector<Pixel>& raw,int rawW,int rawH,std::array<float,20> c) {
        auto lowTex=texture(lowW,lowH,&low), rawTex=texture(rawW,rawH,&raw);
        auto out=draw(composite.Get(),{lowTex.Get(),rawTex.Get(),rawTex.Get(),rawTex.Get()},2,c.data(),sizeof(c),rawW,rawH);
        return readback(out.Get(),rawW,rawH);
    }
};
std::array<float,48> defaults() {
    return {50,50,0,1, 0,4,0,0, 8,.075f,0,1, 2.2f,.8f,1,1,
            .5f,.1f,.2f,0, 128,0,0,0, 1,0,0,0, 0,1,0,0, 0,0,1,0,
            0,0,W,H, 0,0,W,H, 0,0,W,H};
}
void assertNear(float x,float y,float tol=3e-4f) {if(!std::isfinite(x)||std::abs(x-y)>tol) throw std::runtime_error("Mismatch: "+std::to_string(x)+" != "+std::to_string(y));}
void imageNear(const std::vector<Pixel>& a,const std::vector<Pixel>& b,float tol=3e-4f) {for(size_t i=0;i<a.size();++i) for(int c=0;c<4;++c) assertNear(a[i][c],b[i][c],tol);}
int main() {
 try {
    Gpu g;
    std::vector<Pixel> field(W*H,Pixel{.2f,.3f,.4f,.5f}), transparent(W*H,Pixel{}), pattern(W*H);
    for(int y=0;y<H;++y) for(int x=0;x<W;++x) pattern[y*W+x]={(float)x/W*.5f,(float)y/H*.5f,.1f,.5f};
    int tests=0;
    // Constant semi-transparent fields survive every quality, weight, aspect,
    // highlight setting and both positive/negative extreme dispersion.
    for(float count:{128.f,256.f,512.f}) for(float edge:{-1.f,0.f,1.f}) for(float disp:{-3.f,0.f,3.f}) {
        auto c=defaults(); c[20]=count; c[10]=edge; c[9]=disp; c[11]=.5f;
        imageNear(g.render(field,c),field); ++tests;
    }
    auto c=defaults(); c[8]=0; imageNear(g.render(pattern,c),pattern,1e-6f); ++tests;
    c=defaults(); c[5]=1; c[2]=4000; imageNear(g.render(pattern,c),pattern,1e-6f); ++tests;
    c=defaults(); imageNear(g.render(transparent,c),transparent,1e-6f); ++tests;
    c[5]=3; imageNear(g.render(pattern,c),pattern,1e-6f); ++tests; // missing depth
    c=defaults(); c[6]=1; imageNear(g.render(pattern,c),pattern,1e-6f); ++tests;
    c=defaults(); c[7]=1; auto map=g.render(field,c); for(auto px:map) {for(int k=0;k<3;++k) assertNear(px[k],1); assertNear(px[3],.5f);} ++tests;
    c=defaults(); c[5]=3; c[19]=1; std::vector<Pixel> depth(W*H,Pixel{.5f,.5f,.5f,1});
    imageNear(g.render(pattern,c,&depth),pattern,1e-6f); ++tests;
    // A mismatched depth map follows BuildDepthMap's left-edge nearest mapping:
    // floor(x*depthWidth/sourceWidth), not centre-based resampling.
    std::vector<Pixel> depthSmall={Pixel{0,0,0,1},Pixel{1,1,1,1}};
    c=defaults(); c[5]=3;c[7]=1;c[16]=0;c[17]=0;c[18]=.1f;c[19]=1;
    auto resizedDepthMap=g.renderDepth(field,c,depthSmall,2,1);
    for(int y=0;y<H;++y) for(int x=0;x<W;++x) {float want=x<W/2?0.f:1.f;for(int k=0;k<3;++k)assertNear(resizedDepthMap[y*W+x][k],want,1e-6f);assertNear(resizedDepthMap[y*W+x][3],.5f,1e-6f);} ++tests;
    c=defaults(); c[15]=0; c[12]=1; auto fade=g.render(field,c);
    if(!(fade[0][3]<.5f&&fade[W*(H/2)+W/2][3]>.45f)) throw std::runtime_error("Transparent edge did not fade"); ++tests;
    c=defaults(); c[24]=c[29]=c[34]=0; imageNear(g.render(field,c),field); ++tests; // black-picker fallback
    c=defaults(); c[24]=0; auto masked=g.render(field,c); for(auto px:masked) {assertNear(px[0],0); assertNear(px[1],.3f);assertNear(px[2],.4f);} ++tests;
    // Point source confirms actual blur and channel-radius ordering.
    std::vector<Pixel> impulse(W*H,Pixel{0,0,0,1}); impulse[W*(H/2)+W/2]={1,1,1,1};
    c=defaults(); c[8]=3; c[9]=.5f; c[12]=1; c[14]=0;
    auto blurred=g.render(impulse,c); auto center=blurred[W*(H/2)+W/2];
    if(!(center[0]>center[1]&&center[1]>center[2]&&center[0]<1)) throw std::runtime_error("Spectral radius ordering failed"); ++tests;
    // Execute the production downsample shader, including a partial block on
    // odd dimensions, and compare every output texel to exact box averages.
    const int dw=5,dh=3,df=2,dlw=3,dlh=2;
    std::vector<Pixel> sourcePixels(dw*dh);
    for(int y=0;y<dh;++y) for(int x=0;x<dw;++x) {float v=(float)(x+y*10); sourcePixels[y*dw+x]={v,v,v,1};}
    auto down=g.downsampleImage(sourcePixels,dw,dh,df);
    for(int ly=0;ly<dlh;++ly) for(int lx=0;lx<dlw;++lx) {
        float sum=0,n=0; for(int sy=0;sy<df;++sy) for(int sx=0;sx<df;++sx) {int x=lx*df+sx,y=ly*df+sy;if(x<dw&&y<dh){sum+=x+y*10;n++;}}
        for(int k=0;k<3;++k) assertNear(down[ly*dlw+lx][k],sum/n,1e-6f); assertNear(down[ly*dlw+lx][3],1,1e-6f);
    }
    ++tests;
    // Execute the production composite shader and verify CUDA's 4px ramp.
    std::vector<Pixel> low(dlw*dlh,Pixel{1,1,1,1}), raw(dw*dh,Pixel{0,0,0,1});
    std::array<float,20> cc={0,0,0,100, 0,1,0,2, 36,0,0,0, 0,0,(float)dlw,(float)dlh, 0,0,(float)dw,(float)dh};
    auto combined=g.compositeImage(low,dlw,dlh,raw,dw,dh,cc);
    float amount=.5f/100.f, expected=amount/(4.f/36.f);
    for(int k=0;k<3;++k) assertNear(combined[0][k],expected,3e-4f); assertNear(combined[0][3],1,1e-6f); ++tests;
    for(auto px:fade) for(int k=0;k<3;++k) if(!std::isfinite(px[k])||px[k]<0||px[k]>px[3]+1e-6f) throw std::runtime_error("Invalid premultiplied output");
    std::cout<<"PASS: "<<tests<<" production-shader WARP cases\n";
    return 0;
 } catch(const std::exception& e) {std::cerr<<e.what()<<"\n";return 1;}
}
