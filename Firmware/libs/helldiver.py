
from macro import Direction, Color, Macro, Macros
from machine import Pin
import keyboard
import time

# CONFIG
# https://deskthority.net/wiki/Scancode
MODIFIER = 0xe0
MODIFIER_USE_TOGGLE = False
PRESS_TIME = 40
OPEN_MENU_TIME = 500
SLEEP_TIME = 20

STATE_IDLE = 1
STATE_PRESS_WAIT = 2
STATE_RELEASE_WAIT = 3
STATE_OPEN_MENU_WAIT = 4
STATE_FINISHED = 5

DEBUG_PRINT = False

class Hellbutton():
    global btnPressed
    global btnPressedHold
    global btnPressedTimeStp

    def __init__(self, pin, macro):
        self.macro = macro
        self.pin = pin
        self.btnPressed = False
        self.btnPressedHold = False
        self.btnPressedTimeStp = time.ticks_ms()
    
    def setMacro(self, macro):
        self.macro = macro

    def getMacro(self):
        return self.macro

    def getName(self):
        return self.macro.getName()
    
    def isPressed(self, onRelease, msec):
        btnState = not self.pin.value()
        returnValue = 0
        if(onRelease == True):
            if (btnState == False):
                if(self.btnPressed == True and self.btnPressedHold == False):
                    returnValue = 1
                elif(self.btnPressed == True and self.btnPressedHold == True):
                    self.btnPressedHold = False
                self.btnPressed = False
                self.btnPressedTimeStp = time.ticks_ms()
            else:
                if (self.btnPressedTimeStp + msec) < time.ticks_ms():
                    self.btnPressedTimeStp = time.ticks_ms() + msec
                    self.btnPressedHold = True
                    returnValue = 2
                else:
                    self.btnPressed = True
        else:
            returnValue = btnState

        return returnValue

class Hellboard():
    global returnValue

    def __init__(self, keyboard):
        self.k = keyboard
        self.state = STATE_IDLE
        self.waitTime = 0
        self.macroIndex = 0

    def sendMacro (self, macro):

        returnValue = False    

        if self.state == STATE_IDLE:
            
            if(DEBUG_PRINT == True):
                print("PressKey: Modifier")
                print(str(macro) + "   Length: " + str(len(macro.getDirections())))
            self.k.press(MODIFIER)
            self.waitTime = time.ticks_ms()
            self.macroIndex = 0

            if(DEBUG_PRINT == True):
                print("Next State: STATE_OPEN_MENU_WAIT")
            self.state = STATE_OPEN_MENU_WAIT

        elif self.state == STATE_OPEN_MENU_WAIT:
            if ((self.waitTime + OPEN_MENU_TIME) <= time.ticks_ms()):
                if MODIFIER_USE_TOGGLE:
                    if(DEBUG_PRINT == True):
                        print("ReleaseKey: Modifier")
                    self.k.release(MODIFIER)
                    
                self.waitTime = -1
                if(DEBUG_PRINT == True):
                    print("Next State: STATE_PRESS_WAIT")
                self.state = STATE_PRESS_WAIT

        elif self.state == STATE_PRESS_WAIT:

            if self.waitTime == -1:
                if(DEBUG_PRINT == True):
                    print("macroIndex: " + str(self.macroIndex))
                    print("PressKey: " + str(macro.getDirections()[self.macroIndex]))
                self.k.press(macro.getDirections()[self.macroIndex])
                self.waitTime = time.ticks_ms()
            elif ((self.waitTime + PRESS_TIME) <= time.ticks_ms()):
                self.waitTime = -1
                if(DEBUG_PRINT == True):
                    print("Next State: STATE_RELEASE_WAIT")
                self.state = STATE_RELEASE_WAIT

        elif self.state == STATE_RELEASE_WAIT:

            if self.waitTime == -1:
                if(DEBUG_PRINT == True):
                    print("ReleaseKey: " + str(macro.getDirections()[self.macroIndex]))
                self.k.release(macro.getDirections()[self.macroIndex])
                self.waitTime = time.ticks_ms()
            elif ((self.waitTime + SLEEP_TIME) <= time.ticks_ms()):
                if self.macroIndex < (len(macro.getDirections())-1):
                    self.macroIndex = self.macroIndex + 1
                    self.waitTime = -1
                    if(DEBUG_PRINT == True):
                        print("Next State: STATE_PRESS_WAIT")
                    self.state = STATE_PRESS_WAIT
                else:
                    if(DEBUG_PRINT == True):
                        print("Next State: STATE_FINISHED")
                    self.state = STATE_FINISHED
        else:
            if not MODIFIER_USE_TOGGLE:
                if(DEBUG_PRINT == True):
                    print("ReleaseKey: Modifier")
                self.k.release(MODIFIER)
            if(DEBUG_PRINT == True): 
                print("Next State: STATE_IDLE")
            returnValue = True
            self.state = STATE_IDLE

        return returnValue