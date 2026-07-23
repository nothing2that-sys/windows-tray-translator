from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "src" / "WindowsTrayTranslator" / "Assets" / "app-icon.png"
OUTPUT = ROOT / "src" / "WindowsTrayTranslator" / "Assets" / "app-icon.ico"
SIZES = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)]


with Image.open(SOURCE) as source:
    icon = source.convert("RGBA")
    icon.save(OUTPUT, format="ICO", sizes=SIZES, bitmap_format="png")

print(f"Wrote {OUTPUT}")
