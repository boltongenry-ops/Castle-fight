"""Restore exact Unity source assets from the repository's immutable GitHub packs.

Python 3.10+, standard library only. No signing keys or local workstation paths.
"""
from pathlib import Path, PurePosixPath
import argparse, hashlib, json, os, shutil, urllib.request, zipfile, time

REPO = Path(__file__).resolve().parents[1]
MANIFEST = REPO / 'unity-assets.json'

def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest() if hasattr(hashlib, 'file_digest') else stream_hash(stream)

def stream_hash(stream):
    h = hashlib.sha256()
    for block in iter(lambda: stream.read(1024 * 1024), b''): h.update(block)
    return h.hexdigest()

def target(root, path):
    parts = PurePosixPath(path)
    if parts.is_absolute() or parts.as_posix() != path or '..' in parts.parts or '\\' in path or ':' in path or not path.startswith('Assets/'):
        raise ValueError('Unsafe Unity asset path: ' + path)
    result = root / path
    if not result.resolve().is_relative_to(root.resolve()): raise ValueError('Asset outside project')
    return result

def matches(path, row):
    return path.is_file() and path.stat().st_size == row['size'] and digest(path) == row['sha256']

def validate(data):
    if data.get('schema') != 1: raise ValueError('Unsupported asset index')
    names = set(); paths = set()
    for pack in data['archives']:
        if pack['file'] != 'unity-' + pack['sha256'] + '.zip' or len(pack['sha256']) != 64 or pack['size'] <= 0:
            raise ValueError('Invalid archive metadata')
        if not pack['url'].startswith('https://github.com/boltongenry-ops/Castle-fight/releases/download/'):
            raise ValueError('Archive must belong to Castle Fight GitHub releases')
        if pack['file'] in names: raise ValueError('Duplicate archive')
        names.add(pack['file'])
        for row in pack['files']:
            target(REPO / 'UnityProject', row['path'])
            target(REPO / 'UnityProject', row.get('entry', row['path']))
            key = row['path'].casefold()
            if key in paths or len(row['sha256']) != 64 or row['size'] < 0: raise ValueError('Invalid asset entry')
            paths.add(key)

def download(pack, cache):
    cache.mkdir(parents=True, exist_ok=True)
    final = cache / pack['file']; partial = final.with_suffix('.zip.part')
    if final.exists() and matches(final, pack): return final
    if final.exists(): final.unlink()
    for attempt in range(3):
        have = partial.stat().st_size if partial.exists() else 0
        if have > pack['size']: partial.unlink(); have = 0
        if have == pack['size']:
            if digest(partial) == pack['sha256']: partial.replace(final); return final
            partial.unlink(); have = 0
        headers = {'User-Agent': 'CastleFight-UnityProject/1.0'}
        if have: headers['Range'] = f'bytes={have}-'
        try:
            request = urllib.request.Request(pack['url'] + '?resume=' + str(have), headers=headers)
            with urllib.request.urlopen(request, timeout=45) as response:
                if have:
                    expected = f"bytes {have}-{pack['size'] - 1}/{pack['size']}"
                    if response.status != 206 or response.headers.get('Content-Range') != expected:
                        raise ValueError('Server did not honor resume range')
                elif response.status != 200: raise ValueError('Unexpected download status')
                with partial.open('ab' if have else 'wb') as output:
                    for block in iter(lambda: response.read(1024 * 1024), b''):
                        have += len(block)
                        if have > pack['size']: raise ValueError('Archive exceeds expected size')
                        output.write(block)
            if have != pack['size'] or digest(partial) != pack['sha256']:
                partial.unlink(missing_ok=True); raise ValueError('Archive integrity check failed')
            partial.replace(final); return final
        except OSError:
            if attempt == 2: raise
            time.sleep(2)
    raise ValueError('Download did not complete')

def restore(data, project, cache, selected=None):
    restored = 0; verified = 0
    for n, pack in enumerate(data['archives'], 1):
        if selected and pack['file'] != selected: continue
        pending = []
        for row in pack['files']:
            path = target(project, row['path'])
            if matches(path, row): verified += 1; continue
            if path.exists(): raise ValueError('Locally modified asset; refusing to overwrite: ' + str(path))
            pending.append(row)
        if not pending: continue
        print(f"Archive {n}/{len(data['archives'])}: {pack['file']}", flush=True)
        archive = download(pack, cache)
        with zipfile.ZipFile(archive) as source:
            expected = {r.get('entry', r['path']): r for r in pack['files']}
            names = source.namelist()
            if not set(expected).issubset(names) or len(names) != len(set(names)):
                raise ValueError('Missing or duplicate files in archive')
            # Older immutable packs can retain superseded assets; extract only
            # current manifest entries, while still rejecting unsafe member paths.
            for name in names: target(project, name)
            for row in pending:
                info = source.getinfo(row.get('entry', row['path']))
                if info.file_size != row['size'] or info.external_attr >> 16 & 0o170000 == 0o120000:
                    raise ValueError('Invalid asset archive entry')
                path = target(project, row['path']); path.parent.mkdir(parents=True, exist_ok=True)
                temporary = path.with_name(path.name + '.restoring')
                try:
                    with source.open(info) as stream, temporary.open('wb') as output:
                        shutil.copyfileobj(stream, output, 1024 * 1024)
                    if not matches(temporary, row): raise ValueError('Asset hash mismatch: ' + row['path'])
                    temporary.replace(path); restored += 1
                finally: temporary.unlink(missing_ok=True)
    print(json.dumps(dict(status='PASS',restored=restored,alreadyVerified=verified)), flush=True)

def pack_changes(data, project, output, tag):
    if not tag or '/' in tag or '?' in tag or '#' in tag: raise ValueError('Use a simple release tag')
    existing = {r['path']: r for p in data['archives'] for r in p['files']}
    candidates = set(existing)
    candidates.update(p.relative_to(project).as_posix() for p in (project/'Assets').rglob('*.anim'))
    ref = project/'Assets/SourceAssets/Reforged'
    if ref.exists(): candidates.update(p.relative_to(project).as_posix() for p in ref.rglob('*') if p.is_file() and not p.name.endswith('.meta'))
    changed = []
    for path in sorted(candidates):
        source = target(project,path)
        if not source.exists(): raise ValueError('Restore assets before packing changes: '+path)
        if path not in existing or not matches(source,existing[path]): changed.append(dict(path=path,size=source.stat().st_size,sha256=digest(source)))
    if not changed: print('No archived asset changes.'); return
    output.mkdir(parents=True,exist_ok=True); batches=[]; batch=[]; size=0
    for row in changed:
        if batch and size+row['size']>1024**3: batches.append(batch); batch=[]; size=0
        batch.append(row);size+=row['size']
    if batch:batches.append(batch)
    replacement={r['path'] for r in changed};archives=[]
    for old in data['archives']:
        rows=[r for r in old['files'] if r['path'] not in replacement]
        if rows: archives.append(dict(old,files=rows))
    for n, rows in enumerate(batches):
        temp=output/f'pending-{n}.zip'
        with zipfile.ZipFile(temp,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=5) as archive:
            for row in rows: archive.write(target(project,row['path']),row['path'])
        sha=digest(temp);name='unity-'+sha+'.zip';final=output/name
        temp.replace(final);archives.append(dict(file=name,url=f'https://github.com/boltongenry-ops/Castle-fight/releases/download/{tag}/{name}',size=final.stat().st_size,sha256=sha,files=rows))
    data=dict(data,archives=archives);validate(data)
    MANIFEST.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Upload new ZIPs to GitHub release '+tag+' BEFORE committing unity-assets.json. Keep all older packs.')

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('command',choices=['restore','verify','pack-changes']);parser.add_argument('--project',type=Path,default=REPO/'UnityProject');parser.add_argument('--cache',type=Path,default=REPO/'.asset-cache');parser.add_argument('--archive');parser.add_argument('--tag');parser.add_argument('--output',type=Path,default=REPO/'asset-packs');args=parser.parse_args()
    data=json.loads(MANIFEST.read_text(encoding='utf-8'));validate(data)
    if args.command=='restore':restore(data,args.project,args.cache,args.archive)
    elif args.command=='pack-changes':pack_changes(data,args.project,args.output,args.tag)
    else:
        rows=[r for p in data['archives'] for r in p['files'] if not args.archive or p['file']==args.archive]
        bad=[r['path'] for r in rows if not matches(target(args.project,r['path']),r)]
        if bad: raise ValueError(f'{len(bad)} missing/modified assets; first: '+bad[0])
        print(json.dumps(dict(status='PASS',verified=len(rows))))

if __name__=='__main__':main()
