// CPU transcription of Prepare.hlsl / Gather.hlsl for opaque RGB examples.
// Same exact-small-disc branch and repository Vogel tables; not a Gaussian blur.
#include <algorithm>
#include <cmath>
#include <fstream>
#include <iostream>
#include <vector>
#include <array>
#include <stdexcept>
struct float4 {float x,y,z,w; float4(float a,float b,float c,float d):x(a),y(b),z(c),w(d){} };
#include "samples_cpu.inc"
using RGB=std::array<float,3>;
float sat(float x){return std::clamp(x,0.f,1.f);}
float decode(float x){return x<=.04045f?x/12.92f:std::pow((x+.055f)/1.055f,2.4f);}
float encode(float x){return x<=.0031308f?x*12.92f:1.055f*std::pow(x,1.f/2.4f)-.055f;}
float weight(float e,float r,float r2){return e>=0?1-e+e*(.15f+.85f*r*r2):1+e-e*std::exp(-2*r2);}
struct Params {int mode;float radius,disp,edge,boost,aspect,cx,cy,width,feather,angle;bool invert;int color;};
int W,H;
std::vector<RGB> original,prepared;
float bilinear(float x,float y,int c){
 x=std::clamp(x,0.f,float(W-1)); y=std::clamp(y,0.f,float(H-1));
 int x0=int(x),y0=int(y),x1=std::min(x0+1,W-1),y1=std::min(y0+1,H-1);
 float tx=x-x0,ty=y-y0;
 float a=prepared[y0*W+x0][c],b=prepared[y0*W+x1][c],d=prepared[y1*W+x0][c],e=prepared[y1*W+x1][c];
 return (a+(b-a)*tx)*(1-ty)+(d+(e-d)*tx)*ty;
}
float gather(int x,int y,float r,int c,const Params&p){
 if(r<.5f)return prepared[y*W+x][c];
 float aspect=std::sqrt(p.aspect),rx=std::max(r/aspect,.5f),ry=std::max(r*aspect,.5f);
 int ix=int(std::ceil(rx)),iy=int(std::ceil(ry));float sum=0,total=0;
 if((2*ix+1)*(2*iy+1)<=169){
  for(int dy=-iy;dy<=iy;++dy)for(int dx=-ix;dx<=ix;++dx){
   float nx=dx/rx,ny=dy/ry,r2=nx*nx+ny*ny;if(r2>1)continue;
   float rn=std::sqrt(r2),w=weight(p.edge,rn,r2)*sat((1-rn)*std::min(rx,ry));
   sum+=bilinear(float(x+dx),float(y+dy),c)*w;total+=w;
  }
 }else{
  // Match the original AE GPU hybrid gather: 64 samples through radius 16,
  // 128 through radius 40, then the selected quality tier for larger radii.
  int count = 512; // the example renderer uses the High quality tier
  if(r<=16) count=64; else if(r<=40) count=128;
  for(int i=0;i<count;++i){
   float4 s = count<=64 ? samples64[i] : (count<=128 ? samples128[i] : (count<=256 ? samples256[i] : samples512[i]));
   float w=weight(p.edge,s.z,s.w);sum+=bilinear(x+s.x*rx,y+s.y*ry,c)*w;total+=w;
  }
 }
 return total>0?sum/total:prepared[y*W+x][c];
}
int main(int argc,char**argv){try{
 if(argc!=16)throw std::runtime_error("input.ppm output.ppm mode radius dispersion edge boost aspect cx cy width feather angle invert color");
 std::ifstream f(argv[1],std::ios::binary);std::string magic;int maxval;f>>magic>>W>>H>>maxval;f.get();
 if(magic!="P6"||maxval!=255||W<1||H<1)throw std::runtime_error("Expected RGB P6 input");
 std::vector<unsigned char> bytes(W*H*3);f.read((char*)bytes.data(),bytes.size());if(!f)throw std::runtime_error("Short input");
 Params p{std::stoi(argv[3]),std::stof(argv[4]),std::stof(argv[5])*.003f,std::stof(argv[6])*.01f,std::stof(argv[7]),std::stof(argv[8]),std::stof(argv[9]),std::stof(argv[10]),std::stof(argv[11]),std::stof(argv[12]),std::stof(argv[13])*3.14159265358979323846f/180,std::stoi(argv[14])!=0,std::stoi(argv[15])};
 if(p.mode!=1&&p.mode!=2&&p.mode!=4)throw std::runtime_error("Examples support linear/radial/uniform only");
 original.resize(W*H);prepared.resize(W*H);float gamma=1+p.boost*.04f,pivot=.8f;
 for(int i=0;i<W*H;++i)for(int c=0;c<3;++c){float v=bytes[i*3+c]/255.f;original[i][c]=v;v=decode(v);prepared[i][c]=gamma>1.0001f?pivot*std::pow(v/pivot,gamma):v;}
 float colors[3][3]={{1,0,0},{0,1,0},{0,0,1}};
 if(p.color==1){float custom[3][3]={{0,1,1},{1,0,1},{1,1,0}};for(int k=0;k<3;++k)for(int c=0;c<3;++c)colors[k][c]=custom[k][c];}
 for(int c=0;c<3;++c){float total=colors[0][c]+colors[1][c]+colors[2][c];for(int k=0;k<3;++k)colors[k][c]/=total;}
 #pragma omp parallel for schedule(dynamic,1)
 for(int y=0;y<H;++y)for(int x=0;x<W;++x){
  float dx=x+.5f-W*p.cx/100,dy=y+.5f-H*p.cy/100,amount=1;
  if(p.mode==1)amount=sat((std::abs(-std::sin(p.angle)*dx+std::cos(p.angle)*dy)-p.width*.5f)/std::max(p.feather,1e-6f));
  if(p.mode==2)amount=sat((std::sqrt(dx*dx+dy*dy)-p.width)/std::max(p.feather,1e-6f));
  if(p.invert)amount=1-amount;
  if(amount<=0||p.radius<.5f)continue;
  float r=amount*p.radius,radii[]={std::max(r*(1-p.disp),0.f),r,std::max(r*(1+p.disp),0.f)};
  for(int c=0;c<3;++c){float value=0;for(int k=0;k<3;++k)if(colors[k][c]>0)value+=colors[k][c]*gather(x,y,radii[k],c,p);
   if(gamma>1.0001f)value=pivot*std::pow(std::max(value/pivot,0.f),1/gamma);
   bytes[(y*W+x)*3+c]=(unsigned char)std::lround(255*sat(encode(value)));
  }
 }
 std::ofstream out(argv[2],std::ios::binary);out<<"P6\n"<<W<<" "<<H<<"\n255\n";out.write((char*)bytes.data(),bytes.size());if(!out)throw std::runtime_error("Write failed");
}catch(const std::exception&e){std::cerr<<e.what()<<"\n";return 1;}}
