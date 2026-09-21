"""Deterministic CPU rendering of the YMM shader math. No generated imagery.
Requires Python/Pillow and g++ (OpenMP). Run with the original JPEG path.
Opaque RGB input, clamped edges, linear light, 512 samples, threshold .8.
"""
from pathlib import Path
import hashlib, json, os, subprocess, sys, tempfile
from PIL import Image, ImageOps, ImageDraw, ImageFont
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'assets' / 'examples'
# mode, radius, dispersion(UI), edge(UI), boost(UI), aspect, cx,cy,width,feather,angle,invert,color
PRESETS = [
 ('00_original', 'Original', [4,0,0,0,0,1,50,50,0,1,0,0,0]),
 ('01_mild', 'Uniform / mild', [4,8,25,0,30,1,50,50,0,1,0,0,0]),
 ('02_dispersion', 'Uniform / dispersion +250', [4,20,250,0,30,1,50,50,0,1,0,0,0]),
 ('03_reverse', 'Uniform / dispersion -250', [4,20,-250,0,30,1,50,50,0,1,0,0,0]),
 ('04_band', 'Linear focus / desks', [1,36,150,0,30,1,50,76,80,150,0,0,0]),
 ('05_radial', 'Radial focus / chalkboard', [2,38,200,0,30,1,61,35,100,170,0,0,0]),
 ('06_inverted', 'Radial / inverted', [2,38,200,0,30,1,61,35,100,170,0,1,0]),
 ('07_horizontal', 'Anamorphic / wide', [4,24,200,70,65,.5,50,50,0,1,0,0,0]),
 ('08_vertical', 'Anamorphic / tall', [4,24,200,70,65,2,50,50,0,1,0,0,0]),
 ('09_soft', 'Bokeh edge -100 / soft', [4,24,100,-100,50,1,50,50,0,1,0,0,0]),
 ('10_ring', 'Bokeh edge +100 / rim', [4,24,100,100,50,1,50,50,0,1,0,0,0]),
 ('11_colors', 'Custom taps / cyan-magenta-yellow', [1,40,250,50,50,1,50,65,70,180,-20,0,1]),
]
def main():
 OUT.mkdir(parents=True, exist_ok=True)
 source=Path(sys.argv[1]);im=ImageOps.exif_transpose(Image.open(source)).convert('RGB')
 # Largest exact integer 16:9 crop. 900x506 -> 896x504 at (2,1).
 k=min(im.width//16,im.height//9);w,h=16*k,9*k
 box=((im.width-w)//2,(im.height-h)//2,(im.width-w)//2+w,(im.height-h)//2+h)
 crop=im.crop(box);crop.save(OUT/'source_16x9.png')
 metadata={'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'crop_box':box,'size':[w,h],
 'renderer':'CPU transcription of YMM4 HLSL; not YMM4 application output; no AI imagery',
 'fixed':{'samples':512,'linear_light':True,'edge_repeat':True,'threshold':.8},'presets':[]}
 env=dict(os.environ,OMP_NUM_THREADS='4')
 with tempfile.TemporaryDirectory(prefix='coolblur-render-') as tmp:
  tmp=Path(tmp)
  # Consume the production sample coordinates, do not invent another kernel.
  samples=(ROOT/'ymm/Shaders/Samples.hlsli').read_text()
  (tmp/'samples_cpu.inc').write_text(samples)
  exe=tmp/'render'
  subprocess.run(['g++','-O3','-std=c++17','-fopenmp','-I'+str(tmp),str(ROOT/'tools/render_reference.cpp'),'-o',str(exe)],check=True)
  crop.save(tmp/'input.ppm')
  for name,label,params in PRESETS:
   dest=tmp/(name+'.ppm')
   subprocess.run([str(exe),str(tmp/'input.ppm'),str(dest),*map(str,params)],check=True,env=env)
   result=Image.open(dest).convert('RGB')
   if name=='00_original': assert result.tobytes()==crop.tobytes(), 'zero radius must preserve pixels'
   result.save(OUT/(name+'.jpg'),quality=95,subsampling=0)
   metadata['presets'].append({'name':name,'label':label,'arguments':params})
   print(name,flush=True)
 # Four-panel 16:9 comparison: each source is 16:9 and the complete sheet is
 # 1280x720.  Labels are overlaid so the README image itself stays 16:9.
 chosen=[0,4,5,7]
 tw,th=640,360;sheet=Image.new('RGB',(tw*2,th*2),'#11151b')
 font=ImageFont.truetype('DejaVuSans.ttf',18);small=ImageFont.truetype('DejaVuSans.ttf',14)
 for panel,i in enumerate(chosen):
  name,label,params=PRESETS[i];tile=Image.open(OUT/(name+'.jpg')).resize((tw,th),Image.Resampling.LANCZOS).convert('RGBA')
  overlay=Image.new('RGBA',(tw,58),(8,12,18,190));od=ImageDraw.Draw(overlay)
  od.text((12,7),label,font=font,fill='white')
  od.text((12,32),f'R={params[1]}  D={params[2]}  Edge={params[3]}  Hi={params[4]}  A={params[5]}',font=small,fill='#c6d4e2')
  tile.alpha_composite(overlay,(0,0))
  sheet.paste(tile.convert('RGB'),((panel%2)*tw,(panel//2)*th))
 sheet.save(OUT/'comparison.jpg',quality=94,subsampling=0)
 (OUT/'parameters.json').write_text(json.dumps(metadata,indent=2)+'\n')
 print('PASS: radius=0 pixel identity; 12 presets rendered',flush=True)
if __name__=='__main__':main()
