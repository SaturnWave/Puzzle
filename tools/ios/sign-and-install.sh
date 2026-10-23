#!/bin/sh
# Signs the already compiled game with the team's development certificate and installs it on the iPad.
# Run it in the Mac's own Terminal (not over SSH): signing needs the login keychain of the logged-in user.
# If macOS asks whether codesign may use the "Apple Development" key, type the Mac password and choose "Always Allow".
set -e

TEAM=W4P65SPZP9
IPAD=00008112-0016084A0C06201E
ROOT="$HOME/puzzle-ios"
APP="$ROOT/DerivedData/Build/Products/Release-iphoneos/Puzzle.app"

cd "$ROOT/iOS"
echo "1/2 Signing (the code is already compiled; this takes a few minutes at most)..."
xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release \
  -destination "generic/platform=iOS" -derivedDataPath "$ROOT/DerivedData" \
  DEVELOPMENT_TEAM="$TEAM" CODE_SIGN_STYLE=Automatic build > "$ROOT/sign.log" 2>&1 \
  || { echo "Signing failed; the last lines of $ROOT/sign.log:"; tail -15 "$ROOT/sign.log"; exit 1; }

echo "2/2 Installing on the iPad (it must be connected, unlocked, in Developer Mode)..."
xcrun devicectl device install app --device "$IPAD" "$APP"
echo "Done: the game is on the iPad."
