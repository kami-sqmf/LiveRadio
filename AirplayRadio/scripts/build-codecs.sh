#!/usr/bin/env bash
set -euo pipefail
export PATH="/ucrt64/bin:/usr/bin:$PATH"
root="$(cd "$(dirname "$0")/../.." && pwd)"
src="$root/.work/airplay-ffmpeg"
build="$root/.work/airplay-codecs-build"
prefix="$root/.work/airplay-codecs"
if [ ! -f "$src/configure" ]; then
  echo 'Fetch FFmpeg n8.0.1 (894da5ca7d742e4429ffb2af534fcda0103ef593) into .work/airplay-ffmpeg first.' >&2
  exit 1
fi
if [ "$(git -C "$src" rev-parse HEAD)" != "894da5ca7d742e4429ffb2af534fcda0103ef593" ]; then
  echo 'Unexpected FFmpeg revision; use the pinned source documented in README.md.' >&2
  exit 1
fi
mkdir -p "$build"
cd "$build"
if [ ! -f config.h ]; then
  "$src/configure" --prefix="$prefix" --target-os=mingw32 --arch=x86_64 \
    --disable-everything --disable-autodetect --disable-programs --disable-doc \
    --disable-network --disable-avdevice --disable-avformat --disable-avfilter \
    --disable-swscale --disable-x86asm --disable-debug \
    --enable-static --disable-shared --enable-swresample \
    --enable-decoder=alac,aac,pcm_s16le --enable-w32threads
fi
make -j6
make install
