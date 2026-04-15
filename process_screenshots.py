"""
Process raw screenshots into final wizard assets.

Each final image is sized to fit the 340x340-ish placeholder box in the
WPF app without distortion. We preserve aspect ratio, pad with a dark
background color that matches the app theme if needed.
"""
import os
from PIL import Image, ImageOps

RAW_DIR   = r"C:\Users\andre\Downloads\LastEpoch-Mods\LEBuildConverter_WPF\raw_screenshots"
FINAL_DIR = r"C:\Users\andre\Downloads\LastEpoch-Mods\LEBuildConverter_WPF\LEBuildConverter.WPF\Assets\screenshots"

# Target display size — the placeholder box in the wizard
# Using a bit wider than tall because most screenshots are landscape.
TARGET_W = 520
TARGET_H = 420

# App theme colors
BG_COLOR = (45, 45, 48)  # #2d2d30 from the WPF background


def fit_and_pad(img: Image.Image, w: int, h: int, bg=BG_COLOR) -> Image.Image:
    """Resize img to fit inside (w, h) preserving aspect ratio, pad with bg color."""
    img = img.convert("RGB")
    # Scale to fit without distortion
    img.thumbnail((w, h), Image.Resampling.LANCZOS)
    # Center on a new canvas of target size
    canvas = Image.new("RGB", (w, h), bg)
    off_x = (w - img.width) // 2
    off_y = (h - img.height) // 2
    canvas.paste(img, (off_x, off_y))
    return canvas


def process():
    os.makedirs(FINAL_DIR, exist_ok=True)

    # Mapping: raw file -> final filenames (a raw may map to multiple finals)
    mapping = [
        ("screenshot1.jpg",     ["step01_open_maxroll.png"]),
        ("screenshot2.png",     ["step02_set_mastery.png"]),
        ("screenshot3.png",     [
            "step03_paste_equipment.png",   # used as THE canonical paste visual
            # Reused for steps 4, 5, and skill steps — WPF binds to filenames
        ]),
        ("screenshot6.png",     ["step06_specialize_skills.png"]),
        ("screenshot12.png",    ["step_save_build.png"]),   # final save step
    ]

    for raw_name, finals in mapping:
        src = os.path.join(RAW_DIR, raw_name)
        if not os.path.exists(src):
            print(f"  [SKIP] {raw_name} (not found)")
            continue
        img = Image.open(src)
        out = fit_and_pad(img, TARGET_W, TARGET_H)
        for final_name in finals:
            dst = os.path.join(FINAL_DIR, final_name)
            out.save(dst, "PNG", optimize=True)
            print(f"  wrote {final_name}  ({os.path.getsize(dst) // 1024} KB)")

    print("\nDone.")


if __name__ == "__main__":
    process()
