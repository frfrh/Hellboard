import sys
import os
import json
import time
import mss
import keyboard
from PIL import Image
from PySide6.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout, 
                             QHBoxLayout, QPushButton, QLabel, QComboBox, 
                             QMessageBox, QScrollArea)
from PySide6.QtGui import QPixmap, QImage, QPainter, QPen, QColor
from PySide6.QtCore import Qt, QRect, QPoint, Signal

# Modulare Imports aus den anderen Dateien
from config_core import CONFIG_FILE, load_icons
from detector_logic import run_detection_core, send_serial
from gui_widgets import ClickableIconWidget

# ==============================================================================
# DEFINES / KONSTANTEN FÜR DIE GUI-ICON-GRÖSSEN
# ==============================================================================
UPPER_ICON_SIZE = 85      # Groesse der Icons in der oberen Reihe
LOWER_ICON_SIZE = 128     # Groesse der Icons in der unteren Reihe
# ==============================================================================

class BoundingBoxLabel(QLabel):
    """Erlaubt das Zeichnen des Suchrahmens per Maus."""
    def __init__(self, parent=None):
        super().__init__(parent)
        self.start_point = QPoint()
        self.end_point = QPoint()
        self.is_drawing = False
        self.current_rect = QRect()

    def mousePressEvent(self, event):
        if event.button() == Qt.LeftButton:
            self.start_point = event.position().toPoint()
            self.end_point = self.start_point
            self.is_drawing = True
            self.update()

    def mouseMoveEvent(self, event):
        if self.is_drawing:
            self.end_point = event.position().toPoint()
            self.update()

    def mouseReleaseEvent(self, event):
        if event.button() == Qt.LeftButton and self.is_drawing:
            self.end_point = event.position().toPoint()
            self.is_drawing = False
            self.current_rect = QRect(self.start_point, self.end_point).normalized()
            self.update()
            self.window().save_crop_area(self.current_rect)

    def paintEvent(self, event):
        super().paintEvent(event)
        if self.is_drawing or not self.current_rect.isEmpty():
            painter = QPainter(self)
            pen = QPen(QColor(0, 255, 0), 2, Qt.DashLine)
            painter.setPen(pen)
            rect_to_draw = QRect(self.start_point, self.end_point).normalized() if self.is_drawing else self.current_rect
            painter.drawRect(rect_to_draw)
            painter.end()

class MainWindow(QMainWindow):
    """Das Hauptfenster der Anwendung mit dauerhaft offener serieller Pipeline."""
    trigger_ui_update = Signal(tuple)

    def __init__(self):
        super().__init__()
        self.setWindowTitle("OSC Tool")
        self.resize(0, 0)
        
        self.config = {
            "monitor": 1, 
            "port": "COM3", 
            "bbox": None,
            "upper_saved_ids": ["0"] * 7
        }
        self.load_config_file()
        
        # Verbindungsobjekt fuer den Dauerkanal initialisieren
        self.serial_connection = None
        
        self.current_upper_ids = self.config.get("upper_saved_ids", ["0"] * 7)
        while len(self.current_upper_ids) < 7:
            self.current_upper_ids.append("0")
            
        self.current_lower_ids = ["0"] * 4

        widget = QWidget()
        self.layout = QVBoxLayout(widget)
        self.layout.setSpacing(4)
        self.layout.setContentsMargins(6, 6, 6, 6)
        self.setCentralWidget(widget)

        # Controls-Leiste
        controls_layout = QHBoxLayout()
        controls_layout.addWidget(QLabel("Monitor:"))
        self.monitor_select = QComboBox()
        with mss.MSS() as sct:
            for i in range(1, len(sct.monitors)):
                self.monitor_select.addItem(f"Monitor {i}", i)
        self.monitor_select.setCurrentIndex(max(0, self.config.get("monitor", 1) - 1))
        controls_layout.addWidget(self.monitor_select)

        controls_layout.addWidget(QLabel("COM-Port:"))
        self.port_select = QComboBox()
        for i in range(1, 13): self.port_select.addItem(f"COM{i}")
        self.port_select.setCurrentText(self.config.get("port", "COM3"))
        # NEU: Wenn der Benutzer den Port in der GUI wechselt, oeffnen wir den Kanal neu
        self.port_select.currentTextChanged.connect(self.init_permanent_serial)
        controls_layout.addWidget(self.port_select)

        self.btn_preview = QPushButton("🔄 Vorschau-Screenshot")
        self.btn_preview.clicked.connect(self.take_preview)
        controls_layout.addWidget(self.btn_preview)
        self.layout.addLayout(controls_layout)

        self.info_label = QLabel("Rahme im Vorschaubild dein Stratagem-HUD ein. Nutze F11 im Spiel zum Scannen.")
        self.info_label.setStyleSheet("color: gray; font-style: italic;")
        self.layout.addWidget(self.info_label)

        self.scroll_area = QScrollArea()
        self.scroll_area.setFixedHeight(180)
        self.image_label = BoundingBoxLabel()
        self.image_label.setAlignment(Qt.AlignLeft | Qt.AlignTop)
        self.scroll_area.setWidget(self.image_label)
        self.scroll_area.setWidgetResizable(True)
        self.layout.addWidget(self.scroll_area)

        self.layout.addWidget(QLabel("<b>Erkanntes Stratagem-HUD (Anklicken zum Ändern):</b>"))
        
        # Interaktives Raster: Oben
        self.upper_hud_layout = QHBoxLayout()
        self.upper_hud_layout.setAlignment(Qt.AlignLeft)
        self.upper_widgets = []
        for i in range(7):
            w = ClickableIconWidget(i, self.current_upper_ids[i], UPPER_ICON_SIZE)
            w.clicked.connect(self.handle_manual_ui_change)
            self.upper_hud_layout.addWidget(w)
            self.upper_widgets.append(w)
        self.layout.addLayout(self.upper_hud_layout)

        # Interaktives Raster: Unten
        self.lower_hud_layout = QHBoxLayout()
        self.lower_hud_layout.setAlignment(Qt.AlignLeft)
        self.lower_widgets = []
        for i in range(4):
            w = ClickableIconWidget(i, "0", LOWER_ICON_SIZE)
            w.clicked.connect(self.handle_manual_ui_change)
            self.lower_hud_layout.addWidget(w)
            self.lower_widgets.append(w)
        self.layout.addLayout(self.lower_hud_layout)

        # Senden-Leiste
        send_layout = QHBoxLayout()
        self.string_preview_label = QLabel("ID-String: 0")
        self.string_preview_label.setStyleSheet("font-family: monospace; font-size: 14px; font-weight: bold; color: #00AA00;")
        send_layout.addWidget(self.string_preview_label, 1)

        self.btn_send_serial = QPushButton("🚀 [SERIAL] Ergebnis an COM-Port senden")
        self.btn_send_serial.setStyleSheet("background-color: #225522; color: white; font-weight: bold; padding: 10px;")
        self.btn_send_serial.clicked.connect(self.manually_trigger_send)
        send_layout.addWidget(self.btn_send_serial)
        self.layout.addLayout(send_layout)

        self.trigger_ui_update.connect(self.update_gui_hud_display)
        load_icons()
        
        keyboard.unhook_all()
        keyboard.add_hotkey("F11", self.handle_hotkey_trigger)

        with mss.MSS() as sct:
            monitor_idx = self.config.get("monitor", 1)
            if monitor_idx >= len(sct.monitors): monitor_idx = 1
            monitor = sct.monitors[monitor_idx]
            mon_w, mon_h = monitor["width"], monitor["height"]
            seg_w, seg_h = int(mon_w / 4), int(mon_h / 3)
            self.preview_offset_x = 0
            self.preview_offset_y = int(mon_h * (2 / 3))

        base_black = QPixmap(seg_w, seg_h)
        base_black.fill(QColor(0, 0, 0))
        target_w, target_h = int((self.scroll_area.width() - 25) * 1.5), int(180 * 1.5)
        if target_w <= 0: target_w = 825
        
        scaled_black = base_black.scaled(target_w, target_h, Qt.KeepAspectRatio, Qt.SmoothTransformation)
        self.scale_factor_x = seg_w / scaled_black.width()
        self.scale_factor_y = seg_h / scaled_black.height()
        self.image_label.setFixedSize(scaled_black.size())
        self.image_label.setPixmap(scaled_black)

        bbox = self.config.get("bbox", None)
        if bbox:
            gui_x1 = int((bbox["x1"] - self.preview_offset_x) / self.scale_factor_x)
            gui_y1 = int((bbox["y1"] - self.preview_offset_y) / self.scale_factor_y)
            gui_x2 = int((bbox["x2"] - self.preview_offset_x) / self.scale_factor_x)
            gui_y2 = int((bbox["y2"] - self.preview_offset_y) / self.scale_factor_y)
            self.image_label.current_rect = QRect(QPoint(gui_x1, gui_y1), QPoint(gui_x2, gui_y2))
            self.image_label.update()
            
        self.recalculate_id_string()
        # Initialisiert den permanenten COM-Port direkt beim App-Start
        self.init_permanent_serial()

    def init_permanent_serial(self):
        """Oeffnet den ausgewaehlten COM-Port dauerhaft für Echtzeit-Übertragungen."""
        import serial
        target_port = self.port_select.currentText()
        
        # Falls bereits ein alter Port offen war, schließen wir ihn ordnungsgemaess
        if self.serial_connection and self.serial_connection.is_open:
            try: self.serial_connection.close()
            except: pass
            
        try:
            self.serial_connection = serial.Serial()
            self.serial_connection.port = target_port
            self.serial_connection.baudrate = 9600
            self.serial_connection.timeout = 1
            self.serial_connection.dtr = True
            self.serial_connection.rts = True
            self.serial_connection.open()
            print(f"[✓] Echtzeit-Dauerkanal geoeffnet auf {target_port}")
        except Exception as e:
            print(f"[-] Echtzeit-Kanal blockiert bei {target_port}: {e}")
            self.serial_connection = None

    def closeEvent(self, event):
        """Wird aufgerufen, wenn die GUI geschlossen wird, um den Port freizugeben."""
        if self.serial_connection and self.serial_connection.is_open:
            self.serial_connection.close()
            print("[INFO] Echtzeit-Dauerkanal geschlossen.")
        event.accept()

    def load_config_file(self):
        if os.path.exists(CONFIG_FILE):
            try:
                with open(CONFIG_FILE, "r") as f: 
                    loaded = json.load(f)
                    self.config.update(loaded)
            except: pass

    def save_config_file(self):
        self.config["monitor"] = self.monitor_select.currentData()
        self.config["port"] = self.port_select.currentText()
        self.config["upper_saved_ids"] = self.current_upper_ids
        with open(CONFIG_FILE, "w") as f: json.dump(self.config, f)

    def take_preview(self):
        self.save_config_file()
        monitor_idx = self.config["monitor"]
        with mss.MSS() as sct:
            if monitor_idx >= len(sct.monitors): monitor_idx = 1
            monitor = sct.monitors[monitor_idx]
            sct_img = sct.grab(monitor)
            full_pil = Image.frombytes("RGB", sct_img.size, sct_img.bgra, "raw", "BGRX")
            width, height = full_pil.size
            left, top, right, bottom = 0, int(height * (2 / 3)), int(width / 4), height
            self.preview_offset_x, self.preview_offset_y = left, top
            self.preview_pil = full_pil.crop((left, top, right, bottom))
            
        bytes_img = self.preview_pil.convert("RGBA").tobytes("raw", "RGBA")
        qimg = QImage(bytes_img, self.preview_pil.width, self.preview_pil.height, QImage.Format_RGBA8888)
        self.pixmap = QPixmap.fromImage(qimg)
        
        target_w = int((self.scroll_area.width() - 25) * 1.5)
        target_h = int(180 * 1.5)
        scaled_pixmap = self.pixmap.scaled(target_w, target_h, Qt.KeepAspectRatio, Qt.SmoothTransformation)
        
        self.scale_factor_x = self.preview_pil.width / scaled_pixmap.width()
        self.scale_factor_y = self.preview_pil.height / scaled_pixmap.height()
        self.image_label.setFixedSize(scaled_pixmap.size())
        self.image_label.setPixmap(scaled_pixmap)
        
        bbox = self.config.get("bbox", None)
        if bbox:
            gui_x1 = int((bbox["x1"] - self.preview_offset_x) / self.scale_factor_x)
            gui_y1 = int((bbox["y1"] - self.preview_offset_y) / self.scale_factor_y)
            gui_x2 = int((bbox["x2"] - self.preview_offset_x) / self.scale_factor_x)
            gui_y2 = int((bbox["y2"] - self.preview_offset_y) / self.scale_factor_y)
            self.image_label.current_rect = QRect(QPoint(gui_x1, gui_y1), QPoint(gui_x2, gui_y2))
            self.image_label.update()

    def save_crop_area(self, rect):
        if rect.isEmpty(): return
        global_x1 = int(rect.left() * self.scale_factor_x) + self.preview_offset_x
        global_y1 = int(rect.top() * self.scale_factor_y) + self.preview_offset_y
        global_x2 = int(rect.right() * self.scale_factor_x) + self.preview_offset_x
        global_y2 = int(rect.bottom() * self.scale_factor_y) + self.preview_offset_y
        self.config["bbox"] = {"x1": global_x1, "y1": global_y1, "x2": global_x2, "y2": global_y2}
        self.save_config_file()
        self.info_label.setText(f"[✓] Suchbereich definiert: ({global_x1}, {global_y1})")

    def handle_hotkey_trigger(self):
        upper, lower = run_detection_core(self.config)
        # Der In-Game-Hotkey sendet weiterhin sofort das Ergebnis an den COM-Port
        final_ids = self.current_upper_ids + lower
        send_string = "#" + ";".join(final_ids) + "\r"
        send_serial(self.config["port"], send_string, self.serial_connection)
        self.trigger_ui_update.emit((upper, lower))

    def update_gui_hud_display(self, data_tuple):
        _, lower_ids = data_tuple
        self.current_lower_ids = lower_ids
        for i in range(4):
            icon_id = lower_ids[i] if i < len(lower_ids) else "0"
            self.lower_widgets[i].update_id(icon_id)
        self.recalculate_id_string()

    def handle_manual_ui_change(self):
        """Wird ausgeloest, wenn du ein Icon oben manuell anklickst. Sichert nur lokal und rückt auf."""
        raw_upper = [w.current_id for w in self.upper_widgets]
        active_upper = [uid for uid in raw_upper if uid != "0"]
        while len(active_upper) < 7:
            active_upper.append("0")
            
        for i in range(7):
            self.upper_widgets[i].update_id(active_upper[i])
            
        self.current_upper_ids = active_upper
        self.current_lower_ids = [w.current_id for w in self.lower_widgets]
        
        # Sichert die Auswahl lokal in der Config und aktualisiert die Textzeile unten
        self.save_config_file()
        self.recalculate_id_string()
        # KORRIGIERT: Das automatische Absenden an dieser Stelle wurde restlos entfernt!

    def recalculate_id_string(self):
        """Baut den ID-String mit dem '#' Header zusammen."""
        final_ids = self.current_upper_ids + self.current_lower_ids
        if not final_ids or all(i == "0" for i in final_ids):
            self.current_built_string = "#0;0;0;0;0;0;0;0;0;0;0\r"
        else:
            self.current_built_string = "#" + ";".join(final_ids) + "\r"
        self.string_preview_label.setText(f"ID-String: {self.current_built_string.strip()}")

    def manually_trigger_send(self):
        self.save_config_file()
        self.recalculate_id_string()
        # Sendet die Daten jetzt exklusiv nur noch hier ueber den offenen Echtzeit-Dauerkanal
        if send_serial(self.config["port"], self.current_built_string, self.serial_connection):
            QMessageBox.information(self, "Erfolgreich", "Daten übermittelt!")
        else:
            QMessageBox.critical(self, "Fehler", "Verbindung fehlgeschlagen.")

if __name__ == "__main__":
    app = QApplication(sys.argv)
    window = MainWindow()
    window.show()
    sys.exit(app.exec())
