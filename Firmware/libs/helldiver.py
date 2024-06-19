
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
    global btnPressed

    def __init__(self, pin, macro):
        self.macro = macro
        self.pin = pin
        self.btnPressed = False
    
    def setMacro(self, macro):
        self.macro = macro

    def getMacro(self):
        return self.macro

    def getName(self):
        return self.macro.getName()
    
    def isPressed(self, onRelease):
        btnState = not self.pin.value()
        returnValue = False
        if(onRelease == True):
            if (btnState == False):
                if(self.btnPressed == True):
                    returnValue = True
                self.btnPressed = False
            else:
                self.btnPressed = True
        else:
            returnValue = btnState

        return returnValue
        

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