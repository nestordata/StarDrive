using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using SDUtils;

namespace SDGraphics.Shaders;

// XNA 3.1's runtime HLSL compilation APIs (CompiledEffect.FromFile, CompilerOptions, etc.)
// are removed in MonoGame; effects are precompiled to .mgfx via mgfxc and loaded as raw
// bytes through the Effect(GraphicsDevice, byte[]) ctor. FromFile expects a .fx path and
// resolves to a sibling .mgfx of the same base name.
public class Shader : IDisposable
{
    Effect Fx;
    readonly Map<string, EffectParameter> FxParameters;

    Shader(Effect fx)
    {
        Fx = fx;
        FxParameters = new();
        if (fx != null)
        {
            foreach (EffectParameter parameter in Fx.Parameters)
                FxParameters[parameter.Name] = parameter;
        }
    }

    ~Shader() { Destroy(); }

    public bool IsDisposed => Fx == null;

    public void Dispose()
    {
        Destroy();
        GC.SuppressFinalize(this);
    }

    void Destroy()
    {
        FxParameters.Clear();
        Mem.Dispose(ref Fx);
    }

    public EffectParameter this[string name] =>
        FxParameters.TryGetValue(name, out EffectParameter p) ? p : null;

    public EffectTechnique CurrentTechnique => Fx?.CurrentTechnique;

    public class IncludeHandler
    {
        public string LocalDir { get; set; }
        public IncludeHandler(string rootDir)
        {
            LocalDir = rootDir;
        }
    }

    public static IncludeHandler CreateIncludeHandler(string pathToShader)
    {
        string rootDir = Path.GetDirectoryName(pathToShader);
        return new(rootDir);
    }

    /// <summary>
    /// Resolve a precompiled MGFX path for the current host.
    /// DesktopVK prefers Content/.../Vulkan/&lt;name&gt;.(mgfx|mgfxo) over DirectX siblings.
    /// </summary>
    public static string ResolveCompiledPath(string pathToShader)
    {
        // Resolve sibling .mgfx for a .fx request; pass-through for an explicit .mgfx/.mgfxo path.
        string compiledPath;
        if (pathToShader.EndsWith(".fx", StringComparison.OrdinalIgnoreCase))
            compiledPath = pathToShader.Substring(0, pathToShader.Length - 3) + ".mgfx";
        else
            compiledPath = pathToShader;

#if STARDIVE_DESKTOPVK
        string vulkanPath = InsertVulkanDirectory(compiledPath);
        if (File.Exists(vulkanPath))
            return vulkanPath;

        // Also accept .mgfxo under Vulkan/ when only that spelling was baked.
        if (vulkanPath.EndsWith(".mgfx", StringComparison.OrdinalIgnoreCase))
        {
            string vulkanMgfxo = vulkanPath + "o";
            if (File.Exists(vulkanMgfxo))
                return vulkanMgfxo;
        }
#endif
        if (File.Exists(compiledPath))
            return compiledPath;

        // Shader.FromFile historically looks for .mgfx; content pipeline often ships .mgfxo.
        if (compiledPath.EndsWith(".mgfx", StringComparison.OrdinalIgnoreCase))
        {
            string mgfxo = compiledPath + "o";
            if (File.Exists(mgfxo))
                return mgfxo;
        }

        return compiledPath;
    }

#if STARDIVE_DESKTOPVK
    static string InsertVulkanDirectory(string path)
    {
        // Content/Effects/Simple.mgfx -> Content/Effects/Vulkan/Simple.mgfx
        // Effects/Simple.mgfxo -> Effects/Vulkan/Simple.mgfxo
        string dir = Path.GetDirectoryName(path) ?? "";
        string file = Path.GetFileName(path);
        if (string.Equals(Path.GetFileName(dir), "Vulkan", StringComparison.OrdinalIgnoreCase))
            return path;
        return Path.Combine(dir, "Vulkan", file);
    }
#endif

    public static Shader FromFile(GraphicsDevice device, string pathToShader)
    {
        string mgfxPath = ResolveCompiledPath(pathToShader);
        if (!File.Exists(mgfxPath))
            throw new FileNotFoundException($"Shader.FromFile {pathToShader}: no precompiled MGFX at '{mgfxPath}'");
        byte[] bytes = File.ReadAllBytes(mgfxPath);
        return new Shader(new Effect(device, bytes));
    }

    public void Begin()
    {
        // MonoGame has no Effect.Begin; the EffectPass.Apply() call done by callers
        // (e.g. SpriteRenderer.ShaderBegin) handles all state binding.
    }

    public void End()
    {
        // MonoGame has no Effect.End either; pass-state is owned by EffectPass.Apply().
    }
}
