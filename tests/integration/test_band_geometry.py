"""Local, network-free regression tests for OCR band geometry and the invoice acceptance oracle."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("split_band", ROOT / "backend/Hosts/HackthonSpurs.Worker/Ocr/Rendering/split_band.py")
BANDS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BANDS)


class BandTests(unittest.TestCase):
    def test_exact_color_reassembly_and_safe_cut(self):
        """Place content across the midpoint; require an off-center cut and identical reconstructed pixels."""
        im = Image.new("RGB", (700, 120), "white")
        draw = ImageDraw.Draw(im)
        draw.rectangle((45, 48, 605, 70), fill=(55, 8, 180))
        with tempfile.TemporaryDirectory(dir=Path(__file__).parent) as folder:
            directory = Path(folder)
            src, a, b = [directory / part for part in ("in.png", "a.png", "b.png")]
            im.save(src); before = src.read_bytes()
            self.assertTrue(BANDS.split(src, a, b, im.width * im.height))
            with Image.open(a) as top, Image.open(b) as bottom:
                self.assertNotIn(top.height, range(48, 72))
                self.assertEqual((top.width, bottom.width, top.height + bottom.height), (im.width, im.width, im.height))
                self.assertEqual(top.tobytes() + bottom.tobytes(), im.tobytes())
            self.assertEqual(src.read_bytes(), before)

    def test_no_safe_cut(self):
        """Do not cut through a dense region simply to satisfy the byte limit."""
        self.assertIsNone(BANDS.choose_cut(Image.new("RGB", (160, 240), "black")))


if __name__ == "__main__":
    unittest.main()
