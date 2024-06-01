
from macro import Direction, Color, Macro, Macros
from machine import Pin
import keyboard
import time

# CONFIG
# https://deskthority.net/wiki/Scancode
modifier = 0x06
modifierUseToggle = False
sleepTime = 20

class Hellbutton():
    def __init__(self, pin, macro):
        self.macro = macro
        self.pin = pin
    
    def setMacro(self, macro):
        self.macro = macro

    def getMacro(self):
        return self.macro

    def getName(self):
        return self.macro.getName()
    
    def isPressed(self):
        return not self.pin.value()
        

class Hellboard():
    def __init__(self, keyboard):
        self.k = keyboard

    def sendMacro (self, macro):
        self.k.press(modifier)
        time.sleep_ms(sleepTime)
        if modifierUseToggle:
            self.k.release(modifier)
            time.sleep_ms(sleepTime)

        for direction in macro.getDirections():
            self.k.press(direction)
            time.sleep_ms(sleepTime)
            self.k.release(direction)
            time.sleep_ms(sleepTime)

        if not modifierUseToggle:
            self.k.release(modifier)