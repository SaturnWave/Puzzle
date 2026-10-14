"""Makes the game's picture of a painting from a downloaded image: cuts away what is not the painting (frame edge,
mount, colour chart, paper margin), scales it so that its longer side is no more than the game shows, turns it into
sRGB and writes it as a JPEG. A download that needs none of this is copied as it is, byte for byte.

The same download, box and size always give the same bytes: no step depends on time, place or chance.

usage: py -I tools/unity/prepare-painting.py <download> <picture> <left> <top> <right> <bottom> <longer-side>
    left, top, right, bottom: the edges of the part kept, as fractions of the download's width and height
    (0 0 1 1 keeps the whole picture); longer-side: the most pixels the picture may have on its longer side.
Prints the picture's width and height.
"""
import io
import shutil
import sys
from dataclasses import dataclass
from pathlib import Path

from PIL import ExifTags, Image, ImageCms, ImageOps

JPEG_QUALITY = 90
# The museums' largest files (an Arcimboldo of 172 megapixels) are bigger than Pillow's guard against decompression bombs.
Image.MAX_IMAGE_PIXELS = 250_000_000


@dataclass(frozen=True)
class CropBox:
    """The edges of the part of a picture that is kept, as fractions of its width and height."""
    left: float
    top: float
    right: float
    bottom: float

    def __post_init__(self):
        if not (0 <= self.left < self.right <= 1 and 0 <= self.top < self.bottom <= 1):
            raise ValueError(f"the box {self} does not lie inside the picture")

    @property
    def is_whole(self) -> bool:
        return (self.left, self.top, self.right, self.bottom) == (0, 0, 1, 1)

    def in_pixels(self, width: int, height: int) -> tuple[int, int, int, int]:
        return (round(self.left * width), round(self.top * height), round(self.right * width), round(self.bottom * height))


def is_srgb(profile_bytes: bytes | None) -> bool:
    """A picture without a colour profile is read as sRGB, as Unity and the browsers read it. Commons' thumbnails
    carry a compact sRGB profile whose whole description is "c2"."""
    if profile_bytes is None:
        return True
    description = ImageCms.getProfileDescription(ImageCms.ImageCmsProfile(io.BytesIO(profile_bytes))).strip().lower()
    return "srgb" in description or description == "c2"


def scaled_size(width: int, height: int, longer_side: int) -> tuple[int, int]:
    if max(width, height) <= longer_side:
        return width, height
    if width >= height:
        return longer_side, round(height * longer_side / width)
    return round(width * longer_side / height), longer_side


def prepare(download: Path, picture: Path, box: CropBox, longer_side: int) -> tuple[int, int]:
    with Image.open(download) as image:
        profile = image.info.get("icc_profile")
        rotated = image.getexif().get(ExifTags.Base.Orientation, 1) != 1
        needs_work = not box.is_whole or max(image.size) > longer_side or not is_srgb(profile) or rotated or image.mode != "RGB"
        if image.format == "JPEG" and not needs_work:
            shutil.copyfile(download, picture)
            return image.size

        full_width, full_height = image.size
        if image.format == "JPEG":
            # Decodes a very large file at a half, quarter or eighth of its size while that still leaves enough pixels.
            kept_longest = max((box.right - box.left) * full_width, (box.bottom - box.top) * full_height)
            reduction = min(1.0, longer_side / kept_longest)
            image.draft("RGB", (round(full_width * reduction), round(full_height * reduction)))
        image = ImageOps.exif_transpose(image)
        if profile is not None and not is_srgb(profile):
            source = ImageCms.ImageCmsProfile(io.BytesIO(profile))
            image = ImageCms.profileToProfile(image, source, ImageCms.createProfile("sRGB"),
                                              renderingIntent=ImageCms.Intent.RELATIVE_COLORIMETRIC, outputMode="RGB",
                                              flags=ImageCms.Flags.BLACKPOINTCOMPENSATION)
        image = image.convert("RGB")
        kept = image.crop(box.in_pixels(*image.size))
        size = scaled_size(*kept.size, longer_side)
        if size != kept.size:
            kept = kept.resize(size, Image.Resampling.LANCZOS)
        kept.save(picture, "JPEG", quality=JPEG_QUALITY, optimize=True)
        return kept.size


def main(arguments: list[str]) -> None:
    if len(arguments) != 7:
        sys.exit(__doc__)
    download, picture = Path(arguments[0]), Path(arguments[1])
    box = CropBox(*(float(edge) for edge in arguments[2:6]))
    width, height = prepare(download, picture, box, int(arguments[6]))
    print(f"{width} {height}")


if __name__ == "__main__":
    main(sys.argv[1:])
