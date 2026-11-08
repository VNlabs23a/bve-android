# Icon Assets

Place the following icon files in this directory:

## Required
- `icon.png` - 512x512 PNG, main app icon (used by Godot export)
- `icon_round.png` - 512x512 PNG, round icon for Android

## Recommended
- `icon_adaptive_foreground.xml` - Vector drawable for adaptive icon foreground (already in android/res/drawable/)
- `icon_adaptive_background.xml` - Vector drawable for adaptive icon background (color in colors.xml)
- `icon_monochrome.xml` - Vector drawable for monochrome icon (already in android/res/drawable/)
- `splash_screen.xml` - Splash screen drawable (already in android/res/drawable/)

## Specifications
- Format: PNG with transparency
- Size: 512x512 pixels
- Color space: sRGB
- Background: Transparent or #1A2332 (dark blue)
- Foreground: White train silhouette or "OpenBVE" text

## Quick Generation
```bash
# Using ImageMagick
convert -size 512x512 canvas:'#1A2332' -fill white -gravity center -pointsize 120 -annotate +0+0 "OpenBVE" icon.png

# Using Python PIL
python -c "
from PIL import Image, ImageDraw, ImageFont
img = Image.new('RGBA', (512, 512), '#1A2332')
draw = ImageDraw.Draw(img)
try:
    font = ImageFont.truetype('arial.ttf', 120)
except:
    font = ImageFont.load_default()
draw.text((256, 256), 'OpenBVE', fill='white', font=font, anchor='mm')
img.save('icon.png')
"
```

The adaptive icon system will use the vector drawables in `android/res/drawable/` for Android 8.0+ devices.