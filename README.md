# OpenGen

![Build & Release](https://github.com/johnqherman/OpenGen/actions/workflows/release.yml/badge.svg)
![Latest Release](https://img.shields.io/github/v/release/johnqherman/OpenGen)

A [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) plugin for applying CS2 weapon skins via gencodes.

## Features

- **Weapon skins**: Apply any skin by gencode or by explicit parameters
- **Sticker support**: All 5 slots with wear, position, and rotation offsets
- **Knives, gloves, and agents**: Full model support
- **Charms**: Custom position and rotation
- **StatTrak and nametags**: Preserved through gencode parsing
- **Combo gencodes**: Apply a full loadout (primary, secondary, knife, gloves, agent) in one command
- **Persistent**: Skins reapplied across respawns

## Commands

| Command             | Description                                                                          |
| ------------------- | ------------------------------------------------------------------------------------ |
| `!g <gencode>`      | Apply a weapon skin from a [CS2Inspects](https://cs2inspects.com) gencode            |
| `!gen <parameters>` | Apply a weapon skin with explicit parameters                                         |
| `!combo <gencode>`  | Apply a full loadout (primary, secondary, knife, gloves, agent) from a combo gencode |

**Examples:**

```
!g 1835544527
!combo 8920341482
!gen 7 976 321 0.14 4365 0.1 4366 0.0
```

## Requirements

- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) v1.0.366 or later

## Installation

1. Download the latest `OpenGen-<version>.zip` from [Releases](../../releases).
2. Extract the zip and merge the `counterstrikesharp/` folder into `csgo/addons/` on your server.
3. Restart your server or reload plugins.

## Building from Source

**Requirements:** [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```bash
dotnet publish -c Release
```

Output: `bin/Release/net8.0/publish/OpenGen.dll`
