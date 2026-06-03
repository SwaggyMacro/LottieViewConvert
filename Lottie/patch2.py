import os

path = "/tmp/workspace/SwaggyMacro/LottieViewConvert/Lottie/PngExporter.cs"
with open(path, "r", encoding="utf-8") as f:
    content = f.read()

content = content.replace(
    "int? outputHeight = null)",
    "int? outputHeight = null,\n            double rotationAngle = 0.0,\n            bool flipHorizontal = false,\n            bool flipVertical = false)")

with open(path, "w", encoding="utf-8") as f:
    f.write(content)

