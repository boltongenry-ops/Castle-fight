import json
from pathlib import Path
import subprocess
import sys
from tempfile import TemporaryDirectory


REPO = 'boltongenry-ops/Castle-fight'
TAG = 'v0.49.9'
NAME = 'CastleFight-Portable.exe'
EXPECTED = 'sha256:5d7918c91b28080ff0e613610eb685ca1cdebd0b321a7b5019e75935e44d24a1'
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
            if asset['digest'] != EXPECTED or asset['size'] != 2146531304:
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
            f'https://github.com/{REPO}/releases/download/v0.49.8/{NAME}',
        ], check=True)
        subprocess.run([
            sys.executable, str(INPUTS / 'reconstruct_portable.py'), str(baseline),
            str(INPUTS / 'windows.literal'), str(INPUTS / 'recipe0499.json'), '--output', str(portable),
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
