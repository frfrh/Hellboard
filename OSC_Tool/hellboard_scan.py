import sys
import subprocess

REQUIRED_PACKAGES = {
    "cv2": "opencv-python",
    "keyboard": "keyboard",
    "mss": "mss",
    "PIL": "pillow",
    "serial": "pyserial",
    "PySide6": "PySide6"
}

for module_name, pip_name in REQUIRED_PACKAGES.items():
    try:
        __import__(module_name)
    except ImportError:
        print(f"[!] {pip_name} fehlt. Installiere jetzt automatisch...")
        subprocess.call([sys.executable, "-m", "pip", "install", pip_name])

import os
import re
import time
import glob
import cv2
import mss
import numpy as np
from PIL import Image
import keyboard
import serial

from PySide6.QtGui import QImage, QPainter
from PySide6.QtSvg import QSvgRenderer
from PySide6.QtCore import QSize, QByteArray

# ==============================================================================
# KONFIGURATION
# ==============================================================================
HOTKEY = "F4"            
MONITOR_INDEX = 1         
ICON_DIR = "icons"        
THRESHOLD = 0.65          # Schwellenwert für Graustufen-Matching (0.0 bis 1.0)

SERIAL_PORT = "COM3"      
BAUDRATE = 9600

SCALE_MIN = 0.3
SCALE_MAX = 1.3
SCALE_STEPS = 15

BORDER_TO_CROP = 20       # 20 Pixel Rand der Icons ignorieren
# ==============================================================================


def get_bottom_left_grid(monitor_index):
    """
    Macht einen Screenshot, schneidet das untere linke 4x3-Segment aus
    und verwirft direkt alles unterhalb von 4/7 dieses Segments.
    """
    with mss.MSS() as sct:
        if monitor_index >= len(sct.monitors):
            print(f"[-] Fehler: Monitor {monitor_index} nicht gefunden. Nutze Hauptmonitor.")
            monitor_index = 1
            
        monitor = sct.monitors[monitor_index]
        sct_img = sct.grab(monitor)
        
        img = Image.frombytes("RGB", sct_img.size, sct_img.bgra, "raw", "BGRX")
        width, height = img.size
        
        # Urspüngliches 4x3 Segment (Untere linke Ecke)
        left = 0
        top = int(height * (2 / 3))
        right = int(width / 4)
        bottom = height
        
        # "Unterhalb von 4/7 des Bildes muss nichts mehr erkannt werden"
        # Wir passen die maximale Unterkante (bottom) sofort mathematisch an
        segment_height = bottom - top
        bottom = top + int(segment_height * (4 / 7))
        
        cropped_img = img.crop((left, top, right, bottom))
        
        # Konvertierung in Graustufen
        open_cv_image = np.array(cropped_img)
        gray_image = cv2.cvtColor(open_cv_image, cv2.COLOR_RGB2GRAY)
        return gray_image


def rasterize_svg_to_opencv(file_path, target_size=256):
    """Rendert ein SVG-Icon in Graustufen und schneidet den 15px Rand ab."""
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
        
        bgr_img = cv2.cvtColor(rgba_np, cv2.COLOR_BGRA2BGR)
        gray_img = cv2.cvtColor(bgr_img, cv2.COLOR_BGR2GRAY)
        
        b = BORDER_TO_CROP
        cropped_icon = gray_img[b:target_size-b, b:target_size-b]
        return cropped_icon
    except Exception as e:
        print(f"[-] Interner SVG-Fehler bei {os.path.basename(file_path)}: {e}")
        return None


def load_icons(directory):
    """Lädt alle Icons in Graustufen und entfernt den störenden Außenrahmen."""
    script_dir = os.path.dirname(os.path.abspath(__file__))
    absolute_icon_dir = os.path.join(script_dir, directory)
    
    icons = []
    if not os.path.exists(absolute_icon_dir):
        os.makedirs(absolute_icon_dir, exist_ok=True)
        print(f"[-] Verzeichnis wurde erstellt: '{absolute_icon_dir}'. Bitte Icons dort einfügen.")
        return icons

    search_path = os.path.join(absolute_icon_dir, "*")
    files = glob.glob(search_path)
    
    for file_path in files:
        filename = os.path.basename(file_path)
        match = re.match(r"^(\d{3})_", filename)
        
        if match:
            icon_id = match.group(1)
            icon_img = None
            
            if filename.lower().endswith('.svg'):
                icon_img = rasterize_svg_to_opencv(file_path, 256)
            else:
                try:
                    with Image.open(file_path) as pil_img:
                        pil_img = pil_img.convert("RGB").resize((256, 256), Image.Resampling.LANCZOS)
                        gray_img = cv2.cvtColor(np.array(pil_img), cv2.COLOR_RGB2GRAY)
                        b = BORDER_TO_CROP
                        icon_img = gray_img[b:256-b, b:256-b]
                except Exception as e:
                    print(f"[-] Fehler beim Laden von Datei '{filename}': {e}")
                    
            if icon_img is not None:
                icons.append({"id": icon_id, "image": icon_img})
                
    print(f"[INFO] Ordner-Scanner: {len(icons)} Icons erfolgreich aus '{absolute_icon_dir}' registriert.")
    return icons


def send_data_string_over_serial(data_string):
    """Sendet die formatierte ID-Zeile über den COM-Port."""
    print(f"[SERIAL] -> Gesendet: {data_string.strip()}")
    # try:
    #     with serial.Serial(SERIAL_PORT, BAUDRATE, timeout=1) as ser:
    #         ser.write(data_string.encode("utf-8"))
    #         print(f"[SERIAL] -> Gesendet: {data_string.strip()}")
    # except serial.SerialException as e:
    #     print(f"[-] Serieller Fehler auf {SERIAL_PORT}: {e}")


def process_screenshot():
    """Analysiert den Screenshot mit dynamisch berechnetem Icon-Abstand und erhöhter Toleranz."""
    print(f"\n[{time.strftime('%H:%M:%S')}] Hotkey ausgelöst. Analysiere Bildschirm (Raster-Toleranz-Fix)...")
    
    screen_gray = get_bottom_left_grid(MONITOR_INDEX)
    icons = load_icons(ICON_DIR)
    
    if not icons:
        print("[-] Keine gültigen Icons zum Abgleichen geladen.")
        return

    debug_image = cv2.cvtColor(screen_gray, cv2.COLOR_GRAY2BGR)
    img_h, img_w = screen_gray.shape[:2]
    
    # Trennlinie bei exakt 54% der Bildhöhe
    zone_boundary_y = int(img_h * 0.54)
    cv2.line(debug_image, (0, zone_boundary_y), (img_w, zone_boundary_y), (255, 0, 0), 1)

    raw_detections = [] 
    base_template_size = 256 - (2 * BORDER_TO_CROP)

    # 1. Alle Rohdaten sammeln (Nutze hier intern 0.52 für maximale Treffersicherheit rechts)
    DYNAMIC_THRESHOLD = 0.52
    for icon in icons:
        template = icon["image"]
        best_val = -1
        best_loc = None
        best_w, best_h = base_template_size, base_template_size
        
        for scale in np.linspace(SCALE_MIN, SCALE_MAX, SCALE_STEPS):
            new_w = int(base_template_size * scale)
            new_h = int(base_template_size * scale)
            
            if new_h > img_h or new_w > img_w:
                continue
                
            resized_template = cv2.resize(template, (new_w, new_h), interpolation=cv2.INTER_AREA)
            res = cv2.matchTemplate(screen_gray, resized_template, cv2.TM_CCOEFF_NORMED)
            _, max_val, _, max_loc = cv2.minMaxLoc(res)
            
            if max_val > best_val:
                best_val = max_val
                best_loc = max_loc
                best_w, best_h = new_w, new_h
                
        if best_val >= DYNAMIC_THRESHOLD:
            x, y = best_loc
            raw_detections.append((x, y, icon["id"], best_val, best_w, best_h))

    # 2. Non-Maximum Suppression (Doppelte an identischer Stelle filtern)
    raw_detections.sort(key=lambda item: item[3], reverse=True)
    nms_filtered = []

    for item in raw_detections:
        x, y, icon_id, conf, w, h = item
        center_x = x + w / 2
        center_y = y + h / 2
        
        overlap = False
        for d_x, d_y, _, _, d_w, d_h in nms_filtered:
            d_center_x = d_x + d_w / 2
            d_center_y = d_y + d_h / 2
            distance = np.sqrt((center_x - d_center_x)**2 + (center_y - d_center_y)**2)
            
            if distance < (w / 2):
                overlap = True
                break
                
        if not overlap:
            nms_filtered.append(item)

    if not nms_filtered:
        print("[-] Keine passenden Icons auf dem Bildschirm gefunden.")
        cv2.imwrite("debug_screenshot.png", debug_image)
        send_data_string_over_serial("0\n")
        return

    # 3. GEOMETRISCHE ANALYSE
    upper_centers_y = [item[1] + item[5]/2 for item in nms_filtered if (item[1] + item[5]/2) < zone_boundary_y]
    lower_centers_y = [item[1] + item[5]/2 for item in nms_filtered if (item[1] + item[5]/2) >= zone_boundary_y]
    
    ideal_upper_y = np.median(upper_centers_y) if upper_centers_y else int(zone_boundary_y / 2)
    ideal_lower_y = np.median(lower_centers_y) if lower_centers_y else int(zone_boundary_y + (img_h - zone_boundary_y) / 2)

    cv2.line(debug_image, (0, int(ideal_upper_y)), (img_w, int(ideal_upper_y)), (0, 255, 255), 1)
    cv2.line(debug_image, (0, int(ideal_lower_y)), (img_w, int(ideal_lower_y)), (0, 255, 255), 1)

    # Bestimme die Referenzgröße vom besten Treffer
    best_detected_icon = nms_filtered[0]
    detected_width_ref = best_detected_icon[4]  
    detected_height_ref = best_detected_icon[5] 
    
    # DYNAMISCHE RAS-SCHRITTWEITE: Wir messen den realen Abstand der ersten beiden Icons von links
    all_sorted_by_x = sorted(nms_filtered, key=lambda item: item[0])
    start_x = all_sorted_by_x[0][0]
    
    grid_step = detected_width_ref * 1.16  # Fallback-Wert
    if len(all_sorted_by_x) >= 2:
        # Berechne den echten Abstand zwischen dem ersten und zweiten Icon auf der X-Achse
        real_diff = all_sorted_by_x[1][0] - all_sorted_by_x[0][0]
        # Wenn der Abstand plausibel ist, nutzen wir ihn als exaktes Schrittmaß
        if detected_width_ref < real_diff < (detected_width_ref * 1.5):
            grid_step = real_diff

    # Toleranzfenster deutlich vergrößern, um rechtsseitiges "Wandern" abzufangen
    y_tolerance = detected_height_ref * 0.30   
    x_tolerance = grid_step * 0.48             # Auf 48% erhöht: Fängt verschobene Icons perfekt ein!

    # 4. RASTER AUFBAUEN & VALIDIEREN
    def process_row(is_upper_zone, max_slots=None):
        target_y = ideal_upper_y if is_upper_zone else ideal_lower_y
        row_icons = []
        
        for item in nms_filtered:
            x, y, icon_id, conf, w, h = item
            cy = y + h / 2
            if abs(cy - target_y) <= y_tolerance:
                if (is_upper_zone and cy < zone_boundary_y) or (not is_upper_zone and cy >= zone_boundary_y):
                    row_icons.append(item)

        if not row_icons and not max_slots:
            return [], []

        if max_slots:
            slots_count = max_slots
        else:
            max_x = max([item[0] for item in row_icons]) if row_icons else start_x
            slots_count = int(round((max_x - start_x) / grid_step)) + 1
            if slots_count < 3: slots_count = 3 

        row_ids = []
        row_visuals = []

        for slot in range(slots_count):
            perfect_x = int(start_x + (slot * grid_step))
            
            match_found = None
            # Suche das Icon, das diesem Rasterpunkt am nächsten liegt
            closest_dist = 9999
            for item in row_icons:
                dist = abs(item[0] - perfect_x)
                if dist <= x_tolerance and dist < closest_dist:
                    closest_dist = dist
                    match_found = item
            
            if match_found:
                row_ids.append(match_found[2])
                row_visuals.append((match_found[0], match_found[1], match_found[2], True, match_found[4], match_found[5]))
            else:
                row_ids.append("0")
                sim_y = int(target_y - detected_height_ref / 2)
                row_visuals.append((perfect_x, sim_y, "0", False, int(detected_width_ref), int(detected_height_ref)))

        return row_ids, row_visuals

    upper_ids, upper_visual = process_row(is_upper_zone=True, max_slots=None)
    lower_ids, lower_visual = process_row(is_upper_zone=False, max_slots=4)

    # 5. ERGEBNIS UNTERSUCHEN & SENDEN
    final_ids = upper_ids + lower_ids
    if all(id_str == "0" for id_str in final_ids):
        output_string = "0\n"
        print("[-] Keine Icons auf den geometrischen Achsen gefunden.")
    else:
        output_string = ";".join(final_ids) + "\n"
        print(f"[+] Geometrisches Ergebnis synchronisiert: {output_string.strip()}")

    # Visualisierung auf dem Debug-Bild zeichnen
    for idx, (x, y, icon_id, is_real, w, h) in enumerate(upper_visual + lower_visual, 1):
        if is_real:
            cv2.rectangle(debug_image, (x, y), (x + w, y + h), (0, 255, 0), 2)
            cv2.putText(debug_image, f"ID {icon_id}", (x, y - 8), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 2)
        else:
            cv2.rectangle(debug_image, (x, y), (x + w, y + h), (0, 0, 255), 1)
            cv2.putText(debug_image, "0", (x + int(w/2) - 5, y + int(h/2) + 5), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 0, 255), 1)

    debug_filename = "debug_screenshot.png"
    cv2.imwrite(debug_filename, debug_image)
    print(f"[DEBUG] Bild wurde als '{debug_filename}' gespeichert.")
    
    send_data_string_over_serial(output_string)


def main():
    print("=== Bereinigter Helldivers 2 Stratagem Detector gestartet ===")
    print(f"Hotkey: {HOTKEY} | Monitor-Index: {MONITOR_INDEX} | Port: {SERIAL_PORT}")
    print("Bereit für die Demokratie! Warte auf F11...")
    keyboard.add_hotkey(HOTKEY, process_screenshot)
    keyboard.wait()

if __name__ == "__main__":
    main()
    