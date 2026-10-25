#!/bin/sh
# Signs the already compiled game with the team's development certificate and installs it on the iPad.
# Run it in the Mac's own Terminal (not over SSH): signing needs the login keychain of the logged-in user.
# If macOS asks whether codesign may use the "Apple Development" key, type the Mac password and choose "Always Allow".
# The game links StoreKit, so its App ID (com.saturnwave.museumpuzzle) must carry In-App Purchase, which the team's
# wildcard profile ("iOS Team Provisioning Profile: *") cannot. -allowProvisioningUpdates lets Xcode register that
# explicit App ID if the developer portal does not have it yet and create its development profile, with the iPad in
# it; this needs the owner's Apple account signed in under Xcode > Settings > Accounts. If the code is not compiled
# yet, the same call compiles it first, which takes much longer.
set -e

TEAM=W4P65SPZP9
IPAD=00008112-0016084A0C06201E
ROOT="$HOME/puzzle-ios"
APP="$ROOT/DerivedData/Build/Products/Release-iphoneos/Puzzle.app"

cd "$ROOT/iOS"
echo "1/2 Signing (the code is already compiled; this takes a few minutes at most)..."
xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release \
  -destination "generic/platform=iOS" -derivedDataPath "$ROOT/DerivedData" \
  DEVELOPMENT_TEAM="$TEAM" CODE_SIGN_STYLE=Automatic -allowProvisioningUpdates build > "$ROOT/sign.log" 2>&1 \
  || { echo "Signing failed; the last lines of $ROOT/sign.log:"; tail -15 "$ROOT/sign.log"; exit 1; }

echo "2/2 Installing on the iPad (it must be connected, unlocked, in Developer Mode)..."
xcrun devicectl device install app --device "$IPAD" "$APP"
echo "Done: the game is on the iPad."
