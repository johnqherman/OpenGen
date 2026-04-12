# CS2Inspects.com API

Endpoint: `GET https://api.cs2inspects.com/getGenCode?url={input}`

No public documentation exists. Notes based on live testing.

## Valid inputs

| Input                                          | Result                                  |
| ---------------------------------------------- | --------------------------------------- |
| Combo ID (e.g. `1835544527`)                   | 200 - full JSON                         |
| ECON string (`csgo_econ_action_preview {hex}`) | 200 - full JSON, `Authenticity: "Real"` |
| Combo ID not in DB                             | 404                                     |
| Other/invalid input                            | 500                                     |

## Response - `genCodeDetail` object

The full object returned by the API is as follows:

### Identity

| Field                | Type   | Notes                    |
| -------------------- | ------ | ------------------------ |
| `Authenticity`       | string | `Fake` or `Real`         |
| `GenCode`            | string | e.g. `!g {comboid}`      |
| `InspectLink`        | string | Steam inspect URL        |
| `InspectLinkConsole` | string | Console-form ECON string |
| `ECON`               | string | Raw ECON hex             |

### Item

| Field           | Type   | Notes                                                             |
| --------------- | ------ | ----------------------------------------------------------------- |
| `Item_Type`     | string | `primary`, `secondary`, `melee`, `gloves`, etc.                   |
| `Item_ID`       | string | CS2 weapon definition index, see `docs/gencodes.md`               |
| `Item_Name`     | string | `{Weapon} \| {Skin}`, with `★`, `StatTrak™`, or `Souvenir` prefix |
| `Skin_ID`       | string | Skin definition index                                             |
| `Skin_Name`     | string | Same format as `Item_Name`                                        |
| `Skin_FullName` | string | Full name including prefixes                                      |
| `Pattern_ID`    | string | Paint seed, 0 to 1000                                             |
| `Float_Value`   | string | Wear float, 14 decimal places                                     |
| `Float`         | string | Duplicate of `Float_Value`                                        |
| `Wear_Name`     | string | `Factory New` to `Battle-Scarred`                                 |
| `Rarity_ID`     | string | 1 to 7                                                            |
| `Rarity_Name`   | string | `Consumer Grade` through `Contraband`                             |
| `Quality_ID`    | string | Empty if not applicable                                           |
| `Quality_Name`  | string | Empty if not applicable                                           |

### Stickers (repeat for `Sticker1` - `Sticker5`)

| Field              | Type   | Notes                                |
| ------------------ | ------ | ------------------------------------ |
| `Sticker{N}_ID`    | int    | Sticker definition index, 0 if empty |
| `Sticker{N}_Slot`  | int    | Slot index on model                  |
| `Sticker{N}_Value` | float  | Wear/scrape value                    |
| `Sticker{N}_R`     | float  | Rotation                             |
| `Sticker{N}_X`     | float  | X position                           |
| `Sticker{N}_Y`     | float  | Y position                           |
| `Sticker{N}_Image` | string | CDN URL, empty if no sticker         |

### Keychain

| Field                  | Type   | Notes                                |
| ---------------------- | ------ | ------------------------------------ |
| `KeyChain_ID`          | int    | Keychain definition index, 0 if none |
| `KeyChain_Pattern`     | int    | Pattern seed                         |
| `KeyChain_X/Y/Z`       | float  | Position offset                      |
| `KeyChain_Image_Front` | string | CDN URL, empty if none               |

### Other

| Field              | Type   | Notes                        |
| ------------------ | ------ | ---------------------------- |
| `Stattrak_enabled` | string | `0` or `1`                   |
| `Stattrak_value`   | int    | Kill count                   |
| `Nametag_value`    | string | Name tag text, empty if none |

---

## `source` and `Authenticity`

| `source`         | `Authenticity` | Meaning                                      |
| ---------------- | -------------- | -------------------------------------------- |
| `"makefakelink"` | `"Fake"`       | Created via CS2Inspects' fake-link generator |
| `"gencode"`      | `"Fake"`       | Created from a `!gen`/`!gens` code entry     |
| `source_id: 4`   | `"Real"`       | Resolved from a real CS2 ECON/inspect string |

All Combo IDs resolve to the same skin data regardless of authenticity. `"Fake"` just means the item doesn't correspond to a real inventory item.
