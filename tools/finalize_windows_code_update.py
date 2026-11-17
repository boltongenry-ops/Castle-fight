"""Preserve a verified published player, replacing only compatible game code and its supplement.

Only for code-only changes; not for scene/base-resource updates. Run AFTER the
Unity scripts-only build, then package the result. Unknown generated files are
archived outside the player instead of shipped or deleted.
"""
import argparse
import concurrent.futures
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import zipfile


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('build', 'reference', 'reference-index', 'mono', 'compat-check', 'archive'):
        p.add_argument('--' + name, type=Path, required=True)
    p.add_argument('--version', required=True)
    p.add_argument('--replace-burst', action='store_true', help='Retain the newly compiled Burst plugin; original layouts and shipped managed dependencies must still match')
    p.add_argument('--spell-art-sha256', help='Retain the separately built spell-art-036 bundle with this verified hash')
    args = p.parse_args()
    index = json.loads(args.reference_index.read_text())['windows']
    assert args.reference.stat().st_size == index['size'] and sha(args.reference) == index['sha256']
    expected = {row['path']: row for row in index['files']}
    game = 'CastleFightMap_Data/Managed/Assembly-CSharp.dll'
    supplement = 'CastleFightMap_Data/StreamingAssets/Frontend/centaur-028.windows'
    keep = {game, supplement}
    spell_art = 'CastleFightMap_Data/StreamingAssets/Frontend/spell-art-036.windows'
    if args.spell_art_sha256:
        assert (args.build / spell_art).resolve().is_relative_to(args.build.resolve())
        assert sha(args.build / spell_art) == args.spell_art_sha256, 'Spell art bundle hash differs'
        keep.add(spell_art)
    burst = 'CastleFightMap_Data/Plugins/x86_64/lib_burst_generated.dll'
    if args.replace_burst:
        assert burst in expected and (args.build / burst).read_bytes()[:2] == b'MZ', 'Missing compiled Burst plugin'
        keep.add(burst)
    for name in expected:
        assert (args.build / name).resolve().is_relative_to(args.build.resolve())
    assert args.version.encode('utf-16le') in (args.build / game).read_bytes(), 'Visible game version missing'
    with zipfile.ZipFile(args.reference) as source, tempfile.TemporaryDirectory() as work:
        old = Path(work) / 'Assembly-CSharp.dll'
        old.write_bytes(source.read(game))
        command = [str(args.mono), str(args.compat_check), str(old), str((args.build / game).resolve())]
        if args.replace_burst:
            for dependency in ('Unity.Burst', 'Unity.Collections', 'UnityEngine.BurstModule'):
                name = 'CastleFightMap_Data/Managed/' + dependency + '.dll'
                content = source.read(name)
                assert hashlib.sha256(content).hexdigest() == expected[name]['sha256']
                (Path(work) / (dependency + '.dll')).write_bytes(content)
            command.append(work)
        subprocess.run(command, check=True)
        def matches(name):
            target = args.build / name
            return target.is_file() and target.stat().st_size == expected[name]['size'] and sha(target) == expected[name]['sha256']
        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as ex:
            restore = [name for name, ok in zip(expected, ex.map(matches, expected)) if name not in keep and not ok]
        for name in restore:
            target = args.build / name
            target.parent.mkdir(parents=True, exist_ok=True)
            with source.open(name) as stream, target.open('wb') as output:
                shutil.copyfileobj(stream, output, 1024 * 1024)
            assert matches(name), name
        extras = [p for p in args.build.rglob('*') if p.is_file() and p.relative_to(args.build).as_posix() not in expected and p.relative_to(args.build).as_posix() not in keep]
        for path in extras:
            target = args.archive / path.relative_to(args.build)
            target.parent.mkdir(parents=True, exist_ok=True)
            assert not target.exists(), 'Archive collision'
            shutil.move(path, target)
        assert sha(args.build / game) != expected[game]['sha256'], 'Game code was not updated'
        print('CODE_ONLY_PLAYER_PASS preserved=' + str(len(expected.keys() - keep)) + ' restored=' + str(len(restore)) + ' archived=' + str(len(extras)))


if __name__ == '__main__':
    main()
