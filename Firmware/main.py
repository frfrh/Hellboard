
from machine import Pin, I2C, SPI
from ssd1306 import SSD1306_I2C
import keyboard
from helldiver import Hellboard, Hellbutton
from macro import Macros, Color
import framebuf
import time
from hellboard_cfg import *

DEBUG_PRINT = False

k = keyboard.Keyboard()
board = Hellboard(k)

helldiverImage = [
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x07, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xe0, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x07, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xe0, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x07, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xe0, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x07, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xe0, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x07, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xe0, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0xff, 0xf8, 0x1f, 0xff, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0xff, 0xf8, 0x1f, 0xff, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0xff, 0xf8, 0x1f, 0xff, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0xff, 0xf8, 0x1f, 0xff, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0xff, 0xf8, 0x1f, 0xff, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0xf0, 0x79, 0xff, 0xc7, 0x00, 0x70, 0x0f, 0xff, 0x0e, 0x78, 0x1e, 0xff, 0xe7, 0xff, 0x07, 0xf8, 
0xf0, 0x79, 0xff, 0xef, 0x80, 0xf0, 0x0f, 0xff, 0x9f, 0x78, 0x3e, 0xff, 0xf7, 0xff, 0x8f, 0xfc, 
0xf0, 0x79, 0xff, 0xef, 0x80, 0xf0, 0x0f, 0xff, 0xdf, 0x3c, 0x3c, 0xff, 0xf7, 0xff, 0xdf, 0xfe, 
0xf0, 0x79, 0xe0, 0x0f, 0x80, 0xf0, 0x0f, 0xff, 0xdf, 0x3c, 0x3c, 0xf0, 0x07, 0x83, 0xde, 0x00, 
0xff, 0xf9, 0xff, 0xcf, 0x80, 0xf0, 0x0f, 0x03, 0xdf, 0x3e, 0x78, 0xff, 0xe7, 0x83, 0xdf, 0xfc, 
0xff, 0xf9, 0xff, 0xcf, 0x80, 0xf0, 0x0f, 0x03, 0xdf, 0x1e, 0x78, 0xff, 0xe7, 0xc7, 0xcf, 0xfe, 
0xff, 0xf9, 0xff, 0xcf, 0x80, 0xf0, 0x0f, 0x03, 0xdf, 0x1e, 0x70, 0xff, 0xe7, 0xff, 0x8f, 0xff, 
0xff, 0xf9, 0xf0, 0x0f, 0x80, 0xf0, 0x0f, 0x03, 0xdf, 0x0f, 0xf0, 0xf0, 0x07, 0xff, 0x80, 0x0f, 
0xf0, 0x79, 0xff, 0xcf, 0xfc, 0xff, 0xcf, 0xff, 0xdf, 0x0f, 0xf0, 0xff, 0xe7, 0xc7, 0xcf, 0xff, 
0xf0, 0x79, 0xff, 0xef, 0xfe, 0xff, 0xef, 0xff, 0xdf, 0x07, 0xe0, 0xff, 0xf7, 0x83, 0xdf, 0xff, 
0xf0, 0x79, 0xff, 0xef, 0xfe, 0xff, 0xef, 0xff, 0x9f, 0x07, 0xe0, 0xff, 0xf7, 0x83, 0xcf, 0xfe, 
0xf0, 0x79, 0xff, 0xc7, 0xfe, 0xff, 0xef, 0xff, 0x0f, 0x03, 0xc0, 0xff, 0xe7, 0x83, 0xc7, 0xfc, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1f, 0xf8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x7f, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x9f, 0xf9, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xfe, 
0x7f, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x9f, 0xf9, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xfe, 
0x3f, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x9f, 0xf9, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xfc, 
0x1f, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x9f, 0xf9, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xf8, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1b, 0xf8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x07, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xb3, 0xcd, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xe0, 
0x03, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x33, 0xcc, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xc0, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3f, 0xfc, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0xff, 0xff, 0x3f, 0xff, 0x9e, 0x79, 0xff, 0xfc, 0xff, 0xff, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x07, 0xe0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0xff, 0xf0, 0x0f, 0xff, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0xff, 0xf8, 0x0f, 0xff, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0xff, 0xf8, 0x1f, 0xff, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0xff, 0xf8, 0x1f, 0xff, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0xff, 0xf8, 0x1f, 0xff, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0xff, 0xf8, 0x1f, 0xff, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x07, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xe0, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x07, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xe0, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x07, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xe0, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x07, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xe0, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x07, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xe0, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]

ShowLogoCnt = 0
ScreenSaverCnt = 0
ScreenSaverActive = False
PressedBtnIndex = -1

colors = {}

time.sleep_ms(100)
displayReady = False
try:
    i2c = I2C(1, sda=Pin(14), scl=Pin(15))  # Init I2C using I2C0 defaults, SCL=Pin(GP9), SDA=Pin(GP8), freq=400000
    oled = SSD1306_I2C(WIDTH, HEIGHT, i2c, addr=0x3C) # Init oled display
    time.sleep_ms(100)
    oled.rotate(1)
    if(DEBUG_PRINT == True):
        print("Display initialized")
    displayReady = True
except Exception:
    if(DEBUG_PRINT == True):
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
    if(DEBUG_PRINT == True):
        print ("Adding R:" + str(r) + ", G:" + str(g) + ", B:" + str(b))
    data.extend(byteToData(g))
    data.extend(byteToData(r))
    data.extend(byteToData(b))
    return data

def intToData(color):
    if(DEBUG_PRINT == True):
        print ("Adding color: 0x{0:02x}".format(color))
    return rbgToData(((color>>16)&0xFF),
                     ((color>>8)&0xFF),
                     ((color)&0xFF))

index = 0
lastTime = time.ticks_ms()
switchTimeout = 300
btnHoldTime   = 300
brightnessConfirm = (BRIGHTNESS_CONFIRM<<16) + (BRIGHTNESS_CONFIRM<<8) + (BRIGHTNESS_CONFIRM)

btn_text_len = 0
btn_text_index = 0
updateCtr = 0
lastUpdateCtr = 0
button_1_3_txt = ''
button_txt_list = []

# ==============================================================================
# SELEKTOR: MAPPT ID-STRINGS AUF DIE ECHTEN MAKRO-OBJEKTE AUS MACRO.PY
# ==============================================================================
def get_macro_by_id(macro_id):
    """Sucht anhand der dreistelligen ID das passende Makro-Objekt."""
    if macro_id == "0" or macro_id == "000":
        return None
        
    # Lokale Mappings basierend auf den IDs deiner SVG/PNG-Dateien
    # Füge hier bei Bedarf weitere IDs hinzu, falls deine Dateien so nummeriert sind
    # ==============================================================================
    # VOLLSTÄNDIGES 4K-NORMALISIERTES HELI-ID MAPPING (101 bis 913)
    # ==============================================================================
    mapping = {
        # 100er: Offensiv - Orbitale Angriffe
        "101": Macros.OrbitalPrecisionStrike,
        "102": Macros.OrbitalGatlingBarrage,
        "103": Macros.OrbitalGasStrike,
        "104": Macros.Orbital120MMHEBarrage,
        "105": Macros.OrbitalAirburstStrike,
        "106": Macros.OrbitalSmokeStrike,
        "107": Macros.OrbitalEMSStrike,
        "108": Macros.Orbital380MMHEBarrage,
        "109": Macros.OrbitalWalkingBarrage,
        "110": Macros.OrbitalLasers,
        "111": Macros.OrbitalNapalmStrike,
        "112": Macros.OrbitalRailcannonStrike,
        #"113": None, "114": None, "115": None, "116": None, "117": None, "118": None, "119": None, "120": None,

        # 200er: Offensiv - Eagle Angriffe
        "201": Macros.EagleGasAirstrike,
        "202": Macros.EagleStrafingRun,
        "203": Macros.EagleAirstrike,
        "204": Macros.EagleClusterBomb,
        "205": Macros.EagleSmokeStrike,
        "206": Macros.EagleNapalmAirstrike,
        "207": Macros.Eagle110MMRocketPods,
        "208": Macros.Eagle500KGBomb,
        #"209": None, "210": None, "211": None, "212": None, "213": None, "214": None, "215": None, "216": None, "217": None, "218": None, "219": None, "220": None,

        # 300er: Versorgung - Taktische Ausrüstung & Support-Waffen
        "301": Macros.MachineGun,
        "302": Macros.ExpendableAntiTank,
        "303": Macros.Stalwart,
        "304": Macros.LaserCannon,
        "305": Macros.AntiMaterialRifle,
        "306": Macros.RecoiledRifle,
        "307": Macros.GrenadeLauncher,
        "308": Macros.Flamethrower,
        "309": Macros.HeavyMg,
        "310": Macros.Autocannon,
        "311": Macros.Arcthrower,
        "312": Macros.QuasarCannon,
        "313": Macros.AirburstRocketLauncher,
        "314": Macros.Commando,
        "315": Macros.Spear,
        "316": Macros.Railgun,
        "317": Macros.WaspLauncher,
        "318": Macros.WarpPack,
        "319": Macros.Siegehammer,
        "320": Macros.PlasEpoch,
        "321": Macros.BulletStorm,
        "322": Macros.Speargun,
        "323": Macros.DefoliationTool,
        "324": Macros.DeEscalator,
        "325": Macros.ExpendableNapalm,
        "326": Macros.Sterilizer,
        "327": Macros.Leveller,
        "328": Macros.BeltFedGL,
        "329": Macros.C4Pack,
        "330": Macros.SoloSilo,
        "332": Macros.OneTrueFlag,
        "333": Macros.Cremator,
        "334": Macros.Maxigun,
        "335": Macros.Meltagun,
        #"336": None, "337": None, "338": None, "339": None, "340": None,

        # 400er: Versorgung - Rucksäcke & Drohnen
        "401": Macros.SupplyPack,
        "402": Macros.JumpPack,
        "403": Macros.BallisticShieldBackpack,
        "404": Macros.GuardDog,
        "405": Macros.Rover,
        "406": Macros.ShieldGeneratorPack,
        "407": Macros.DirectionalShieldBackpack,
        "408": Macros.HotDog,
        "409": Macros.PortableHellbomb,
        "410": Macros.KNine,
        "411": Macros.HoverPack,
        "412": Macros.DogBreath,
        "413": Macros.WarpPack,
        #"414": None, "415": None, "416": None, "417": None, "418": None, "419": None, "420": None,

        # 500er: Versorgung - Fahrzeuge & Exosuits
        "501": Macros.SupplyFRV,
        "502": Macros.Maelstrom,
        "503": Macros.IncineratorFRV,
        "504": Macros.PatriotSuitPexo,
        "505": Macros.PatriotSuitExo,
        "506": Macros.GunnerFRV,
        "507": Macros.BastionMKXVI,
        "508": Macros.BreakthroughExo,
        "509": Macros.LumbererExo,
        #"510": None,"511": None, "512": None, "513": None, "514": None, "515": None, "516": None, "517": None, "518": None, "519": None, "520": None,

        # 600er: Defensiv - Geschütze (Sentries)
        "601": Macros.MachineGunSentry,
        "602": Macros.GatlingSentry,
        "603": Macros.AutocannonSentry,
        "604": Macros.MortarSentry,
        "605": Macros.RocketSentry,
        "606": Macros.TeslaTower,
        "607": Macros.EMSMortarSentry,
        "608": Macros.LaserSentry,
        "609": Macros.FlameSentry,
        "610": Macros.GasMortarSentry,
        #"611": None, "612": None, "613": None, "614": None, "615": None, "616": None, "617": None, "618": None, "619": None, "620": None,

        # 700er: Defensiv - Minenfelder & Relais-Strukturen
        "701": Macros.AntiPersonnelMinefield,
        "702": Macros.IncendiaryMines,
        "703": Macros.AntiTankMinefield,
        "704": Macros.ShieldGenerationRelay,
        "705": Macros.HMGEmplacement,
        "706": Macros.GrenadierBattlement,
        "707": Macros.GasMines,
        "708": Macros.AntiTankEmplacement,
        #"709": None, "710": None, "711": None, "712": None, "713": None, "714": None, "715": None, "716": None, "717": None, "718": None, "719": None, "720": None,

        # 800er: Spezifische Missionstaktiken & Standard-Abrufe
        "801": Macros.EagleRearm,
        "802": Macros.SuperDestroyer,
        "803": Macros.SOSBeacon,
        "804": Macros.Reinforce,
        "805": Macros.Resupply,

        # 900er: Reserve / Zukünftige Taktiken
        "901": Macros.SSSDDelivery,
        "902": Macros.SEAFArtilery,
        "903": Macros.HiveBreakerDrill,
        "904": Macros.CargoContainer,
        "905": Macros.TacticalVideoCamera,
        "906": Macros.UploadData,
        "907": Macros.SeismicProbe,
        "908": Macros.SuperEarthFlag,
        "909": Macros.SSSDDelivery2,
        "910": Macros.CargoContainer,
        "911": Macros.DarkFluidVessel,
        "912": Macros.TectonicDrill,
        "913": Macros.Hellbomb
    }

    
    # Dynamischer Fallback: Falls du Makros anhand des Variablennamens suchst,
    # prüft diese Schleife alle Attribute der Macros-Klasse aus der macro.py
    if macro_id in mapping:
        return mapping[macro_id]
        
    for attr in dir(Macros):
        macro_obj = getattr(Macros, attr)
        if isinstance(macro_obj, Macro):
            # Falls deine IDs im Namen stehen oder du das Mapping erweitern willst
            if attr.lower() == macro_id.lower():
                return macro_obj
                
    return None

def parse_incoming_serial_string(serial_line):
    """
    Verarbeitet die vom PC gesendete Zeile (z.B. '101;203;0;0;0;0;0;312;0;413;0\n')
    Trennt die IDs auf, ordnet sie zu und schaltet das Board live um.
    """
    global row1List
    global index
    
    # Bereinigen von Zeilenumbrüchen und Splitten am Semikolon
    clean_line = serial_line.strip()
    if not clean_line or clean_line == "0":
        if(DEBUG_PRINT == True):
            print("[LINK] Leeres Signal erhalten. Keine Aktion.")
        return

    ids = clean_line.split(";")
    if(DEBUG_PRINT == True):
        print("[LINK] Verarbeite IDs vom COM-Port:", ids)

    # Erwartete Struktur vom PC-Skript:
    # Indizes 0 bis 6 (max 7 IDs): Obere Reihe (row1List)
    # Indizes 7 bis 10 (genau 4 IDs): Untere Reihe (button_2_1 bis button_2_4)
    
    # 1. OBERE REIHE (row1List) DYNAMISCH NEU BEFÜLLEN
    new_row1 = []
    for i in range(0, min(7, len(ids))):
        macro_obj = get_macro_by_id(ids[i])
        if macro_obj:
            new_row1.append(macro_obj)
            
    # Falls gültige obere Makros gefunden wurden, überschreiben wir die Liste temporär
    if new_row1:
        row1List = new_row1
        index = 0  # Selektor-Index auf das erste neue Element zurücksetzen
        button_1_3.setMacro(row1List[index]) # Button 1-3 sofort aktualisieren
        if(DEBUG_PRINT == True):
            print(f"[LINK] Obere Reihe mit {len(row1List)} dynamischen Makros aktualisiert.")

    # 2. UNTERE REIHE (Direktzuweisung an die Buttons 2_1 bis 2_4)
    # Die IDs für unten beginnen im String ab Index 7
    lower_start_idx = 7
    lower_buttons = [button_2_1, button_2_2, button_2_3, button_2_4]
    
    for btn_idx, button in enumerate(lower_buttons):
        array_idx = lower_start_idx + btn_idx
        if array_idx < len(ids):
            macro_obj = get_macro_by_id(ids[array_idx])
            if macro_obj:
                button.setMacro(macro_obj)
                if(DEBUG_PRINT == True):
                    print(f"[LINK] Button_2_{btn_idx+1} dynamisch umbelegt auf: {macro_obj.getName()}")
            else:
                # Wenn an dieser Stelle eine "0" steht, bleibt das im Boot vordefinierte
                # Standard-Makro des Buttons unangetastet (wird nicht gelöscht!).
                if(DEBUG_PRINT == True):
                    print(f"[LINK] Slot unten {btn_idx+1} ist leer (0). Behalte Standard-Belegung.")


def doButtons():
    global ScreenSaverCnt
    global ScreenSaverActive
    global index
    global PressedBtnIndex
    global lastTime
    onBtnRelease = True
    pressed = False

 
    if(PressedBtnIndex == -1):
        btnIndex = 0
        for button in buttons:
            buttonState = button.isPressed(onBtnRelease, btnHoldTime)
            
            if buttonState == 2:
                if button.getMacro() == Macros.CYCLE:
                    if(DEBUG_PRINT == True):
                        print("buttonState: " + str(buttonState))
                    index = index - 1
                    if index < 0:
                        index = len(row1List) - 1
                    pressed = True
                    button_1_3.setMacro(row1List[index])

                    if(DEBUG_PRINT == True):
                        print("Button 1-3 set to: \"" + button_1_3.getMacro().getName() + "\"")
            elif buttonState == 1:
                if button.getMacro() == Macros.CYCLE:
                    if(DEBUG_PRINT == True):
                        print("buttonState: " + str(buttonState))
                    if(ScreenSaverActive == True):
                        ScreenSaverCnt = 0
                    else:
                        if (lastTime + switchTimeout) < time.ticks_ms():
                            lastTime = time.ticks_ms()
                            index = index + 1
                            if index >= len(row1List):
                                index = 0
                            pressed = True
                            button_1_3.setMacro(row1List[index])
                            
                            if(DEBUG_PRINT == True):
                                print("Button 1-3 set to: \"" + button_1_3.getMacro().getName() + "\"")
                else:
                    if(DEBUG_PRINT == True):
                        print(button.getMacro())
                    board.sendMacro(button.getMacro())
                    PressedBtnIndex = btnIndex
                    
            
                ScreenSaverCnt = 0
            btnIndex = btnIndex +1
    else:
        if PressedBtnIndex >= 0 and PressedBtnIndex < len(buttons):
            pressed = board.sendMacro(buttons[PressedBtnIndex].getMacro())
        
        if(pressed == True):
            PressedBtnIndex = -1

    return pressed

def doDisplay():
    global ScreenSaverActive
    global displayReady
    global updateCtr
    global lastUpdateCtr
    global btn_text_len
    global button_1_3_txt
    global button_txt_list
    global btn_text_index

    
    if button_1_3_txt != button_1_3.getName():
        button_1_3_txt = button_1_3.getName()
        button_txt_list.clear()
        button_txt_list.append(button_1_3.getName())
        button_txt_list.append(button_2_1.getName())
        button_txt_list.append(button_2_2.getName())
        button_txt_list.append(button_2_3.getName())
        button_txt_list.append(button_2_4.getName())
        btn_text_len = len(max(button_txt_list, key = len))
        btn_text_index = 0
        updateCtr = 0

    
    if displayReady:
        if (ScreenSaverActive == False):
            oled.fill(0)
            oled.text(button_1_3.getName()[btn_text_index::1],0,4)
            #oled.text("----------------",0,18)
            oled.text(button_2_1.getName()[btn_text_index::1],0,20)
            oled.text(button_2_2.getName()[btn_text_index::1],0,32)
            oled.text(button_2_3.getName()[btn_text_index::1],0,44)
            oled.text(button_2_4.getName()[btn_text_index::1],0,56)
            oled.show()
        else:
            oled.fill(0)
            oled.show()
    
    if SCROLLING_TEXT == True:
        updateCtr = updateCtr + 1
        
        if (updateCtr > 10 and updateCtr % 5 == 0):
            if btn_text_index + 16 < btn_text_len:
                btn_text_index = btn_text_index + 1
                lastUpdateCtr = updateCtr
        if btn_text_index + 16 >= btn_text_len and updateCtr - lastUpdateCtr > 10:
            btn_text_index = 0
            updateCtr = 0

def drawLogo():
    global helldiverImage
    pxlWidth = 0
    pxlHight = 0
    pxlColor = 0
    if displayReady:
        oled.fill(0)
        oled.show()
        for pxlByte in helldiverImage:
            for i in range(0, 8):
                if(((pxlByte >> (7-i)) & 0x1) > 0):
                    pxlColor = 1
                else:
                    pxlColor = 0
                oled.pixel(pxlWidth, pxlHight, pxlColor)
                pxlWidth = pxlWidth + 1
                if(pxlWidth >= WIDTH):
                    pxlWidth = 0
                    pxlHight = pxlHight + 1
        oled.show()

def doScreenSaver():
    global ScreenSaverCnt
    global ScreenSaverActive

    if(ScreenSaverCnt < ACTIVATE_SCREENSAVER_MSEC):
        ScreenSaverCnt = ScreenSaverCnt + 100
        ScreenSaverActive = False
    else:
        ScreenSaverActive = True


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


# ==============================================================================
# MAIN INITIALISIERUNG
# ==============================================================================
print (spiLed)
print ("Brightness set to " + str((BRIGHTNESS * 100) / 255) + "%")
button_1_3.setMacro(row1List[index])

colors[Color.YELLOW] = (BRIGHTNESS<<16) + (BRIGHTNESS<<8)
colors[Color.RED] = (BRIGHTNESS<<16)
colors[Color.GREEN] = (BRIGHTNESS<<8)
colors[Color.BLUE] = (BRIGHTNESS)
colors[Color.WHITE] = (BRIGHTNESS<<16) + (BRIGHTNESS<<8) + (BRIGHTNESS)

drawLogo()

time.sleep(3)

# Jetzt erst wird der REPL-Interrupt für die PC-Gui deaktiviert
import micropython
micropython.kbd_intr(-1)

import sys
import uselect as select
spoll = select.poll()
spoll.register(sys.stdin, select.POLLIN)

serial_buffer = "" # Speicher für die eintreffenden Zeichenfragmente

lastTaskTime_50ms = -1
lastTaskTime_100ms = -1

taskTimer_50ms = 50
taskTimer_100ms = 100

pressed = False

serial_buffer = ""
is_collecting = False

while True:
    taskTimer = time.ticks_ms()

    # 1. KUGELSICHERER ENERGETISCHER SERIAL-EMPFÄNGER
    while spoll.poll(0):
        char = sys.stdin.read(1)
        
        # FALL A: Das Startzeichen der Helldivers-GUI trifft ein
        if char == "#":
            serial_buffer = ""      # Alten Puffer leeren
            is_collecting = True    # Modus: "Ich sammle die ID-Kette" aktivieren
            continue
            
        # FALL B: Wir befinden uns im aktiven Sammel-Modus
        if is_collecting:
            if char == "\n" or char == "\r":
                # Sobald das ERSTE Trennzeichen kommt, prüfen wir, ob wir Daten haben
                clean_packet = "".join([c for c in serial_buffer if ord(c) > 31]).strip()
                
                if len(clean_packet) > 0:
                    # Paket ist vollständig! Wir schalten das Sammeln erst NACH der Verarbeitung ab
                    is_collecting = False
                    try:
                        parse_incoming_serial_string(clean_packet)
                        pressed = True  # Löst das Bestätigungs-Blinken aus!
                    except Exception as err:
                        try:
                            with open("error_log.txt", "w") as log:
                                sys.print_exception(err, log)
                        except:
                            pass
                    
                    serial_buffer = "" # Puffer leeren
                else:
                    # Falls es ein doppeltes Steuerzeichen (\n nach \r) war, 
                    # ignorieren wir es einfach und sammeln weiter, falls noch was kommt.
                    pass
            else:
                # Normales Zeichen an die Kette hängen
                serial_buffer += char
            continue
            
        # FALL C: Es kommt kein '#' und wir sammeln nicht -> VS-Code / IDE-Befehl!
        else:
            # Erlaubt weiterhin ein sauberes Abbrechen in VS-Code, 
            # ignoriert aber isolierte, nachlaufende Newlines (\n)
            if char not in ["\n", "\r", "\x00"]:
                micropython.kbd_intr(3)  # REPL-Interrupts reaktivieren
                raise KeyboardInterrupt
        
    # 2. REGULÄRER TASTATUR-BETRIEB
    if ((ShowLogoCnt + SHOWLOGO_MSEC) < taskTimer):    

        if doButtons() == True:
            pressed = True

        if ((lastTaskTime_50ms + taskTimer_50ms) < taskTimer):
            doDisplay()
            lastTaskTime_50ms = taskTimer

        if ((lastTaskTime_100ms + taskTimer_100ms) < taskTimer):
            doScreenSaver()
            doLed(pressed)
            pressed = False
            lastTaskTime_100ms = taskTimer