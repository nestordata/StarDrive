#pragma once
/**
 * Cross-platform shims for SDNative (Clang/GCC on macOS/Linux).
 * Force-included by CMake on non-MSVC. Windows SDNative.vcxproj is unchanged.
 */

#include <cstddef>
#include <cstdlib>
#include <cstring>

#if defined(_MSC_VER)

#  ifndef SD_EXPORT
#    define SD_EXPORT __declspec(dllexport)
#  endif
#  ifndef SD_CALL
#    define SD_CALL __stdcall
#  endif

#else // !_MSC_VER

#  ifndef SD_EXPORT
#    define SD_EXPORT __attribute__((visibility("default")))
#  endif
#  ifndef SD_CALL
#    define SD_CALL /* cdecl — matches Ship_Game.Platform.NativeLib.CallConv */
#  endif
#  ifndef __forceinline
#    define __forceinline inline __attribute__((always_inline))
#  endif

// MSVC aligned heap → POSIX aligned_alloc / free
inline void* _aligned_malloc(size_t size, size_t alignment)
{
    if (alignment < sizeof(void*))
        alignment = sizeof(void*);
    // aligned_alloc requires size to be a multiple of alignment
    size_t padded = (size + alignment - 1) / alignment * alignment;
    if (padded == 0)
        padded = alignment;
    return ::aligned_alloc(alignment, padded);
}

inline void _aligned_free(void* p)
{
    ::free(p);
}

inline void* _aligned_realloc(void* p, size_t newSize, size_t alignment)
{
    // No POSIX aligned realloc. Callers that grow by doubling (SlabArray)
    // only need the previous capacity preserved — copy newSize/2 bytes.
    void* n = _aligned_malloc(newSize, alignment);
    if (n && p)
    {
        size_t copyBytes = newSize / 2;
        if (copyBytes)
            std::memcpy(n, p, copyBytes);
        _aligned_free(p);
    }
    return n;
}

#endif // !_MSC_VER

#ifndef SPATIAL_API
#  define SPATIAL_API SD_EXPORT
#endif
#ifndef SPATIAL_C_API
#  define SPATIAL_C_API extern "C" SD_EXPORT
#endif
#ifndef SPATIAL_CC
#  define SPATIAL_CC SD_CALL
#endif
#ifndef DLLAPI
#  define DLLAPI(returnType) extern "C" SD_EXPORT returnType SD_CALL
#endif
#ifndef DLLEXPORT
#  define DLLEXPORT extern "C" SD_EXPORT
#endif
#ifndef STDCALL
#  define STDCALL(ret) DLLEXPORT ret SD_CALL
#endif

// C# UnmanagedType.LPWStr is always UTF-16. Windows wchar_t matches that;
// macOS/Linux wchar_t is UTF-32, so public SDNative APIs use sd_wchar instead.
#include <string>
#if defined(_WIN32)
using sd_wchar = wchar_t;
#else
using sd_wchar = char16_t;
#endif

inline std::string sd_utf16_to_utf8(const sd_wchar* wideStr)
{
    std::string out;
    if (!wideStr)
        return out;
    for (const sd_wchar* p = wideStr; *p; ++p)
    {
        unsigned c = static_cast<unsigned>(*p);
        if (c < 0x80u)
            out.push_back(static_cast<char>(c));
        else if (c < 0x800u)
        {
            out.push_back(static_cast<char>(0xC0u | (c >> 6)));
            out.push_back(static_cast<char>(0x80u | (c & 0x3Fu)));
        }
        else
        {
            out.push_back(static_cast<char>(0xE0u | (c >> 12)));
            out.push_back(static_cast<char>(0x80u | ((c >> 6) & 0x3Fu)));
            out.push_back(static_cast<char>(0x80u | (c & 0x3Fu)));
        }
    }
    return out;
}
