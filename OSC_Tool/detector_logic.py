import time
import cv2
import mss
import numpy as np
import serial
from PIL import Image
from config_core import load_icons, BORDER_TO_CROP, SCALE_MIN, SCALE_MAX, SCALE_STEPS, DYNAMIC_THRESHOLD

DEBUG_PRINT = False

def send_serial(port, data, existing_ser=None):
    """
    Sendet Daten an den COM-Port. Nutzt bevorzugt eine dauerhaft offene 
    Verbindung (existing_ser), um das Puffer-Hinterherhinken zu eliminieren.
    """
    try:
        import serial
        # Wenn eine dauerhaft offene Verbindung uebergeben wurde
        if existing_ser and existing_ser.is_open:
            existing_ser.write(data.encode("utf-8"))
            existing_ser.flush()
            print(f"[SERIAL LOGLINK] -> Live gesendet (Direktkanal): {data.strip()}")
            return True
            
        # Fallback, falls der Port temporaer geoeffnet werden muss
        ser = serial.Serial()
        ser.port = port
        ser.baudrate = 9600
        ser.timeout = 1
        ser.dtr = True
        ser.rts = True
        ser.open()
        ser.write(data.encode("utf-8"))
        ser.flush()
        import time
        time.sleep(0.04)
        ser.close()
        print(f"[SERIAL FALLBACK] -> Gesendet: {data.strip()}")
        return True
    except Exception as e:
        print(f"[-] COM-Port Fehler bei {port}: {e}")
        return False

def run_detection_core(config):
    """Führt die Bildanalyse aus, normalisiert die Auflösung auf 4K-Niveau und speichert das Debug-Bild."""
    icons = load_icons()
    if not icons:
        return [], []

    monitor_idx = config.get("monitor", 1)
    bbox = config.get("bbox", None)
    
    if not bbox:
        return [], []

    with mss.MSS() as sct:
        if monitor_idx >= len(sct.monitors): monitor_idx = 1
        monitor = sct.monitors[monitor_idx]
        sct_img = sct.grab(monitor)
        full_img = Image.frombytes("RGB", sct_img.size, sct_img.bgra, "raw", "BGRX")
        mon_w, mon_h = full_img.size

        # Absicherung der Box-Grenzen auf dem physischen Monitor
        x1 = max(0, min(bbox["x1"], mon_w - 10))
        y1 = max(0, min(bbox["y1"], mon_h - 10))
        x2 = max(x1 + 10, min(bbox["x2"], mon_w))
        y2 = max(y1 + 10, min(bbox["y2"], mon_h))

        cropped = full_img.crop((x1, y1, x2, y2))
        screen_gray_raw = cv2.cvtColor(np.array(cropped), cv2.COLOR_RGB2GRAY)
        
    # ==============================================================================
    # NEU: RESOLUTION NORMALIZATION (Skalierung auf 4K-Basis)
    # ==============================================================================
    # Wir berechnen, wie stark der aktuelle Monitor von einem 4K-Referenzmonitor abweicht
    # Referenz-4K-Breite: 3840 Pixel
    scale_factor_to_4k = 3840.0 / mon_w
    
    # Berechne die neue, hochskalierte Zielgröße für den Bildausschnitt
    target_img_w = int(screen_gray_raw.shape[1] * scale_factor_to_4k)
    target_img_h = int(screen_gray_raw.shape[0] * scale_factor_to_4k)
    
    # Hochskalieren mit INTER_CUBIC (erhält Kanten und Details bei Vergrößerung am besten)
    screen_gray = cv2.resize(screen_gray_raw, (target_img_w, target_img_h), interpolation=cv2.INTER_CUBIC)
    # ==============================================================================
        
    debug_image = cv2.cvtColor(screen_gray, cv2.COLOR_GRAY2BGR)
    img_h, img_w = screen_gray.shape[:2]
    
    # Trennlinie bei exakt 42% der Bildhöhe des normalisierten Ausschnitts
    zone_boundary_y = int(img_h * 0.42)
    cv2.line(debug_image, (0, zone_boundary_y), (img_w, zone_boundary_y), (255, 0, 0), 1)

    upper_raw = []
    lower_raw = []
    base_template_size = 256 - (2 * BORDER_TO_CROP)

    # Alle Rohdaten sammeln und nach Y-Koordinate trennen
    for icon in icons:
        template = icon["image"]
        best_val, best_loc = -1, None
        best_w, best_h = base_template_size, base_template_size
        best_scale_found = 1.0
        
        for scale in np.linspace(SCALE_MIN, SCALE_MAX, SCALE_STEPS):
            new_w, new_h = int(base_template_size * scale), int(base_template_size * scale)
            if new_h > img_h or new_w > img_w: continue
                
            resized = cv2.resize(template, (new_w, new_h), interpolation=cv2.INTER_AREA)
            res = cv2.matchTemplate(screen_gray, resized, cv2.TM_CCOEFF_NORMED)
            _, max_val, _, max_loc = cv2.minMaxLoc(res)
            
            if max_val > best_val:
                best_val, best_loc, best_w, best_h = max_val, max_loc, new_w, new_h
                best_scale_found = scale
                
        if best_loc is not None:
            x_loc, y_loc = best_loc
            cy = y_loc + best_h / 2
            
            if cy < zone_boundary_y:
                if best_val >= DYNAMIC_THRESHOLD:
                    upper_raw.append((x_loc, y_loc, icon["id"], best_val, best_w, best_h, best_scale_found))
            else:
                if best_val >= (DYNAMIC_THRESHOLD - 0.05):
                    lower_raw.append((x_loc, y_loc, icon["id"], best_val, best_w, best_h, best_scale_found))

    # Funktion zur Filterung und Gitter-Zuweisung pro Reihe
    def process_individual_row(row_raw_detections, is_upper, max_slots=None):
        if not row_raw_detections:
            if max_slots:
                return ["0"] * max_slots, [(0, 0, "0", False, 60, 60)] * max_slots
            return ["0"] * 3, [(0, 0, "0", False, 60, 60)] * 3

        row_raw_detections.sort(key=lambda item: item[3], reverse=True)
        best_row_item = row_raw_detections[0]
        row_ideal_w = best_row_item[4]
        row_ideal_h = best_row_item[5]

        size_locked = []
        for item in row_raw_detections:
            if abs(item[4] - row_ideal_w) <= (row_ideal_w * 0.15):
                size_locked.append(item)

        row_nms = []
        for item in size_locked:
            x, y, icon_id, conf, w, h, sc = item
            cx, cy = x + w / 2, y + h / 2
            overlap = False
            for d_x, d_y, _, _, d_w, d_h, _ in row_nms:
                if np.sqrt((cx - (d_x + d_w/2))**2 + (cy - (d_y + d_h/2))**2) < (w / 2):
                    overlap = True
                    break
            if not overlap:
                row_nms.append(item)

        if not row_nms:
            if max_slots: return ["0"] * max_slots, []
            return ["0"] * 3, []

        centers_y = [item[1] + item[5]/2 for item in row_nms]
        target_y = np.median(centers_y)
        cv2.line(debug_image, (0, int(target_y)), (img_w, int(target_y)), (0, 255, 255), 1)

        grid_step = row_ideal_w * 1.18
        sorted_x = sorted(row_nms, key=lambda item: item[0])
        start_x = sorted_x[0][0]
        if len(sorted_x) >= 2:
            real_diff = sorted_x[1][0] - sorted_x[0][0]
            if row_ideal_w < real_diff < (row_ideal_w * 1.5):
                grid_step = real_diff

        y_tolerance = row_ideal_h * 0.30
        x_tolerance = grid_step * 0.48

        if max_slots:
            slots_count = max_slots
        else:
            max_x = sorted_x[-1][0]
            slots_count = int(round((max_x - start_x) / grid_step)) + 1
            if slots_count < 3: slots_count = 3
            if slots_count > 7: slots_count = 7

        row_ids = []
        row_visuals = []

        for slot in range(slots_count):
            perfect_x = int(start_x + (slot * grid_step))
            match_found = None
            closest = 9999
            
            for item in row_nms:
                if abs(item[0] - perfect_x) <= x_tolerance and abs(item[0] - perfect_x) < closest:
                    closest = abs(item[0] - perfect_x)
                    match_found = item
                    
            if match_found:
                row_ids.append(match_found[2])
                row_visuals.append((match_found[0], match_found[1], match_found[2], True, match_found[4], match_found[5]))
            else:
                row_ids.append("0")
                row_visuals.append((perfect_x, int(target_y - row_ideal_h/2), "0", False, row_ideal_w, row_ideal_h))

        return row_ids, row_visuals

    # Untere Reihe starr im 5er-Raster einteilen
    def process_lower_row_fixed_5(raw_detections):
        slots_count = 4 
        row_ids = []
        row_visuals = []
        col_width = img_w / 5.0
        
        lower_y_centers = [item[1] + item[5] / 2 for item in raw_detections]
        target_y = np.median(lower_y_centers) if lower_y_centers else int(zone_boundary_y + (img_h - zone_boundary_y) / 2)
        cv2.line(debug_image, (0, int(target_y)), (img_w, int(target_y)), (0, 255, 255), 1)
        
        if raw_detections:
            raw_detections.sort(key=lambda item: item[3], reverse=True)
            row_ideal_w = raw_detections[0][4]
            row_ideal_h = raw_detections[0][5]
        else:
            row_ideal_w = int(base_template_size * 0.8)
            row_ideal_h = int(base_template_size * 0.8)
        
        row_nms = []
        for item in raw_detections:
            x, y, icon_id, conf, w, h, sc = item
            cx, cy = x + w / 2, y + h / 2
            if abs(w - row_ideal_w) <= (row_ideal_w * 0.15):
                if not any(np.sqrt((cx - (d[0]+d[4]/2))**2 + (cy - (d[1]+d[5]/2))**2) < (w/2) for d in row_nms):
                    row_nms.append(item)

        for slot in range(slots_count):
            perfect_center_x = int((slot * col_width) + (col_width / 2))
            perfect_x = int(perfect_center_x - (row_ideal_w / 2))
            
            match_found = None
            for item in row_nms:
                if (slot * col_width) <= (item[0] + item[4] / 2) < ((slot + 1) * col_width):
                    match_found = item
                    break
            
            if match_found:
                row_ids.append(match_found[2])
                row_visuals.append((match_found[0], match_found[1], match_found[2], True, match_found[4], match_found[5]))
            else:
                row_ids.append("0")
                row_visuals.append((perfect_x, int(target_y - row_ideal_h/2), "0", False, row_ideal_w, row_ideal_h))
                
        return row_ids, row_visuals

    # Beide Reihen auswerten
    upper_ids, upper_vis = process_individual_row(upper_raw, is_upper=True, max_slots=None)
    lower_ids, lower_vis = process_lower_row_fixed_5(lower_raw)

    if(DEBUG_PRINT == True):
        # Boxen und Confidence auf das Debug-Bild zeichnen
        for x, y, icon_id, is_real, w, h in (upper_vis + lower_vis):
            if w > 0 and h > 0:
                color = (0, 255, 0) if is_real else (0, 0, 255)
                cv2.rectangle(debug_image, (x, y), (x + w, y + h), color, 2 if is_real else 1)
                
                conf_val = 0.0

                if is_real:
                    # Suche den passenden Wert aus den gefilterten Rohdaten heraus
                    all_detections = upper_raw + lower_raw
                    for item in all_detections:
                        if item[2] == icon_id and abs(item[0] - x) < 5:
                            conf_val = item[3] # Index 3 ist der Match-Wert (best_val)
                            break
                
                # Text formatieren: Wenn erkannt -> "ID: 101 (0.85)", wenn leer -> "0"
                if is_real and conf_val > 0:
                    text = f"ID:{icon_id} ({conf_val:.2f})"
                else:
                    text = icon_id
                    
                # Text auf das Bild schreiben
                cv2.putText(debug_image, text, (x + 5, y - 5 if is_real else y + 20), 
                            cv2.FONT_HERSHEY_SIMPLEX, 0.4, color, 1)

        # DEBUG-BILD SPEICHERN
        cv2.imwrite("debug_screenshot.png", debug_image)
    
    return upper_ids, lower_ids
