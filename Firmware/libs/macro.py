

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
    EagleRearm = Macro("Eagle Rearm", Color.YELLOW, [Direction.UP, Direction.UP, Direction.LEFT, Direction.UP, Direction.RIGHT])
    SuperDestroyer = Macro("Call In Super Destroyer", Color.YELLOW, [Direction.UP, Direction.UP, Direction.DOWN, Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.LEFT, Direction.RIGHT])
    SOSBeacon = Macro("SOS Beacon", Color.YELLOW, [Direction.UP, Direction.DOWN, Direction.RIGHT, Direction.UP])
    Reinforce = Macro("Reinforce", Color.YELLOW, [Direction.UP, Direction.DOWN, Direction.RIGHT, Direction.LEFT, Direction.UP])
    Resupply = Macro("Resupply", Color.YELLOW, [Direction.DOWN, Direction.DOWN, Direction.UP, Direction.RIGHT])
    SSSDDelivery = Macro("SSSD Delivery", Color.YELLOW, [Direction.DOWN, Direction.DOWN, Direction.DOWN, Direction.UP, Direction.UP])
    SEAFArtilery = Macro("SEAF Artilery", Color.YELLOW, [Direction.RIGHT, Direction.UP, Direction.UP, Direction.DOWN])
    HiveBreakerDrill = Macro("Hive Breaker Drill", Color.YELLOW, [Direction.LEFT, Direction.UP, Direction.DOWN, Direction.RIGHT, Direction.DOWN, Direction.DOWN])
    PortableCommsRelay = Macro("Portable Comms Relay", Color.YELLOW, [Direction.UP, Direction.UP, Direction.DOWN, Direction.DOWN, Direction.LEFT, Direction.LEFT, Direction.DOWN])
    TacticalVideoCamera = Macro("Tactical Video Camera", Color.YELLOW, [Direction.RIGHT, Direction.DOWN, Direction.DOWN, Direction.UP, Direction.LEFT, Direction.LEFT, Direction.UP])
    UploadData = Macro("Upload Data", Color.YELLOW, [Direction.LEFT, Direction.RIGHT, Direction.UP, Direction.UP, Direction.UP])
    SeismicProbe = Macro("Seismic Probe", Color.YELLOW, [Direction.UP, Direction.UP, Direction.LEFT, Direction.RIGHT, Direction.DOWN, Direction.DOWN])
    SuperEarthFlag = Macro("Super Earth Flag", Color.YELLOW, [Direction.DOWN, Direction.UP, Direction.DOWN, Direction.UP])
    SSSDDelivery2 = Macro("SSSD Delivery", Color.YELLOW, [Direction.DOWN, Direction.DOWN, Direction.DOWN, Direction.DOWN, Direction.DOWN, Direction.UP, Direction.UP])
    CargoContainer = Macro("Cargo Container", Color.YELLOW, [Direction.UP, Direction.UP, Direction.DOWN, Direction.DOWN, Direction.RIGHT, Direction.DOWN])
    DarkFluidVessel = Macro("Dark Fluid Vessel", Color.YELLOW, [Direction.UP, Direction.LEFT, Direction.RIGHT, Direction.DOWN, Direction.UP, Direction.UP])
    ActivateE711ExtractionDrill = Macro("Activate E-711 Extraction Drill", Color.YELLOW, [Direction.DOWN, Direction.DOWN, Direction.LEFT, Direction.LEFT, Direction.DOWN, Direction.DOWN])
    ProspectingDrill = Macro("Prospecting Drill", Color.YELLOW, [Direction.DOWN, Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.DOWN, Direction.DOWN])
    AquiferDrill = Macro("Aquifer Drill", Color.YELLOW, [Direction.DOWN, Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.DOWN, Direction.DOWN])
    ReinforcementPods = Macro("Reinforcement Pods", Color.YELLOW, [Direction.LEFT, Direction.RIGHT, Direction.UP, Direction.UP, Direction.UP])
    TectonicDrill = Macro("Tectonic Drill", Color.YELLOW, [Direction.UP, Direction.DOWN, Direction.UP, Direction.DOWN, Direction.UP, Direction.DOWN])
    Hellbomb = Macro("NUX-223 Hellbomb", Color.YELLOW, [Direction.DOWN, Direction.UP, Direction.LEFT, Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.DOWN, Direction.UP])
    OrbitalIlluminationFlare = Macro("Orbital Illumination Flare", Color.YELLOW, [Direction.RIGHT, Direction.RIGHT, Direction.LEFT, Direction.LEFT])
    
    # Supply: Support Weapons 
    MachineGun = Macro("MG-43 Machine Gun", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.DOWN, Direction.UP, Direction.RIGHT])
    ExpendableAntiTank = Macro("Expendable Anti-Tank", Color.BLUE, [Direction.DOWN, Direction.DOWN, Direction.LEFT, Direction.UP, Direction.RIGHT])
    Stalwart = Macro("M-105 Stalwart", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.DOWN, Direction.UP, Direction.UP, Direction.LEFT])
    LaserCannon = Macro("Laser Cannon", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.DOWN, Direction.UP, Direction.LEFT])
    AntiMaterialRifle = Macro("Anti-Material Rifle", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.UP, Direction.DOWN])
    RecoiledRifle = Macro("Recoiled Rifle", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.RIGHT, Direction.LEFT])
    GrenadeLauncher = Macro("Grenade Launcher", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.UP, Direction.LEFT, Direction.DOWN])
    Flamethrower = Macro("Flamethrower", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.UP, Direction.DOWN, Direction.UP])
    HeavyMg = Macro("MG-206 Heavy Machine Gun", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.UP, Direction.DOWN, Direction.DOWN])
    Autocannon = Macro("Autocannon", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.DOWN, Direction.UP, Direction.UP, Direction.RIGHT])
    Arcthrower = Macro("Arc thrower", Color.BLUE, [Direction.DOWN, Direction.RIGHT, Direction.DOWN, Direction.UP, Direction.LEFT, Direction.LEFT])
    QuasarCannon = Macro("Quasar Cannon", Color.BLUE, [Direction.DOWN, Direction.DOWN, Direction.UP, Direction.LEFT, Direction.RIGHT])
    AirburstRocketLauncher = Macro("Airburst Rocket Launcher", Color.BLUE, [Direction.DOWN, Direction.UP, Direction.UP, Direction.LEFT, Direction.RIGHT])
    Commando = Macro("MLS-4X Commando", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.UP, Direction.DOWN, Direction.RIGHT])
    Spear = Macro("FAF-14 Spear Launcher", Color.BLUE, [Direction.DOWN, Direction.DOWN, Direction.UP, Direction.DOWN, Direction.DOWN])
    Railgun = Macro("RS-422 Railgun", Color.BLUE, [Direction.DOWN, Direction.RIGHT, Direction.DOWN, Direction.UP, Direction.LEFT, Direction.RIGHT])
    WaspLauncher = Macro("StA-X3 W.A.S.P. Launcher", Color.BLUE, [Direction.DOWN, Direction.DOWN, Direction.UP, Direction.DOWN, Direction.RIGHT])
    Siegehammer = Macro("Siege Hammer", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.LEFT, Direction.UP])
    PlasEpoch = Macro("PLAS-45 Epoch", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.UP, Direction.LEFT, Direction.RIGHT])
    BulletStorm = Macro("MGX-42 Bullet Storm", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.DOWN, Direction.RIGHT, Direction.UP, Direction.LEFT])
    Speargun = Macro("S-11 Speargun", Color.BLUE, [Direction.DOWN, Direction.RIGHT, Direction.DOWN, Direction.LEFT, Direction.UP, Direction.RIGHT])
    DefoliationTool = Macro("CQC-9 Defoliation Tool", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.RIGHT, Direction.DOWN])
    DeEscalator = Macro("GL-52 De-Escalator", Color.BLUE, [Direction.DOWN, Direction.RIGHT, Direction.UP, Direction.LEFT, Direction.RIGHT])
    ExpendableNapalm = Macro("EAT-700 Expendable Napalm", Color.BLUE, [Direction.DOWN, Direction.DOWN, Direction.LEFT, Direction.UP, Direction.LEFT])
    Sterilizer = Macro("TX-41 Sterilizer", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.UP, Direction.DOWN, Direction.LEFT])
    Leveller = Macro("EAT-411 Leveller", Color.BLUE, [Direction.DOWN, Direction.DOWN, Direction.LEFT, Direction.UP, Direction.DOWN])
    BeltFedGL = Macro("GL-28 Belt-Fed Grenade Launcher", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.UP, Direction.LEFT, Direction.UP, Direction.UP])
    C4Pack = Macro("B/MD C4 Pack", Color.BLUE, [Direction.DOWN, Direction.RIGHT, Direction.UP, Direction.UP, Direction.RIGHT, Direction.UP])
    SoloSilo = Macro("MS-11 Solo Silo", Color.BLUE, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.DOWN, Direction.DOWN])
    Cremator = Macro("B/FLAM-80 Cremator", Color.BLUE, [Direction.DOWN, Direction.DOWN, Direction.RIGHT, Direction.DOWN, Direction.UP, Direction.UP])
    Maxigun = Macro("M-1000 Maxigun", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.DOWN, Direction.UP, Direction.UP])
    OneTrueFlag = Macro("CQC-1 One True Flag", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.RIGHT, Direction.UP])
    Meltagun = Macro("40-K Meltagun", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.UP, Direction.LEFT, Direction.LEFT, Direction.DOWN])
        
    # Supply: Backpacks
    SupplyPack = Macro("B-1 Supply Pack", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.DOWN, Direction.UP, Direction.UP, Direction.DOWN])
    JumpPack = Macro("LIFT-850 Jump Pack", Color.BLUE, [Direction.DOWN, Direction.UP, Direction.UP, Direction.DOWN, Direction.UP])
    BallisticShieldBackpack = Macro("Ballistic Shield Backpack", Color.BLUE, [Direction.Down, Direction.Left, Direction.Down, Direction.Down, Direction.Up, Direction.Left])
    GuardDog = Macro("AX/AR-23 Guard Dog", Color.BLUE, [Direction.DOWN, Direction.UP, Direction.LEFT, Direction.UP, Direction.RIGHT, Direction.DOWN])
    Rover = Macro("AX/LAS-5 Rover", Color.BLUE, [Direction.DOWN, Direction.UP, Direction.LEFT, Direction.UP, Direction.RIGHT, Direction.RIGHT])
    ShieldGeneratorPack = Macro("SH-32 Shield Generator Pack", Color.BLUE, [Direction.DOWN, Direction.UP, Direction.LEFT, Direction.RIGHT, Direction.LEFT, Direction.RIGHT])
    DirectionalShieldBackpack = Macro("SH-51 Directional Shield", Color.BLUE, [Direction.DOWN, Direction.UP, Direction.LEFT, Direction.RIGHT, Direction.UP, Direction.UP])
    HotDog = Macro("AX/FLAM-75 Hot Dog", Color.BLUE, [Direction.DOWN, Direction.UP, Direction.LEFT, Direction.UP, Direction.LEFT, Direction.LEFT])
    PortableHellbomb = Macro("B-100 Portable Hellbomb", Color.BLUE, [Direction.DOWN, Direction.RIGHT, Direction.UP, Direction.UP, Direction.UP])
    K9 = Macro("AX/ARC-3 K-9", Color.BLUE, [Direction.DOWN, Direction.UP, Direction.LEFT, Direction.UP, Direction.RIGHT, Direction.LEFT])
    HoverPack = Macro("LIFT-860 Hover Pack", Color.BLUE, [Direction.DOWN, Direction.UP, Direction.UP, Direction.DOWN, Direction.LEFT, Direction.RIGHT])
    DogBreath = Macro("AX/TX-13 Dog Breath", Color.BLUE, [Direction.DOWN, Direction.UP, Direction.LEFT, Direction.UP, Direction.RIGHT, Direction.UP])
    WarpPack = Macro("LIFT-182 Warp Pack ", Color.BLUE, [Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.DOWN, Direction.LEFT, Direction.RIGHT])
    
    # Supply: Vehicles
    SupplyFRV = Macro("M-103 Supply FRV", Color.BLUE, [Direction.LEFT, Direction.DOWN, Direction.LEFT, Direction.LEFT, Direction.DOWN, Direction.UP, Direction.RIGHT])
    Maelstrom = Macro("TD-110 Maelstrom", Color.BLUE, [Direction.LEFT, Direction.DOWN, Direction.RIGHT, Direction.DOWN, Direction.LEFT, Direction.DOWN, Direction.UP, Direction.LEFT, Direction.RIGHT])
    IncineratorFRV = Macro("M-104 Incinerator FRV", Color.BLUE, [Direction.LEFT, Direction.DOWN, Direction.RIGHT, Direction.LEFT, Direction.DOWN, Direction.UP, Direction.UP])
    PatriotSuitPexo = Macro("PEXO-49 Emancipator Exosuit", Color.BLUE, [Direction.LEFT, Direction.DOWN, Direction.RIGHT, Direction.UP, Direction.LEFT, Direction.DOWN, Direction.UP])
    PatriotSuitExo = Macro("EXO-45 Patriot Exosuit", Color.BLUE, [Direction.LEFT, Direction.DOWN, Direction.RIGHT, Direction.UP, Direction.LEFT, Direction.DOWN, Direction.DOWN])
    GunnerFRV = Macro("M-102 Gunner FRV", Color.BLUE, [Direction.LEFT, Direction.DOWN, Direction.RIGHT, Direction.DOWN, Direction.RIGHT, Direction.DOWN, Direction.UP])
    BastionMKXVI = Macro("Bastion MK XVI", Color.BLUE, [Direction.LEFT, Direction.DOWN, Direction.RIGHT, Direction.DOWN, Direction.LEFT, Direction.DOWN, Direction.UP, Direction.DOWN, Direction.UP])
    BreakthroughExo = Macro("EXO-55 Breakthrough Exosuit", Color.BLUE, [Direction.LEFT, Direction.DOWN, Direction.RIGHT, Direction.LEFT, Direction.RIGHT, Direction.DOWN, Direction.UP])
    LumbererExo = Macro("EXO-51 Lumberer Exosuit", Color.BLUE, [Direction.LEFT, Direction.DOWN, Direction.RIGHT, Direction.UP, Direction.RIGHT, Direction.LEFT, Direction.UP])
    
    # Offensive: Orbital
    OrbitalPrecisionStrike = Macro("Orbital Precision Strike", Color.RED, [Direction.RIGHT, Direction.RIGHT, Direction.UP])
    OrbitalGatlingBarrage = Macro("Orbital Gatling Barrage", Color.RED, [Direction.RIGHT, Direction.DOWN, Direction.LEFT, Direction.UP, Direction.UP])
    OrbitalGasStrike = Macro("Orbital Gas Strike", Color.RED, [Direction.RIGHT, Direction.RIGHT, Direction.DOWN, Direction.RIGHT])
    Orbital120MMHEBarrage = Macro("Orbital 120MM HE Barrage", Color.RED, [Direction.RIGHT, Direction.RIGHT, Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.DOWN])
    OrbitalAirburstStrike = Macro("Orbital Airburst Strike", Color.RED, [Direction.RIGHT, Direction.RIGHT, Direction.RIGHT])
    OrbitalSmokeStrike = Macro("Orbital Smoke Strike", Color.RED, [Direction.RIGHT, Direction.RIGHT, Direction.DOWN, Direction.UP])
    OrbitalEMSStrike = Macro("Orbital EMS Strike", Color.RED, [Direction.RIGHT, Direction.RIGHT, Direction.LEFT, Direction.DOWN])
    Orbital380MMHEBarrage = Macro("Orbital 380MM HE Barrage", Color.RED, [Direction.RIGHT, Direction.DOWN, Direction.UP, Direction.UP, Direction.LEFT, Direction.DOWN, Direction.DOWN])
    OrbitalWalkingBarrage = Macro("Orbital Walking Barrage", Color.RED, [Direction.Right, Direction.Down, Direction.Right, Direction.Down, Direction.Right, Direction.Down])
    OrbitalLasers = Macro("Orbital Lasers", Color.RED, [Direction.RIGHT, Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.DOWN])
    OrbitalNapalmStrike = Macro("Orbital Napalm Barrage", Color.RED, [Direction.RIGHT, Direction.RIGHT, Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.UP])
    OrbitalRailcannonStrike = Macro("Orbital Railcannon Strike", Color.RED, [Direction.RIGHT, Direction.UP, Direction.DOWN, Direction.DOWN, Direction.RIGHT])
    
    # Offensive: Eagle
    EagleGasAirstrike = Macro("Eagle Gas Airstrike", Color.RED, [Direction.UP, Direction.RIGHT, Direction.LEFT, Direction.RIGHT])
    EagleStrafingRun = Macro("Eagle Strafing Run", Color.RED, [Direction.UP, Direction.RIGHT, Direction.RIGHT])
    EagleAirstrike = Macro("Eagle Airstrike", Color.RED, [Direction.UP, Direction.RIGHT, Direction.DOWN, Direction.RIGHT])
    EagleClusterBomb = Macro("Eagle Cluster Bomb", Color.RED, [Direction.UP, Direction.RIGHT, Direction.DOWN, Direction.DOWN, Direction.RIGHT])
    EagleSmokeStrike = Macro("Eagle Smoke Strike", Color.RED, [Direction.UP, Direction.RIGHT, Direction.UP, Direction.DOWN])
    EagleNapalmAirstrike = Macro("Eagle Napalm Airstrike", Color.RED, [Direction.UP, Direction.RIGHT, Direction.DOWN, Direction.UP])
    Eagle110MMRocketPods = Macro("Eagle 110MM Rocket Pods", Color.RED, [Direction.UP, Direction.RIGHT, Direction.UP, Direction.LEFT])
    Eagle500KGBomb = Macro("Eagle 500KG Bomb", Color.RED, [Direction.UP, Direction.RIGHT, Direction.DOWN, Direction.DOWN, Direction.DOWN])
    
    # Defensive: Sentries
    MachineGunSentry = Macro("Machine Gun Sentry", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.RIGHT, Direction.UP])
    GatlingSentry = Macro("Gatling Sentry", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.LEFT])
    AutocannonSentry = Macro("A/AC-8 Autocannon Sentry", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.UP, Direction.LEFT, Direction.UP])
    MortarSentry = Macro("A/M-12 Mortar Sentry", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.RIGHT, Direction.DOWN])
    RocketSentry = Macro("A/MLS-4X Rocket Sentry", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.RIGHT, Direction.LEFT])
    TeslaTower = Macro("A/ARC-3 Tesla Tower", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.UP, Direction.LEFT, Direction.RIGHT])
    EMSMortarSentry = Macro("A/M-23 EMS Mortar Sentry", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.DOWN, Direction.RIGHT])
    LaserSentry = Macro("A/LAS-98 Laser Sentry", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.DOWN, Direction.UP, Direction.RIGHT])
    FlameSentry = Macro("E/FLAM-40 Flame Sentry", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.DOWN, Direction.UP, Direction.UP])
    GasMortarSentry = Macro("A/GM-17 Gas Mortar Sentry", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.RIGHT, Direction.DOWN, Direction.LEFT])
    
    # Defensive: Emplacements
    AntiPersonnelMinefield = Macro("MD-6 Anti-Personnel Minefield", Color.GREEN, [Direction.DOWN, Direction.LEFT, Direction.UP, Direction.RIGHT])
    IncendiaryMines = Macro("MD-I4 Incendiary Mines", Color.GREEN, [Direction.DOWN, Direction.LEFT, Direction.LEFT, Direction.DOWN])
    AntiTankMinefield = Macro("MD-17 Anti-Tank Mines", Color.GREEN, [Direction.DOWN, Direction.LEFT, Direction.UP, Direction.UP])
    ShieldGenerationRelay = Macro("FX-12 Shield Generator Relay", Color.GREEN, [Direction.DOWN, Direction.DOWN, Direction.LEFT, Direction.RIGHT, Direction.LEFT, Direction.RIGHT])
    HMGEmplacement = Macro("E/MG-101 HMG Emplacement", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.LEFT, Direction.RIGHT, Direction.RIGHT, Direction.LEFT])
    GrenadierBattlement = Macro("E/GL-21 Grenadier Battlement", Color.GREEN, [Direction.DOWN, Direction.RIGHT, Direction.DOWN, Direction.LEFT, Direction.RIGHT])
    GasMines = Macro("MD-8 Gas Mines", Color.GREEN, [Direction.DOWN, Direction.LEFT, Direction.UP, Direction.RIGHT])
    AntiTankEmplacement = Macro("E/AT-12 Anti-Tank Emplacement", Color.GREEN, [Direction.DOWN, Direction.UP, Direction.LEFT, Direction.RIGHT, Direction.RIGHT, Direction.RIGHT])
    
