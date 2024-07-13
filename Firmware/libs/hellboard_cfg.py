from machine import Pin
from helldiver import Hellboard, Hellbutton
from macro import Macros, Color

WIDTH = 128 # oled display width
HEIGHT = 64 # oled display height

BRIGHTNESS = 0x30
BRIGHTNESS_CONFIRM = 0x80
SCROLLING_TEXT = True

SHOWLOGO_MSEC = 3000
ACTIVATE_SCREENSAVER_MSEC = 0*60*1000 + 30*1000 + 0

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

button_2_1 = Hellbutton(b2_1, Macros.OrbitalGatlingBarrage)
button_2_2 = Hellbutton(b2_2, Macros.Eagle500KGBomb)
button_2_3 = Hellbutton(b2_3, Macros.Eagle110MMRocketPods)
button_2_4 = Hellbutton(b2_4, Macros.OrbitalPrecisionStrike)


row1List = []
row1List.append (Macros.ShieldGeneratorPack)
row1List.append (Macros.QuasarCannon)
row1List.append (Macros.Commando)
row1List.append (Macros.Autocannon)
row1List.append (Macros.AutocannonSentry)
row1List.append (Macros.MortarSentry)
row1List.append (Macros.EMSMortarSentry)
row1List.append (Macros.OrbitalRailcannonStrike)
row1List.append (Macros.AirburstRocketLauncher)
row1List.append (Macros.Hellbomb)
#row1List.append (Macros.MachineGun)
#row1List.append (Macros.Eagle110MMRocketPods)
#row1List.append (Macros.GuardDogRover)
#row1List.append (Macros.Eagle110MMRocketPods)
# row1List.append (Macros.PatriotSuitExo)
# row1List.append (Macros.PatriotSuitPexo)
# row1List.append (Macros.EagleRearm)
# row1List.append (Macros.SEAFArtilery)
# row1List.append (Macros.Hellbomb)
# row1List.append (Macros.SOSBeacon)
# row1List.append (Macros.SuperEarthFlag)
# row1List.append (Macros.UploadData)
# row1List.append (Macros.SeismicProbe)
# row1List.append (Macros.OrbitalIlluminationFlare)


buttons = []
buttons.append(button_1_4)
buttons.append(button_1_3)
buttons.append(button_1_2)
buttons.append(button_1_1)
buttons.append(button_2_1)
buttons.append(button_2_2)
buttons.append(button_2_3)
buttons.append(button_2_4)