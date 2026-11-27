import json
from pathlib import Path
import subprocess
import sys
from tempfile import TemporaryDirectory


REPO = 'boltongenry-ops/Castle-fight'
TAG = 'v0.49.8'
NAME = 'CastleFight-Portable.exe'
EXPECTED = 'sha256:5b2c1958002fecbf369d2d5941770ccaa82e413de0eb6b20acf0d858433bea2c'
INPUTS = Path(__file__).resolve().parent


def check_release():
    response = subprocess.check_output([
        'gh', 'release', 'view', TAG, '-R', REPO, '--json', 'assets,isDraft,isPrerelease',
    ])
    release = json.loads(response)
    if release['isPrerelease']:
        raise RuntimeError('Expected the existing public stable release')
    for asset in release['assets']:
        if asset['name'] == NAME and asset['state'] == 'uploaded':
            if asset['digest'] != EXPECTED or asset['size'] != 2147323405:
                raise RuntimeError('Published Portable differs from the verified build')
            return True
    return False


def main():
    if check_release():
        print('Verified Portable already published', flush=True)
        return
    with TemporaryDirectory(prefix='castle-fight-release-049-') as tmp:
        temp = Path(tmp)
        baseline = temp / 'baseline.exe'
        portable = temp / NAME
        subprocess.run([
            'curl', '--fail', '--location', '--retry', '3', '--max-time', '600',
            '--silent', '--show-error', '--output', str(baseline),
            f'https://github.com/{REPO}/releases/download/v0.49.7/{NAME}',
        ], check=True)
        subprocess.run([
            sys.executable, str(INPUTS / 'reconstruct_portable.py'), str(baseline),
            str(INPUTS / 'windows.literal'), str(INPUTS / 'recipe0498.json'), '--output', str(portable),
        ], check=True)
        if not check_release():
            upload = subprocess.run(['gh', 'release', 'upload', TAG, str(portable), '-R', REPO])
            if upload.returncode and not check_release():
                upload.check_returncode()
    if not check_release():
        raise RuntimeError('The verified Portable was not published')
    print('Verified Portable available on GitHub', flush=True)


if __name__ == '__main__':
    main()
