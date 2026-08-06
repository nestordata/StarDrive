using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.Data;
using Ship_Game.Platform;
using SynapseGaming.LightingSystem.Core;
using Vector2 = SDGraphics.Vector2;
#if STARDIVE_WINDOWSDX
using System.Drawing;
using System.Windows.Forms;
#endif

namespace Ship_Game
{
    public class GameBase : Game
    {
        #pragma warning disable CA2213 // managed by Game
        public GraphicsDeviceManager Graphics;
        #pragma warning restore CA2213
        LightingSystemPreferences Preferences;
        public static ScreenManager ScreenManager;
        public ScreenManager Manager => ScreenManager;

        // This is equivalent to PresentationParameters.BackBufferWidth
        public static int ScreenWidth  { get; protected set; }
        public static int ScreenHeight { get; protected set; }
        public static Viewport Viewport;
        public static Vector2 ScreenSize   { get; protected set; }
        public static Vector2 ScreenCenter { get; protected set; }
        public static int MainThreadId { get; protected set; }

        public static GameBase Base;
        public new GameContentManager Content { get; }
        public static GameContentManager GameContent => Base?.Content;

        public int FrameId { get; protected set; }
        public UpdateTimes Elapsed { get; protected set; }

        // DesktopVK/MoltenVK deadlocks if Texture2D.SetData/GetData runs off the
        // main thread while Present is in flight. Background loaders enqueue GPU
        // work here; Update pumps it before screen updates each frame.
        readonly ConcurrentQueue<Action> PendingMainThreadActions = new();

        /// <summary>
        /// Run <paramref name="action"/> on the game/UI thread. If already on that
        /// thread, runs inline; otherwise blocks until the next <see cref="PumpMainThreadActions"/>.
        /// </summary>
        public void InvokeOnMainThread(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (Thread.CurrentThread.ManagedThreadId == MainThreadId)
            {
                action();
                return;
            }

            Exception error = null;
            using var done = new ManualResetEventSlim(false);
            PendingMainThreadActions.Enqueue(() =>
            {
                try { action(); }
                catch (Exception e) { error = e; }
                finally { done.Set(); }
            });
            done.Wait();
            if (error != null)
                throw new Exception("InvokeOnMainThread failed", error);
        }

        public void PumpMainThreadActions()
        {
            while (PendingMainThreadActions.TryDequeue(out Action action))
                action();
        }

        /// <summary>
        /// Total elapsed Game time while the Game window has been active
        /// </summary>
        public float TotalElapsed { get; protected set; }

#if STARDIVE_WINDOWSDX
        public Form Form => (Form)Control.FromHandle(Window.Handle);
#endif

        /// <summary>
        /// TRUE if GraphicsDevice is not null or disposed
        /// </summary>
        public bool IsDeviceGood => GraphicsDevice is { IsDisposed: false, GraphicsDeviceStatus: GraphicsDeviceStatus.Normal };

        public GameBase()
        {
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
            Base = this;

            string contentDir = Path.Combine(Directory.GetCurrentDirectory(), "Content");
            base.Content = Content = new GameContentManager(Services, "Game", contentDir);

            Graphics = new GraphicsDeviceManager(this)
            {
                // Depth16 is fine on WindowsDX; DesktopVK/MoltenVK needs a stencil-capable format.
#if STARDIVE_DESKTOPVK
                PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
#else
                PreferredDepthStencilFormat = DepthFormat.Depth16,
#endif
            };
            Graphics.PreferMultiSampling = true;
            Graphics.GraphicsProfile = GraphicsProfile.HiDef;
#if STARDIVE_DESKTOPVK
            // Do NOT ApplyChanges() here. On DesktopVK, that creates the Vulkan device via
            // SDL_Vulkan_GetInstanceExtensions before Game.Run() has created an SDL window,
            // which null-derefs inside libmgruntime (MGG_GraphicsSystem_Create).
            // MonoGame creates the device on Run() once the window exists.
            IsMouseVisible = true;
#else
            Graphics.ApplyChanges();
#endif
        }

        void UpdateRendererPreferences(ref GraphicsSettings settings)
        {
            var p = new LightingSystemPreferences
            {
                MaxAnisotropy   = settings.MaxAnisotropy,
                ShadowQuality   = GlobalStats.GetShadowQuality(settings.ShadowDetail),
                ShadowDetail    = (DetailPreference) settings.ShadowDetail,
                EffectDetail    = (DetailPreference) settings.EffectDetail,
                TextureQuality  = (DetailPreference) settings.TextureQuality,
                TextureSampling = (SamplingPreference) settings.TextureSampling,
                PostProcessingDetail = DetailPreference.High,
            };

            if (Preferences != null && Preferences.Equals(p))
                return; // nothing changed.

            if (StarDriveGame.Instance != null)
            {
                Log.Write(ConsoleColor.Magenta, "Apply 3D Graphics Preferences:");
                Log.Write(ConsoleColor.Magenta, $"  Resolution:      {settings.Width}x{settings.Height} Fullscreen:{Graphics.IsFullScreen}");
                Log.Write(ConsoleColor.Magenta, $"  ShadowQuality:   {p.ShadowQuality}");
                Log.Write(ConsoleColor.Magenta, $"  ShadowDetail:    {p.ShadowDetail}");
                Log.Write(ConsoleColor.Magenta, $"  EffectDetail:    {p.EffectDetail}");
                Log.Write(ConsoleColor.Magenta, $"  TextureQuality:  {p.TextureQuality}");
                Log.Write(ConsoleColor.Magenta, $"  TextureSampling: {p.TextureSampling}");
                Log.Write(ConsoleColor.Magenta, $"  MaxAnisotropy:   {p.MaxAnisotropy}");
            }

            Preferences = p;
            ScreenManager?.UpdatePreferences(p);
        }

        bool ApplySettings(ref GraphicsSettings settings)
        {
            GraphicsDevice before = Graphics.GraphicsDevice;
            Graphics.ApplyChanges();
            bool deviceChanged = before != Graphics.GraphicsDevice;

            PresentationParameters p = GraphicsDevice.PresentationParameters;
            ScreenWidth  = p.BackBufferWidth;
            ScreenHeight = p.BackBufferHeight;
            ScreenSize   = new Vector2(ScreenWidth, ScreenHeight);
            ScreenCenter = ScreenSize * 0.5f;
            Viewport     = GraphicsDevice.Viewport;

            UpdateRendererPreferences(ref settings);
            ScreenManager?.UpdateViewports();
            return deviceChanged;
        }

        // Last graphics settings successfully applied via ApplyGraphics.
        // Captured so OnActivated can re-apply them when the WindowsDX
        // OnClientSizeChanged handler shrinks the backbuffer during an
        // alt-tab minimize from exclusive fullscreen.
        GraphicsSettings LastAppliedSettings;
        bool Restoring; // re-entrancy guard for OnActivated → ApplyGraphics → ApplyChanges

        // @return TRUE if graphics device changed
        public bool ApplyGraphics(GraphicsSettings settings)
        {
            DisplayMode currentMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

            // check if resolution from graphics settings is ok:
            if (currentMode.Width < settings.Width || currentMode.Height < settings.Height)
            {
                settings.Width  = currentMode.Width;
                settings.Height = currentMode.Height;
            }

            if (settings.Width <= 0 || settings.Height <= 0)
            {
                settings.Width  = 800;
                settings.Height = 600;
            }
            if (Debugger.IsAttached && settings.Mode == WindowMode.Fullscreen)
                settings.Mode = WindowMode.Borderless;

            PlatformServices.WindowChrome.ApplyWindowMode(Window, settings.Mode, settings.Width, settings.Height);

            Graphics.PreferredBackBufferWidth = settings.Width;
            Graphics.PreferredBackBufferHeight = settings.Height;
            Graphics.SynchronizeWithVerticalRetrace = settings.VSync;

            if (settings.Mode != WindowMode.Fullscreen && Graphics.IsFullScreen)
            {
                Graphics.ToggleFullScreen(); // Exiting fullscreen — always safe
            }
            else if (settings.Mode == WindowMode.Fullscreen && !Graphics.IsFullScreen)
            {
                // Entering fullscreen can fail with DXGI_ERROR_NOT_CURRENTLY_AVAILABLE
                // on WindowsDX; DesktopVK uses the same retry helper (HRESULT may differ).
                if (!TryEnterFullScreen(maxAttempts: 2, retryDelayMs: 500))
                {
                    settings.Mode = WindowMode.Borderless;
                }
            }

            if (settings.Mode != WindowMode.Fullscreen)
                PlatformServices.WindowChrome.CenterWindow(Window, settings.Width, settings.Height);

            bool deviceChanged = ApplySettings(ref settings);

            PresentationParameters pp = GraphicsDevice.PresentationParameters;
            Log.Write(ConsoleColor.Cyan,
                $"ApplyGraphics: backbuffer={pp.BackBufferWidth}x{pp.BackBufferHeight} mode={settings.Mode}");

            LastAppliedSettings = settings;
            return deviceChanged;
        }

        // After an alt-tab cycle out of exclusive fullscreen, MonoGame's
        // WindowsDX OnClientSizeChanged handler auto-calls Graphics.ApplyChanges
        // with the form's transient (minimized/restored) ClientSize, shrinking
        // the backbuffer. On alt-tab back the backbuffer stays small, the form
        // is again at fullscreen extents but only the top-left ClientSize-sized
        // region renders. Detect the drift here and re-apply the last user
        // settings to restore the proper backbuffer.
        protected override void OnActivated(object sender, EventArgs args)
        {
            base.OnActivated(sender, args);

            if (Restoring) return;
            if (LastAppliedSettings == null || Graphics == null || GraphicsDevice == null)
                return;
            if (LastAppliedSettings.Mode != WindowMode.Fullscreen)
                return;

            PresentationParameters p = GraphicsDevice.PresentationParameters;
            if (p.BackBufferWidth == LastAppliedSettings.Width
                && p.BackBufferHeight == LastAppliedSettings.Height)
                return;

            Log.Warning($"Alt-tab restore: backbuffer drifted to {p.BackBufferWidth}x{p.BackBufferHeight}, restoring to {LastAppliedSettings.Width}x{LastAppliedSettings.Height}");
            Restoring = true;
            try { ApplyGraphics(LastAppliedSettings); }
            finally { Restoring = false; }
        }

        // DXGI_ERROR_NOT_CURRENTLY_AVAILABLE — SetFullscreenState rejected the
        // transition. Catching by HRESULT avoids a dependency on SharpDX's
        // exception type here in GameBase; any wrapper that surfaces this code
        // gets handled the same way.
        const int DXGI_ERROR_NOT_CURRENTLY_AVAILABLE = unchecked((int)0x887A0022);

        // Try to enter exclusive fullscreen, retrying once on DXGI transient
        // failures. Returns false after exhausting attempts so the caller can
        // fall back to Borderless. After a failed ApplyChanges the
        // GraphicsDeviceManager's IsFullScreen flag may be left flipped to true
        // (the assignment happens before the underlying SetFullscreenState
        // throws), so each retry resets it explicitly rather than calling
        // ToggleFullScreen — which would invert the desired direction on the
        // second pass.
        bool TryEnterFullScreen(int maxAttempts, int retryDelayMs)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    Graphics.IsFullScreen = true;
                    Graphics.ApplyChanges();
                    return true;
                }
                catch (Exception ex) when (ex.HResult == DXGI_ERROR_NOT_CURRENTLY_AVAILABLE)
                {
                    // Reset the flag so a stale "true" doesn't leak into the
                    // Borderless fallback path or the next retry.
                    Graphics.IsFullScreen = false;
                    if (attempt < maxAttempts)
                    {
                        Log.Warning($"DXGI rejected fullscreen (attempt {attempt}/{maxAttempts}); retrying in {retryDelayMs}ms: {ex.Message}");
                        Thread.Sleep(retryDelayMs);
                    }
                    else
                    {
                        Log.Warning($"DXGI rejected fullscreen after {maxAttempts} attempts; falling back to Borderless: {ex.Message}");
                    }
                }
            }
            return false;
        }

        public void InitializeAudio()
        {
            GameAudio.Initialize(null, "Audio/AudioConfig.yaml");
        }

        protected void UpdateGame(GameTime gameTime)
        {
            if (Log.IsTerminating) // game is crashing, don't update anymore
            {
                Thread.Sleep(15);
                return;
            }

            try
            {
                ++FrameId;
                TotalElapsed = (float)gameTime.TotalGameTime.TotalSeconds;
                float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
                Elapsed = new UpdateTimes(deltaTime, TotalElapsed);

                // Drain GPU uploads from background loaders before any screen work.
                PumpMainThreadActions();

                if (IsDeviceGood) // only Update if device is OK
                {
                    // 1. Handle Input and 2. Update for each game screen
                    ScreenManager.Update(Elapsed);
                }

                base.Update(gameTime); // MonoGame Update
            }
            catch (Exception ex)
            {
                Log.ErrorDialog(ex, "UpdateGame() failed", Program.SCREEN_UPDATE_FAILURE);
            }
        }

        protected override void Dispose(bool disposing)
        {
            GameAudio.Destroy();
            if (ScreenManager != null)
                ResourceManager.UnloadAllData(ScreenManager);
            Mem.Dispose(ref ScreenManager);

            base.Dispose(disposing); // disposes Graphics
        }
    }
}