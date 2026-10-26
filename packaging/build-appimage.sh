#!/bin/sh
# Publish Content Manager and pack a linux-x64 AppImage. --stage-only stops after the AppDir.
set -eu
cd "$(dirname "$0")/.."
ROOT=$(pwd)
OUT=${APPIMAGE_OUT:-$ROOT/artifacts/appimage}
STAGE_ONLY=0
PUBLISH=""

while [ $# -gt 0 ]; do
  case "$1" in
    --stage-only)
      STAGE_ONLY=1
      shift
      ;;
    --publish)
      PUBLISH=$2
      shift 2
      ;;
    --output)
      OUT=$2
      shift 2
      ;;
    *)
      echo "Unknown argument $1" >&2
      exit 1
      ;;
  esac
done

VERSION=$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' "$ROOT/src/AcManager.App/AcManager.App.csproj" | head -n 1)
if [ -z "$VERSION" ]; then
  echo "The app version is missing from AcManager.App.csproj." >&2
  exit 1
fi

if [ -z "$PUBLISH" ]; then
  PUBLISH=$OUT/publish
  dotnet publish "$ROOT/src/AcManager.App/AcManager.App.csproj" \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained true \
    -p:PublishTrimmed=false \
    -p:PublishSingleFile=false \
    -p:AppImageBuild=true \
    -p:DebugType=none \
    -p:DebugSymbols=false \
    -o "$PUBLISH"
fi

APPDIR=$OUT/AppDir
rm -rf "$APPDIR"
dotnet run --project "$ROOT/src/AcManager.App/AcManager.App.csproj" --configuration Release -p:PublishAcBridge=false -- \
  --pack-appdir \
  --publish "$PUBLISH" \
  --appdir "$APPDIR" \
  --license "$ROOT/LICENSE.txt" \
  --notice "$ROOT/NOTICE.txt" \
  --icon "$ROOT/packaging/io.github.giobuilds.actools.png"

if [ "$STAGE_ONLY" -eq 1 ]; then
  exit 0
fi

mkdir -p "$OUT/tools"
TOOL=${APPIMAGETOOL:-$OUT/tools/appimagetool}
if [ ! -x "$TOOL" ]; then
  curl -fsSL -o "$TOOL" https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
  chmod +x "$TOOL"
fi

UPDATE=$(tr -d '\n' < "$APPDIR/update-information")
NAME="ContentManager-${VERSION}-x86_64.AppImage"
export ARCH=x86_64
export APPIMAGE_EXTRACT_AND_RUN=1
(
  cd "$OUT"
  "$TOOL" -u "$UPDATE" "$APPDIR" "$NAME"
)
if [ -f "$ROOT/$NAME.zsync" ]; then
  mv -f "$ROOT/$NAME.zsync" "$OUT/$NAME.zsync"
fi
if [ ! -f "$OUT/$NAME.zsync" ]; then
  echo "appimagetool did not write $NAME.zsync." >&2
  exit 1
fi
