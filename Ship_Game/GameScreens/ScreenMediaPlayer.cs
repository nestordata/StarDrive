using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Microsoft.Xna.Framework.Media;
using Ship_Game.Audio;
using Ship_Game.Data;
using System;
using System.IO;
using SDGraphics;
using SDUtils;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
#if STARDIVE_DESKTOPVK
using Ship_Game.Platform.DesktopVk;
#endif

namespace Ship_Game.GameScreens
{
    /// <summary>
    /// GameScreen compatible media player which automatically
    /// pauses/resumes video if game screen goes out of focus
    /// and resumes normal game music after media stopped
    /// </summary>
    public sealed class ScreenMediaPlayer : IDisposable
    {
#if STARDIVE_DESKTOPVK
        readonly IVideoPlayback Player;
#else
        Video Video;
        readonly VideoPlayer Player;
#endif
        readonly GameContentManager Content;
        #pragma warning disable CA2213
        Texture2D Frame;
        #pragma warning restore CA2213
        public bool Active = true;
        public bool Visible = true;

        public Rectangle Rect;

        AudioHandle ExtraMusic = AudioHandle.DoNotPlay;

        public bool EnableInteraction = false;
        public bool IsHovered;
        public bool CaptureThumbnail;
        public bool MuteGameAudioWhilePlaying;
        bool GameAudioMuted;

        MediaState LastSeenPlayerState = MediaState.Stopped;

        public Action OnPlayStatusChange;

        public string Name { get; private set; } = "";
#if STARDIVE_DESKTOPVK
        public Vector2 Size => Player.IsOpen ? new Vector2(Player.Width, Player.Height) : Vector2.Zero;
#else
        public Vector2 Size => Video != null ? new Vector2(Video.Width, Video.Height) : Vector2.Zero;
#endif

        public bool ReadyToPlay => Frame != null || IsPlaying || IsPaused;
        public bool PlaybackFailed { get; private set; }
        public bool PlaybackSuccess { get; private set; }

        TaskResult BeginPlayTask;

        public bool IsDisposed { get; private set; }
        readonly bool WantLooping;

        public ScreenMediaPlayer(GameContentManager content, bool looping = true)
        {
            Content = content;
            WantLooping = looping;
#if STARDIVE_DESKTOPVK
            Player = new SdNativeVideoPlayer { IsLooped = looping, Volume = GlobalStats.MusicVolume };
#else
            Player = new VideoPlayer();
            Player.Volume = GlobalStats.MusicVolume;
            // MonoGame WindowsDX 3.8.1.303: IsLooped setter still unimplemented.
#endif
        }

        ~ScreenMediaPlayer() { Dispose(false); }

        void MuteGameAudioIfRequested()
        {
            if (MuteGameAudioWhilePlaying && !GameAudioMuted)
            {
                GameAudio.MuteMixerOutput();
                GameAudioMuted = true;
            }
        }

        void RestoreGameAudioIfMuted()
        {
            if (GameAudioMuted)
            {
                GameAudio.RestoreMixerOutput();
                GameAudioMuted = false;
            }
        }

        void Dispose(bool disposing)
        {
            IsDisposed = true;
            Active = false;
            Visible = false;
            OnPlayStatusChange = null;
            Frame = null;
            RestoreGameAudioIfMuted();

            if (ExtraMusic is { IsPlaying: true })
            {
                ExtraMusic.Stop();
                ExtraMusic = null;
            }

            // Wait for async Open/Play so it cannot resurrect a native handle after close.
            BeginPlayTask?.CancelAndWait(2000);
            Mem.Dispose(ref BeginPlayTask);

#if STARDIVE_DESKTOPVK
            if (Player is { IsDisposed: false })
            {
                if (SafePlayerState() != MediaState.Stopped)
                    Player.Stop();
                Player.Dispose();
            }
#else
            if (Video != null)
            {
                Video = null;
                if (!Player.IsDisposed)
                {
                    if (SafePlayerState() != MediaState.Stopped)
                        Player.Stop();
                    Player.Dispose();
                }
            }
#endif
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            if (GlobalStats.DebugAssetLoading) Log.Write(ConsoleColor.Magenta, $"Disposing ScreenMediaPlayer {Name}");
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        static string ResolveDesktopVkVideoPath(string videoPath)
        {
            // Prefer Content/Video/{name}.mp4 next to CWD (game/) or Content root.
            string name = videoPath;
            if (name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".wmv", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".xnb", StringComparison.OrdinalIgnoreCase))
                name = Path.GetFileNameWithoutExtension(name);

            string[] candidates =
            {
                Path.Combine("Content", "Video", name + ".mp4"),
                Path.Combine("Video", name + ".mp4"),
                Path.GetFullPath(Path.Combine("Content", "Video", name + ".mp4")),
            };
            foreach (string c in candidates)
            {
                if (File.Exists(c))
                    return Path.GetFullPath(c);
            }
            return null;
        }

        public void PlayVideo(string videoPath, bool looping = true, bool startPaused = false)
        {
            if (IsPlaying || IsDisposed)
                return;

            if (GlobalStats.VideoDisabled)
            {
                PlaybackFailed = true;
                return;
            }

            RestoreGameAudioIfMuted();

            try
            {
                Name = videoPath;
#if STARDIVE_DESKTOPVK
                string path = ResolveDesktopVkVideoPath(videoPath);
                if (path == null)
                {
                    PlaybackFailed = true;
                    Log.Warning($"PlayVideo failed: no mp4 for 'Video/{videoPath}'");
                    return;
                }

                Player.IsLooped = looping;
                if (Player.Volume.NotEqual(GlobalStats.MusicVolume))
                    Player.Volume = GlobalStats.MusicVolume;

                BeginPlayTask = Parallel.Run(() =>
                {
                    try
                    {
                        if (IsDisposed)
                            return;
                        if (!Player.Open(path))
                            throw new InvalidOperationException("SDVideoOpen failed");
                        if (IsDisposed)
                        {
                            Player.Stop();
                            return;
                        }

                        Rect = new Rectangle(0, 0, Player.Width, Player.Height);
                        Player.Play();
                        if (startPaused)
                        {
                            CaptureThumbnail = true;
                            Player.Pause();
                        }
                        else
                        {
                            MuteGameAudioIfRequested();
                        }
                        PlaybackSuccess = true;
                        OnPlayStatusChange?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"Player.Play failed: 'Video/{videoPath}' reason: {ex.Message}");
                        PlaybackFailed = true;
                    }
                    finally
                    {
                        BeginPlayTask = null;
                    }
                });
#else
                Video = ResourceManager.LoadVideo(Content, videoPath);
                Rect = new Rectangle(0, 0, Video.Width, Video.Height);

                if (Player.Volume.NotEqual(GlobalStats.MusicVolume))
                    Player.Volume = GlobalStats.MusicVolume;

                BeginPlayTask = Parallel.Run(() =>
                {
                    try
                    {
                        Player.Play(Video);
                        if (startPaused)
                        {
                            CaptureThumbnail = true;
                            Player.Pause();
                        }
                        else
                        {
                            MuteGameAudioIfRequested();
                        }
                        PlaybackSuccess = true;
                        OnPlayStatusChange?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"Player.Play failed: 'Video/{videoPath}' reason: {ex.Message}");
                        PlaybackFailed = true;
                    }
                    finally
                    {
                        BeginPlayTask = null;
                    }
                });
#endif
            }
            catch (Exception ex)
            {
                PlaybackFailed = true;
                Log.Warning($"PlayVideo failed: 'Video/{videoPath}' reason: {ex.Message}");
            }
        }

        public void PlayVideoAndMusic(Empire empire, bool warMusic)
        {
            if (IsDisposed)
                return;

            if (GlobalStats.VideoDisabled)
            {
                if (empire.data.MusicCue != null && ExtraMusic.IsStopped)
                {
                    ExtraMusic = GameAudio.PlayMusic(warMusic ? "CombatMusic" : empire.data.MusicCue);
                    GameAudio.SwitchToRacialMusic();
                }
                PlaybackFailed = true;
                return;
            }

            if (IsPlaying)
                return;

            PlayVideo(empire.data.Traits.VideoPath, WantLooping);

            if (empire.data.MusicCue != null && SafePlayerState() != MediaState.Playing)
            {
                ExtraMusic = GameAudio.PlayMusic(warMusic ? "CombatMusic" : empire.data.MusicCue);
                GameAudio.SwitchToRacialMusic();
            }
        }

        MediaState SafePlayerState()
        {
            if (GlobalStats.VideoDisabled || Player.IsDisposed || PlaybackFailed)
                return MediaState.Stopped;
            try
            {
                return Player.State;
            }
            catch (Exception ex)
            {
                Log.Warning($"VideoPlayer.State failed for '{Name}': {ex.Message}");
                PlaybackFailed = true;
                return MediaState.Stopped;
            }
        }

#if STARDIVE_DESKTOPVK
        public bool IsPlaying => BeginPlayTask != null || (Player.IsOpen && SafePlayerState() == MediaState.Playing);
        public bool IsPaused  => Player.IsOpen && SafePlayerState() == MediaState.Paused;
        public bool IsStopped => !Player.IsOpen || Player.IsDisposed || SafePlayerState() == MediaState.Stopped;
#else
        public bool IsPlaying => BeginPlayTask != null || (Video != null && SafePlayerState() == MediaState.Playing);
        public bool IsPaused  => Video != null && SafePlayerState() == MediaState.Paused;
        public bool IsStopped => Video == null || Player.IsDisposed || SafePlayerState() == MediaState.Stopped;
#endif

        public void Stop()
        {
            if (IsDisposed)
                return;

            Frame = null;
            RestoreGameAudioIfMuted();

            if (!IsStopped)
            {
                Player.Stop();
                OnPlayStatusChange?.Invoke();
            }

            if (ExtraMusic.IsPlaying)
            {
                ExtraMusic.Stop();
                GameAudio.SwitchBackToGenericMusic();
            }
        }

        public void Resume()
        {
            if (IsDisposed)
                return;

#if STARDIVE_DESKTOPVK
            if (Player.IsOpen && SafePlayerState() != MediaState.Playing)
            {
                try
                {
                    Player.Resume();
                    MuteGameAudioIfRequested();
                    OnPlayStatusChange?.Invoke();
                }
                catch (Exception ex)
                {
                    Log.Warning($"ScreenMediaPlayer.Resume failed for '{Name}': {ex.Message}");
                    PlaybackFailed = true;
                    RestoreGameAudioIfMuted();
                }
            }
#else
            if (Video != null && SafePlayerState() != MediaState.Playing)
            {
                try
                {
                    Player.Stop();
                    Player.Play(Video);
                    MuteGameAudioIfRequested();
                    OnPlayStatusChange?.Invoke();
                }
                catch (Exception ex)
                {
                    Log.Warning($"ScreenMediaPlayer.Resume Stop+Play failed for '{Name}': {ex.Message}");
                    PlaybackFailed = true;
                    RestoreGameAudioIfMuted();
                }
            }
#endif

            if (ExtraMusic.IsPaused)
            {
                ExtraMusic.Resume();
                GameAudio.PauseGenericMusic();
            }
        }

        public void Pause()
        {
            if (IsDisposed)
                return;

            if (IsPlaying)
            {
                Player.Pause();
                RestoreGameAudioIfMuted();
                OnPlayStatusChange?.Invoke();
            }

            if (ExtraMusic.IsPlaying)
            {
                ExtraMusic.Pause();
                GameAudio.SwitchBackToGenericMusic();
            }
        }

        public bool HandleInput(InputState input)
        {
            IsHovered = false;
            if (!Visible || IsDisposed)
                return false;

            if (EnableInteraction)
            {
                IsHovered = Rect.HitTest(input.CursorPosition);
                if (IsPlaying && (input.Escaped || input.RightMouseClick))
                {
                    GameAudio.EchoAffirmative();
                    Pause();
                    return true;
                }
                if (IsHovered && input.InGameSelect)
                {
                    if (!IsPlaying)
                    {
                        GameAudio.EchoAffirmative();
                        Resume();
                    }
                    return true;
                }
            }
            return false;
        }

        public void Update(GameScreen screen)
        {
            if (!PlaybackSuccess || IsDisposed || PlaybackFailed)
                return;

            MediaState currentState = SafePlayerState();
            try
            {
#if STARDIVE_DESKTOPVK
                bool hasVideo = Player.IsOpen;
                Player.PumpAudio();
#else
                bool hasVideo = Video != null;
#endif
                if (hasVideo && currentState != MediaState.Stopped)
                {
                    if (screen.IsActive && currentState == MediaState.Paused)
                    {
                        Player.Resume();
                        MuteGameAudioIfRequested();
                    }
                    else if (!screen.IsActive && currentState == MediaState.Playing)
                    {
                        Player.Pause();
                        RestoreGameAudioIfMuted();
                    }
                }
                else if (hasVideo
                         && LastSeenPlayerState == MediaState.Playing
                         && currentState == MediaState.Stopped)
                {
                    RestoreGameAudioIfMuted();
                }

                if (!ExtraMusic.IsStopped)
                {
                    if (screen.IsActive && ExtraMusic.IsPaused)
                        ExtraMusic.Resume();
                    else if (!screen.IsActive && ExtraMusic.IsPlaying)
                        ExtraMusic.Pause();
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"ScreenMediaPlayer.Update Pause/Resume failed for '{Name}': {ex.Message}");
                PlaybackFailed = true;
                RestoreGameAudioIfMuted();
            }
            finally
            {
                LastSeenPlayerState = currentState;
            }
        }

        public void Draw(SpriteBatch batch)
        {
            Draw(batch, Color.White);
        }

        public void Draw(SpriteBatch batch, Color color)
        {
            Draw(batch, Rect, color, 0f, SpriteEffects.None);
        }

        public void Draw(SpriteBatch batch, in Rectangle rect, Color color, float rotation, SpriteEffects effects)
        {
            if (!PlaybackSuccess || Player.IsDisposed || !Active || IsDisposed || PlaybackFailed)
                return;

            if (!Visible)
            {
                if (IsPlaying)
                    Stop();
                return;
            }

#if STARDIVE_DESKTOPVK
            if (Player.IsOpen && SafePlayerState() != MediaState.Stopped)
            {
                Player.PumpAudio();
                if (CaptureThumbnail || Player.PlayPosition.TotalMilliseconds > 0)
                {
                    try
                    {
                        var gd = batch.GraphicsDevice;
                        Frame = Player.GetTexture(gd) ?? Frame;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"ScreenMediaPlayer.Draw GetTexture failed for '{Name}': {ex.Message}");
                        PlaybackFailed = true;
                        return;
                    }
                }
            }
#else
            if (Video != null && SafePlayerState() != MediaState.Stopped)
            {
                if (CaptureThumbnail || Player.PlayPosition.TotalMilliseconds > 0)
                {
                    try
                    {
                        Frame = Player.GetTexture();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"ScreenMediaPlayer.Draw GetTexture failed for '{Name}': {ex.Message}");
                        PlaybackFailed = true;
                        return;
                    }
                }
            }
#endif

            if (Frame != null)
                batch.Draw(Frame, rect, null, color, rotation, Vector2.Zero, effects, 0.9f);

            if (EnableInteraction)
            {
                batch.DrawRectangle(rect, new Color(32, 30, 18));
                if (IsHovered && SafePlayerState() != MediaState.Playing)
                {
                    var playIcon = new Rectangle(rect.CenterX() - 64, rect.CenterY() - 64, 128, 128);
                    batch.Draw(ResourceManager.Texture("icon_play"), playIcon, new Color(255, 255, 255, 200).Premultiplied());
                }
            }
        }
    }
}
