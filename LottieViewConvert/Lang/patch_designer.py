import re
import os

path = "/tmp/workspace/SwaggyMacro/LottieViewConvert/LottieViewConvert/Lang/Resources.Designer.cs"
with open(path, "r", encoding="utf-8-sig") as f:
    content = f.read()

props = """
        public static string RotateAngle {
            get {
                return ResourceManager.GetString("RotateAngle", resourceCulture);
            }
        }
        public static string FlipHorizontal {
            get {
                return ResourceManager.GetString("FlipHorizontal", resourceCulture);
            }
        }
        public static string FlipVertical {
            get {
                return ResourceManager.GetString("FlipVertical", resourceCulture);
            }
        }
"""

if "RotateAngle" not in content:
    content = content.replace("    }\n}\n", props + "    }\n}\n")

with open(path, "w", encoding="utf-8-sig") as f:
    f.write(content)

