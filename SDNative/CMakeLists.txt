cmake_minimum_required(VERSION 3.20)
project(SDNative LANGUAGES CXX)
if(APPLE)
  enable_language(OBJCXX)
endif()

# SDNative CMake for Apple Silicon (DesktopVK). Windows continues to use SDNative.vcxproj.
# FBX: when SDNATIVE_ENABLE_FBX=ON and the platform runtime is present, NanoMesh uses the
# same Mesh_Fbx.cpp path as Windows (FbxToOpenGL, materials, skinning).

option(SDNATIVE_ENABLE_FBX "Link Autodesk FBX SDK" ON)
option(SDNATIVE_ENABLE_FFMPEG "Link FFmpeg for DesktopVK video (SDVideo)" ON)
option(SDNATIVE_ENABLE_MESH "Build NanoMesh / SdMesh (requires NanoMesh + ReCpp submodules)" ON)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

set(SDNATIVE_SOURCES
  MemoryUtils.cpp
  ShipDataSerializer.cpp
  spatial/Collision.cpp
  spatial/ObjectCollection.cpp
  spatial/Search.cpp
  spatial/SlabAllocator.cpp
  spatial/Spatial.cpp
  spatial/SpatialDebug.cpp
  spatial/grid/Grid.cpp
  spatial/grid/GridCellView.cpp
  spatial/gridL2/GridL2.cpp
  spatial/qtree/Qtree.cpp
  util/apex_memmove.cpp
  util/byte_buffer.cpp
  util/image_utils.cpp
  util/math_utils.cpp
  util/os_utils.cpp
  video/SDVideo.cpp
  3rdparty/lodepng/lodepng.cpp
  3rdparty/soil2/image_DXT.cpp
  3rdparty/soil2/image_helper.cpp
)

set(HAVE_NANOMESH FALSE)
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/NanoMesh/src/Mesh.cpp"
   AND EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/ReCpp/src/rpp/strview.cpp")
  set(HAVE_NANOMESH TRUE)
endif()

if(SDNATIVE_ENABLE_MESH AND HAVE_NANOMESH)
  list(APPEND SDNATIVE_SOURCES
    SdMesh/SdAnimation.cpp
    SdMesh/SdMaterial.cpp
    SdMesh/SdMesh.cpp
    SdMesh/SdMeshGroup.cpp
    NanoMesh/src/Mesh.cpp
    NanoMesh/src/MeshLoader.cpp
    NanoMesh/src/Mesh_Obj.cpp
    NanoMesh/src/Mesh_Txt.cpp
    ReCpp/src/rpp/debugging.cpp
    ReCpp/src/rpp/file_io.cpp
    ReCpp/src/rpp/paths.cpp
    ReCpp/src/rpp/sprint.cpp
    ReCpp/src/rpp/strview.cpp
    ReCpp/src/rpp/vec.cpp
  )
  # Mesh_Fbx.cpp provides real FBX when ENABLE_FBX_MESH_LOADER, else stubs
  list(APPEND SDNATIVE_SOURCES NanoMesh/src/Mesh_Fbx.cpp)
  if(APPLE)
    list(APPEND SDNATIVE_SOURCES ReCpp/src/rpp/file_io.mm)
  endif()
elseif(SDNATIVE_ENABLE_MESH)
  message(WARNING "NanoMesh/ReCpp submodules missing — building spatial/image/math only. Run: git submodule update --init --recursive")
endif()

add_library(SDNative SHARED ${SDNATIVE_SOURCES})

# Resolve Autodesk FBX runtime for this host
set(SDNATIVE_FBX_READY FALSE)
set(SDNATIVE_FBX_DYLIB "")
if(SDNATIVE_ENABLE_FBX AND SDNATIVE_ENABLE_MESH AND HAVE_NANOMESH)
  if(APPLE)
    set(_fbx_dylib "${CMAKE_CURRENT_SOURCE_DIR}/3rdparty/fbxsdk/macos/libfbxsdk.dylib")
    if(EXISTS "${_fbx_dylib}")
      set(SDNATIVE_FBX_READY TRUE)
      set(SDNATIVE_FBX_DYLIB "${_fbx_dylib}")
    else()
      message(WARNING
        "SDNATIVE_ENABLE_FBX=ON but ${_fbx_dylib} missing. "
        "Run: bash scripts/fetch-fbxsdk-macos.sh — building with NANOMESH_NO_FBX stubs.")
    endif()
  elseif(WIN32)
    # Windows uses SDNative.vcxproj; CMake path is not the primary Windows build.
    set(SDNATIVE_FBX_READY TRUE)
  endif()
endif()

if(SDNATIVE_FBX_READY)
  # Mesh.h defaults ENABLE_FBX_MESH_LOADER to (_WIN32) only — force on for macOS too.
  target_compile_definitions(SDNative PRIVATE ENABLE_FBX_MESH_LOADER=1)
  target_include_directories(SDNative PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/3rdparty/fbxsdk
  )
  if(APPLE AND SDNATIVE_FBX_DYLIB)
    target_link_libraries(SDNative PRIVATE "${SDNATIVE_FBX_DYLIB}")
    # System deps already linked by libfbxsdk.dylib; keep xml2/iconv available for static edge cases.
    target_link_libraries(SDNative PRIVATE xml2 iconv z
      "-framework CoreFoundation"
      "-framework CoreServices"
      "-framework SystemConfiguration"
    )
    # Embed @rpath so libSDNative finds libfbxsdk next to the executable / itself.
    set_target_properties(SDNative PROPERTIES
      BUILD_RPATH "@loader_path;@loader_path/.."
      INSTALL_RPATH "@loader_path;@loader_path/.."
    )
    message(STATUS "FBX SDK: enabled (macOS) → ${SDNATIVE_FBX_DYLIB}")
  else()
    message(STATUS "FBX SDK: enabled")
  endif()
else()
  target_compile_definitions(SDNative PRIVATE NANOMESH_NO_FBX=1)
  message(STATUS "FBX SDK: disabled (NANOMESH_NO_FBX)")
endif()

# FFmpeg (LGPL) for SDVideo — macOS arm64 vendored under 3rdparty/ffmpeg/macos
set(SDNATIVE_FFMPEG_READY FALSE)
set(SDNATIVE_FFMPEG_ROOT "")
if(SDNATIVE_ENABLE_FFMPEG)
  if(APPLE)
    set(_ff_root "${CMAKE_CURRENT_SOURCE_DIR}/3rdparty/ffmpeg/macos")
    if(EXISTS "${_ff_root}/include/libavformat/avformat.h"
       AND EXISTS "${_ff_root}/lib/libavformat.dylib")
      set(SDNATIVE_FFMPEG_READY TRUE)
      set(SDNATIVE_FFMPEG_ROOT "${_ff_root}")
    else()
      message(WARNING
        "SDNATIVE_ENABLE_FFMPEG=ON but ${_ff_root} incomplete. "
        "Run: bash scripts/fetch-ffmpeg-macos.sh — building SDVideo stubs.")
    endif()
  endif()
endif()

if(SDNATIVE_FFMPEG_READY)
  target_compile_definitions(SDNative PRIVATE SDNATIVE_ENABLE_FFMPEG=1)
  target_include_directories(SDNative PRIVATE "${SDNATIVE_FFMPEG_ROOT}/include")
  target_link_directories(SDNative PRIVATE "${SDNATIVE_FFMPEG_ROOT}/lib")
  target_link_libraries(SDNative PRIVATE avformat avcodec avutil swscale swresample)
  if(APPLE)
    target_link_libraries(SDNative PRIVATE
      "-framework VideoToolbox"
      "-framework AudioToolbox"
      "-framework CoreMedia"
      "-framework CoreVideo"
      "-framework CoreFoundation"
      "-framework CoreServices"
    )
    set_target_properties(SDNative PROPERTIES
      BUILD_RPATH "@loader_path;@loader_path/.."
      INSTALL_RPATH "@loader_path;@loader_path/.."
    )
  endif()
  message(STATUS "FFmpeg: enabled → ${SDNATIVE_FFMPEG_ROOT}")
else()
  target_compile_definitions(SDNative PRIVATE SDNATIVE_ENABLE_FFMPEG=0)
  message(STATUS "FFmpeg: disabled (SDVideo stubs)")
endif()

target_include_directories(SDNative PRIVATE
  ${CMAKE_CURRENT_SOURCE_DIR}
  ${CMAKE_CURRENT_SOURCE_DIR}/spatial
  ${CMAKE_CURRENT_SOURCE_DIR}/SdMesh
  ${CMAKE_CURRENT_SOURCE_DIR}/util
  ${CMAKE_CURRENT_SOURCE_DIR}/video
  ${CMAKE_CURRENT_SOURCE_DIR}/NanoMesh/src
  ${CMAKE_CURRENT_SOURCE_DIR}/NanoMesh/include
  ${CMAKE_CURRENT_SOURCE_DIR}/ReCpp/src
  ${CMAKE_CURRENT_SOURCE_DIR}/3rdparty
  ${CMAKE_CURRENT_SOURCE_DIR}/3rdparty/lodepng
  ${CMAKE_CURRENT_SOURCE_DIR}/3rdparty/soil2
  ${CMAKE_CURRENT_SOURCE_DIR}/3rdparty/libpng
)

# Portable export / aligned-alloc shims (function-style macros cannot be -D'd on Clang)
target_compile_options(SDNative PRIVATE
  "SHELL:-include ${CMAKE_CURRENT_SOURCE_DIR}/portability.h"
)
target_compile_definitions(SDNative PRIVATE SDNATIVE_BUILDING=1)

if(APPLE)
  set_target_properties(SDNative PROPERTIES
    OUTPUT_NAME SDNative
    MACOSX_RPATH ON
    INSTALL_NAME_DIR "@rpath"
    CXX_VISIBILITY_PRESET default
    VISIBILITY_INLINES_HIDDEN OFF
  )
  target_link_libraries(SDNative PRIVATE "-framework Foundation")
elseif(UNIX)
  set_target_properties(SDNative PROPERTIES
    OUTPUT_NAME SDNative
    CXX_VISIBILITY_PRESET default
    VISIBILITY_INLINES_HIDDEN OFF
  )
endif()

find_package(PNG)
find_package(ZLIB)
if(PNG_FOUND AND ZLIB_FOUND)
  target_link_libraries(SDNative PRIVATE PNG::PNG ZLIB::ZLIB)
else()
  message(WARNING "libpng/zlib not found via CMake — linking system png/z if available")
  target_link_libraries(SDNative PRIVATE png z)
endif()

install(TARGETS SDNative LIBRARY DESTINATION lib RUNTIME DESTINATION bin ARCHIVE DESTINATION lib)
