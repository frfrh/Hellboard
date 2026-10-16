import sys
import os
import re
import glob
import cv2
import numpy as np
from PIL import Image
from PySide6.QtGui import QImage, QPainter, QColor, QPixmap
from PySide6.QtCore import QSize, QByteArray
from PySide6.QtSvg import QSvgRenderer

CONFIG_FILE = "config.json"
ICON_DIR = "icons"
BORDER_TO_CROP = 15
SCALE_MIN = 0.3
SCALE_MAX = 1.3
SCALE_STEPS = 15
DYNAMIC_THRESHOLD = 0.52

GLOBAL_ICONS_CACHE = []

def rasterize_svg_to_opencv(file_path, target_size=256, return_color=False):
    """Rendert ein SVG-Icon verlustfrei in den RAM (Graustufen oder Farbe für GUI)."""
    try:
        with open(file_path, "r", encoding="utf-8", errors="ignore") as f:
            svg_content = f.read()
        svg_content = re.sub(r'^.*?<svg', '<svg', svg_content, flags=re.DOTALL)
        
        renderer = QSvgRenderer()
        if not renderer.load(QByteArray(svg_content.encode("utf-8"))):
            return None
            
        qimage = QImage(QSize(target_size, target_size), QImage.Format_ARGB32)
        qimage.fill(0)
        
        painter = QPainter(qimage)
        renderer.render(painter)
        painter.end()
        
        ptr = qimage.bits()
        rgba_np = np.frombuffer(ptr, dtype=np.uint8).reshape((target_size, target_size, 4))
        
        if return_color:
            return cv2.cvtColor(rgba_np, cv2.COLOR_BGRA2RGBA)
            
        bgr_img = cv2.cvtColor(rgba_np, cv2.COLOR_BGRA2BGR)
        gray_img = cv2.cvtColor(bgr_img, cv2.COLOR_BGR2GRAY)
        b = BORDER_TO_CROP
        return gray_img[b:target_size-b, b:target_size-b]
    except Exception as e:
        print(f"[-] SVG-Fehler bei {os.path.basename(file_path)}: {e}")
        return None

def load_icons():
    """Lädt alle Icons aus dem Unterordner 'icons' – kompatibel mit PyInstaller EXE."""
    global GLOBAL_ICONS_CACHE
    
    # NEU: Prüft, ob das Programm als verpackte EXE läuft, und holt den korrekten Pfad
    if hasattr(sys, '_MEIPASS'):
        base_dir = sys._MEIPASS
    else:
        base_dir = os.path.dirname(os.path.abspath(__file__))
        
    absolute_icon_dir = os.path.join(base_dir, ICON_DIR)
    
    if GLOBAL_ICONS_CACHE:
        return GLOBAL_ICONS_CACHE
        
    icons = []
    if not os.path.exists(absolute_icon_dir):
        os.makedirs(absolute_icon_dir, exist_ok=True)
        return icons

    files = glob.glob(os.path.join(absolute_icon_dir, "*"))
    for file_path in files:
        filename = os.path.basename(file_path)
        match = re.match(r"^(\d{3})_", filename)
        if match:
            icon_id = match.group(1)
            icon_img = rasterize_svg_to_opencv(file_path, 256, return_color=False)
            
            gui_pixmap = None
            if filename.lower().endswith('.svg'):
                rgba_np = rasterize_svg_to_opencv(file_path, 256, return_color=True)
                if rgba_np is not None:
                    qimg = QImage(rgba_np.data, 256, 256, QImage.Format_RGBA8888)
                    gui_pixmap = QPixmap.fromImage(qimg)
            else:
                try:
                    with Image.open(file_path) as pil_img:
                        pil_img = pil_img.convert("RGBA").resize((256, 256), Image.Resampling.LANCZOS)
                        bytes_img = pil_img.tobytes("raw", "RGBA")
                        qimg = QImage(bytes_img, 256, 256, QImage.Format_RGBA8888)
                        gui_pixmap = QPixmap.fromImage(qimg)
                except Exception as e:
                    print(f"[-] GUI-Bildfehler bei {filename}: {e}")
                    
            if icon_img is not None and gui_pixmap is not None:
                icons.append({
                    "id": icon_id, 
                    "image": icon_img, 
                    "pixmap": gui_pixmap, 
                    "name": filename,
                    "path": file_path
                })
    GLOBAL_ICONS_CACHE = icons
    return icons
