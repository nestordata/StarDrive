#!/usr/bin/env python3
"""Post-process Assimp FBX→OBJ sidecars for StarDrive DesktopVK.

NanoMesh's FBX loader remaps control points with FbxToOpenGL:
  (x, y, z)_fbx → (x, z, -y)_gl

Assimp's OBJ export leaves verts in FBX/Assimp space and, when a mesh node
has Y scale -1 (common PipelineFBX), bakes that flip into positions:
  obj = (x, -y_local, z)  ⇒  gl = (x, z, -y_local) = (obj.x, obj.z, obj.y)

When the node is identity, Assimp does not flip Y:
  obj = local  ⇒  gl = (obj.x, obj.z, -obj.y)

Assimp's default export preset (TargetRealtime_MaxQuality) also runs
aiProcess_FlipUVs. NanoMesh's FBX SDK path does not — so we un-flip V
(vt.y = 1 - vt.y) to match Windows.

Also rewrites the .mtl: Assimp drops texture refs from these FBXs, so we
wire map_Kd / map_bump / map_Ks / map_Ke to sibling *_d/_n/_s/_e DDS files
(searching the model folder, race parent, and race sibling folders).
"""
from __future__ import annotations

import argparse
import re
import subprocess
import sys
import tempfile
from pathlib import Path

_VERT_RE = re.compile(
    r"^(v|vn)\s+([-+0-9.eE]+)\s+([-+0-9.eE]+)\s+([-+0-9.eE]+)(.*)$"
)
_VT_RE = re.compile(
    r"^vt\s+([-+0-9.eE]+)\s+([-+0-9.eE]+)(.*)$"
)


def assimp_baked_y_flip(fbx: Path) -> bool:
    """True if Assimp will bake a negative Y scale from some mesh node."""
    if not fbx.is_file():
        return True
    with tempfile.TemporaryDirectory() as td:
        dump = Path(td) / "dump.assxml"
        r = subprocess.run(
            ["assimp", "dump", str(fbx), str(dump)],
            capture_output=True,
            text=True,
        )
        if r.returncode != 0 or not dump.is_file():
            return True
        text = dump.read_text(errors="ignore")
    for body in re.findall(r"<Matrix4>\s*([^<]+)</Matrix4>", text, re.S):
        nums = [float(x) for x in body.split()]
        if len(nums) >= 16 and nums[5] < 0:
            return True
    return False


def remap_vec(x: float, y: float, z: float, baked_y_flip: bool) -> tuple[float, float, float]:
    if baked_y_flip:
        return (x, z, y)
    return (x, z, -y)


def _channel_base(name: str, channel: str) -> str:
    base = name.lower()
    for suf in (f"_{channel}_0", f"_{channel}"):
        if base.endswith(suf):
            return base[: -len(suf)]
    return base


def _stem_variants(stem: str) -> list[str]:
    """Fighter1 / ship10b → prefixes worth matching against ship10_d.dds."""
    s = stem.lower()
    out = [s]
    # strip trailing variant letter: ship10b → ship10, ship09a → ship09
    if len(s) > 2 and s[-1].isalpha() and s[-2].isdigit():
        out.append(s[:-1])
    return out


def find_channel(dir_path: Path, stem: str, channel: str) -> str | None:
    """Return relative path (filename or ../... or ../../...) of best DDS."""
    variants = _stem_variants(stem)

    search_dirs: list[tuple[Path, str]] = [(dir_path, "")]
    parent = dir_path.parent
    if parent != dir_path:
        search_dirs.append((parent, "../"))
        # Race siblings: Model/Ships/Terran/HeavyGunboat → Terran/*/ship10_d.dds
        for sibling in sorted(parent.iterdir()):
            if sibling.is_dir() and sibling != dir_path:
                search_dirs.append((sibling, f"../{sibling.name}/"))

    exact: list[tuple[int, str]] = []
    fuzzy: list[tuple[int, int, str]] = []
    same_dir_any: list[str] = []

    for d, prefix in search_dirs:
        if not d.is_dir():
            continue
        depth = prefix.count("..")
        try:
            files = list(d.iterdir())
        except OSError:
            continue
        for p in files:
            if not p.is_file():
                continue
            n = p.name.lower()
            if not (n.endswith(f"_{channel}.dds") or n.endswith(f"_{channel}_0.dds")):
                continue
            base = _channel_base(p.stem, channel)
            rel = prefix + p.name
            matched = False
            for i, var in enumerate(variants):
                if base == var or p.stem.lower().startswith(var + f"_{channel}"):
                    exact.append((depth * 10 + i, rel))
                    matched = True
                    break
            if matched:
                continue
            score = 0
            for var in variants:
                common = 0
                for a, b in zip(base, var):
                    if a != b:
                        break
                    common += 1
                score = max(score, common)
            fuzzy.append((depth, -score, rel))
            # Last resort: any map in the model folder itself (not race siblings).
            if prefix == "":
                same_dir_any.append(rel)

    if exact:
        return sorted(exact, key=lambda t: t[0])[0][1]
    if fuzzy:
        best = sorted(fuzzy, key=lambda t: (t[0], t[1]))[0]
        if -best[1] >= 4:
            return best[2]
    if same_dir_any:
        return sorted(same_dir_any)[0]
    return None


def write_mtl(mtl_path: Path, obj_stem: str) -> None:
    d = mtl_path.parent
    kd = find_channel(d, obj_stem, "d")
    kn = find_channel(d, obj_stem, "n")
    ks = find_channel(d, obj_stem, "s")
    ke = find_channel(d, obj_stem, "e") or find_channel(d, obj_stem, "g")

    lines = [
        "# StarDrive DesktopVK sidecar (Assimp + FbxToOpenGL remap)",
        "newmtl DefaultMaterial",
        "Kd 1.0 1.0 1.0",
        "Ka 0.0 0.0 0.0",
        "Ks 0.5 0.5 0.5",
        "Ns 32",
        "illum 2",
    ]
    if kd:
        lines.append(f"map_Kd {kd}")
    if kn:
        lines.append(f"map_bump {kn}")
    if ks:
        lines.append(f"map_Ks {ks}")
    if ke:
        lines.append(f"map_Ke {ke}")
    lines.append("")
    mtl_path.write_text("\n".join(lines), encoding="utf-8")


def postprocess_obj(obj_path: Path, fbx_path: Path | None = None) -> None:
    fbx = fbx_path
    if fbx is None:
        for ext in (".fbx", ".FBX"):
            cand = obj_path.with_suffix(ext)
            if cand.is_file():
                fbx = cand
                break
    baked = assimp_baked_y_flip(fbx) if fbx else True

    text = obj_path.read_text(encoding="utf-8", errors="replace")
    out_lines: list[str] = []
    for line in text.splitlines():
        m = _VERT_RE.match(line)
        if m:
            kind, xs, ys, zs, rest = m.groups()
            x, y, z = remap_vec(float(xs), float(ys), float(zs), baked)
            out_lines.append(f"{kind} {x:.9g} {y:.9g} {z:.9g}{rest}")
            continue
        m = _VT_RE.match(line)
        if m:
            u, v, rest = m.groups()
            # Undo Assimp aiProcess_FlipUVs to match NanoMesh FBX SDK UVs.
            out_lines.append(f"vt {float(u):.9g} {1.0 - float(v):.9g}{rest}")
            continue
        out_lines.append(line)
    obj_path.write_text("\n".join(out_lines) + "\n", encoding="utf-8")

    stem = obj_path.stem
    mtl_name = None
    for line in out_lines:
        if line.startswith("mtllib "):
            mtl_name = line.split(None, 1)[1].strip()
            break
    mtl_path = obj_path.parent / (mtl_name if mtl_name else f"{stem}.mtl")
    write_mtl(mtl_path, stem)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("obj", type=Path, help="Path to .obj to remap + fix MTL")
    ap.add_argument("--fbx", type=Path, default=None, help="Source FBX (for Y-flip detect)")
    args = ap.parse_args()
    if not args.obj.is_file():
        print(f"ERROR: missing {args.obj}", file=sys.stderr)
        return 1
    postprocess_obj(args.obj, args.fbx)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
