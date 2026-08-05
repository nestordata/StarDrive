#pragma once
#include <cstddef>

#ifndef SPATIAL_API
#  if defined(_MSC_VER)
#    define SPATIAL_API __declspec(dllexport)
#  else
#    define SPATIAL_API __attribute__((visibility("default")))
#  endif
#endif

#ifndef SPATIAL_C_API
#  if defined(_MSC_VER)
#    define SPATIAL_C_API extern "C" __declspec(dllexport)
#  else
#    define SPATIAL_C_API extern "C" __attribute__((visibility("default")))
#  endif
#endif

/// Calling convention of Spatial C-interface
/// stdcall on MSVC (C# default); cdecl elsewhere (NativeLib.CallConv)
#ifndef SPATIAL_CC
#  if defined(_MSC_VER)
#    define SPATIAL_CC __stdcall
#  else
#    define SPATIAL_CC
#  endif
#endif

//// @note Some strong hints that some functions are merely wrappers, so should be forced inline
#ifndef SPATIAL_FINLINE
#  ifdef _MSC_VER
#    define SPATIAL_FINLINE __forceinline
#  elif __APPLE__
#    define SPATIAL_FINLINE inline __attribute__((always_inline))
#  else
#    define SPATIAL_FINLINE __attribute__((always_inline))
#  endif
#endif

//// @note Some functions get inlined too aggressively, leading to some serious code bloat
////       Need to hint the compiler to take it easy ^_^'
#ifndef SPATIAL_NOINLINE
#  ifdef _MSC_VER
#    define SPATIAL_NOINLINE __declspec(noinline)
#  else
#    define SPATIAL_NOINLINE __attribute__((noinline))
#  endif
#endif

namespace spatial
{
    /// <summary>
    /// Size of a single linear allocator slab
    /// </summary>
    constexpr size_t AllocatorSlabSize = 256 * 1024;

    /// <summary>
    /// How many objects to store per quad tree cell before subdividing
    /// </summary>
    constexpr int QuadDefaultLeafSplitThreshold = 64;
    
    /// <summary>
    /// Ratio of search radius where we switch to Linear search
    /// because Quad search would traverse entire tree
    /// </summary>
    constexpr float QuadToLinearRatio = 0.75f;

    /**
     * Default capacity for a single grid cell before reallocating
     */
    constexpr int GridDefaultCellCapacity = 16;
}
