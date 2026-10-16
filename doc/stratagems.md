# Stratagems

**Single source of truth for the stratagem input codes of the Hellboard project.** The firmware (`Firmware/`) and the companion software (`Software/`) build their code tables from this list only. A code is added or corrected here and nowhere else; the other parts are updated from this file.

> Status: the list is in place; generating the firmware macros and the software seed catalog from it is not wired up yet. Until then `Firmware/libs/macro.py` and `Software/assets/catalog.seed.json` must be kept in line with this file by hand.

## Format

One table per category. The tables are meant to be read by tools, so keep the layout:

| Column | Meaning |
|-|-|
| Id | Stable key, lowercase `a-z0-9_`. Equals the icon id of the companion software (file name slug in the icon repository `nvigneux/Helldivers-2-Stratagems-icons-svg`). Never change an existing id. |
| Name | Short English name (icon repository name). |
| Designation | Full in-game designation, for reference. |
| Code | Input code with the arrows `↑ ↓ ← →`, no spaces. |
| Firmware macro | Name of the current macro in `Firmware/libs/macro.py`, empty if the firmware has none yet. |
| Notes | Unverified or disputed codes, missing icons and other remarks. A code without a note agrees in every source that lists it. |

Rules:

- Every code must be unique among the stratagems a player can have at the same time.
- A code taken from only one source or disputed between sources gets a note until it is checked in the game.
- Sources for looking up codes are listed in the knowledge base of [`Software/README.md`](../Software/README.md#knowledge-base). They are only read manually; no part of the project queries them automatically.

Checked against helldivers.wiki.gg, DiversHub and the Fandom wiki on 2026-10-16: 115 stratagems.

## Orbital

| Id | Name | Designation | Code | Firmware macro | Notes |
|-|-|-|-|-|-|
| `orbital_120mm_he_barrage` | Orbital 120MM HE Barrage | Orbital 120mm HE Barrage | `→→↓←→↓` | `Orbital120MMHEBarrage` |  |
| `orbital_380mm_he_barrage` | Orbital 380MM HE Barrage | Orbital 380mm HE Barrage | `→↓↑↑←↓↓` | `Orbital380MMHEBarrage` |  |
| `orbital_airburst_strike` | Orbital Airburst Strike | Orbital Airburst Strike | `→→→` | `OrbitalAirburstStrike` |  |
| `orbital_ems_strike` | Orbital EMS Strike | Orbital EMS Strike | `→→←↓` | `OrbitalEMSStrike` |  |
| `orbital_gas_strike` | Orbital Gas Strike | Orbital Gas Strike | `→→↓→` | `OrbitalGasStrike` |  |
| `orbital_gatling_barrage` | Orbital Gatling Barrage | Orbital Gatling Barrage | `→↓←↑↑` | `OrbitalGatlingBarrage` |  |
| `orbital_laser` | Orbital Laser | Orbital Laser | `→↓↑→↓` | `OrbitalLasers` |  |
| `orbital_napalm_barrage` | Orbital Napalm Barrage | Orbital Napalm Barrage | `→→↓←→↑` | `OrbitalNapalmBarrage` |  |
| `orbital_precision_strike` | Orbital Precision Strike | Orbital Precision Strike | `→→↑` | `OrbitalPrecisionStrike` |  |
| `orbital_railcannon_strike` | Orbital Railcannon Strike | Orbital Railcannon Strike | `→↑↓↓→` | `OrbitalRailcannonStrike` |  |
| `orbital_smoke_strike` | Orbital Smoke Strike | Orbital Smoke Strike | `→→↓↑` | `OrbitalSmokeStrike` |  |
| `orbital_walking_barrage` | Orbital Walking Barrage | Orbital Walking Barrage | `→↓→↓→↓` | `OrbitalWalkingBarrage` |  |

## Eagle

| Id | Name | Designation | Code | Firmware macro | Notes |
|-|-|-|-|-|-|
| `eagle_110mm_rocket_pods` | Eagle 110MM Rocket Pods | Eagle 110mm Rocket Pods | `↑→↑←` | `Eagle110MMRocketPods` |  |
| `eagle_500kg_bomb` | Eagle 500KG Bomb | Eagle 500kg Bomb | `↑→↓↓↓` | `Eagle500KGBomb` |  |
| `eagle_airstrike` | Eagle Airstrike | Eagle Airstrike | `↑→↓→` | `EagleAirstrike` |  |
| `eagle_cluster_bomb` | Eagle Cluster Bomb | Eagle Cluster Bomb | `↑→↓↓→` | `EagleClusterBomb` |  |
| `eagle_gas_strike` | Eagle Gas Strike | Eagle Gas Airstrike | `↑→←→` |  |  |
| `eagle_napalm_airstrike` | Eagle Napalm Airstrike | Eagle Napalm Airstrike | `↑→↓↑` | `EagleNapalmAirstrike` |  |
| `eagle_smoke_strike` | Eagle Smoke Strike | Eagle Smoke Strike | `↑→↑↓` | `EagleSmokeStrike` |  |
| `eagle_strafing_run` | Eagle Strafing Run | Eagle Strafing Run | `↑→→` | `EagleStrafingRun` |  |

## Support weapon

| Id | Name | Designation | Code | Firmware macro | Notes |
|-|-|-|-|-|-|
| `40_k_meltagun` | 40-K Meltagun | 40-K Meltagun | `↓←↑←←↓` |  |  |
| `airburst_rocket_launcher` | Airburst Rocket Launcher | RL-77 Airburst Rocket Launcher | `↓↑↑←→` | `AirburstRocketLauncher` |  |
| `anti_materiel_rifle` | Anti-Materiel Rifle | APW-1 Anti-Materiel Rifle | `↓←→↑↓` | `AntiMaterialRifle` |  |
| `arc_thrower` | Arc Thrower | ARC-3 Arc Thrower | `↓→↓↑←←` | `Arcthrower` |  |
| `autocannon` | Autocannon | AC-8 Autocannon | `↓←↓↑↑→` | `Autocannon` |  |
| `bullet_storm` | Bullet Storm | MGX-42 Bullet Storm | `↓←↓→↑←` |  |  |
| `c4_pack` | C4 Pack | B/MD C4 Pack | `↓→↑↑→↑` |  |  |
| `commando` | Commando | MLS-4X Commando | `↓←↑↓→` | `Commando` |  |
| `cqc_20` | CQC-20 | CQC-20 Breaching Hammer | `↓←→←↑` | `Siegehammer` |  |
| `cremator` | Cremator | B/FLAM-80 Cremator | `↓↓→↓↑↑` |  | Unverified: wiki.gg; DiversHub lists ↓←↑←↑↑ (the code of GL-28) |
| `defoliation_tool` | Defoliation Tool | CQC-9 Defoliation Tool | `↓←→→↓` | `Chainsword` |  |
| `eat_411` | EAT-411 | EAT-411 Leveller | `↓↓←↑↓` |  |  |
| `epoch` | Epoch | PLAS-45 Epoch | `↓←↑←→` | `Epoch` |  |
| `expendable_anti_tank` | Expendable Anti-Tank | EAT-17 Expendable Anti-Tank | `↓↓←↑→` | `ExpendableAntiTank` |  |
| `expendable_napalm` | Expendable Napalm | EAT-700 Expendable Napalm | `↓↓←↑←` | `ExpendableNapalm` |  |
| `flamethrower` | Flamethrower | FLAM-40 Flamethrower | `↓←↑↓↑` | `Flamethrower` |  |
| `gl_28` | GL-28 | GL-28 Belt-Fed Grenade Launcher | `↓←↑←↑↑` |  |  |
| `gl_52_de_escalator` | GL-52 De-Escalator | GL-52 De-Escalator | `↓→↑←→` |  |  |
| `grenade_launcher` | Grenade Launcher | GL-21 Grenade Launcher | `↓←↑←↓` | `GrenadeLauncher` |  |
| `heavy_machine_gun` | Heavy Machine Gun | MG-206 Heavy Machine Gun | `↓←↑↓↓` | `HeavyMg` |  |
| `laser_cannon` | Laser Cannon | LAS-98 Laser Cannon | `↓←↓↑←` | `LaserCannon` |  |
| `machine_gun` | Machine Gun | MG-43 Machine Gun | `↓←↓↑→` | `MachineGun` |  |
| `maxigun` | Maxigun | M-1000 Maxigun | `↓←→↓↑↑` | `Maxigun` |  |
| `one_true_flag` | One True Flag | CQC-1 One True Flag | `↓←→→↑` | `Flag` |  |
| `quasar_cannon` | Quasar Cannon | LAS-99 Quasar Cannon | `↓↓↑←→` | `QuasarCannon` |  |
| `railgun` | Railgun | RS-422 Railgun | `↓→↓↑←→` | `Railgun` |  |
| `recoilless_rifle` | Recoilless Rifle | GR-8 Recoilless Rifle | `↓←→→←` | `RecoilessRifle` |  |
| `solo_silo` | Solo Silo | MS-11 Solo Silo | `↓↑→↓↓` | `Silo` |  |
| `spear` | Spear | FAF-14 Spear | `↓↓↑↓↓` | `Spear` |  |
| `speargun` | Speargun | S-11 Speargun | `↓→↓←↑→` | `Harpoon` |  |
| `sta_x3_w_a_s_p_launcher` | StA-X3 W.A.S.P. Launcher | StA-X3 W.A.S.P. Launcher | `↓↓↑↓→` | `WaspLauncher` |  |
| `stalwart` | Stalwart | M-105 Stalwart | `↓←↓↑↑←` | `Stalwart` |  |
| `sterilizer` | Sterilizer | TX-41 Sterilizer | `↓←↑↓←` | `Sterilizer` |  |

## Backpack

| Id | Name | Designation | Code | Firmware macro | Notes |
|-|-|-|-|-|-|
| `ballistic_shield_backpack` | Ballistic Shield Backpack | SH-20 Ballistic Shield Backpack | `↓←↓↓↑←` | `BallisticShieldBackpack` |  |
| `directional_shield` | Directional Shield | SH-51 Directional Shield | `↓↑←→↑↑` | `DirectionalShield` |  |
| `guard_dog` | Guard Dog | AX/AR-23 Guard Dog | `↓↑←↑→↓` | `GuardDog` |  |
| `guard_dog_breath` | Guard Dog Breath | AX/TX-13 Dog Breath | `↓↑←↑→↑` | `GuardDogBreath` |  |
| `guard_dog_hot_dog` | Guard Dog Hot Dog | AX/FLAM-75 Hot Dog | `↓↑←↑←←` | `Hotdog` |  |
| `guard_dog_k_9` | Guard Dog K-9 | AX/ARC-3 K-9 | `↓↑←↑→←` |  |  |
| `guard_dog_rover` | Guard Dog Rover | AX/LAS-5 Rover | `↓↑←↑→→` | `GuardDogRover` |  |
| `hellbomb_portable` | Hellbomb Portable | B-100 Portable Hellbomb | `↓→↑↑↑` |  |  |
| `hover_pack` | Hover Pack | LIFT-860 Hover Pack | `↓↑↑↓←→` | `HoverPack` |  |
| `jump_pack` | Jump Pack | LIFT-850 Jump Pack | `↓↑↑↓↑` | `JumpPack` |  |
| `shield_generator_pack` | Shield Generator Pack | SH-32 Shield Generator Pack | `↓↑←→←→` | `ShieldGeneratorPack` |  |
| `supply_pack` | Supply Pack | B-1 Supply Pack | `↓←↓↑↑↓` | `SupplyPack` |  |
| `warp_pack` | Warp Pack | LIFT-182 Warp Pack | `↓←→↓←→` | `WarpPack` |  |

## Vehicle

| Id | Name | Designation | Code | Firmware macro | Notes |
|-|-|-|-|-|-|
| `bastion_mk_xvi` | Bastion MK XVI | TD-220 Bastion MK XVI | `←↓→↓←↓↑↓↑` | `BastionMKXVI` |  |
| `breakthrough_exosuit` | Breakthrough Exosuit | EXO-55 Breakthrough Exosuit | `←↓→←→↓↑` |  |  |
| `emancipator_exosuit` | Emancipator Exosuit | EXO-49 Emancipator Exosuit | `←↓→↑←↓↑` | `EmancipatorSuit` |  |
| `fast_recon_vehicle` | Fast Recon Vehicle | M-102 Gunner FRV | `←↓→↓→↓↑` | `FRV` |  |
| `incinerator_frv` | Incinerator FRV | M-104 Incinerator FRV | `←↓→←↓↑↑` |  |  |
| `lumberer_exosuit` | Lumberer Exosuit | EXO-51 Lumberer Exosuit | `←↓→↑→←↑` |  |  |
| `patriot_exosuit` | Patriot Exosuit | EXO-45 Patriot Exosuit | `←↓→↑←↓↓` | `PatriotSuit` |  |
| `supply_frv` | Supply FRV | M-103 Supply FRV | `←↓←←↓↑→` |  |  |
| `td_110_maelstrom` | TD-110 Maelstrom | TD-110 Maelstrom | `←↓→↓←↓↑←→` |  | Unverified: wiki.gg; DiversHub starts with ↑ instead of ← |

## Defensive

| Id | Name | Designation | Code | Firmware macro | Notes |
|-|-|-|-|-|-|
| `anti_personnel_minefield` | Anti-Personnel Minefield | MD-6 Anti-Personnel Minefield | `↓←↑→` | `AntiPersonnelMinefield` |  |
| `anti_tank_emplacement` | Anti-Tank Emplacement | E/AT-12 Anti-Tank Emplacement | `↓↑←→→→` |  |  |
| `anti_tank_mines` | Anti-Tank Mines | MD-17 Anti-Tank Mines | `↓←↑↑` | `AntiTankMinefield` |  |
| `autocannon_sentry` | Autocannon Sentry | A/AC-8 Autocannon Sentry | `↓↑→↑←↑` | `AutocannonSentry` |  |
| `ems_mortar_sentry` | EMS Mortar Sentry | A/M-23 EMS Mortar Sentry | `↓↑→↓→` | `EMSMortarSentry` |  |
| `flame_sentry` | Flame Sentry | A/FLAM-40 Flame Sentry | `↓↑→↓↑↑` |  |  |
| `gas_mine` | Gas Mine | MD-8 Gas Mines | `↓←←→` |  |  |
| `gas_mortar_sentry` | Gas Mortar Sentry | A/GM-17 Gas Mortar Sentry | `↓↑→↓←` | `GasMortarSentry` |  |
| `gatling_sentry` | Gatling Sentry | A/G-16 Gatling Sentry | `↓↑→←` | `GatlingSentry` |  |
| `grenadier_battlement` | Grenadier Battlement | E/GL-21 Grenadier Battlement | `↓→↓←→` |  |  |
| `hmg_emplacement` | HMG Emplacement | E/MG-101 HMG Emplacement | `↓↑←→→←` | `HMGEmplacement` |  |
| `incendiary_mines` | Incendiary Mines | MD-I4 Incendiary Mines | `↓←←↓` | `IncendiaryMines` |  |
| `laser_sentry` | Laser Sentry | A/LAS-98 Laser Sentry | `↓↑→↓↑→` | `LaserSentry` |  |
| `machine_gun_sentry` | Machine Gun Sentry | A/MG-43 Machine Gun Sentry | `↓↑→→↑` | `MachineGunSentry` |  |
| `mortar_sentry` | Mortar Sentry | A/M-12 Mortar Sentry | `↓↑→→↓` | `MortarSentry` |  |
| `rocket_sentry` | Rocket Sentry | A/MLS-4X Rocket Sentry | `↓↑→→←` | `RocketSentry` |  |
| `shield_generator_relay` | Shield Generator Relay | FX-12 Shield Generator Relay | `↓↓←→←→` | `ShieldGenerationRelay` |  |
| `tesla_tower` | Tesla Tower | A/ARC-3 Tesla Tower | `↓↑→↑←→` | `TeslaTower` |  |

## Mission

| Id | Name | Designation | Code | Firmware macro | Notes |
|-|-|-|-|-|-|
| `e711_extraction_drill` | Activate E-711 Extraction Drill | Activate E-711 Extraction Drill | `↓↓←←↓↓` |  | No icon in the icon repository. Code only on wiki.gg |
| `aquifer_drill` | Aquifer Drill | Aquifer Drill | `←←←↑↓→↓↓` |  | No icon in the icon repository. Code only on wiki.gg |
| `call_in_super_destroyer` | Call In Super Destroyer | Call In Super Destroyer | `↑↑↓↓←→←→` |  |  |
| `cargo_container` | Cargo Container | Cargo Container | `↑↑↓↓→↓` |  |  |
| `dark_fluid_vessel` | Dark Fluid Vessel | Dark Fluid Vessel | `↑←→↓↑↑` |  |  |
| `eagle_rearm` | Eagle Rearm | Eagle Rearm | `↑↑←↑→` | `EagleRearm` |  |
| `hellbomb` | Hellbomb | NUX-223 Hellbomb | `↓↑←↓↑→↓↑` | `Hellbomb` |  |
| `hive_breaker_drill` | Hive Breaker Drill | Hive Breaker Drill | `←↑↓→↓↓` |  |  |
| `orbital_illumination_flare` | Orbital Illumination Flare | Orbital Illumination Flare | `→→←←` | `OrbitalIlluminationFlare` |  |
| `portable_comms_relay` | Portable Comms Relay | Portable Comms Relay | `↑↑↓↓←←↓` |  | No icon in the icon repository. Code only on wiki.gg |
| `prospecting_drill` | Prospecting Drill | Prospecting Drill | `↓↓←→↓↓` |  |  |
| `reinforce` | Reinforce | Reinforce | `↑↓→←↑` | `Reinforce` |  |
| `reinforcement_pods` | Reinforcement Pods | Reinforcement Pods | `←→↑↑↑` |  | No icon in the icon repository. Same code as Upload Data in all sources |
| `resupply` | Resupply | Resupply | `↓↓↑→` | `Resupply` |  |
| `seaf_artillery` | SEAF Artillery | SEAF Artillery | `→↑↑↓` | `SEAFArtilery` |  |
| `seismic_probe` | Seismic Probe | Seismic Probe | `↑↑←→↓↓` | `SeismicProbe` |  |
| `sos_beacon` | SOS Beacon | SOS Beacon | `↑↓→↑` | `SOSBeacon` |  |
| `sssd_delivery` | SSSD Delivery | SSSD Delivery | `↓↓↓↑↑` |  | No icon in the icon repository. Decided 2026-10-16: Fandom wiki code; ↓↓↓↓↓↑↑ (wiki.gg, DiversHub) considered too long |
| `super_earth_flag` | Super Earth Flag | Super Earth Flag | `↓↑↓↑` | `SuperEarthFlag` |  |
| `tactical_video_camera` | Tactical Video Camera | Tactical Video Camera | `→↓↓↑←←↑` |  | No icon in the icon repository. Code only on wiki.gg |
| `tectonic_drill` | Tectonic Drill | Tectonic Drill | `↑↓↑↓↑↓` |  |  |
| `upload_data` | Upload Data | Upload Data | `←→↑↑↑` | `UploadData` |  |
