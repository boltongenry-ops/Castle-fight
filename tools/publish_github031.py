"""Publish tested release assets first; switch the public update index last."""
from pathlib import Path
import subprocess,json,hashlib,struct,zipfile,shutil,urllib.request,time,os
R=Path(os.environ.get('CF_WORKSPACE',str(Path(__file__).resolve().parents[1])));O=R/'outputs';G=O/'GitHubDistribution';V='0.31.0';REPO='boltongenry-ops/Castle-fight';TAG='v'+V;BASE=f'https://github.com/{REPO}/releases/download/{TAG}/'
def run(args,cwd=G):return subprocess.run([str(x) for x in args],cwd=cwd,check=True,text=True,capture_output=True).stdout
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
proof=json.loads((O/'Validation/Structure031/structure031-verification.json').read_text());assert proof['status']=='PASS' and proof['actualPortable']
apkproof=json.loads((O/'Validation/Android031/delivery-verification.json').read_text());assert apkproof['status']=='PASS'
exe=O/'CastleFight-Portable.exe';apk=O/'CastleFight-Android-OnlineAssets.apk';launcher=O/'CastleFight-Launcher.exe';assert sha(exe)==proof['portableSha256'] and sha(apk)==apkproof['apkSha256'];assert exe.stat().st_size<2147483648
stage=O/'GitHubRelease031';stage.mkdir(exist_ok=True);shutil.copy2(apk,stage/'CastleFight-Android.apk')
content=O/'CastleFightUnity/Builds/ContentAndroid021/manifest.json';shutil.copy2(content,stage/'content-android.json')
files=[]
with zipfile.ZipFile(exe) as archive,exe.open('rb') as stream:
 for row in archive.infolist():
  if row.is_dir():continue
  stream.seek(row.header_offset);head=struct.unpack('<IHHHHHIIIHH',stream.read(30));assert head[0]==0x04034b50
  h=hashlib.sha256()
  with archive.open(row) as data:
   for block in iter(lambda:data.read(1024*1024),b''):h.update(block)
  files.append(dict(path=row.filename,size=row.file_size,sha256=h.hexdigest(),offset=row.header_offset+30+head[-2]+head[-1],compressedSize=row.compress_size,method=row.compress_type))
index=dict(schema=1,version=V,windows=dict(url=BASE+exe.name,size=exe.stat().st_size,sha256=sha(exe),entryPoint='CastleFightMap.exe',files=files),android=dict(url=BASE+'CastleFight-Android.apk',size=apk.stat().st_size,sha256=sha(apk),versionCode=15,minimumPlayerVersion=V,contentManifest=BASE+'content-android.json'),launcher=dict(url=BASE+launcher.name,size=launcher.stat().st_size,sha256=sha(launcher)))
(stage/'release.json').write_text(json.dumps(index,ensure_ascii=False,indent=2),'utf-8')
checksums=''.join(sha(p)+'  '+p.name+'\n' for p in [exe,launcher,stage/'CastleFight-Android.apk',stage/'content-android.json']);(stage/'SHA256SUMS.txt').write_text(checksums,'utf-8')
notes='''Лаунчер Windows с проверкой и докачкой файлов, входом в общий сетевой аккаунт или гостевым запуском. Android: отдельная загрузка ресурсов и обновление APK из игры с системным подтверждением.

Легендарные бойцы панд и кентавров перенесены в цепочки улучшений; добавлены легендарные специальные постройки и цены в дереве. Упорядочено строительство. Синяя полоса маны под HP, регенерация и таймеры заклинаний на иконках.

Android APK устанавливается поверх предыдущего, с тем же пакетом и подписью. Первая загрузка ресурсов требует интернета. На физическом телефоне этот выпуск не проверялся.
'''
(stage/'notes.md').write_text(notes,'utf-8')
run(['git','add','README.md','.gitignore','.gitattributes','AGENTS.md','OPEN-PROJECT.ru.md','UnityProject','unity-assets.json','source','tools','CENTAUR-ASSET-CREDITS.ru.md'])
if run(['git','status','--porcelain']):run(['git','commit','-m','Add Castle Fight editable source and distribution instructions'])
run(['git','push','-u','origin','main'])
present=subprocess.run(['gh','release','view',TAG,'--repo',REPO],capture_output=True).returncode==0
if not present:run(['gh','release','create',TAG,'--repo',REPO,'--draft','--title','Castle Fight '+V,'--notes-file',stage/'notes.md'])
for p in [launcher,stage/'CastleFight-Android.apk',stage/'content-android.json',stage/'release.json',stage/'SHA256SUMS.txt',exe]:
 print('GITHUB_UPLOAD',p.name,p.stat().st_size,flush=True);run(['gh','release','upload',TAG,p,'--repo',REPO,'--clobber'])
assets=next(r for r in json.loads(run(['gh','api',f'repos/{REPO}/releases'])) if r['tag_name']==TAG)['assets'];byname={a['name']:a for a in assets}
for p in [exe,launcher,stage/'CastleFight-Android.apk',stage/'content-android.json']:
 row=byname[p.name];assert row['size']==p.stat().st_size
 if row.get('digest'):assert row['digest']=='sha256:'+sha(p)
run(['gh','release','edit',TAG,'--repo',REPO,'--draft=false','--latest'])
# GitHub's redirect must preserve Range, because the launcher uses it per file.
row=next(r for r in files if r['compressedSize']>32 and r['compressedSize']<10000)
request=urllib.request.Request(BASE+exe.name,headers={'Range':f"bytes={row['offset']}-{row['offset']+row['compressedSize']-1}",'User-Agent':'CastleFight-release-check'})
with urllib.request.urlopen(request,timeout=30) as response:
 assert response.status==206;blob=response.read();assert len(blob)==row['compressedSize']
import zlib
assert hashlib.sha256(zlib.decompress(blob,-15) if row['method']==8 else blob).hexdigest()==row['sha256']
shutil.copy2(stage/'release.json',G/'release.json');run(['git','add','release.json']);
if run(['git','status','--porcelain']):run(['git','commit','-m','Publish verified Castle Fight '+V+' update channel'])
run(['git','push','origin','main'])
report=dict(status='PASS',repository='https://github.com/'+REPO,release='https://github.com/'+REPO+'/releases/tag/'+TAG,files=len(files),githubRange206=True,assetDigestsVerified=True,portableSha256=sha(exe),apkSha256=sha(apk),launcherSha256=sha(launcher))
(O/'Validation/Launcher031/github-release-verification.json').write_text(json.dumps(report,indent=2),'utf-8');print(json.dumps(report),flush=True)
