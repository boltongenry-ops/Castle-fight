"""Regenerate the compact Ultimate asset path table from existing Unity GUIDs.
Run after restoring unity-assets.json. Does not build or replace any bundles.
"""
from pathlib import Path
import argparse,json,re
p=argparse.ArgumentParser();p.add_argument('--project',type=Path,default=Path(__file__).resolve().parents[1]/'UnityProject');a=p.parse_args();P=a.project.resolve();S=P/'Assets/Scripts'
def write(name,text):
 (S/name).write_text(text,encoding='utf-8',newline='\r\n')
t=(P/'Assets/Resources/UltimateBuilder.asset').read_text()
refs={}
for m in re.finditer(r'^  (\w+):([^\n]*)\n((?:  - [^\n]*\n)*)',t,re.M):
 key,value,items=m.groups()
 if key in ['units','buildings','builders','buildingIcons','abilityIcons']:
  refs[key]=[re.search(r'guid: ([0-9a-f]{32})',v).group(1) if 'guid:' in v else '' for v in items.splitlines()]
 elif key in ['builderIcon','buildIcon']:refs[key]=re.search(r'guid: ([0-9a-f]{32})',value).group(1)
 elif key=='abilityIds':refs[key]=[v[4:].strip() for v in items.splitlines()]
wanted=set(v for k,a in refs.items() if k!='abilityIds' for v in (a if isinstance(a,list) else [a]) if v)
paths={}
for meta in (P/'Assets').rglob('*.meta'):
 m=re.search(r'^guid: ([0-9a-f]{32})',meta.read_text(encoding='utf-8-sig',errors='replace'),re.M)
 if m and m[1] in wanted:paths[m[1]]=str(meta.relative_to(P)).replace('\\','/')[:-5].replace('Assets/Resources/','Assets/RemoteContentSource/').lower()
assert wanted<=paths.keys(),wanted-paths.keys()
for k,a in list(refs.items()):
 if k!='abilityIds':refs[k]=[paths.get(v,'') for v in a] if isinstance(a,list) else paths[a]
assert len(refs['units'])==len(refs['buildings'])==2*len(refs['buildingIcons'])
assert len(refs['abilityIds'])==len(refs['abilityIcons'])
data=json.dumps(refs,separators=(',',':')).replace('"','""')
write('UltimateAssetMap034.cs','// Generated from UltimateBuilder.asset GUIDs; paths address existing immutable Android bundles.\nnamespace CastleFight { public static class UltimateAssetMap034 { public const string Json=@"'+data+'"; } }\n')

print('Regenerated UltimateAssetMap034.cs; existing meta/GUID retained')
