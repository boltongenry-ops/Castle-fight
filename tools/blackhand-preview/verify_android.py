"""Verify the signed Black Hand APK and preserve the published resource catalog."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from zipfile import ZipFile

import UnityPy


def file_sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def verify_certificate(android, build_tools, apk):
    result = subprocess.check_output(
        [str(android / "OpenJDK/bin/java.exe"), "-jar", str(build_tools / "lib/apksigner.jar"),
         "verify", "--print-certs", str(apk)], text=True,
    )
    return next(row.split(": ", 1)[1] for row in result.splitlines()
                if "certificate SHA-256 digest" in row)


def unity_data(apk):
    with ZipFile(apk) as package:
        assert {"lib/arm64-v8a/libil2cpp.so", "lib/arm64-v8a/lib_burst_generated.so"} <= set(package.namelist())
        return UnityPy.load(package.read("assets/bin/Data/data.unity3d"))


def resources(data):
    manager = next(item.read() for item in data.objects if item.type.name == "ResourceManager")
    return {key.lower(): asset for key, asset in manager.m_Container}


def manifest(data):
    text_assets = (obj.read() for obj in data.objects if obj.type.name == "TextAsset")
    return next(json.loads(asset.m_Script) for asset in text_assets if asset.m_Name == "content")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--android-player", required=True, type=Path)
    parser.add_argument("--reference", required=True, type=Path)
    parser.add_argument("--apk", required=True, type=Path)
    parser.add_argument("--art", required=True, type=Path)
    parser.add_argument("--report", required=True, type=Path)
    args = parser.parse_args()

    tools = args.android_player / "SDK/build-tools/36.0.0"
    certificate = verify_certificate(args.android_player, tools, args.apk)
    assert certificate == verify_certificate(args.android_player, tools, args.reference)
    badge = subprocess.check_output([str(tools / "aapt.exe"), "dump", "badging", str(args.apk)], encoding="utf-8")
    assert "package: name='com.castlefight.unitymap'" in badge
    assert "versionCode='38'" in badge and "versionName='0.43.0'" in badge
    assert "android.permission.RECORD_AUDIO" in badge
    subprocess.run([str(tools / "zipalign.exe"), "-c", "-P", "16", "4", str(args.apk)], check=True)

    baseline = unity_data(args.reference)
    preview = unity_data(args.apk)
    preview_resources = resources(preview)
    assert set(resources(baseline)) <= set(preview_resources)
    assert manifest(preview) == manifest(baseline), "The online resource catalog changed"

    expected = {}
    for file in args.art.rglob("*.bytes"):
        key = "blackhand/" + file.relative_to(args.art).with_suffix("").as_posix().lower()
        data = preview_resources[key].read().m_Script
        if isinstance(data, str):
            data = data.encode("utf-8", errors="surrogateescape")
        assert data == file.read_bytes(), key
        expected[key] = hashlib.sha256(data).hexdigest()
    assert len(expected) == 35
    assert preview_resources["blackhand/clansurface"].read().object_reader.type.name == "Shader"
    assert len(manifest(preview)["bundles"]) == len(manifest(baseline)["bundles"])
    report = {"status": "PASS", "bytes": args.apk.stat().st_size, "sha256": file_sha(args.apk),
              "sameCertificate": True, "versionCode": 38, "blackHandAssets": expected,
              "bundles": len(manifest(preview)["bundles"])}
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("BLACK_HAND_ANDROID_PACKAGE_PASS", report["bytes"], report["sha256"], flush=True)


if __name__ == "__main__":
    main()
