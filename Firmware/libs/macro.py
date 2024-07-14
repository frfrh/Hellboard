

class Direction():
# CONFIG
# https://deskthority.net/wiki/Scancode
    UP = 0x52
    Up = 0x52
    RIGHT = 0x4f
    Right = 0x4f
    DOWN = 0x51
    Down = 0x51
    LEFT = 0x50
    Left = 0x50

class Color():
    YELLOW = 1
    RED = 2
    GREEN = 3
    BLUE = 4
    WHITE = 5

class Macro:
    def __init__(self, name, color, directions):
        self.name = name
        self.color = color
        self.directions = []
        for direction in directions:
            self.directions.append(direction)
    
    def __str__(self):
        return str("\"" + self.name) + "\", " + str(self.color) + ", " + str(self.directions)
    
    def getDirections(self):
        return self.directions
    
    def getName(self):
        return self.name
    
    def getColor(self):
        return self.color

class Macros:
    CYCLE = Macro("CYCLE", Color.WHITE, [])
    # https://helldivers2.game-vault.net/w/images/e/ef/All_Stratagem_Inputs.png
    # Mission Stratagems
    Reinforce = Macro("Reinforce", Color.YELLOW, [Direction.UP, Direction.DOWN, Direction.RIGHT, Direction.LEFT, Direction.UP])
    SOSBeacon = Macro("SOS Beacon", Color.YELLOW, [Direction.UP, Direction.DOWN, Direction.RIGHT, Direction.UP])
    Resupply = Macro("Resupply", Color.YELLOW, [Direction.DOWN, Direction.DOWN, Direction.UP, Direction.RIGHT])
    Hellbomb = Macro("NUX-223 Hellbomb", Color.YELLOW, [Direction.DOWN, Direction.UP, Direction.LEFT, Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.DOWN, Direction.UP])
    SSSDDelivery = Macro("SSSD Delivery", Color.YELLOW, [Direction.DOWN, Direction.DOWN, Direction.DOWN, Direction.UP, Direction.UP])
    SeismicProbe = Macro("Seismic Probe", Color.YELLOW, [Direction.UP, Direction.UP, Direction.LEFT, Direction.RIGHT, Direction.DOWN, Direction.DOWN])
    UploadData = Macro("Upload Data", Color.YELLOW, [Direction.LEFT, Direction.RIGHT, Direction.UP, Direction.UP, Direction.UP])
    EagleRearm = Macro("Eagle Rearm", Color.YELLOW, [Direction.UP, Direction.UP, Direction.LEFT, Direction.UP, Direction.RIGHT])
    SEAFArtilery = Macro("SEAF Artilery", Color.YELLOW, [Direction.RIGHT, Direction.UP, Direction.UP, Direction.DOWN])
    SuperEarthFlag = Macro("Super Earth Flag", Color.YELLOW, [Direction.DOWN, Direction.UP, Direction.DOWN, Direction.UP])
    OrbitalIlluminationFlare = Macro("Orbital Illumination Flare", Color.YELLOW, [Direction.RIGHT, Direction.RIGHT, Direction.LEFT, Direction.LEFT])
    
    # Supply: Support Weapons
    MachineGun = Macro("MG-43 Machine Gun", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.DOWN, Direction.UP, Direction.RIGHT])
    AntiMaterialRifle = Macro("Anti-Material Rifle", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.UP, Direction.DOWN])
    Stalwart = Macro("M-105 Stalwart", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.DOWN, Direction.UP, Direction.UP, Direction.LEFT])
    ExpendableAntiTank = Macro("Expendable Anti-Tank", Color.BLUE, [Direction.DOWN, Direction.DOWN, Direction.LEFT, Direction.UP, Direction.RIGHT])
    RecoiledRifle = Macro("Recoiled Rifle", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.RIGHT, Direction.LEFT])
    Flamethrower = Macro("Flamethrower", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.UP, Direction.DOWN, Direction.UP])
    Autocannon = Macro("Autocannon", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.DOWN, Direction.UP, Direction.UP, Direction.RIGHT])
    HeavyMg = Macro("MG-206 Heavy Machine Gun", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.UP, Direction.DOWN, Direction.DOWN])
    Railgun = Macro("Railgun", Color.BLUE, [Direction.DOWN, Direction.RIGHT, Direction.DOWN, Direction.UP, Direction.LEFT, Direction.RIGHT])
    Spear = Macro("Spear", Color.BLUE, [Direction.DOWN, Direction.DOWN, Direction.UP, Direction.DOWN, Direction.DOWN])
    GrenadeLauncher = Macro("Grenade Launcher", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.UP, Direction.LEFT, Direction.DOWN])
    LaserCannon = Macro("Laser Cannon", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.DOWN, Direction.UP, Direction.LEFT])
    Arcthrower = Macro("Arc thrower", Color.BLUE, [Direction.DOWN, Direction.RIGHT, Direction.DOWN, Direction.UP, Direction.LEFT, Direction.LEFT])
    QuasarCannon = Macro("Quasar Cannon", Color.BLUE, [Direction.DOWN, Direction.DOWN, Direction.UP, Direction.LEFT, Direction.RIGHT])
    AirburstRocketLauncher = Macro("Airburst Rocket Launcher", Color.BLUE, [Direction.DOWN, Direction.UP, Direction.UP, Direction.LEFT, Direction.RIGHT])
    Commando = Macro("MLS-4X Commando", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.UP, Direction.DOWN, Direction.RIGHT])    

    # Supply: Other
    PatriotSuitExo = Macro("EXO-45 Patriot Exosuit", Color.BLUE, [Direction.LEFT, Direction.DOWN, Direction.RIGHT, Direction.UP, Direction.LEFT, Direction.DOWN, Direction.DOWN])
    PatriotSuitPexo = Macro("PEXO-49 Emancipator Exosuit", Color.BLUE, [Direction.LEFT, Direction.DOWN, Direction.RIGHT, Direction.UP, Direction.LEFT, Direction.DOWN, Direction.UP])

    # Offensive: Orbital
    OrbitalGatlingBarrage = Macro("Orbital Gatling Barrage", Color.RED, [Direction.RIGHT, Direction.DOWN, Direction.LEFT, Direction.UP, Direction.UP])
    OrbitalAirburstStrike = Macro("Orbital Airburst Strike", Color.RED, [Direction.RIGHT, Direction.RIGHT, Direction.RIGHT])
    Orbital120MMHEBarrage = Macro("Orbital 120MM HE Barrage", Color.RED, [Direction.RIGHT, Direction.RIGHT, Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.DOWN])
    Orbital380MMHEBarrage = Macro("Orbital 380MM HE Barrage", Color.RED, [Direction.RIGHT, Direction.DOWN, Direction.UP, Direction.UP, Direction.LEFT, Direction.DOWN, Direction.DOWN])
    OrbitalWalkingBarrage = Macro("Orbital Walking Barrage", Color.RED, [Direction.Right, Direction.Down, Direction.Right, Direction.Down, Direction.Right, Direction.Down])
    OrbitalLasers = Macro("Orbital Lasers", Color.RED, [Direction.RIGHT, Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.DOWN])
    OrbitalRailcannonStrike = Macro("Orbital Railcannon Strike", Color.RED, [Direction.RIGHT, Direction.UP, Direction.DOWN, Direction.DOWN, Direction.RIGHT])
    OrbitalPrecisionStrike = Macro("Orbital Precision Strike", Color.RED, [Direction.RIGHT, Direction.RIGHT, Direction.UP])
    OrbitalGasStrike = Macro("Orbital Gas Strike", Color.RED, [Direction.RIGHT, Direction.RIGHT, Direction.DOWN, Direction.RIGHT])
    OrbitalEMSStrike = Macro("Orbital EMS Strike", Color.RED, [Direction.RIGHT, Direction.RIGHT, Direction.LEFT, Direction.DOWN])
    OrbitalSmokeStrike = Macro("Orbital Smoke Strike", Color.RED, [Direction.RIGHT, Direction.RIGHT, Direction.DOWN, Direction.UP])

    # Offensive: Eagle
    EagleStrafingRun = Macro("Eagle Strafing Run", Color.RED, [Direction.UP, Direction.RIGHT, Direction.RIGHT])
    EagleAirstrike = Macro("Eagle Airstrike", Color.RED, [Direction.UP, Direction.RIGHT, Direction.DOWN, Direction.RIGHT])
    EagleClusterBomb = Macro("Eagle Cluster Bomb", Color.RED, [Direction.UP, Direction.RIGHT, Direction.DOWN, Direction.DOWN, Direction.RIGHT])
    EagleNapalmAirstrike = Macro("Eagle Napalm Airstrike", Color.RED, [Direction.UP, Direction.RIGHT, Direction.DOWN, Direction.UP])
    EagleSmokeStrike = Macro("Eagle Smoke Strike", Color.RED, [Direction.UP, Direction.RIGHT, Direction.UP, Direction.DOWN])
    Eagle110MMRocketPods = Macro("Eagle 110MM Rocket Pods", Color.RED, [Direction.UP, Direction.RIGHT, Direction.UP, Direction.LEFT])
    Eagle500KGBomb = Macro("Eagle 500KG Bomb", Color.RED, [Direction.UP, Direction.RIGHT, Direction.DOWN, Direction.DOWN, Direction.DOWN])

    # Defensive
    HMGEmplacement = Macro("HMG Emplacement", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.LEFT, Direction.RIGHT, Direction.RIGHT, Direction.LEFT])
    ShieldGenerationRelay = Macro("Shield Generation Relay", Color.GREEN, [Direction.DOWN, Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.LEFT, Direction.RIGHT])
    TeslaTower = Macro("Tesla Tower", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.UP, Direction.LEFT, Direction.RIGHT])
    AntiPersonnelMinefield = Macro("Anti-Personnel Minefield", Color.GREEN, [Direction.DOWN, Direction.LEFT, Direction.UP, Direction.RIGHT])
    IncendiaryMines = Macro("Incendiary Mines", Color.GREEN, [Direction.DOWN, Direction.LEFT, Direction.LEFT, Direction.DOWN])
    MachineGunSentry = Macro("Machine Gun Sentry", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.RIGHT, Direction.UP])
    GatlingSentry = Macro("Gatling Sentry", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.LEFT])
    MortarSentry = Macro("Mortar Sentry", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.RIGHT, Direction.DOWN])
    AutocannonSentry = Macro("Autocannon Sentry", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.UP, Direction.LEFT, Direction.UP])
    RocketSentry = Macro("Rocket Sentry", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.RIGHT, Direction.LEFT])
    EMSMortarSentry = Macro("EMS Mortar Sentry", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.DOWN, Direction.RIGHT])

    # Supply: Backpacks
    JumpPack = Macro("Jump Pack", Color.BLUE, [Direction.DOWN, Direction.UP, Direction.UP, Direction.DOWN, Direction.UP])
    SupplyPack = Macro("Supply Pack", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.DOWN, Direction.UP, Direction.UP, Direction.DOWN])
    GuardDogRover = Macro("Guard Dog Rover", Color.BLUE, [Direction.DOWN, Direction.UP, Direction.LEFT, Direction.UP, Direction.RIGHT, Direction.RIGHT])
    BallisticShieldBackpack = Macro("Ballistic Shield Backpack", Color.BLUE, [Direction.Down, Direction.Left, Direction.Down, Direction.Down, Direction.Up, Direction.Left])
    ShieldGeneratorPack = Macro("Shield Generator Pack", Color.BLUE, [Direction.DOWN, Direction.UP, Direction.LEFT, Direction.RIGHT, Direction.LEFT, Direction.RIGHT])
    GuardDog = Macro("Guard Dog", Color.BLUE, [Direction.DOWN, Direction.UP, Direction.LEFT, Direction.UP, Direction.RIGHT, Direction.DOWN])

    