#!/usr/bin/env python3
"""Download NelderimLauncher builds from GitHub Actions artifacts.

Layout produced (expected by NelderimManifestUpdate):
    <out>/win/NelderimLauncher.exe
    <out>/linux/NelderimLauncher
    <out>/osx/NelderimLauncher

Without arguments the latest successful build of master is used. Pass a run url
(https://github.com/<owner>/<repo>/actions/runs/<id>) or a run id to pick another one.

A token is required, GitHub does not serve artifacts anonymously. Set GITHUB_TOKEN below,
or the GITHUB_TOKEN env var which takes precedence.
"""

import argparse
import io
import os
import re
import stat
import sys
import urllib.error
import urllib.request
import zipfile
from pathlib import Path

from build_package import API, api_get, fail, make_request

# Fill in on the server only, never commit it
GITHUB_TOKEN = ""

REPO = "UONelderim/Launcher"
WORKFLOW = "build.yml"
BRANCH = "master"
# Artifact name (see .github/workflows/build.yml) -> platform directory, binary name
ARTIFACTS = {
    "NelderimLauncher-Windows-X64": ("win", "NelderimLauncher.exe"),
    "NelderimLauncher-Linux-X64": ("linux", "NelderimLauncher"),
    "NelderimLauncher-macOS-ARM64": ("osx", "NelderimLauncher"),
}


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


def resolve_run(run, repo):
    """Returns (repo, run id) for a run url, a run id or the latest successful build."""
    if run is None:
        runs = api_get(f"{API}/repos/{repo}/actions/workflows/{WORKFLOW}/runs"
                       f"?branch={BRANCH}&event=push&status=success&per_page=1")["workflow_runs"]
        if not runs:
            fail(f"no successful {WORKFLOW} run on {BRANCH} in {repo}")
        return repo, runs[0]["id"]
    if run.isdigit():
        return repo, int(run)
    match = re.search(r"github\.com/([^/]+/[^/]+)/actions/runs/(\d+)", run)
    if not match:
        fail(f"not a run url or run id: {run}")
    return match.group(1), int(match.group(2))


def download(artifact):
    """Returns artifact zip content. Storage url is presigned and rejects the GitHub token, so redirect is followed by hand."""
    print(f"  downloading {artifact['name']} ({artifact['size_in_bytes'] / 1024 / 1024:.1f} MB)")
    opener = urllib.request.build_opener(NoRedirect)
    try:
        try:
            with opener.open(make_request(artifact["archive_download_url"], "application/vnd.github+json")) as resp:
                return resp.read()
        except urllib.error.HTTPError as e:
            if e.code not in (301, 302, 303, 307, 308):
                raise
            with urllib.request.urlopen(e.headers["Location"]) as resp:
                return resp.read()
    except urllib.error.HTTPError as e:
        fail(f"download of {artifact['name']} failed: {e.code} {e.reason}")
    except urllib.error.URLError as e:
        fail(f"download of {artifact['name']} failed: {e.reason}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("run", nargs="?", help="workflow run url or id (default: latest successful build of master)")
    parser.add_argument("--out", type=Path, default=Path("launcher"), help="output directory (default: ./launcher)")
    args = parser.parse_args()

    # build_package helpers read the token from the environment
    if not os.environ.get("GITHUB_TOKEN"):
        if not GITHUB_TOKEN:
            fail(f"GITHUB_TOKEN is not set, it is required to download artifacts; set it in {Path(__file__).name}")
        os.environ["GITHUB_TOKEN"] = GITHUB_TOKEN

    repo, run_id = resolve_run(args.run, REPO)
    run = api_get(f"{API}/repos/{repo}/actions/runs/{run_id}")
    print(f"{repo} run {run_id}: {run['head_sha'][:7]} {run['display_title']} ({run['created_at']})")
    if run["conclusion"] != "success":
        print(f"warning: run conclusion is '{run['conclusion']}'", file=sys.stderr)

    artifacts = {a["name"]: a
                 for a in api_get(f"{API}/repos/{repo}/actions/runs/{run_id}/artifacts?per_page=100")["artifacts"]}
    missing = [name for name in ARTIFACTS if name not in artifacts]
    if missing:
        fail(f"run {run_id} has no artifacts: {', '.join(missing)}; available: {', '.join(artifacts) or '<none>'}")
    expired = [name for name in ARTIFACTS if artifacts[name]["expired"]]
    if expired:
        fail(f"artifacts expired: {', '.join(expired)}")

    # Everything is downloaded before the first write, so a failure never leaves a mix of two builds
    binaries = {}
    for name, (platform, binary) in ARTIFACTS.items():
        with zipfile.ZipFile(io.BytesIO(download(artifacts[name]))) as zf:
            members = [n for n in zf.namelist() if n.rsplit("/", 1)[-1] == binary]
            if len(members) != 1:
                fail(f"expected one {binary} in {name}, found: {', '.join(zf.namelist()) or '<none>'}")
            binaries[platform, binary] = zf.read(members[0])

    for (platform, binary), data in binaries.items():
        target = args.out / platform / binary
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        if platform != "win" and os.name != "nt":
            target.chmod(target.stat().st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)
        print(f"  {target} ({len(data) / 1024 / 1024:.1f} MB)")

    print(f"\nLaunchers ready ({run['head_sha'][:7]})")


if __name__ == "__main__":
    main()
