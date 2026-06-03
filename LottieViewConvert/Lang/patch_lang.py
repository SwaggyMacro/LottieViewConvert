import xml.etree.ElementTree as ET
import os

def add_entry(tree, root, name, value):
    # Check if exists
    for data in root.findall('data'):
        if data.attrib.get('name') == name:
            return
    data = ET.SubElement(root, 'data')
    data.set('name', name)
    data.set('xml:space', 'preserve')
    val = ET.SubElement(data, 'value')
    val.text = value

def process_resx(path, is_zh):
    tree = ET.parse(path)
    root = tree.getroot()
    if is_zh:
        add_entry(tree, root, "RotateAngle", "旋转角度")
        add_entry(tree, root, "FlipHorizontal", "水平翻转")
        add_entry(tree, root, "FlipVertical", "垂直翻转")
    else:
        add_entry(tree, root, "RotateAngle", "Rotation Angle")
        add_entry(tree, root, "FlipHorizontal", "Flip Horizontal")
        add_entry(tree, root, "FlipVertical", "Flip Vertical")
    
    with open(path, "wb") as f:
        tree.write(f, encoding="utf-8", xml_declaration=True)

process_resx("/tmp/workspace/SwaggyMacro/LottieViewConvert/LottieViewConvert/Lang/Resources.resx", False)
process_resx("/tmp/workspace/SwaggyMacro/LottieViewConvert/LottieViewConvert/Lang/Resources.zh.resx", True)

