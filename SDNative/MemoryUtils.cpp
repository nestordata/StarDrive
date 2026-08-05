#include "util/apex_memmove.h"

#ifndef SPATIAL_C_API
#  if defined(_MSC_VER)
#    define SPATIAL_C_API extern "C" __declspec(dllexport)
#  else
#    define SPATIAL_C_API extern "C" __attribute__((visibility("default")))
#  endif
#endif
#ifndef SPATIAL_CC
#  if defined(_MSC_VER)
#    define SPATIAL_CC __stdcall
#  else
#    define SPATIAL_CC
#  endif
#endif

/**
 * To do an even faster block copy of C# Arrays, we have this fine little beast:
 */
SPATIAL_C_API void SPATIAL_CC MemCopy(char* dst, const char* src, int numBytes)
{
    // Around ~20% faster than platform memcpy :O
    apex::memcpy(dst, src, numBytes);
}