"""Builds a .unitypackage from a package folder without Unity.

A .unitypackage is a gzipped tar with one directory per asset, named by the asset's GUID
(from its .meta file), holding `asset` (the file; folders have none), `asset.meta` and
`pathname` (the path the asset is imported to). The package is imported to
Packages/<package name>/, the same place VCC and the Package Manager put it.

usage: python make_unitypackage.py <package folder> <output .unitypackage>
"""
import io
import json
import os
import re
import sys
import tarfile

GUID = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.MULTILINE)


def add_bytes(tar, name, data):
    info = tarfile.TarInfo(name)
    info.size = len(data)
    info.mode = 0o644
    tar.addfile(info, io.BytesIO(data))


def main(package_dir, output):
    with open(os.path.join(package_dir, "package.json"), encoding="utf-8") as f:
        name = json.load(f)["name"]
    root = "Packages/" + name
    count = 0
    with tarfile.open(output, "w:gz") as tar:
        for dirpath, dirnames, filenames in os.walk(package_dir):
            dirnames.sort()
            entries = [os.path.join(dirpath, d) for d in dirnames] + [os.path.join(dirpath, f) for f in sorted(filenames)]
            for path in entries:
                if path.endswith(".meta"):
                    continue
                meta = path + ".meta"
                if not os.path.exists(meta):
                    print("skipped (no .meta): " + path, file=sys.stderr)
                    continue
                with open(meta, "rb") as f:
                    meta_bytes = f.read()
                match = GUID.search(meta_bytes.decode("utf-8", "replace"))
                if not match:
                    print("skipped (no guid): " + path, file=sys.stderr)
                    continue
                guid = match.group(1)
                rel = os.path.relpath(path, package_dir).replace(os.sep, "/")
                add_bytes(tar, guid + "/pathname", (root + "/" + rel).encode("utf-8"))
                add_bytes(tar, guid + "/asset.meta", meta_bytes)
                if os.path.isfile(path):
                    with open(path, "rb") as f:
                        add_bytes(tar, guid + "/asset", f.read())
                count += 1
    print(f"{output}: {count} assets under {root}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
