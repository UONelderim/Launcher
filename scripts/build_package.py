#!/usr/bin/env python3
"""Build a ready-to-use ClassicUO package with Razor Enhanced and Razor CE plugins.

Layout produced:
    <out>/ClassicUO/                         ClassicUO release for chosen platform
    <out>/ClassicUO/settings.json            copied from template
    <out>/ClassicUO/Plugins/RazorEnhanced/   latest Razor Enhanced release
    <out>/ClassicUO/Plugins/Razor/           latest Razor CE release (x64)

Set GITHUB_TOKEN env var to avoid GitHub API rate limits.
"""

import argparse
import json
import os
import re
import shutil
import stat
import sys
import tempfile
import urllib.error
import urllib.request
import zipfile
from pathlib import Path

API = "https://api.github.com"
USER_AGENT = "NelderimPackageBuilder"

CLASSICUO_RELEASE = f"{API}/repos/ClassicUO/ClassicUO/releases/tags/ClassicUO-main-release"
RAZOR_ENHANCED_RELEASE = f"{API}/repos/UltimaTools/RazorEnhanced/releases/latest"
RAZOR_CE_RELEASE = f"{API}/repos/markdwags/Razor/releases/latest"

PLATFORMS = ("win", "linux", "osx")
# Files in the linux/osx ClassicUO zip that must be executable
UNIX_EXECUTABLES = ("ClassicUO", "ClassicUO.bin.x86_64", "ClassicUO.bin.osx")


def fail(msg):
    print(f"error: {msg}", file=sys.stderr)
    sys.exit(1)


def make_request(url, accept):
    headers = {"User-Agent": USER_AGENT, "Accept": accept}
    token = os.environ.get("GITHUB_TOKEN")
    if token:
        headers["Authorization"] = f"Bearer {token}"
    return urllib.request.Request(url, headers=headers)


def api_get(url):
    try:
        with urllib.request.urlopen(make_request(url, "application/vnd.github+json")) as resp:
            return json.load(resp)
    except urllib.error.HTTPError as e:
        fail(f"GET {url} failed: {e.code} {e.reason}")
    except urllib.error.URLError as e:
        fail(f"GET {url} failed: {e.reason}")


def find_asset(release, pattern):
    regex = re.compile(pattern)
    for asset in release.get("assets", []):
        if regex.fullmatch(asset["name"]):
            return asset
    names = ", ".join(a["name"] for a in release.get("assets", [])) or "<none>"
    fail(f"no asset matching '{pattern}' in release {release.get('tag_name')}; available: {names}")


def download(asset, dest_dir):
    dest = Path(dest_dir) / asset["name"]
    print(f"  downloading {asset['name']} ({asset['size'] / 1024 / 1024:.1f} MB)")
    try:
        with urllib.request.urlopen(make_request(asset["browser_download_url"], "application/octet-stream")) as resp, \
                open(dest, "wb") as f:
            shutil.copyfileobj(resp, f)
    except urllib.error.URLError as e:
        fail(f"download of {asset['name']} failed: {e}")
    return dest


def extract(zip_path, dest):
    dest.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(zip_path) as zf:
        names = [n for n in zf.namelist() if n.strip("/")]
        roots = {n.split("/", 1)[0] for n in names}
        # Flatten if the whole archive sits inside a single top-level folder
        prefix = ""
        if len(roots) == 1 and all(n.startswith(next(iter(roots)) + "/") for n in names):
            prefix = next(iter(roots)) + "/"

        for info in zf.infolist():
            rel = info.filename[len(prefix):]
            if not rel or info.is_dir():
                continue
            target = (dest / rel).resolve()
            if not target.is_relative_to(dest.resolve()):
                fail(f"unsafe path in archive: {info.filename}")
            target.parent.mkdir(parents=True, exist_ok=True)
            with zf.open(info) as src, open(target, "wb") as out:
                shutil.copyfileobj(src, out)
            mode = info.external_attr >> 16
            if mode and os.name != "nt":
                os.chmod(target, stat.S_IMODE(mode))


def install(name, release_url, asset_pattern, dest, tmp):
    print(f"{name}:")
    release = api_get(release_url)
    asset = find_asset(release, asset_pattern)
    zip_path = download(asset, tmp)
    print(f"  extracting to {dest}")
    extract(zip_path, dest)
    return release["tag_name"]


def main():
    script_dir = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--platform", choices=PLATFORMS, default="win",
                        help="ClassicUO build to fetch (plugins are the same for all platforms)")
    parser.add_argument("--out", type=Path, help="output directory (default: ./dist/<platform>)")
    parser.add_argument("--settings", type=Path, default=script_dir / "settings.json",
                        help="settings.json template to copy into ClassicUO directory")
    parser.add_argument("--clean", action="store_true", help="remove existing ClassicUO directory first")
    args = parser.parse_args()

    out = (args.out or Path("dist") / args.platform).resolve()
    cuo_dir = out / "ClassicUO"
    plugins_dir = cuo_dir / "Plugins"

    try:
        json.loads(args.settings.read_text(encoding="utf-8"))
    except FileNotFoundError:
        fail(f"settings template not found: {args.settings}")
    except json.JSONDecodeError as e:
        fail(f"settings template {args.settings} is not valid JSON: {e}")

    if args.clean and cuo_dir.exists():
        print(f"removing {cuo_dir}")
        shutil.rmtree(cuo_dir)

    with tempfile.TemporaryDirectory() as tmp:
        versions = {
            "ClassicUO": install("ClassicUO", CLASSICUO_RELEASE,
                                 rf"ClassicUO-{args.platform}-x64-release\.zip", cuo_dir, tmp),
            "RazorEnhanced": install("Razor Enhanced", RAZOR_ENHANCED_RELEASE,
                                     r"RazorEnhanced-.*\.zip", plugins_dir / "RazorEnhanced", tmp),
            "Razor": install("Razor CE", RAZOR_CE_RELEASE,
                             r"Razor-x64-.*\.zip", plugins_dir / "Razor", tmp),
        }

    if args.platform != "win":
        for name in UNIX_EXECUTABLES:
            path = cuo_dir / name
            if path.exists():
                path.chmod(path.stat().st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)

    shutil.copyfile(args.settings, cuo_dir / "settings.json")

    print(f"\nPackage ready ({args.platform})")


if __name__ == "__main__":
    main()
