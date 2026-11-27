import argparse
import hashlib
import json
from contextlib import nullcontext
from pathlib import Path


def hash_file(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def verify(path, expected):
    if path.stat().st_size != expected['size'] or hash_file(path) != expected['sha256']:
        raise ValueError(f'Wrong size or SHA-256 for {path}')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('baseline', type=Path)
    parser.add_argument('patch', type=Path)
    parser.add_argument('recipe', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    recipe = json.loads(args.recipe.read_bytes())
    verify(args.baseline, recipe['old'])
    verify(args.patch, recipe['patch'])

    checksum = hashlib.sha256()
    size = 0
    target = args.output.open('xb') if args.output else nullcontext()
    with args.baseline.open('rb') as baseline, args.patch.open('rb') as patch, target as output:
        inputs = {'old': baseline, 'patch': patch}
        for source, offset, length in recipe['blocks']:
            stream = inputs[source]
            stream.seek(offset)
            remaining = length
            while remaining:
                block = stream.read(min(remaining, 2**20))
                if not block:
                    raise ValueError(f'Incomplete {source} range at offset {offset}')
                if output is not None:
                    output.write(block)
                checksum.update(block)
                size += len(block)
                remaining -= len(block)

    digest = checksum.hexdigest()
    if size != recipe['windows']['size'] or digest != recipe['windows']['sha256']:
        raise ValueError('Reconstructed Portable does not match the verified player')
    print(f'Windows Portable verified, {size} bytes, SHA-256 {digest}', flush=True)


if __name__ == '__main__':
    main()
