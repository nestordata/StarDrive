using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.Data.Texture;
#if STARDIVE_WINDOWSDX
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
#endif

namespace Ship_Game.GameScreens
{
    public class GameCursor
    {
#if STARDIVE_WINDOWSDX
        public Cursor OSCursor;
#endif
        public Texture2D SoftwareCursor;
        public Vector2 HotSpot;
    }

    public static class GameCursors
    {
        public static GameCursor DefaultOSCursor;

        public static GameCursor Regular;
        public static GameCursor RegularNav;

        public static GameCursor Cinematic;

        public static GameCursor Aggressive;
        public static GameCursor AggressiveNav;

        public static GameCursor StandGround;
        public static GameCursor StandGroundNav;

        static GameCursor CurrentCursor;
#if STARDIVE_WINDOWSDX
        static Cursor CurrentOSCursor;
        static Form TargetForm;
#endif

        public static void Initialize(GameBase game, bool software)
        {
#if STARDIVE_DESKTOPVK
            // DesktopVK: always use software cursors (no WinForms / user32 cursor path).
            software = true;
#endif
            DefaultOSCursor = LoadCursor(game, software: false, "Cursors/Regular.png");
            if (DefaultOSCursor == null)
                throw new NullReferenceException("GameCursors.Initialize: Default OS Cursor cannot be null! [Cursors/Cursor.png]");

            Regular    = LoadCursor(game, software, "Cursors/Regular.png");
            RegularNav = LoadCursor(game, software, "Cursors/RegularNav.png");
            Cinematic  = LoadCursor(game, software, "Cursors/Cinematic.png", 0.5f, 0.5f);

            Aggressive    = LoadCursor(game, software, "Cursors/Aggressive.png");
            AggressiveNav = LoadCursor(game, software, "Cursors/AggressiveNav.png");

            StandGround    = LoadCursor(game, software, "Cursors/StandGround.png");
            StandGroundNav = LoadCursor(game, software, "Cursors/StandGroundNav.png");

#if STARDIVE_WINDOWSDX
            TargetForm = game.Form;
#endif
            CurrentCursor = Regular;
            game.IsMouseVisible = !software;
        }

        public static void SetCurrentCursor(GameCursor cursor)
        {
            CurrentCursor = cursor;
        }

        public static void Draw(GameBase game, SpriteBatch batch, Vector2 cursorScreenPos, bool software)
        {
            if (DefaultOSCursor == null)
                return; // unit tests don't load cursors

#if STARDIVE_DESKTOPVK
            software = true;
#endif

            if (software && CurrentCursor.SoftwareCursor?.IsDisposed == false)
            {
                game.IsMouseVisible = false;
                batch.SafeBegin();
                batch.Draw(CurrentCursor.SoftwareCursor, cursorScreenPos, null, Color.White, 0f,
                           CurrentCursor.HotSpot, 1f, SpriteEffects.None, 1f);
                batch.SafeEnd();
            }
            else
            {
                game.IsMouseVisible = true;
#if STARDIVE_WINDOWSDX
                var osCursor = CurrentCursor.OSCursor ?? DefaultOSCursor.OSCursor;
                if (CurrentOSCursor != osCursor)
                {
                    CurrentOSCursor = osCursor;
                    TargetForm.Cursor = osCursor;
                }
#endif
            }
        }

        static GameCursor LoadCursor(GameBase game, bool software, string fileName, float hotSpotX = 0f, float hotSpotY = 0f)
        {
            FileInfo file = ResourceManager.GetModOrVanillaFile(fileName);
            if (file == null)
            {
                Log.Error($"GameCursors.LoadCursor failed: {fileName} not found!");
                return Regular;
            }

            var wrappedCursor = new GameCursor();
#if STARDIVE_DESKTOPVK
            software = true;
#endif
            if (software)
            {
                Texture2D texture = ImageUtils.LoadPng(game.GraphicsDevice, file.FullName, premultiplyAlpha: true);
                texture.Name = file.FullName;
                wrappedCursor.SoftwareCursor = texture;
                wrappedCursor.HotSpot = new Vector2(hotSpotX * texture.Width, hotSpotY * texture.Height);
            }
#if STARDIVE_WINDOWSDX
            else
            {
                Bitmap bitmap;
                try
                {
                    bitmap = new Bitmap(file.FullName, useIcm: true);
                }
                catch
                {
                    try
                    {
                        bitmap = new Bitmap(file.FullName, useIcm: false);
                    }
                    catch
                    {
                        return null;
                    }
                }
                int hotX = (int)(bitmap.Width * hotSpotX);
                int hotY = (int)(bitmap.Height * hotSpotY);
                var cursor = CreateCursorNoResize(bitmap, hotX, hotY);
                wrappedCursor.OSCursor = cursor;
                wrappedCursor.HotSpot = new Vector2(hotX, hotY);
            }
#endif
            return wrappedCursor;
        }

#if STARDIVE_WINDOWSDX
        public struct IconInfo
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetIconInfo(IntPtr hIcon, ref IconInfo pIconInfo);

        [DllImport("user32.dll")]
        static extern IntPtr CreateIconIndirect(ref IconInfo icon);

        public static Cursor CreateCursorNoResize(Bitmap bmp, int xHotSpot, int yHotSpot)
        {
            IntPtr ptr = bmp.GetHicon();
            IconInfo tmp = new IconInfo();
            GetIconInfo(ptr, ref tmp);
            tmp.xHotspot = xHotSpot;
            tmp.yHotspot = yHotSpot;
            tmp.fIcon = false;
            ptr = CreateIconIndirect(ref tmp);
            return new Cursor(ptr);
        }
#endif
    }
}
