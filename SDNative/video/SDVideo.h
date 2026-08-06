#pragma once
/**
 * Cross-platform H.264/AAC video decoder via FFmpeg (LGPL).
 * Used by DesktopVK ScreenMediaPlayer; Windows keeps Media Foundation.
 */
#include "portability.h"
#include <cstdint>

enum class SDVideoState : int32_t
{
    Stopped = 0,
    Playing = 1,
    Paused  = 2,
};

struct SDVideo; // opaque

DLLAPI(int) SDVideoIsSupported(); // 1 if FFmpeg linked

DLLAPI(SDVideo*) SDVideoOpen(const sd_wchar* fileName);
DLLAPI(void)     SDVideoClose(SDVideo* video);

DLLAPI(void) SDVideoPlay(SDVideo* video);
DLLAPI(void) SDVideoPause(SDVideo* video);
DLLAPI(void) SDVideoStop(SDVideo* video);

DLLAPI(SDVideoState) SDVideoGetState(SDVideo* video);
DLLAPI(int)          SDVideoGetWidth(SDVideo* video);
DLLAPI(int)          SDVideoGetHeight(SDVideo* video);
DLLAPI(double)       SDVideoGetPositionSeconds(SDVideo* video);
DLLAPI(double)       SDVideoGetDurationSeconds(SDVideo* video);

DLLAPI(void) SDVideoSetVolume(SDVideo* video, float volume01);
DLLAPI(void) SDVideoSetLooped(SDVideo* video, int looped);

// Lock latest RGBA8 frame (width*height*4). Returns 1 if a frame is available.
// Call Unlock when done reading. Only one lock at a time.
DLLAPI(int) SDVideoLockFrame(SDVideo* video, const uint8_t** outRgba, int* outWidth, int* outHeight, int* outStride);
DLLAPI(void) SDVideoUnlockFrame(SDVideo* video);

// Read up to maxBytes of interleaved PCM s16le into outPcm. Returns bytes written.
DLLAPI(int) SDVideoReadAudio(SDVideo* video, uint8_t* outPcm, int maxBytes, int* outSampleRate, int* outChannels);
