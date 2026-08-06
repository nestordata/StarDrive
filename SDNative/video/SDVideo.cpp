#include "SDVideo.h"

#if !defined(SDNATIVE_ENABLE_FFMPEG) || SDNATIVE_ENABLE_FFMPEG == 0

DLLAPI(int) SDVideoIsSupported() { return 0; }
DLLAPI(SDVideo*) SDVideoOpen(const sd_wchar*) { return nullptr; }
DLLAPI(void) SDVideoClose(SDVideo*) {}
DLLAPI(void) SDVideoPlay(SDVideo*) {}
DLLAPI(void) SDVideoPause(SDVideo*) {}
DLLAPI(void) SDVideoStop(SDVideo*) {}
DLLAPI(SDVideoState) SDVideoGetState(SDVideo*) { return SDVideoState::Stopped; }
DLLAPI(int) SDVideoGetWidth(SDVideo*) { return 0; }
DLLAPI(int) SDVideoGetHeight(SDVideo*) { return 0; }
DLLAPI(double) SDVideoGetPositionSeconds(SDVideo*) { return 0; }
DLLAPI(double) SDVideoGetDurationSeconds(SDVideo*) { return 0; }
DLLAPI(void) SDVideoSetVolume(SDVideo*, float) {}
DLLAPI(void) SDVideoSetLooped(SDVideo*, int) {}
DLLAPI(int) SDVideoLockFrame(SDVideo*, const uint8_t**, int*, int*, int*) { return 0; }
DLLAPI(void) SDVideoUnlockFrame(SDVideo*) {}
DLLAPI(int) SDVideoReadAudio(SDVideo*, uint8_t*, int, int*, int*) { return 0; }

#else

extern "C" {
#include <libavcodec/avcodec.h>
#include <libavformat/avformat.h>
#include <libavutil/avutil.h>
#include <libavutil/imgutils.h>
#include <libavutil/opt.h>
#include <libswscale/swscale.h>
#include <libswresample/swresample.h>
}

#include <algorithm>
#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cstring>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

struct SDVideo
{
    std::string path;
    AVFormatContext* fmt = nullptr;
    AVCodecContext* vctx = nullptr;
    AVCodecContext* actx = nullptr;
    SwsContext* sws = nullptr;
    SwrContext* swr = nullptr;
    int vStream = -1;
    int aStream = -1;
    int width = 0;
    int height = 0;
    double durationSec = 0;
    int audioSampleRate = 44100;
    int audioChannels = 2;

    std::atomic<SDVideoState> state{SDVideoState::Stopped};
    std::atomic<bool> quit{false};
    std::atomic<bool> looped{false};
    std::atomic<bool> wantPlay{false};
    std::atomic<float> volume{1.f};
    std::atomic<double> positionSec{0};

    std::thread worker;
    std::mutex frameMtx;
    std::vector<uint8_t> frameRgba;
    int frameW = 0, frameH = 0, frameStride = 0;
    bool frameReady = false;
    bool frameLocked = false;

    std::mutex audioMtx;
    std::vector<uint8_t> audioRing;
    size_t audioRead = 0;
    size_t audioWrite = 0;

    std::mutex ctrlMtx;
    std::condition_variable ctrlCv;
    bool wantPause = false;
    bool wantStop = false;
    bool wantSeekStart = false;

    ~SDVideo()
    {
        quit = true;
        wantPlay = false;
        {
            std::lock_guard<std::mutex> lock(ctrlMtx);
            wantStop = true;
        }
        ctrlCv.notify_all();
        if (worker.joinable())
            worker.join();
        freeCodecs();
    }

    void freeCodecs()
    {
        if (sws) { sws_freeContext(sws); sws = nullptr; }
        if (swr) { swr_free(&swr); }
        if (vctx) { avcodec_free_context(&vctx); }
        if (actx) { avcodec_free_context(&actx); }
        if (fmt) { avformat_close_input(&fmt); }
    }

    void pushAudio(const uint8_t* data, int nbytes)
    {
        if (nbytes <= 0) return;
        std::lock_guard<std::mutex> lock(audioMtx);
        const size_t cap = 1 << 20; // 1 MB ring
        if (audioRing.size() < cap)
            audioRing.resize(cap);
        for (int i = 0; i < nbytes; ++i)
        {
            audioRing[audioWrite % cap] = data[i];
            audioWrite++;
            if (audioWrite - audioRead > cap)
                audioRead = audioWrite - cap;
        }
    }
};

static bool openCodecs(SDVideo* v)
{
    if (avformat_open_input(&v->fmt, v->path.c_str(), nullptr, nullptr) < 0)
        return false;
    if (avformat_find_stream_info(v->fmt, nullptr) < 0)
        return false;

    v->vStream = av_find_best_stream(v->fmt, AVMEDIA_TYPE_VIDEO, -1, -1, nullptr, 0);
    v->aStream = av_find_best_stream(v->fmt, AVMEDIA_TYPE_AUDIO, -1, -1, nullptr, 0);
    if (v->vStream < 0)
        return false;

    if (v->fmt->duration > 0)
        v->durationSec = (double)v->fmt->duration / AV_TIME_BASE;

    {
        AVStream* st = v->fmt->streams[v->vStream];
        const AVCodec* dec = avcodec_find_decoder(st->codecpar->codec_id);
        if (!dec) return false;
        v->vctx = avcodec_alloc_context3(dec);
        if (!v->vctx) return false;
        if (avcodec_parameters_to_context(v->vctx, st->codecpar) < 0) return false;
        if (avcodec_open2(v->vctx, dec, nullptr) < 0) return false;
        v->width = v->vctx->width;
        v->height = v->vctx->height;
    }

    if (v->aStream >= 0)
    {
        AVStream* st = v->fmt->streams[v->aStream];
        const AVCodec* dec = avcodec_find_decoder(st->codecpar->codec_id);
        if (dec)
        {
            v->actx = avcodec_alloc_context3(dec);
            if (v->actx && avcodec_parameters_to_context(v->actx, st->codecpar) >= 0
                && avcodec_open2(v->actx, dec, nullptr) >= 0)
            {
                // Always downmix/upmix to stereo s16 for DynamicSoundEffectInstance.
                v->audioSampleRate = v->actx->sample_rate > 0 ? v->actx->sample_rate : 44100;
                v->audioChannels = 2;
                AVChannelLayout outLayout;
                av_channel_layout_default(&outLayout, 2);
                if (swr_alloc_set_opts2(&v->swr,
                        &outLayout, AV_SAMPLE_FMT_S16, v->audioSampleRate,
                        &v->actx->ch_layout, v->actx->sample_fmt, v->actx->sample_rate,
                        0, nullptr) < 0
                    || swr_init(v->swr) < 0)
                {
                    swr_free(&v->swr);
                }
                av_channel_layout_uninit(&outLayout);
            }
            else
            {
                avcodec_free_context(&v->actx);
            }
        }
    }

    v->sws = sws_getContext(
        v->width, v->height, v->vctx->pix_fmt,
        v->width, v->height, AV_PIX_FMT_RGBA,
        SWS_BILINEAR, nullptr, nullptr, nullptr);
    return v->sws != nullptr;
}

static void seekStart(SDVideo* v)
{
    if (!v->fmt) return;
    av_seek_frame(v->fmt, -1, 0, AVSEEK_FLAG_BACKWARD);
    if (v->vctx) avcodec_flush_buffers(v->vctx);
    if (v->actx) avcodec_flush_buffers(v->actx);
    v->positionSec = 0;
    {
        std::lock_guard<std::mutex> lock(v->audioMtx);
        v->audioRead = v->audioWrite = 0;
    }
}

static void handleVideoFrame(SDVideo* v, AVFrame* frame)
{
    if (!v->sws) return;
    const int stride = v->width * 4;
    std::vector<uint8_t> rgba((size_t)stride * v->height);
    uint8_t* dst[4] = { rgba.data(), nullptr, nullptr, nullptr };
    int dstStride[4] = { stride, 0, 0, 0 };
    sws_scale(v->sws, frame->data, frame->linesize, 0, v->height, dst, dstStride);

    {
        std::lock_guard<std::mutex> lock(v->frameMtx);
        if (!v->frameLocked)
        {
            v->frameRgba.swap(rgba);
            v->frameW = v->width;
            v->frameH = v->height;
            v->frameStride = stride;
            v->frameReady = true;
        }
    }

    AVRational tb = v->fmt->streams[v->vStream]->time_base;
    if (frame->best_effort_timestamp != AV_NOPTS_VALUE)
        v->positionSec = frame->best_effort_timestamp * av_q2d(tb);
}

static void handleAudioFrame(SDVideo* v, AVFrame* frame)
{
    if (!v->swr || !v->actx) return;
    const int outCh = v->audioChannels;
    const int maxOut = frame->nb_samples * 4 + 256;
    std::vector<uint8_t> pcm((size_t)maxOut * outCh * 2);
    uint8_t* outPlanes[1] = { pcm.data() };
    int outSamples = swr_convert(v->swr, outPlanes, maxOut,
                                 (const uint8_t**)frame->extended_data, frame->nb_samples);
    if (outSamples <= 0) return;
    int nbytes = outSamples * outCh * (int)sizeof(int16_t);

    // Apply volume in-place
    float vol = v->volume.load();
    if (vol < 0.999f)
    {
        auto* s = reinterpret_cast<int16_t*>(pcm.data());
        int n = nbytes / 2;
        for (int i = 0; i < n; ++i)
        {
            float x = (float)s[i] * vol;
            s[i] = (int16_t)std::clamp(x, -32768.f, 32767.f);
        }
    }
    v->pushAudio(pcm.data(), nbytes);
}

static void decodePacket(SDVideo* v, AVPacket* pkt, AVFrame* frame)
{
    AVCodecContext* ctx = nullptr;
    bool isVideo = false;
    if (pkt->stream_index == v->vStream) { ctx = v->vctx; isVideo = true; }
    else if (pkt->stream_index == v->aStream) { ctx = v->actx; }
    else return;
    if (!ctx) return;

    if (avcodec_send_packet(ctx, pkt) < 0) return;
    while (avcodec_receive_frame(ctx, frame) == 0)
    {
        if (isVideo) handleVideoFrame(v, frame);
        else handleAudioFrame(v, frame);
        av_frame_unref(frame);
    }
}

static void workerMain(SDVideo* v)
{
    AVPacket* pkt = av_packet_alloc();
    AVFrame* frame = av_frame_alloc();
    if (!pkt || !frame)
    {
        if (pkt) av_packet_free(&pkt);
        if (frame) av_frame_free(&frame);
        return;
    }

    using clock = std::chrono::steady_clock;
    auto playOrigin = clock::now();
    double mediaOrigin = 0;

    while (!v->quit.load())
    {
        {
            std::unique_lock<std::mutex> lock(v->ctrlMtx);
            if (!v->wantPlay.load() && v->state != SDVideoState::Playing)
            {
                v->ctrlCv.wait_for(lock, std::chrono::milliseconds(50));
            }
            if (v->wantStop)
            {
                v->wantStop = false;
                v->wantPlay = false;
                v->wantPause = false;
                v->state = SDVideoState::Stopped;
                seekStart(v);
                continue;
            }
            if (v->wantPause)
            {
                v->wantPause = false;
                v->wantPlay = false;
                if (v->state == SDVideoState::Playing)
                    v->state = SDVideoState::Paused;
                continue;
            }
            if (v->wantSeekStart)
            {
                v->wantSeekStart = false;
                seekStart(v);
                playOrigin = clock::now();
                mediaOrigin = 0;
            }
            if (v->wantPlay.load())
            {
                if (v->state != SDVideoState::Playing)
                {
                    if (v->state == SDVideoState::Stopped)
                    {
                        seekStart(v);
                        mediaOrigin = 0;
                    }
                    else
                    {
                        mediaOrigin = v->positionSec.load();
                    }
                    playOrigin = clock::now();
                    v->state = SDVideoState::Playing;
                }
            }
        }

        if (v->state != SDVideoState::Playing)
            continue;

        // Pace video to wall clock
        double wall = std::chrono::duration<double>(clock::now() - playOrigin).count();
        double target = mediaOrigin + wall;
        if (v->positionSec.load() > target + 0.02)
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(5));
            continue;
        }

        int ret = av_read_frame(v->fmt, pkt);
        if (ret < 0)
        {
            if (v->looped.load())
            {
                seekStart(v);
                playOrigin = clock::now();
                mediaOrigin = 0;
                continue;
            }
            v->wantPlay = false;
            v->state = SDVideoState::Stopped;
            continue;
        }
        decodePacket(v, pkt, frame);
        av_packet_unref(pkt);
    }

    av_packet_free(&pkt);
    av_frame_free(&frame);
}

DLLAPI(int) SDVideoIsSupported() { return 1; }

DLLAPI(SDVideo*) SDVideoOpen(const sd_wchar* fileName)
{
    if (!fileName) return nullptr;
    auto* v = new (std::nothrow) SDVideo();
    if (!v) return nullptr;
    v->path = sd_utf16_to_utf8(fileName);
    if (!openCodecs(v))
    {
        delete v;
        return nullptr;
    }
    v->worker = std::thread(workerMain, v);
    return v;
}

DLLAPI(void) SDVideoClose(SDVideo* video)
{
    delete video;
}

DLLAPI(void) SDVideoPlay(SDVideo* video)
{
    if (!video) return;
    video->wantPlay = true;
    {
        std::lock_guard<std::mutex> lock(video->ctrlMtx);
        video->wantPause = false;
        video->wantStop = false;
    }
    video->ctrlCv.notify_all();
}

DLLAPI(void) SDVideoPause(SDVideo* video)
{
    if (!video) return;
    video->wantPlay = false;
    {
        std::lock_guard<std::mutex> lock(video->ctrlMtx);
        video->wantPause = true;
    }
    video->ctrlCv.notify_all();
}

DLLAPI(void) SDVideoStop(SDVideo* video)
{
    if (!video) return;
    video->wantPlay = false;
    {
        std::lock_guard<std::mutex> lock(video->ctrlMtx);
        video->wantStop = true;
        video->wantPause = false;
    }
    video->ctrlCv.notify_all();
}

DLLAPI(SDVideoState) SDVideoGetState(SDVideo* video)
{
    return video ? video->state.load() : SDVideoState::Stopped;
}

DLLAPI(int) SDVideoGetWidth(SDVideo* video) { return video ? video->width : 0; }
DLLAPI(int) SDVideoGetHeight(SDVideo* video) { return video ? video->height : 0; }
DLLAPI(double) SDVideoGetPositionSeconds(SDVideo* video) { return video ? video->positionSec.load() : 0; }
DLLAPI(double) SDVideoGetDurationSeconds(SDVideo* video) { return video ? video->durationSec : 0; }

DLLAPI(void) SDVideoSetVolume(SDVideo* video, float volume01)
{
    if (!video) return;
    video->volume = std::clamp(volume01, 0.f, 1.f);
}

DLLAPI(void) SDVideoSetLooped(SDVideo* video, int looped)
{
    if (!video) return;
    video->looped = looped != 0;
}

DLLAPI(int) SDVideoLockFrame(SDVideo* video, const uint8_t** outRgba, int* outWidth, int* outHeight, int* outStride)
{
    if (!video || !outRgba) return 0;
    std::lock_guard<std::mutex> lock(video->frameMtx);
    if (!video->frameReady || video->frameRgba.empty()) return 0;
    video->frameLocked = true;
    *outRgba = video->frameRgba.data();
    if (outWidth) *outWidth = video->frameW;
    if (outHeight) *outHeight = video->frameH;
    if (outStride) *outStride = video->frameStride;
    return 1;
}

DLLAPI(void) SDVideoUnlockFrame(SDVideo* video)
{
    if (!video) return;
    std::lock_guard<std::mutex> lock(video->frameMtx);
    video->frameLocked = false;
}

DLLAPI(int) SDVideoReadAudio(SDVideo* video, uint8_t* outPcm, int maxBytes, int* outSampleRate, int* outChannels)
{
    if (!video || !outPcm || maxBytes <= 0) return 0;
    if (outSampleRate) *outSampleRate = video->audioSampleRate;
    if (outChannels) *outChannels = video->audioChannels;
    std::lock_guard<std::mutex> lock(video->audioMtx);
    if (video->audioRing.empty() || video->audioWrite <= video->audioRead)
        return 0;
    const size_t cap = video->audioRing.size();
    size_t available = video->audioWrite - video->audioRead;
    int n = (int)std::min(available, (size_t)maxBytes);
    // Keep stereo s16 frames aligned
    n -= n % (video->audioChannels * 2);
    for (int i = 0; i < n; ++i)
        outPcm[i] = video->audioRing[(video->audioRead + (size_t)i) % cap];
    video->audioRead += (size_t)n;
    return n;
}

#endif // SDNATIVE_ENABLE_FFMPEG
