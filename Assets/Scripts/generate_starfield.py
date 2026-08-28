from PIL import Image, ImageDraw, ImageFilter
import numpy as np
import random
import math

# ============================================================
# SETTINGS
# ============================================================

WIDTH = 8192
HEIGHT = 4096

STAR_COUNT = 35000
BRIGHT_STAR_COUNT = 350

OUTPUT = "starfield_8k.png"

SEED = 1337
random.seed(SEED)
np.random.seed(SEED)


# ============================================================
# BACKGROUND
# ============================================================

# RGB working image.
image = Image.new("RGB", (WIDTH, HEIGHT), (0, 0, 0))
pixels = np.array(image, dtype=np.float32)


# ============================================================
# SUBTLE GALACTIC HAZE
# ============================================================

# Generate low-resolution noise, then scale it up.
noise_width = 512
noise_height = 256

noise = np.random.random(
    (noise_height, noise_width)
).astype(np.float32)

noise_img = Image.fromarray(
    np.uint8(noise * 255),
    mode="L"
)

noise_img = noise_img.resize(
    (WIDTH, HEIGHT),
    Image.Resampling.BICUBIC
)

noise_img = noise_img.filter(
    ImageFilter.GaussianBlur(radius=80)
)

noise = np.array(noise_img, dtype=np.float32) / 255.0


# Galactic band centered roughly around the equator.
y = np.arange(HEIGHT)
distance = np.abs(y - HEIGHT / 2)

band = np.exp(
    -(distance ** 2) /
    (2 * (HEIGHT * 0.10) ** 2)
)

band = band[:, None]

galaxy = band * noise


# Very subtle blue-gray haze.
pixels[:, :, 0] += galaxy * 10
pixels[:, :, 1] += galaxy * 12
pixels[:, :, 2] += galaxy * 18

pixels = np.clip(pixels, 0, 255)

image = Image.fromarray(
    pixels.astype(np.uint8),
    mode="RGB"
)


# ============================================================
# STAR LAYERS
# ============================================================

stars = Image.new(
    "RGBA",
    (WIDTH, HEIGHT),
    (0, 0, 0, 0)
)

draw = ImageDraw.Draw(stars)


def star_color():
    """
    Approximate stellar color distribution.
    Most stars are white-ish, with occasional
    blue and warm stars.
    """

    roll = random.random()

    if roll < 0.10:
        # Blue-white
        return (180, 210, 255)

    elif roll < 0.30:
        # Warm
        return (255, 220, 170)

    else:
        # White
        return (240, 245, 255)


for _ in range(STAR_COUNT):

    x = random.randrange(WIDTH)
    y = random.randrange(HEIGHT)

    # Most stars should be dim.
    brightness = random.random() ** 3

    radius = random.choice([
        0.4,
        0.5,
        0.6,
        0.8,
        1.0
    ])

    color = star_color()

    alpha = int(
        40 + brightness * 215
    )

    r = radius

    draw.ellipse(
        (
            x - r,
            y - r,
            x + r,
            y + r
        ),
        fill=(
            color[0],
            color[1],
            color[2],
            alpha
        )
    )


# ============================================================
# BRIGHT STARS
# ============================================================

bright_layer = Image.new(
    "RGBA",
    (WIDTH, HEIGHT),
    (0, 0, 0, 0)
)

bright_draw = ImageDraw.Draw(bright_layer)


for _ in range(BRIGHT_STAR_COUNT):

    x = random.randrange(WIDTH)
    y = random.randrange(HEIGHT)

    color = star_color()

    radius = random.uniform(1.0, 2.5)

    # Core
    bright_draw.ellipse(
        (
            x - radius,
            y - radius,
            x + radius,
            y + radius
        ),
        fill=(
            color[0],
            color[1],
            color[2],
            255
        )
    )


# ============================================================
# GLOW
# ============================================================

glow = bright_layer.filter(
    ImageFilter.GaussianBlur(radius=5)
)

image = Image.alpha_composite(
    image.convert("RGBA"),
    glow
)

image = Image.alpha_composite(
    image,
    bright_layer
)


# ============================================================
# EXTRA LARGE STARS
# ============================================================

large_layer = Image.new(
    "RGBA",
    (WIDTH, HEIGHT),
    (0, 0, 0, 0)
)

large_draw = ImageDraw.Draw(
    large_layer
)


for _ in range(30):

    x = random.randrange(WIDTH)
    y = random.randrange(HEIGHT)

    color = star_color()

    # Bright center
    large_draw.ellipse(
        (
            x - 2,
            y - 2,
            x + 2,
            y + 2
        ),
        fill=(
            color[0],
            color[1],
            color[2],
            255
        )
    )

    # Small diffraction spike
    spike = random.randint(4, 10)

    large_draw.line(
        (
            x - spike,
            y,
            x + spike,
            y
        ),
        fill=(
            color[0],
            color[1],
            color[2],
            100
        )
    )

    large_draw.line(
        (
            x,
            y - spike,
            x,
            y + spike
        ),
        fill=(
            color[0],
            color[1],
            color[2],
            100
        )
    )


large_glow = large_layer.filter(
    ImageFilter.GaussianBlur(radius=8)
)

image = Image.alpha_composite(
    image,
    large_glow
)

image = Image.alpha_composite(
    image,
    large_layer
)


# ============================================================
# SAVE
# ============================================================

image = image.convert("RGB")

image.save(
    OUTPUT,
    quality=95,
    optimize=True
)

print(
    f"Generated {OUTPUT} "
    f"({WIDTH}x{HEIGHT})"
)