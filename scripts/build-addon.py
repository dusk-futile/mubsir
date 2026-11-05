#!/usr/bin/env python3

import argparse
import os
import re
import sys
import time
import zipfile

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_SOURCE = os.path.join(REPO_ROOT, "nvda-addon", "mubsirBridge")
DEFAULT_OUT_DIR = os.path.join(REPO_ROOT, "dist")

REQUIRED_KEYS = ("name", "summary", "description", "author", "version", "minimumNVDAVersion", "lastTestedNVDAVersion")
API_VERSION_RE = re.compile(r"^(0|\d{4})\.(\d)(?:\.(\d))?$")
ADDON_VERSION_RE = re.compile(r"^\d+\.\d+\.\d+$")
EXCLUDED_DIRS = {"__pycache__", ".pytest_cache"}
EXCLUDED_SUFFIXES = (".pyc", ".pyo")
FIXED_TIME = (1980, 1, 1, 0, 0, 0)

class AddonError(Exception):
    pass

def read_manifest(path):
    values = {}
    with open(path, encoding="utf-8") as f:
        for number, raw in enumerate(f, 1):
            line = raw.strip()
            if not line or line.startswith("#"):
                continue
            if line.startswith("["):
                raise AddonError("manifest.ini line %d: sections are not supported by this script" % number)
            key, sep, value = line.partition("=")
            if not sep:
                raise AddonError("manifest.ini line %d: expected 'key = value'" % number)
            key, value = key.strip(), value.strip()
            if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
                value = value[1:-1]
            elif "," in value:
                raise AddonError("manifest.ini line %d: quote values that contain commas" % number)
            values[key] = value
    return values

def validate(source):
    manifest_path = os.path.join(source, "manifest.ini")
    if not os.path.isfile(manifest_path):
        raise AddonError("missing %s" % manifest_path)
    manifest = read_manifest(manifest_path)
    missing = [k for k in REQUIRED_KEYS if not manifest.get(k)]
    if missing:
        raise AddonError("manifest.ini is missing: %s" % ", ".join(missing))
    if not ADDON_VERSION_RE.match(manifest["version"]):
        raise AddonError("version %r is not major.minor.patch" % manifest["version"])
    versions = []
    for key in ("minimumNVDAVersion", "lastTestedNVDAVersion"):
        match = API_VERSION_RE.match(manifest[key])
        if not match:
            raise AddonError("%s %r is not an NVDA version like 2026.1" % (key, manifest[key]))
        versions.append(tuple(int(g or 0) for g in match.groups()))
    if versions[0] > versions[1]:
        raise AddonError("minimumNVDAVersion is newer than lastTestedNVDAVersion")
    plugin_init = os.path.join(source, "globalPlugins", manifest["name"], "__init__.py")
    if not os.path.isfile(plugin_init):
        raise AddonError("missing %s" % plugin_init)
    for path in package_files(source):
        if path.endswith(".py"):
            with open(path, encoding="utf-8") as f:
                try:
                    compile(f.read(), path, "exec")
                except SyntaxError as e:
                    raise AddonError("syntax error in %s: %s" % (path, e))
    return manifest

def package_files(source):
    files = []
    for folder, dirs, names in os.walk(source):
        dirs[:] = sorted(d for d in dirs if d not in EXCLUDED_DIRS and not d.startswith("."))
        for name in names:
            if name.startswith(".") or name.endswith(EXCLUDED_SUFFIXES):
                continue
            files.append(os.path.join(folder, name))
    return sorted(files, key=lambda p: os.path.relpath(p, source).replace(os.sep, "/"))

def zip_time():
    epoch = os.environ.get("SOURCE_DATE_EPOCH")
    if not epoch:
        return FIXED_TIME
    stamp = time.gmtime(max(int(epoch), 315532800))
    return stamp[:6]

def build(source, out_dir, manifest):
    os.makedirs(out_dir, exist_ok=True)
    target = os.path.join(out_dir, "%s-%s.nvda-addon" % (manifest["name"], manifest["version"]))
    stamp = zip_time()
    partial = target + ".partial"
    with zipfile.ZipFile(partial, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for path in package_files(source):
            info = zipfile.ZipInfo(os.path.relpath(path, source).replace(os.sep, "/"), date_time=stamp)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            with open(path, "rb") as f:
                archive.writestr(info, f.read())
    os.replace(partial, target)
    return target

def main(argv=None):
    parser = argparse.ArgumentParser(description="Package the Mubsir Bridge NVDA add-on.")
    parser.add_argument("--source", default=DEFAULT_SOURCE, help="add-on folder (default: nvda-addon/mubsirBridge)")
    parser.add_argument("--out-dir", default=DEFAULT_OUT_DIR, help="output folder (default: dist)")
    parser.add_argument("--check", action="store_true", help="validate only, do not write the package")
    args = parser.parse_args(argv)

    try:
        manifest = validate(args.source)
        if args.check:
            print("OK: %s %s is valid" % (manifest["name"], manifest["version"]))
            return 0
        target = build(args.source, args.out_dir, manifest)
    except (AddonError, OSError) as e:
        print("ERROR: %s" % e, file=sys.stderr)
        return 1
    with zipfile.ZipFile(target) as archive:
        count = len(archive.namelist())
    print("Built %s (%d files, %d bytes)" % (os.path.relpath(target), count, os.path.getsize(target)))
    return 0

if __name__ == "__main__":
    sys.exit(main())
