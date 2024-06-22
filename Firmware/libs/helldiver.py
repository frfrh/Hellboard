
from macro import Direction, Color, Macro, Macros
from machine import Pin
import keyboard
import time

# CONFIG
# https://deskthority.net/wiki/Scancode
MODIFIER = 0xe0
CALL_CYCLE_TIME_MS = 5
MODIFIER_USE_TOGGLE = False
PRESS_TIME = 50
OPEN_MENU_TIME = 500
SLEEP_TIME = 50

STATE_IDLE = 1
STATE_PRESS_WAIT = 2
STATE_RELEASE_WAIT = 3
STATE_OPEN_MENU_WAIT = 4
STATE_FINISHED = 5

DEBUG_PRINT = False

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
            self.waitTime = PRESS_TIME
            self.macroIndex = 0

            if(DEBUG_PRINT == True):
                print("Next State: STATE_OPEN_MENU_WAIT")
            self.state = STATE_OPEN_MENU_WAIT

        elif self.state == STATE_OPEN_MENU_WAIT:
            if self.waitTime > 0:
                self.waitTime = self.waitTime - CALL_CYCLE_TIME_MS
            else:
                if MODIFIER_USE_TOGGLE:
                    if(DEBUG_PRINT == True):
                        print("ReleaseKey: Modifier")
                    self.k.release(MODIFIER)
                    
                self.waitTime = -1
                if(DEBUG_PRINT == True):
                    print("Next State: STATE_PRESS_WAIT")
                self.state = STATE_PRESS_WAIT

        elif self.state == STATE_PRESS_WAIT:

            if self.waitTime > 0:
                self.waitTime = self.waitTime - CALL_CYCLE_TIME_MS
            else:
                if self.waitTime == -1:
                    if(DEBUG_PRINT == True):
                        print("macroIndex: " + str(self.macroIndex))
                        print("PressKey: " + str(macro.getDirections()[self.macroIndex]))
                    self.k.press(macro.getDirections()[self.macroIndex])
                    self.waitTime = PRESS_TIME
                else:
                    self.waitTime = -1
                    if(DEBUG_PRINT == True):
                        print("Next State: STATE_RELEASE_WAIT")
                    self.state = STATE_RELEASE_WAIT

        elif self.state == STATE_RELEASE_WAIT:

                if self.waitTime > 0:
                    self.waitTime = self.waitTime - CALL_CYCLE_TIME_MS
                else:
                    if self.waitTime == -1:
                        if(DEBUG_PRINT == True):
                            print("ReleaseKey: " + str(macro.getDirections()[self.macroIndex]))
                        self.k.release(macro.getDirections()[self.macroIndex])
                        self.waitTime = SLEEP_TIME
                    else:
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
        # self.k.press(MODIFIER)
        # time.sleep_ms(PRESS_TIME)
        # if MODIFIER_USE_TOGGLE:
        #     self.k.release(MODIFIER)
        #     time.sleep_ms(SLEEP_TIME)

        # time.sleep_ms(OPEN_MENU_TIME)

        # for direction in macro.getDirections():
        #     self.k.press(direction)
        #     time.sleep_ms(PRESS_TIME)
        #     self.k.release(direction)
        #     time.sleep_ms(SLEEP_TIME)

        # if not MODIFIER_USE_TOGGLE:
        #     self.k.release(MODIFIER)