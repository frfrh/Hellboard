import os
from PySide6.QtWidgets import QDialog, QVBoxLayout, QListWidget, QListWidgetItem, QPushButton, QLabel
from PySide6.QtGui import QPixmap, QColor, QIcon, QPainter, QPen
from PySide6.QtCore import Qt, QSize, Signal

# Importiert den Icon-Ordner-Scanner aus deiner config_core.py
from config_core import load_icons

class IconSelectorDialog(QDialog):
    """Das Popup-Fenster zur manuellen Stratagem-Auswahl."""
    def __init__(self, current_id, parent=None):
        super().__init__(parent)
        self.setWindowTitle("Stratagem auswählen")
        self.resize(400, 500)
        self.selected_id = current_id
        
        layout = QVBoxLayout(self)
        self.list_widget = QListWidget()
        self.list_widget.setIconSize(QSize(48, 48))
        layout.addWidget(self.list_widget)
        
        # Leerer Slot (0)
        empty_item = QListWidgetItem("000 - Leerer Slot / Nicht belegt")
        empty_pix = QPixmap(48, 48)
        empty_pix.fill(QColor(140, 30, 30))
        empty_item.setIcon(QIcon(empty_pix))
        empty_item.setData(Qt.UserRole, "0")
        self.list_widget.addItem(empty_item)
        if current_id == "0": 
            self.list_widget.setCurrentItem(empty_item)

        for icon_data in load_icons():
            item = QListWidgetItem(f"{icon_data['id']} - {icon_data['name']}")
            item.setIcon(QIcon(icon_data['pixmap']))
            item.setData(Qt.UserRole, icon_data['id'])
            self.list_widget.addItem(item)
            if icon_data['id'] == current_id:
                self.list_widget.setCurrentItem(item)
                
        self.list_widget.itemDoubleClicked.connect(self.accept_selection)
        btn_ok = QPushButton("Auswählen")
        btn_ok.clicked.connect(self.accept_selection)
        layout.addWidget(btn_ok)

    def accept_selection(self):
        current_item = self.list_widget.currentItem()
        if current_item:
            self.selected_id = current_item.data(Qt.UserRole)
        self.accept()

class ClickableIconWidget(QLabel):
    """Die interaktive Icon-Zelle für das HUD-Ergebnis-Raster."""
    clicked = Signal(str)

    def __init__(self, slot_index, current_id, size, parent=None):
        super().__init__(parent)
        self.slot_index = slot_index
        self.current_id = current_id
        self.size_px = size
        self.setFixedSize(size, size)
        self.setAlignment(Qt.AlignCenter)
        self.update_visual()

    def update_id(self, new_id):
        self.current_id = new_id
        self.update_visual()

    def update_visual(self):
        if self.current_id == "0":
            pix = QPixmap(self.size_px, self.size_px)
            pix.fill(QColor(40, 20, 20))
            painter = QPainter(pix)
            painter.setPen(QPen(QColor(200, 0, 0), 2))
            painter.drawRect(1, 1, self.size_px-2, self.size_px-2)
            painter.setPen(QColor(200, 0, 0))
            painter.drawText(pix.rect(), Qt.AlignCenter, "0")
            painter.end()
            self.setPixmap(pix)
        else:
            found = False
            for icon_data in load_icons():
                if icon_data['id'] == self.current_id:
                    scaled_pix = icon_data['pixmap'].scaled(self.size_px, self.size_px, Qt.KeepAspectRatio, Qt.SmoothTransformation)
                    self.setPixmap(scaled_pix)
                    found = True
                    break
            if not found:
                self.current_id = "0"
                self.update_visual()

    def mousePressEvent(self, event):
        if event.button() == Qt.LeftButton:
            dialog = IconSelectorDialog(self.current_id, self)
            if dialog.exec() == QDialog.Accepted:
                self.update_id(dialog.selected_id)
                self.clicked.emit(self.current_id)
