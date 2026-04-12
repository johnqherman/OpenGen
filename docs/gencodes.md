# Gencodes

A gencode is a string of numbers that encodes a CS2 weapon skin (weapon type, skin, pattern, wear, and stickers) so it can be applied on inspect servers.

There are three formats in common use. This plugin currently only accepts `!g`.

## Formats

### `!g` - Combo ID (compact)

```
!g 1835544527
```

The Combo ID is a database key assigned by CS2Inspects when a skin combination is created or looked up on the site. It's the most compact format, but requires a lookup to decode. Any additional fields are ignored.

### `!gen` - Explicit fields

```
!gen {defindex} {skin_id} {pattern} {float} [{sticker fields...}]
!gen 28 763 1 0.00
```

Each field is specified directly. The sticker fields are optional, but if any are included, then all 5 sticker slots must be included. Keychain fields are not included in this format. Produced by CS2Inspects as `genCode1`.

### `!gens` - Full extended fields

```
!gens 28 763 1 0.00000000 0 0 0.00 0 0 0 0 0 0.00 0 0 0 0 0 0.00 0 0 0 0 0 0.00 0 0 0 0 0 0 0 0 0 0
```

Same as `!gen`, but with every sticker and keychain slot fully expanded. Produced by CS2Inspects as `genCode2`.

## Field meanings

| Field           | Description                                                                                 |
| --------------- | ------------------------------------------------------------------------------------------- |
| `defindex`      | Weapon definition index (see table below)                                                   |
| `skin_id`       | Paint kit ID (e.g. 763 = Mjölnir)                                                           |
| `pattern`       | Pattern seed, 0 to 1000.                                                                    |
| `float`         | Wear value, 0.0 (Factory New) to 1.0 (Battle-Scarred). Each skin has its own min/max range. |
| sticker fields  | Per slot: slot index, sticker ID, wear float, X offset, Y offset, rotation                  |
| keychain fields | Slot, charm ID, X/Y/Z position, pattern seed                                                |

## Weapon definition indexes

**Pistols**

| #   | Weapon        | Class               |
| --- | ------------- | ------------------- |
| 1   | Desert Eagle  | weapon_deagle       |
| 2   | Dual Berettas | weapon_elite        |
| 3   | Five-SeveN    | weapon_fiveseven    |
| 4   | Glock-18      | weapon_glock        |
| 30  | Tec-9         | weapon_tec9         |
| 32  | P2000         | weapon_hkp2000      |
| 36  | P250          | weapon_p250         |
| 61  | USP-S         | weapon_usp_silencer |
| 63  | CZ75-Auto     | weapon_cz75a        |
| 64  | R8 Revolver   | weapon_revolver     |

**Rifles**

| #   | Weapon   | Class                |
| --- | -------- | -------------------- |
| 7   | AK-47    | weapon_ak47          |
| 8   | AUG      | weapon_aug           |
| 10  | FAMAS    | weapon_famas         |
| 13  | Galil AR | weapon_galilar       |
| 16  | M4A4     | weapon_m4a1          |
| 39  | SG 553   | weapon_sg556         |
| 40  | SSG 08   | weapon_ssg08         |
| 60  | M4A1-S   | weapon_m4a1_silencer |

**Snipers**

| #   | Weapon  | Class         |
| --- | ------- | ------------- |
| 9   | AWP     | weapon_awp    |
| 11  | G3SG1   | weapon_g3sg1  |
| 38  | SCAR-20 | weapon_scar20 |

**SMGs**

| #   | Weapon   | Class        |
| --- | -------- | ------------ |
| 17  | MAC-10   | weapon_mac10 |
| 19  | P90      | weapon_p90   |
| 23  | MP5-SD   | weapon_mp5sd |
| 24  | UMP-45   | weapon_ump45 |
| 26  | PP-Bizon | weapon_bizon |
| 33  | MP7      | weapon_mp7   |
| 34  | MP9      | weapon_mp9   |

**Heavy**

| #   | Weapon    | Class           |
| --- | --------- | --------------- |
| 14  | M249      | weapon_m249     |
| 25  | XM1014    | weapon_xm1014   |
| 27  | MAG-7     | weapon_mag7     |
| 28  | Negev     | weapon_negev    |
| 29  | Sawed-Off | weapon_sawedoff |
| 35  | Nova      | weapon_nova     |

**Knives**

| #   | Weapon             | Class                        |
| --- | ------------------ | ---------------------------- |
| 42  | Knife (CT default) | weapon_knife                 |
| 59  | Knife (T default)  | weapon_knife_t               |
| 500 | Bayonet            | weapon_knife_bayonet         |
| 503 | Classic Knife      | weapon_knife_css             |
| 505 | Flip Knife         | weapon_knife_flip            |
| 506 | Gut Knife          | weapon_knife_gut             |
| 507 | Karambit           | weapon_knife_karambit        |
| 508 | M9 Bayonet         | weapon_knife_m9_bayonet      |
| 509 | Huntsman Knife     | weapon_knife_tactical        |
| 512 | Falchion Knife     | weapon_knife_falchion        |
| 514 | Bowie Knife        | weapon_knife_survival_bowie  |
| 515 | Butterfly Knife    | weapon_knife_butterfly       |
| 516 | Shadow Daggers     | weapon_knife_push            |
| 517 | Paracord Knife     | weapon_knife_cord            |
| 518 | Survival Knife     | weapon_knife_canis           |
| 519 | Ursus Knife        | weapon_knife_ursus           |
| 520 | Navaja Knife       | weapon_knife_gypsy_jackknife |
| 521 | Nomad Knife        | weapon_knife_outdoor         |
| 522 | Stiletto Knife     | weapon_knife_stiletto        |
| 523 | Talon Knife        | weapon_knife_widowmaker      |
| 525 | Skeleton Knife     | weapon_knife_skeleton        |
| 526 | Kukri Knife        | weapon_knife_kukri           |
