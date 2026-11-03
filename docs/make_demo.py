from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageSequence
import imageio.v2 as imageio
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
SIZE = (1280, 720)
FPS = 20
BG = (246, 240, 229)
BROWN = (78, 51, 43)
GOLD = (207, 157, 56)
FONT_PATH = Path(r"C:\Windows\Fonts\msyh.ttc")
FONT_BOLD_PATH = Path(r"C:\Windows\Fonts\msyhbd.ttc")


def font(size, bold=False):
    path = FONT_BOLD_PATH if bold and FONT_BOLD_PATH.exists() else FONT_PATH
    return ImageFont.truetype(str(path), size)


def fit(image, box):
    image = image.convert("RGBA")
    image.thumbnail(box, Image.Resampling.LANCZOS)
    return image


def card(title, subtitle, image=None):
    frame = Image.new("RGB", SIZE, BG)
    draw = ImageDraw.Draw(frame)
    draw.rounded_rectangle((55, 55, 1225, 665), radius=34, fill=(255, 253, 248), outline=(224, 210, 186), width=3)
    draw.text((90, 95), title, font=font(56, True), fill=BROWN)
    draw.text((92, 175), subtitle, font=font(29), fill=(112, 87, 73))
    draw.rounded_rectangle((90, 235, 1190, 610), radius=24, fill=(250, 246, 238))
    if image:
        art = fit(image, (1020, 340))
        frame.paste(art, ((1280 - art.width) // 2, 255 + (330 - art.height) // 2), art)
    draw.text((95, 625), "KleinCat Desktop Pet", font=font(22), fill=GOLD)
    return frame


def repeat(frame, seconds):
    for _ in range(int(FPS * seconds)):
        yield np.asarray(frame)


def gif_frames(path, title, subtitle, seconds=4):
    source = Image.open(path)
    frames = [f.convert("RGBA") for f in ImageSequence.Iterator(source)]
    count = int(FPS * seconds)
    for i in range(count):
        yield np.asarray(card(title, subtitle, frames[i * len(frames) // count % len(frames)]))


icon = Image.open(ROOT / "src" / "assets" / "cat-icon.png")
output = DOCS / "克喵功能演示.mp4"
with imageio.get_writer(output, fps=FPS, codec="libx264", quality=8, pixelformat="yuv420p") as writer:
    sequences = [
        repeat(card("克喵", "会陪伴、会互动、也会认真听你说话", icon), 3),
        gif_frames(DOCS / "陪伴与提溜预览.gif", "桌面互动", "摸摸、安慰、亲亲，以及横向四脚扑腾的提溜动作"),
        gif_frames(DOCS / "七种睡姿.gif", "自己挑睡姿", "困了就蜷起来，偶尔换一种舒服的姿势"),
        gif_frames(DOCS / "思考与讨金币.gif", "聊天与金币", "支持本地模型和 API，也会思考、讨金币、数钱"),
        repeat(card("把克喵留在桌面", "Windows 10/11 · .NET 10 · 本地运行", icon), 3),
    ]
    for sequence in sequences:
        for frame in sequence:
            writer.append_data(frame)
print(output, output.stat().st_size)

