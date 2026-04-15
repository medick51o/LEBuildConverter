"""Generate placeholder screenshot PNGs for the wizard."""
import os
import struct
import zlib

OUT_DIR = r"C:\Users\andre\Downloads\LastEpoch-Mods\LEBuildConverter_WPF\LEBuildConverter.WPF\Assets\screenshots"

def make_png(path: str, width: int = 400, height: int = 300, color=(45, 45, 48)):
    """Create a simple solid-color PNG with border using stdlib only."""
    # Build raw image data: each row prefixed with filter byte 0
    row_bytes = bytearray()
    for y in range(height):
        row_bytes.append(0)  # filter: none
        for x in range(width):
            # Draw a 2-pixel border in a lighter color
            if x < 2 or x >= width - 2 or y < 2 or y >= height - 2:
                row_bytes.extend((85, 85, 93))
            else:
                row_bytes.extend(color)
    raw = bytes(row_bytes)

    def chunk(tag: bytes, data: bytes) -> bytes:
        crc = zlib.crc32(tag + data) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)

    # PNG signature
    sig = b"\x89PNG\r\n\x1a\n"
    # IHDR: width, height, bit_depth=8, color_type=2 (truecolor RGB), compression=0, filter=0, interlace=0
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    # IDAT: zlib-compressed image data
    idat = zlib.compress(raw, 9)

    with open(path, "wb") as f:
        f.write(sig)
        f.write(chunk(b"IHDR", ihdr))
        f.write(chunk(b"IDAT", idat))
        f.write(chunk(b"IEND", b""))


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    # Known step names
    steps = [
        "step01_open_maxroll",
        "step02_set_mastery",
        "step03_paste_equipment",
        "step04_paste_passives",
        "step05_paste_weaver",
        "step06_specialize_skills",
    ]
    for name in steps:
        path = os.path.join(OUT_DIR, f"{name}.png")
        make_png(path)
        print(f"  wrote {path}")

    # Generic skill placeholders (we reuse "step_skill_" prefix)
    for tree_id in ["sb44eQ", "fl71ds", "rn7iv", "fw3d", "vm53dx"]:
        path = os.path.join(OUT_DIR, f"step_skill_{tree_id}.png")
        make_png(path)
        print(f"  wrote {path}")

    print("Done.")


if __name__ == "__main__":
    main()
