"""Package a Windows player with the previously verified portable bootstrap.

The reference EXE must match its published release.json. Never accepts an
unverified bootstrap or includes signing material and build/debug directories.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import struct
import shutil
import zipfile


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=Path, required=True)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--reference-index', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    expected = json.loads(args.reference_index.read_text())['windows']
    assert args.reference.stat().st_size == expected['size']
    assert sha(args.reference) == expected['sha256'], 'Reference EXE checksum mismatch'
    with args.reference.open('rb') as original:
        original.seek(-64, 2)
        footer = original.read(64)
    prefix_size, previous_zip_size = struct.unpack('<qq', footer[:16])
    assert footer[48:] == b'CFPORTABLE-V1!!!'
    assert prefix_size + previous_zip_size + 64 == args.reference.stat().st_size
    with args.reference.open('rb') as source:
        assert source.read(2) == b'MZ'
        source.seek(0)
        prefix = source.read(prefix_size)
    assert (args.build / 'CastleFightMap.exe').is_file()
    assert (args.build / 'UnityPlayer.dll').is_file()
    assert (args.build / 'CastleFightMap_Data').is_dir()
    files = []
    for path in sorted(args.build.rglob('*')):
        if not path.is_file():
            continue
        relative = path.relative_to(args.build)
        if len(relative.parts) > 1 and relative.parts[0] not in ('CastleFightMap_Data', 'MonoBleedingEdge', 'D3D12'):
            continue
        if path.suffix.lower() in ('.pdb', '.mdb'):
            continue
        assert not path.is_symlink(), 'Unexpected player symlink'
        assert path.suffix.lower() not in ('.keystore', '.jks', '.key', '.ulf')
        files.append((path, relative.as_posix()))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    payload = args.output.with_suffix('.payload.zip')
    rows = []
    reference_rows = {r['path']: r for r in expected['files']}
    with zipfile.ZipFile(args.reference) as reference_zip, args.reference.open('rb') as raw, zipfile.ZipFile(payload, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9, allowZip64=True) as archive:
        for path, name in files:
            source_hash = sha(path)
            previous = reference_rows.get(name)
            if previous and source_hash == previous['sha256']:
                # Reuse authenticated compressed bytes for unchanged player files.
                # CPython ZipFile owns central-directory generation; only local records are copied raw.
                info = copy.copy(reference_zip.getinfo(name))
                assert not info.flag_bits & 1, 'Encrypted reference entry'
                info.extra = b''
                info.flag_bits &= ~8  # The new header contains CRC/sizes, so no data descriptor is needed.
                info.header_offset = archive.fp.tell()
                archive._writecheck(info)
                archive._didModify = True
                archive.fp.write(info.FileHeader(info.file_size > zipfile.ZIP64_LIMIT or info.compress_size > zipfile.ZIP64_LIMIT))
                raw.seek(previous['offset'])
                remaining = info.compress_size
                while remaining:
                    block = raw.read(min(1024 * 1024, remaining))
                    assert block, 'Truncated reference compressed entry'
                    archive.fp.write(block)
                    remaining -= len(block)
                archive.start_dir = archive.fp.tell()
                archive.filelist.append(info)
                archive.NameToInfo[name] = info
            else:
                archive.write(path, name)
            row = archive.getinfo(name)
            rows.append(dict(path=name, size=row.file_size, sha256=source_hash, compressedSize=row.compress_size, method=row.compress_type, headerOffset=row.header_offset))
            print('PACKED', name, row.file_size, flush=True)
    payload_size = payload.stat().st_size
    payload_hash = bytes.fromhex(sha(payload))
    assert prefix_size + payload_size + 64 < 2**31, 'Portable exceeds the existing bootstrap size limit'
    with args.output.open('wb') as destination, payload.open('rb') as source:
        destination.write(prefix)
        shutil.copyfileobj(source, destination, 1024 * 1024)
        destination.write(struct.pack('<qq', prefix_size, payload_size) + payload_hash + b'CFPORTABLE-V1!!!')
    payload.unlink()
    with zipfile.ZipFile(args.output) as archive, args.output.open('rb') as stream:
        for row in rows:
            with archive.open(row['path']) as content:
                assert hashlib.file_digest(content, 'sha256').hexdigest() == row['sha256'], row['path']
            row.pop('headerOffset')
            stream.seek(archive.getinfo(row['path']).header_offset)
            header = struct.unpack('<IHHHHHIIIHH', stream.read(30))
            assert header[0] == 0x04034b50
            row['offset'] = stream.tell() + header[-2] + header[-1]
    report = dict(url='', size=args.output.stat().st_size, sha256=sha(args.output), entryPoint='CastleFightMap.exe', files=rows)
    args.output.with_suffix('.index.json').write_text(json.dumps(report, indent=2) + '\n')
    print('PORTABLE_VERIFIED files=' + str(len(rows)) + ' sha256=' + report['sha256'], flush=True)


if __name__ == '__main__':
    main()
