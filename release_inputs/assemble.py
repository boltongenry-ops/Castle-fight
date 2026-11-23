from pathlib import Path
import hashlib,subprocess,json,sys,zipfile
base=Path('release_inputs'); m=json.loads((base/'recipe.json').read_bytes());repo='boltongenry-ops/Castle-fight';tag='v0.44.1'
def gh(*args):return subprocess.check_output(['gh',*args,'-R',repo],text=True)
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def assets():return {a['name']:a for a in json.loads(gh('release','view',tag,'--json','assets,isDraft'))['assets']}
def download_parts(items,out):
 with out.open('wb') as dest:
  for name,h,n in items:
   x=Path('chunks')/name;assert x.stat().st_size==n and sha(x)==h;dest.write(x.read_bytes())
def ensure_asset(p,h):
 a=assets().get(p.name)
 if a and a['state']=='uploaded':assert a['digest']=='sha256:'+h;return
 gh('release','upload',tag,str(p),'--clobber')
 assert assets()[p.name]['digest']=='sha256:'+h
assert json.loads(gh('release','view',tag,'--json','isDraft'))['isDraft']
gh('release','download',tag,'-p','*.part','-D','chunks')
apk=Path('CastleFight-Android.apk');download_parts(m['android']['parts'],apk);assert sha(apk)==m['android']['sha'];ensure_asset(apk,sha(apk))
old=Path('old/CastleFight-Portable.exe');gh('release','download','v0.44.0','-p',old.name,'-D','old');assert sha(old)==m['old']
lit=Path('literal');download_parts(m['parts'],lit);new=Path(old.name)
with new.open('wb') as out,old.open('rb') as o,lit.open('rb') as l:
 for kind,offset,size in m['blocks']:
  inp=o if kind=='old' else l;inp.seek(offset)
  while size:
   data=inp.read(min(size,2**20));assert data;out.write(data);size-=len(data)
assert sha(new)==m['new'];ensure_asset(new,m['new']);print('Both uploaded and verified.')
