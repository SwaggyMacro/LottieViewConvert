import os

path = "/tmp/workspace/SwaggyMacro/LottieViewConvert/Lottie/PngExporter.cs"

# Revert git change
os.system("git checkout " + path)

with open(path, "r", encoding="utf-8") as f:
    content = f.read()

content = content.replace(
    "int? outputHeight = null,\n            Action<ExportProgressEventArgs> progressCallback = null)",
    "int? outputHeight = null,\n            double rotationAngle = 0.0,\n            bool flipHorizontal = false,\n            bool flipVertical = false,\n            Action<ExportProgressEventArgs> progressCallback = null)")

content = content.replace(
    "int? outputHeight = null,\n            IProgress<ExportProgressEventArgs> progress = null)",
    "int? outputHeight = null,\n            double rotationAngle = 0.0,\n            bool flipHorizontal = false,\n            bool flipVertical = false,\n            IProgress<ExportProgressEventArgs> progress = null)")

content = content.replace(
    "outputHeight,\n                progress.Report);",
    "outputHeight,\n                rotationAngle,\n                flipHorizontal,\n                flipVertical,\n                progress.Report);")

searchStr = """                    // Scale if the output size differs
                    if (width != baseWidth || height != baseHeight)
                    {
                        var scaleX = width / (float)baseWidth;
                        var scaleY = height / (float)baseHeight;
                        canvas.Scale(scaleX, scaleY);
                    }

                    animation.Render(canvas, new SKRect(0, 0, baseWidth, baseHeight));"""

replaceStr = """                    // Scale if the output size differs
                    if (width != baseWidth || height != baseHeight)
                    {
                        var scaleX = width / (float)baseWidth;
                        var scaleY = height / (float)baseHeight;
                        canvas.Scale(scaleX, scaleY);
                    }

                    if (rotationAngle != 0 || flipHorizontal || flipVertical)
                    {
                        var cx = baseWidth / 2f;
                        var cy = baseHeight / 2f;
                        canvas.Translate(cx, cy);
                        if (rotationAngle != 0) canvas.RotateDegrees((float)rotationAngle);
                        canvas.Scale(flipHorizontal ? -1 : 1, flipVertical ? -1 : 1);
                        canvas.Translate(-cx, -cy);
                    }

                    animation.Render(canvas, new SKRect(0, 0, baseWidth, baseHeight));"""

content = content.replace(searchStr, replaceStr)
content = content.replace(searchStr.replace("                    ", "                "), replaceStr.replace("                    ", "                "))

with open(path, "w", encoding="utf-8") as f:
    f.write(content)

