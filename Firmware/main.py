
from machine import Pin, I2C, SPI
from ssd1306 import SSD1306_I2C
import keyboard
from helldiver import Hellboard, Hellbutton
from macro import Macros, Color
import framebuf
import time

k = keyboard.Keyboard()
board = Hellboard(k)

WIDTH = 128 # oled display width
HEIGHT = 64 # oled display height

BRIGHTNESS = 0x50
BRIGHTNESS_CONFIRM = 0x80

b1_4 = Pin(0, Pin.IN, Pin.PULL_UP)
b1_3 = Pin(1, Pin.IN, Pin.PULL_UP)
b1_2 = Pin(2, Pin.IN, Pin.PULL_UP)
b1_1 = Pin(3, Pin.IN, Pin.PULL_UP)

b2_1 = Pin(29, Pin.IN, Pin.PULL_UP)
b2_2 = Pin(28, Pin.IN, Pin.PULL_UP)
b2_3 = Pin(27, Pin.IN, Pin.PULL_UP)
b2_4 = Pin(26, Pin.IN, Pin.PULL_UP)

button_1_1 = Hellbutton(b1_1, Macros.Reinforce)
button_1_2 = Hellbutton(b1_2, Macros.Resupply)
button_1_3 = Hellbutton(b1_3, Macros.Reinforce)
button_1_4 = Hellbutton(b1_4, Macros.CYCLE)

button_2_1 = Hellbutton(b2_1, Macros.GrenadeLauncher)
button_2_2 = Hellbutton(b2_2, Macros.ShieldGeneratorPack)
button_2_3 = Hellbutton(b2_3, Macros.MortarSentry)
button_2_4 = Hellbutton(b2_4, Macros.Eagle500KGBomb)

row1List = []
row1List.append (Macros.SOSBeacon)
row1List.append (Macros.EagleRearm)
row1List.append (Macros.SuperEarthFlag)
row1List.append (Macros.SEAFArtilery)
row1List.append (Macros.UploadData)
row1List.append (Macros.SeismicProbe)
row1List.append (Macros.Hellbomb)
row1List.append (Macros.OrbitalIlluminationFlare)

colors = {}

time.sleep_ms(100)
displayReady = False
try:
    i2c = I2C(1, sda=Pin(14), scl=Pin(15))  # Init I2C using I2C0 defaults, SCL=Pin(GP9), SDA=Pin(GP8), freq=400000
    oled = SSD1306_I2C(WIDTH, HEIGHT, i2c, addr=0x3C) # Init oled display
    time.sleep_ms(100)
    oled.rotate(1)
    print("Display initialized")
    displayReady = True
except Exception:
    print("Display error " + str(Exception))

# Initialize SPI
spiLed = SPI(1,
            baudrate=2500000,
            polarity=1,
            phase=1,
            bits=8,
            firstbit=SPI.MSB,
            sck=Pin(10),
            mosi=Pin(11),
            miso=Pin(12))

# 1/2 mix
def byteToData(byte):
    # Initialize all `0`
    data = bytearray(b'\x92\x49\x24')
    data[0] |= 0x01 if (byte & 0x20) else 0
    data[0] |= 0x08 if (byte & 0x40) else 0
    data[0] |= 0x40 if (byte & 0x80) else 0
    data[1] |= 0x04 if (byte & 0x08) else 0
    data[1] |= 0x20 if (byte & 0x10) else 0
    data[2] |= 0x02 if (byte & 0x01) else 0
    data[2] |= 0x10 if (byte & 0x02) else 0
    data[2] |= 0x80 if (byte & 0x04) else 0
    return data

def rbgToData(r, g, b):
    data = bytearray()
    # print ("Adding R:" + str(r) + ", G:" + str(g) + ", B:" + str(b))
    data.extend(byteToData(g))
    data.extend(byteToData(r))
    data.extend(byteToData(b))
    return data

def intToData(color):
    # print ("Adding color: 0x{0:02x}".format(color))
    return rbgToData(((color>>16)&0xFF),
                     ((color>>8)&0xFF),
                     ((color)&0xFF))

buttons = []
buttons.append(button_1_4)
buttons.append(button_1_3)
buttons.append(button_1_2)
buttons.append(button_1_1)
buttons.append(button_2_1)
buttons.append(button_2_2)
buttons.append(button_2_3)
buttons.append(button_2_4)

index = 0
lastTime = time.ticks_ms()
switchTimeout = 300
brightnessConfirm = (BRIGHTNESS_CONFIRM<<16) + (BRIGHTNESS_CONFIRM<<8) + (BRIGHTNESS_CONFIRM)

def doButtons():
    global index
    global lastTime
    pressed = False
    for button in buttons:
        if button.isPressed():
            if button.getMacro() == Macros.CYCLE:
                if (lastTime + switchTimeout) < time.ticks_ms():
                    lastTime = time.ticks_ms()
                    index = index + 1
                    if index >= len(row1List):
                        index = 0
                    pressed = True
                    button_1_3.setMacro(row1List[index])
                    print("Button 1-3 set to: \"" + button_1_3.getMacro().getName() + "\"")
            else:
                print(button.getMacro())
                board.sendMacro(button.getMacro())
                pressed = True

    return pressed

def doDisplay():
    global displayReady
    if displayReady:
        oled.fill(0)
        oled.text(button_1_3.getName(),0,4)
        oled.text("----------------",0,18)
        oled.text(button_2_1.getName(),0,24)
        oled.text(button_2_2.getName(),0,34)
        oled.text(button_2_3.getName(),0,44)
        oled.text(button_2_4.getName(),0,54)
        oled.show()

def doLed(buttonPressed):
    global brightnessConfirm
    data = bytearray()
    if buttonPressed:
        for x in range(8):
            data.extend(intToData(brightnessConfirm))
    else:
        for button in buttons:
            data.extend(intToData(colors[button.getMacro().getColor()]))
    spiLed.write(data)


# MAIN 

print (spiLed)
print ("Brightness set to " + str((BRIGHTNESS * 100) / 255) + "%")
button_1_3.setMacro(row1List[index])

colors[Color.YELLOW] = (BRIGHTNESS<<16) + (BRIGHTNESS<<8)
colors[Color.RED] = (BRIGHTNESS<<16)
colors[Color.GREEN] = (BRIGHTNESS<<8)
colors[Color.BLUE] = (BRIGHTNESS)
colors[Color.WHITE] = (BRIGHTNESS<<16) + (BRIGHTNESS<<8) + (BRIGHTNESS)

while True:
    pressed = doButtons()
    doDisplay()
    doLed(pressed)
    
    time.sleep_ms(50)
    
