using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDUtils;
using Ship_Game.Data.Texture;

namespace Ship_Game.SpriteSystem
{
    public partial class TextureAtlas
    {
        static TextureInfo[] CreateTextureInfos(AtlasPath path, FileInfo[] textureFiles)
        {
            var textures = new TextureInfo[textureFiles.Length];

            bool noPackAll = ResourceManager.AtlasExcludeFolder.Contains(path.OriginalName);
            bool losslessAlpha = ResourceManager.AtlasLosslessAlphaFolders.Contains(path.OriginalName);
            HashSet<string> ignore = ResourceManager.AtlasExcludeTextures; // HACK

            for (int i = 0; i < textureFiles.Length; ++i)
            {
                FileInfo info = textureFiles[i];
                string texName = info.NameNoExt();
                string ext = info.Extension.Substring(1);
                bool noPack = noPackAll || ignore.Contains(texName);

                // Decode png/dds on the CPU only. Uploading via Texture2D.SetData from
                // the background load thread deadlocks MoltenVK against Present.
                if (TryLoadColors(info, ext, out Color[] colors, out int width, out int height))
                {
                    textures[i] = new TextureInfo
                    {
                        Name          = texName,
                        Type          = ext,
                        Width         = width,
                        Height        = height,
                        Colors        = colors,
                        NoPack        = noPack,
                        LosslessAlpha = losslessAlpha,
                    };
                    continue;
                }

                Texture2D tex = ResourceManager.RootContent.LoadUncachedTexture(info, ext);
                textures[i] = new TextureInfo
                {
                    Name           = texName,
                    Type           = ext,
                    Width          = tex.Width,
                    Height         = tex.Height,
                    Texture        = tex,
                    NoPack         = noPack,
                    LosslessAlpha  = losslessAlpha,
                };
            }
            return textures;
        }

        static bool TryLoadColors(FileInfo info, string ext, out Color[] colors, out int width, out int height)
        {
            colors = null;
            width = height = 0;
            try
            {
                if (ext.Equals("png", StringComparison.OrdinalIgnoreCase))
                {
                    colors = ImageUtils.LoadPngColors(info.FullName, out width, out height);
                    return colors != null;
                }
                if (ext.Equals("dds", StringComparison.OrdinalIgnoreCase))
                {
                    colors = ImageUtils.LoadDdsColors(info.FullName, out width, out height);
                    return colors != null;
                }
            }
            catch (Exception e)
            {
#if STARDIVE_DESKTOPVK
                // Off-thread GPU upload deadlocks MoltenVK; do not fall back.
                throw new Exception($"CreateTextureInfos CPU load failed: {info.FullName}", e);
#else
                Log.Warning($"CreateTextureInfos CPU load failed {info.FullName}: {e.Message}; falling back to GPU load");
#endif
            }
            return false;
        }

        static FileInfo[] GatherUniqueTextures(string folder)
        {
            FileInfo[] textureFiles = ResourceManager.GatherTextureFiles(folder, recursive: false);
            var uniqueTextures = new Map<string, FileInfo>();
            foreach (FileInfo info in textureFiles)
            {
                string texName = info.NameNoExt();
                if (uniqueTextures.TryGetValue(texName, out FileInfo existing))
                {
                    if (existing.Extension == "xnb") // only replace if old was xnb
                        uniqueTextures[texName] = info;
                }
                else uniqueTextures.Add(texName, info);
            }
            return uniqueTextures.Values.ToArr();
        }

        static ulong CreateHash(FileInfo[] textures)
        {
            // @note Had to roll back to a custom Fnv1AHash over text,
            //       since typical int hash-combine gave bad results.
            var ms = new MemoryStream(4096);
            var bw = new BinaryWriter(ms);
            bw.Write(textures.Length);
            bw.Write(Version);
            foreach (FileInfo info in textures)
            {
                bw.Write(info.Name);
                bw.Write(info.Length);
                bw.Write(info.LastWriteTimeUtc.Ticks);
            }
            return Fnv1AHash(ms.ToArray());
        }

        static ulong Fnv1AHash(byte[] bytes)
        {
            ulong hash = 0xcbf29ce484222325;
            foreach (byte b in bytes)
            {
                hash = hash ^ b;
                hash = hash * 0x100000001b3;
            }
            return hash;
        }
    }
}
