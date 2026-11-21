import concurrent.futures as cf
import hashlib, json, os, re, subprocess
from pathlib import Path
import requests
from PIL import Image
tool=Path(__file__).resolve().parent
root=tool.parents[1]
work=Path(os.environ['CF_MAP_WORK']).resolve()
cache=work/'source'
models=cache/'models'
models.mkdir(parents=True,exist_ok=True)
data=work/'content'
data.mkdir(exist_ok=True)
base='https://cdn.hiveworkshop.com/assets/wc3/war3.w3mod/_hd.w3mod/'
assets=root/'UnityProject/Assets'
def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def get(path, dest, marker):
    if dest.exists() and dest.read_bytes().startswith(marker): return True
    for attempt in range(3):
        try:
            r=requests.get(base+path,timeout=20)
            if r.status_code==404:return False
            if r.ok and r.content.startswith(marker):
                dest.write_bytes(r.content)
                return True
        except requests.RequestException: pass
    return False
def image(path, key, limit):
    source=cache/(key+'.dds')
    if not get(path,source,b'DDS '):return False
    im=Image.open(source).convert('RGBA')
    im.thumbnail((limit,limit), Image.Resampling.LANCZOS)
    im.save(data/(key+'.png'))
    return True
slk_url='https://cdn.hiveworkshop.com/assets/wc3/war3.w3mod/terrainart/terrain.slk'
response=requests.get(slk_url,timeout=25)
response.raise_for_status()
slk=response.content.decode('cp1252')
tiles=read(assets/'Resources/map-terrain.json')['ground']
def tile(item):
    i,key=item
    r=re.search(r'K"'+key+r'"\s+C;X2;[^\n]+\s+C;X3;K"([^"]+)"\s+C;X4;K"([^"]+)"',slk)
    path=(r[1]+'/'+r[2]+'_diffuse.dds').replace('\\','/').lower()
    assert image(path,'ground'+str(i),2048),path
    if key=='Zdtr':
        file=data/('ground'+str(i)+'.png')
        with Image.open(file) as im:
            if im.width==im.height:
                cell=im.height//4
                out=Image.new('RGBA',(im.width*2,im.height))
                out.paste(im,(0,0))
                full=im.crop((3*cell,3*cell,4*cell,4*cell))
                for x in range(4):
                    for y in range(4):out.paste(full,((4+x)*cell,y*cell))
                out.save(file)
    return path
def pool(fn,items):
    with cf.ThreadPoolExecutor(8) as p:return list(p.map(fn,items))
paths=pool(tile,enumerate(tiles))
props=read(assets/'StreamingAssets/SourceData/models_doodads.json')
placed={x['resolvedModel'] for x in read(assets/'Editor/MapSource/manifest.json')['placements']}
static=('shrub','flower','wall','rock','shrooms','bush','rubble','column','obelisk','obilisk','statue','stump','ruinsblocks','cattail','lilypad','lillypad','vinyplant','arch','barrel','tree')
def fetch_model(key):
    path=props[key]['mdx'].replace('\\','/').lower()
    return key if get(path,models/(key+'.mdx'),b'MDLXVERS') else None
keys=list(filter(None,pool(fetch_model,sorted(k for k in placed if k in props and any(s in k for s in static)))))
subprocess.run(['dotnet','run','--project',str(tool/'Extract.csproj'),'--',str(models)],check=True)
shapes={k:read(models/(k+'.json')) for k in keys}
def texpath(s):return str(Path(s.replace('\\','/')).with_suffix('.dds')).replace('\\','/').lower()
def texkey(s):return 'prop'+hashlib.sha256(s.encode()).hexdigest()[:16]
textures={texpath(s['texture']) for parts in shapes.values() for s in parts}
ok=set(filter(None,pool(lambda s:s if image(s,texkey(s),512) else None,textures)))
packed=[]
for k,parts in shapes.items():
    if not parts or any(texpath(s['texture']) not in ok for s in parts):continue
    for part in parts:part['texture']=texkey(texpath(part['texture']))
    (data/(k+'.json')).write_text(json.dumps(parts,separators=(',',':')))
    packed.append(k)
(data/'catalog.txt').write_text('\n'.join(sorted(packed)))
print('TILES',paths,'DECOR',len(packed),'PLACEMENTS',sum(x['resolvedModel'] in packed for x in read(assets/'Editor/MapSource/manifest.json')['placements']),flush=True)



