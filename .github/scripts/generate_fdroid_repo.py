#!/usr/bin/env python3
"""Generate F-Droid-compatible repository metadata for RequestTracker Android.

Usage:
    python generate_fdroid_repo.py \
        --version <semver> \
        --version-code <int> \
        --apk-url <release-asset-url> \
        --apk-hash <sha256-hex> \
        --apk-size <bytes> \
        --repo-url <github-pages-fdroid-url> \
        --output-dir <path-to-docs/fdroid/repo>

Generates:
    - index-v1.json  (F-Droid repo index)
    - entry.json     (F-Droid entry point)
"""

import argparse
import json
import os
import time


def generate_index_v1(args):
    """Generate the F-Droid index-v1.json with app metadata and package info."""
    timestamp = int(time.time()) * 1000  # F-Droid uses millis

    index = {
        "repo": {
            "name": "RequestTracker F-Droid Repo",
            "description": "F-Droid repository for the RequestTracker Android application.",
            "icon": "icons/com.requesttracker.android.128.png",
            "address": args.repo_url,
            "timestamp": timestamp,
            "version": 21,
        },
        "requests": {},
        "apps": [
            {
                "packageName": "com.requesttracker.android",
                "name": "RequestTracker",
                "summary": "View and analyze Copilot session logs on Android",
                "description": (
                    "RequestTracker is an Avalonia-based Android app for browsing, "
                    "searching, and analyzing Copilot request/session logs. Supports "
                    "phone (portrait NavigationView) and tablet (desktop-like) layouts."
                ),
                "license": "MIT",
                "webSite": "https://github.com/sharpninja/RequestTracker",
                "sourceCode": "https://github.com/sharpninja/RequestTracker",
                "issueTracker": "https://github.com/sharpninja/RequestTracker/issues",
                "categories": ["Development", "System"],
                "icon": "icons/com.requesttracker.android.128.png",
                "suggestedVersionName": args.version,
                "suggestedVersionCode": args.version_code,
            }
        ],
        "packages": {
            "com.requesttracker.android": [
                {
                    "versionName": args.version,
                    "versionCode": args.version_code,
                    "apkName": f"RequestTracker-{args.version}.apk",
                    "hash": args.apk_hash,
                    "hashType": "sha256",
                    "size": args.apk_size,
                    "minSdkVersion": 21,
                    "targetSdkVersion": 35,
                    "nativecode": ["arm64-v8a", "armeabi-v7a", "x86_64"],
                    "srcname": f"RequestTracker-{args.version}-src.tar.gz",
                    "sig": "",
                    "packageName": "com.requesttracker.android",
                    "apkUrl": args.apk_url,
                }
            ]
        },
    }

    return index


def generate_entry(args):
    """Generate the F-Droid entry.json pointing to the index."""
    return {
        "timestamp": int(time.time()) * 1000,
        "version": args.version,
        "index": {
            "name": "/repo/index-v1.json",
            "sha256": "",  # Will be filled after writing index
            "size": 0,
            "numPackages": 1,
        },
    }


def main():
    parser = argparse.ArgumentParser(description="Generate F-Droid repo metadata")
    parser.add_argument("--version", required=True, help="Full SemVer version string")
    parser.add_argument(
        "--version-code", required=True, type=int, help="Android version code (int)"
    )
    parser.add_argument(
        "--apk-url", required=True, help="Download URL for APK (GitHub release asset)"
    )
    parser.add_argument(
        "--apk-hash", required=True, help="SHA-256 hash of the APK file"
    )
    parser.add_argument(
        "--apk-size", required=True, type=int, help="Size of APK in bytes"
    )
    parser.add_argument(
        "--repo-url", required=True, help="Base URL of the F-Droid repo on GitHub Pages"
    )
    parser.add_argument(
        "--output-dir", required=True, help="Output directory for repo files"
    )
    args = parser.parse_args()

    os.makedirs(args.output_dir, exist_ok=True)

    # Generate index-v1.json
    index = generate_index_v1(args)
    index_path = os.path.join(args.output_dir, "index-v1.json")
    index_json = json.dumps(index, indent=2)
    with open(index_path, "w") as f:
        f.write(index_json)

    # Compute hash and size of the index file for entry.json
    import hashlib

    index_bytes = index_json.encode("utf-8")
    index_sha256 = hashlib.sha256(index_bytes).hexdigest()

    # Generate entry.json
    entry = generate_entry(args)
    entry["index"]["sha256"] = index_sha256
    entry["index"]["size"] = len(index_bytes)
    entry_path = os.path.join(args.output_dir, "entry.json")
    with open(entry_path, "w") as f:
        json.dump(entry, f, indent=2)

    print(f"Generated F-Droid repo metadata in {args.output_dir}")
    print(f"  index-v1.json: {len(index_bytes)} bytes, sha256={index_sha256}")
    print(f"  Version: {args.version} (code {args.version_code})")
    print(f"  APK URL: {args.apk_url}")
    print(f"  APK hash: {args.apk_hash}")


if __name__ == "__main__":
    main()
