#!/usr/bin/env python3
"""Rewrite an HLSL .fx source for mgfxc /Profile:Vulkan (SM6 + Texture2D/3D.Sample)."""
from __future__ import annotations

import re
import sys
from pathlib import Path


def convert(src: str) -> str:
    # Shader models
    src = re.sub(r"\bvs_4_0_level_9_[13]\b", "vs_6_0", src)
    src = re.sub(r"\bps_4_0_level_9_[13]\b", "ps_6_0", src)
    src = re.sub(r"\bvs_4_0\b", "vs_6_0", src)
    src = re.sub(r"\bps_4_0\b", "ps_6_0", src)
    src = re.sub(r"\bvs_3_0\b", "vs_6_0", src)
    src = re.sub(r"\bps_3_0\b", "ps_6_0", src)

    tex_for_sampler: dict[str, str] = {}
    tex3d_names: set[str] = set()

    # Only match Texture= inside an explicit sampler_state block (do not cross decls).
    for m in re.finditer(
        r"sampler([23])D\s+(\w+)(?:\s*:\s*register\s*\(\s*s\d+\s*\))?\s*"
        r"=\s*sampler_state\s*\{([\s\S]*?)\}",
        src,
    ):
        dim, samp, body = m.group(1), m.group(2), m.group(3)
        tm = re.search(r"Texture\s*=\s*[<(]\s*(\w+)\s*[>)]", body)
        if tm:
            tex_for_sampler[samp] = tm.group(1)
            if dim == "3":
                tex3d_names.add(tm.group(1))

    for m in re.finditer(
        r"sampler\s+(\w+)(?:\s*:\s*register\s*\(\s*s\d+\s*\))?\s*"
        r"=\s*sampler_state\s*\{([\s\S]*?)\}",
        src,
    ):
        samp, body = m.group(1), m.group(2)
        tm = re.search(r"Texture\s*=\s*[<(]\s*(\w+)\s*[>)]", body)
        if tm:
            tex_for_sampler.setdefault(samp, tm.group(1))

    # texture Name; → Texture2D / Texture3D Name;
    def repl_texture(m: re.Match) -> str:
        name = m.group(1)
        kind = "Texture3D" if name in tex3d_names else "Texture2D"
        return f"{kind} {name};"

    src = re.sub(r"^\s*texture\s+(\w+)\s*;", repl_texture, src, flags=re.MULTILINE)

    # sampler Foo : register(sN);  (SpriteBatch s0 style, no sampler_state)
    def repl_sampler_reg(m: re.Match) -> str:
        name = m.group(1)
        tex_for_sampler.setdefault(name, f"{name}_Texture")
        return f"Texture2D {name}_Texture;\nSamplerState {name};"

    src = re.sub(
        r"^\s*sampler\s+(\w+)\s*:\s*register\s*\(\s*s\d+\s*\)\s*;",
        repl_sampler_reg,
        src,
        flags=re.MULTILINE,
    )

    # sampler2D Name [: register(sN)] = sampler_state { ... };
    src = re.sub(
        r"sampler2D\s+(\w+)(?:\s*:\s*register\s*\(\s*s\d+\s*\))?\s*"
        r"=\s*sampler_state\s*\{[\s\S]*?\}\s*;",
        r"SamplerState \1;",
        src,
    )

    # sampler3D Name = sampler_state { ... };
    src = re.sub(
        r"sampler3D\s+(\w+)\s*=\s*sampler_state\s*\{[\s\S]*?\}\s*;",
        r"SamplerState \1;",
        src,
    )

    # Drop leftover SamplerState Name = sampler_state { ... };
    src = re.sub(
        r"(SamplerState\s+\w+)\s*=\s*sampler_state\s*\{[\s\S]*?\}\s*;",
        r"\1;",
        src,
    )

    known = {
        "ShadowSampler": "ShadowMap",
        "EmissiveSampler": "EmissiveMap",
        "SpecularSampler": "SpecularMap",
        "NormalSampler": "NormalMap",
        "TextureSampler": "TextureSampler_Texture",
        "AlphaMapSampler": "AlphaMap",
        "texsampler": "tex",
        "WrapSampler": "Texture",
        "ClampSampler": "Texture",
        "LightsSampler": "LightsTexture",
        "BaseSampler": "BaseTexture",
        "BloomSampler": "BloomSampler_Texture",
        "ColorSampler": "ColorSampler_Texture",
        "Noise": "noise_texture",
    }

    def guess_tex(samp: str) -> str:
        if samp in tex_for_sampler:
            return tex_for_sampler[samp]
        if samp in known:
            return known[samp]
        if samp.endswith("Sampler"):
            stem = samp[: -len("Sampler")]
            for candidate in (f"{samp}_Texture", stem + "Map", stem + "Texture", stem):
                if re.search(rf"\bTexture[23]D\s+{re.escape(candidate)}\b", src):
                    return candidate
            return f"{samp}_Texture"
        if samp.endswith("sampler"):
            stem = samp[: -len("sampler")]
            return stem if stem else samp
        return f"{samp}_Texture"

    def repl_sample(m: re.Match) -> str:
        samp, uv = m.group(1), m.group(2)
        return f"{guess_tex(samp)}.Sample({samp}, {uv})"

    src = re.sub(r"\btex2D\s*\(\s*(\w+)\s*,\s*([^)]+)\)", repl_sample, src)
    src = re.sub(
        r"\btex2Dlod\s*\(\s*(\w+)\s*,\s*float4\s*\(\s*([^,]+)\s*,[^)]+\)\s*\)",
        lambda m: f"{guess_tex(m.group(1))}.Sample({m.group(1)}, {m.group(2)})",
        src,
    )
    src = re.sub(r"\btex3D\s*\(\s*(\w+)\s*,\s*([^)]+)\)", repl_sample, src)

    # Clip-space Position only on *Output* structs (keep VS input POSITION0)
    def fix_output_struct(m: re.Match) -> str:
        body = m.group(0)
        return re.sub(r"(\bfloat4\s+Position\s*:\s*)POSITION0\b", r"\1SV_POSITION", body)

    src = re.sub(r"struct\s+\w*Output\w*\s*\{[\s\S]*?\}", fix_output_struct, src)

    # PS return semantic only: float4 Foo(...) : COLOR0
    src = re.sub(r"(\)\s*:\s*)COLOR0\b", r"\1SV_TARGET", src)

    return src


def main() -> int:
    if len(sys.argv) != 3:
        print("Usage: fx-to-vulkan.py <in.fx> <out.fx>", file=sys.stderr)
        return 2
    inp, outp = Path(sys.argv[1]), Path(sys.argv[2])
    outp.write_text(convert(inp.read_text()), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
