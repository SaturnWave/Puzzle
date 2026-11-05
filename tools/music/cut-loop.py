"""Cuts the game's music out of the owner's Suno piece: an intro played once, then a loop that repeats with no seam.

The record art/sounds/music.json says everything this tool does: which source file (checked by its sha256), which of
the listed loops is chosen ("cut.loop": change that one value to M1-A or M1-C and run the tool again), how long the
crossfade at the loop's end is, and the level both clips are brought to. Nothing else is read, and the same record and
source always give the same bytes.

The intro is the source from its first sample up to the loop's start. The loop is the source from its start to its
end, whose last `crossfadeSeconds` blend, at equal power, from the source's last moments before the end into its last
moments before the start: played on repeat, the loop's last sample leads into its first exactly as the source's sample
before the start does, so the join cannot be heard. The intro ends on that same sample, so the intro runs into the loop
the same way.

Both clips get one and the same gain, so that the intro runs into the loop at the source's own balance: the gain that
brings the loop's loud part (samples above `quietBelowDbfs`) to `targetRmsDbfs`, lowered if needed until neither clip
peaks above `peakCeilingDbfs`.

The clips are written as 16-bit PCM WAV at the source's rate and channels. Unity imports uncompressed audio best: it
compresses a clip to Vorbis once, from lossless samples, while an OGG would be decoded and encoded a second time; and a
WAV carries its exact length in samples, which the game needs to start the loop on the intro's last sample. Python's
wave module writes no date or tool name into the file, so the bytes depend on the samples alone.

usage (from the repository root, Python 3 with numpy):
  py tools/music/cut-loop.py           writes the two clips into the record's folder
  py tools/music/cut-loop.py --check   writes nothing; fails when the clips on disk differ from what it would write
  --record <file>                       cuts by another record than art/sounds/music.json
After the clips have changed, run ProjectSetup.Run in the Unity Editor's batch mode: it reports their imported sizes.
"""
import argparse
import hashlib
import io
import json
import sys
import wave
from pathlib import Path

import numpy as np

REPOSITORY = Path(__file__).resolve().parents[2]
RECORD = REPOSITORY / "art" / "sounds" / "music.json"
SOURCE_FOLDER = RECORD.parent

FULL_SCALE = 32768.0
PCM16_LOWEST, PCM16_HIGHEST = -32768, 32767
BYTES_PER_SAMPLE = 2
HASH_CHUNK_BYTES = 1 << 20


class CutError(Exception):
    """The record, the source or the clips on disk are not what the cut needs; the message says which and why."""


def decibels(amplitude):
    return 20.0 * np.log10(max(float(amplitude), 1e-12))


def amplitude_of(dbfs):
    return 10.0 ** (dbfs / 20.0)


def require(mapping, keys, where):
    missing = [key for key in keys if key not in mapping]
    if missing:
        raise CutError(f"the record has no {', '.join(missing)} in {where}")


def load_record(path):
    """The record, with every field the cut reads; the chosen loop must be one of those it lists."""
    record = json.loads(path.read_text(encoding="utf-8"))
    require(record, ("piece", "source", "cut"), "its top level")
    require(record["source"], ("file", "sha256", "sampleRate", "channels", "bitsPerSample", "frames"), "source")
    cut = record["cut"]
    require(cut, ("loop", "loops", "crossfadeSeconds", "targetRmsDbfs", "peakCeilingDbfs", "quietBelowDbfs", "folder", "introFile", "loopFile"), "cut")
    require(cut["loops"], (cut["loop"],), "cut.loops (cut.loop names a loop it does not list)")
    require(cut["loops"][cut["loop"]], ("startSample", "endSample"), f"cut.loops.{cut['loop']}")
    return record


def sha256_of(path):
    digest = hashlib.sha256()
    with path.open("rb") as file:
        for chunk in iter(lambda: file.read(HASH_CHUNK_BYTES), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_source(source):
    """The source's samples as floats in [-1, 1), one row a frame, after its bytes and its format are found as recorded."""
    path = SOURCE_FOLDER / source["file"]
    if not path.is_file():
        raise CutError(f"the source {path.relative_to(REPOSITORY)} is missing; the owner's file goes there, untouched")
    found = sha256_of(path)
    if found != source["sha256"]:
        raise CutError(f"{path.name} has the sha256 {found}, not the recorded {source['sha256']}: it is not the file the cut was chosen on")

    with wave.open(str(path)) as reader:
        layout = (reader.getframerate(), reader.getnchannels(), 8 * reader.getsampwidth(), reader.getnframes())
        expected = (source["sampleRate"], source["channels"], source["bitsPerSample"], source["frames"])
        if layout != expected or reader.getcomptype() != "NONE":
            raise CutError(f"{path.name} is {layout} (rate, channels, bits, frames), not the recorded {expected}")
        raw = reader.readframes(reader.getnframes())
    samples = np.frombuffer(raw, dtype="<i2").reshape(-1, source["channels"]).astype(np.float64) / FULL_SCALE
    return samples, source["sampleRate"]


def loop_clip(samples, start, end, crossfade_frames):
    """The source's [start, end), whose last frames blend at equal power from what leads to `end` into what leads to `start`."""
    if start < crossfade_frames or end - start <= crossfade_frames or end > len(samples):
        raise CutError(f"the loop {start}..{end} leaves no room for a crossfade of {crossfade_frames} frames inside a source of {len(samples)}")
    clip = samples[start:end].copy()
    angle = np.linspace(0.0, np.pi / 2.0, crossfade_frames)[:, None]
    clip[-crossfade_frames:] = samples[end - crossfade_frames:end] * np.cos(angle) + samples[start - crossfade_frames:start] * np.sin(angle)
    return clip


def loud_rms_dbfs(clip, quiet_below_dbfs):
    """The level of the music itself: the RMS of the mono mix over the samples above the quiet threshold."""
    mono = clip.mean(axis=1)
    loud = mono[np.abs(mono) > amplitude_of(quiet_below_dbfs)]
    if len(loud) == 0:
        raise CutError("the loop is silent: nothing in it is louder than the quiet threshold")
    return decibels(np.sqrt(np.mean(loud ** 2)))


def peak_dbfs(clip):
    return decibels(np.abs(clip).max())


def common_gain_db(intro, loop, cut):
    """One gain for both clips: the loop at the target level, unless a peak of either would then pass the ceiling."""
    to_target = cut["targetRmsDbfs"] - loud_rms_dbfs(loop, cut["quietBelowDbfs"])
    to_ceiling = cut["peakCeilingDbfs"] - max(peak_dbfs(intro), peak_dbfs(loop))
    return min(to_target, to_ceiling)


def as_pcm16(clip):
    return np.clip(np.round(clip * FULL_SCALE), PCM16_LOWEST, PCM16_HIGHEST).astype("<i2")


def wav_bytes(pcm, rate):
    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as writer:
        writer.setnchannels(pcm.shape[1])
        writer.setsampwidth(BYTES_PER_SAMPLE)
        writer.setframerate(rate)
        writer.writeframes(pcm.tobytes())
    return buffer.getvalue()


def describe(name, pcm, rate, cut, data):
    clip = pcm.astype(np.float64) / FULL_SCALE
    return (f"{name}: {len(pcm)} frames ({len(pcm) / rate:.2f} s), {len(data)} bytes, "
            f"RMS {loud_rms_dbfs(clip, cut['quietBelowDbfs']):.2f} dBFS, peak {peak_dbfs(clip):.2f} dBFS, "
            f"sha256 {hashlib.sha256(data).hexdigest()}")


def cut_clips(record):
    """The bytes of the intro's file and the loop's file, by their paths, and the lines that describe them."""
    source, cut = record["source"], record["cut"]
    samples, rate = read_source(source)
    chosen = cut["loops"][cut["loop"]]
    start, end = chosen["startSample"], chosen["endSample"]
    crossfade_frames = int(round(cut["crossfadeSeconds"] * rate))

    intro = samples[:start]
    loop = loop_clip(samples, start, end, crossfade_frames)
    gain = amplitude_of(common_gain_db(intro, loop, cut))
    folder = REPOSITORY / cut["folder"]
    clips = {}
    lines = [f"{record['piece']} {cut['loop']}: loop {start}..{end} ({start / rate:.2f} s to {end / rate:.2f} s), "
             f"crossfade {crossfade_frames} frames, gain {decibels(gain):+.2f} dB on both clips"]
    for file_name, clip in ((cut["introFile"], intro), (cut["loopFile"], loop)):
        pcm = as_pcm16(clip * gain)
        data = wav_bytes(pcm, rate)
        clips[folder / file_name] = data
        lines.append(describe(file_name, pcm, rate, cut, data))
    return clips, lines


def main():
    parser = argparse.ArgumentParser(description="Cuts the game's intro and loop out of the music's source, as art/sounds/music.json says.")
    parser.add_argument("--check", action="store_true", help="write nothing; fail when the clips on disk differ from the cut")
    parser.add_argument("--record", type=Path, default=RECORD, help="another record to cut by, to hear a choice before it is made (default: art/sounds/music.json)")
    arguments = parser.parse_args()
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")  # the source's name is Turkish; a Windows console's default code page cannot write it
    try:
        clips, lines = cut_clips(load_record(arguments.record))
        if arguments.check:
            differing = [path for path, data in clips.items() if not path.is_file() or path.read_bytes() != data]
            if differing:
                raise CutError("these clips differ from the cut: " + ", ".join(path.relative_to(REPOSITORY).as_posix() for path in differing))
        else:
            for path, data in clips.items():
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
    except CutError as failure:
        print(f"cut-loop: {failure}", file=sys.stderr)
        return 1
    print("\n".join(lines))
    print("the clips on disk are the cut" if arguments.check else "both clips written")
    return 0


if __name__ == "__main__":
    sys.exit(main())
