"""Create two disjoint full-width PNGs; stdout is only a no-safe-cut signal."""
import sys

from PIL import Image, ImageOps


def choose_cut(image):
    """Find the nearest quiet gutter in the middle half of image, tolerating thin table rules."""
    gray = image.convert("RGBA")
    paper = Image.new("RGB", image.size, "white")
    paper.paste(gray, mask=gray.getchannel("A"))
    ink = paper.convert("L").point(lambda value: 255 if value < 200 else 0)
    rows = ink.resize((1, image.height), Image.Resampling.BOX).tobytes()
    mid = image.height // 2
    choices = (y for y in range(max(2, mid // 2), min(image.height - 1, mid + mid // 2 + 1))
               if max(rows[y - 2:y + 2]) <= 2)
    return min(choices, key=lambda y: abs(mid - y), default=None)


def split(source, upper, lower, limit):
    """Split source at a quiet row into upper/lower PNGs without resizing; enforce limit before decode."""
    with Image.open(source) as raw:
        if raw.width * raw.height > limit or getattr(raw, "n_frames", 1) != 1:
            return False
        # Pillow can silently reduce multichannel 16-bit PNG to 8-bit on decode.
        if raw.format == "PNG":
            with open(source, "rb") as stream:
                if stream.read(25)[24] == 16:
                    return False
        image = ImageOps.exif_transpose(raw)
        if image.mode not in ("1", "L", "LA", "P", "RGB", "RGBA"):
            return False
        y = choose_cut(image)
        if y is None:
            return False
        for path, top, bottom in ((upper, 0, y), (lower, y, image.height)):
            image.crop((0, top, image.width, bottom)).save(path, "PNG", optimize=True)
        return True


if __name__ == "__main__":
    if not split(*sys.argv[1:4], int(sys.argv[4])):
        print(0)
