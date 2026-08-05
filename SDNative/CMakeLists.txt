cmake_minimum_required(VERSION 3.20)
project(SDNative LANGUAGES CXX)
if(APPLE)
  enable_language(OBJCXX)
endif()

# Cross-platform SDNative (Apple Silicon / Linux). Windows continues to use SDNative.vcxproj.
# Phase 2–4: build WITHOUT Autodesk FBX. Phase 5 can re-enable FBX when SDK + NanoMesh submodule exist.

option(SDNATIVE_ENABLE_FBX "Link Autodesk FBX SDK" OFF)
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

if(NOT SDNATIVE_ENABLE_FBX)
  target_compile_definitions(SDNative PRIVATE NANOMESH_NO_FBX=1)
endif()

target_include_directories(SDNative PRIVATE
  ${CMAKE_CURRENT_SOURCE_DIR}
  ${CMAKE_CURRENT_SOURCE_DIR}/spatial
  ${CMAKE_CURRENT_SOURCE_DIR}/SdMesh
  ${CMAKE_CURRENT_SOURCE_DIR}/util
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
