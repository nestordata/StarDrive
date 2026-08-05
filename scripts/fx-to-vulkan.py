#!/usr/bin/env python3
"""Rewrite an HLSL .fx source for mgfxc /Profile:Vulkan (SM6 + Texture2D.Sample)."""
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

    # Collect texture↔sampler links from sampler_state { Texture = (Name); } or <Name>
    tex_for_sampler: dict[str, str] = {}
    for m in re.finditer(
        r"sampler2D\s+(\w+)[\s\S]*?Texture\s*=\s*[<(]\s*(\w+)\s*[>)]",
        src,
    ):
        tex_for_sampler[m.group(1)] = m.group(2)
    for m in re.finditer(
        r"sampler\s+(\w+)\s*(?::\s*register)?[\s\S]*?Texture\s*=\s*[<(]\s*(\w+)\s*[>)]",
        src,
    ):
        tex_for_sampler.setdefault(m.group(1), m.group(2))

    # texture Name; → Texture2D Name;
    src = re.sub(r"^\s*texture\s+(\w+)\s*;", r"Texture2D \1;", src, flags=re.MULTILINE)

    # sampler Foo : register(sN); → Texture2D Foo_Texture; SamplerState Foo;
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

    # sampler2D Name ... ;  (possibly multi-line sampler_state) → SamplerState Name;
    src = re.sub(
        r"sampler2D\s+(\w+)(\s*(?:=\s*sampler_state\s*\{[\s\S]*?\})?\s*;)",
        r"SamplerState \1\2",
        src,
    )

    # Drop obsolete sampler_state bodies (DXC ignores / errors on Texture=<>)
    src = re.sub(
        r"(SamplerState\s+\w+)\s*=\s*sampler_state\s*\{[\s\S]*?\}\s*;",
        r"\1;",
        src,
    )

    # Common StarDrive naming: FooSampler samples texture FooMap / Foo / tex
    known = {
        "ShadowSampler": "ShadowMap",
        "EmissiveSampler": "EmissiveMap",
        "SpecularSampler": "SpecularMap",
        "NormalSampler": "NormalMap",
        "TextureSampler": "Texture",
        "AlphaMapSampler": "AlphaMap",
        "texsampler": "tex",
        "WrapSampler": "Texture",
        "ClampSampler": "Texture",
        "LightsSampler": "LightsTexture",
        "BaseSampler": "BaseTexture",
        "BloomSampler": "BloomTexture",
        "ColorSampler": "ColorTexture",
        "Noise": "noise_texture",
    }

    def guess_tex(samp: str) -> str:
        if samp in tex_for_sampler:
            return tex_for_sampler[samp]
        if samp in known:
            return known[samp]
        if samp.endswith("Sampler"):
            stem = samp[: -len("Sampler")]
            for candidate in (stem + "Map", stem + "Texture", stem):
                if re.search(rf"\bTexture2D\s+{re.escape(candidate)}\b", src) or re.search(
                    rf"\btexture\s+{re.escape(candidate)}\b", src
                ):
                    return candidate
            return stem + "Map" if stem else samp
        if samp.endswith("sampler"):
            stem = samp[: -len("sampler")]
            return stem if stem else samp
        return f"{samp}_Texture"

    def repl_tex2d(m: re.Match) -> str:
        samp, uv = m.group(1), m.group(2)
        return f"{guess_tex(samp)}.Sample({samp}, {uv})"

    src = re.sub(r"\btex2D\s*\(\s*(\w+)\s*,\s*([^)]+)\)", repl_tex2d, src)
    src = re.sub(
        r"\btex2Dlod\s*\(\s*(\w+)\s*,\s*float4\s*\(\s*([^,]+)\s*,[^)]+\)\s*\)",
        lambda m: f"{guess_tex(m.group(1))}.Sample({m.group(1)}, {m.group(2)})",
        src,
    )

    # Clip-space VS *output* Position (keep VS input POSITION0 for attributes)
    src = re.sub(r"(\bfloat4\s+Position\s*:\s*)POSITION0\b", r"\1SV_POSITION", src)

    # PS return semantic only: float4 Foo(...) : COLOR0
    src = re.sub(r"(\)\s*:\s*)COLOR0\b", r"\1SV_TARGET", src)
    src = re.sub(r"(\)\s*:\s*)SV_TARGET\b", r"\1SV_TARGET", src)  # idempotent

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
