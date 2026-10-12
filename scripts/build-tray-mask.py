"""Regenerate the native 32px alpha mask from the supplied embedded-PNG SVG logo."""
from pathlib import Path
import base64
import io
import re
from PIL import Image, ImageChops, ImageOps

root = Path(__file__).resolve().parent.parent
svg = (root / 'ui/logo.svg').read_text(encoding='utf-8')
embedded = re.search(r'data:image/png;base64,([^\"]+)', svg)
if not embedded:
    raise ValueError('The supplied SVG must contain its PNG luminance mask')
image = Image.open(io.BytesIO(base64.b64decode(embedded.group(1)))).convert('RGBA')
alpha = ImageChops.multiply(ImageOps.grayscale(image.convert('RGB')), image.getchannel('A'))
alpha = alpha.resize((32, 32), Image.Resampling.LANCZOS)
(root / 'ui/tray-mask.bin').write_bytes(alpha.tobytes())
icon_alpha = ImageChops.multiply(ImageOps.grayscale(image.convert('RGB')), image.getchannel('A')).resize((256, 256), Image.Resampling.LANCZOS)
icon = Image.new('RGBA', (256, 256), (220, 92, 59, 0))
icon.putalpha(icon_alpha)
icon.save(root / 'ui/launchpad.ico', sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (256, 256)])
