# macOS code signing and notarization

`scripts/build-mac.sh` produces an ad-hoc signed `.app` and DMG suitable for local testing. Gatekeeper distribution to other Macs requires Apple Developer ID signing + notarization.

## Local / CI ad-hoc (current)

```bash
bash scripts/build-mac.sh
# codesign --sign - (ad-hoc) on StarDrive + nested dylibs
```

Users may need: right-click → Open, or `xattr -dr com.apple.quarantine StarDrive.app`.

## Release signing (maintainer)

Prerequisites: Apple Developer ID Application certificate in the keychain, app-specific notarization credentials (`notarytool`).

1. Build unsigned or ad-hoc: `bash scripts/build-mac.sh --skip-dmg`
2. Sign the app bundle (hardened runtime), including nested `Contents/Resources/game/*.dylib` and the apphost:

```bash
APP=artifacts/StarDrive.app
IDENTITY="Developer ID Application: Your Name (TEAMID)"
codesign --force --options runtime --timestamp --sign "$IDENTITY" \
  $(find "$APP/Contents/Resources/game" -name '*.dylib' -type f)
codesign --force --options runtime --timestamp --sign "$IDENTITY" \
  "$APP/Contents/Resources/game/StarDrive"
codesign --force --options runtime --timestamp --sign "$IDENTITY" \
  "$APP/Contents/MacOS/StarDrive"
codesign --force --options runtime --timestamp --sign "$IDENTITY" "$APP"
codesign --verify --deep --strict "$APP"
```

3. Package DMG (or zip) and submit:

```bash
xcrun notarytool submit artifacts/StarDrive-mac-arm64.dmg \
  --apple-id YOU@example.com --team-id TEAMID --password "@keychain:NOTARY" \
  --wait
xcrun stapler staple artifacts/StarDrive-mac-arm64.dmg
```

4. Smoke: download on a clean Mac, open without right-click bypass.

Entitlements: if hardened runtime blocks dylib load, add a minimal entitlements plist (`com.apple.security.cs.disable-library-validation` only if required after testing — prefer fixing `@rpath` / signatures instead).
