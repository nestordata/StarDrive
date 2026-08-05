# Autodesk FBX SDK — macOS runtime

`libfbxsdk.dylib` is the **arm64** slice of Autodesk FBX SDK **2020.3.7**
(`clang/release`), install name `@rpath/libfbxsdk.dylib`.

Source package (Autodesk CDN):
`https://damassets.autodesk.net/content/dam/autodesk/www/files/fbx202037_fbxsdk_clang_mac.pkg.tgz`

Headers live in the parent `../` tree (same 2020.3.7 as Windows).

Redistribute only this runtime dylib with the app (Autodesk FBX SDK EULA).
To refresh: `bash scripts/fetch-fbxsdk-macos.sh`
