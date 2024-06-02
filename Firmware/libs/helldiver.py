
from macro import Direction, Color, Macro, Macros
from machine import Pin
import keyboard
import time

# CONFIG
# https://deskthority.net/wiki/Scancode
modifier = 0xe0
modifierUseToggle = False
pressTime = 50
openMenueTime = 500
sleepTime = 100

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
        time.sleep_ms(pressTime)
        if modifierUseToggle:
            self.k.release(modifier)
            time.sleep_ms(sleepTime)

        time.sleep_ms(openMenueTime)

        for direction in macro.getDirections():
            self.k.press(direction)
            time.sleep_ms(pressTime)
            self.k.release(direction)
            time.sleep_ms(sleepTime)

        if not modifierUseToggle:
            self.k.release(modifier)