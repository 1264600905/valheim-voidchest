# Void Chest

A Valheim mod: a magic container that travels with your character.

Equip it and press the hotkey to open your personal container. Contents are bound to the **character save** — dropping it, trading it, dying, or upgrading it never moves or loses the contents. Comes with a four-tier upgrade chain, nearby quick storage, and a guard-stone remote deposit.

> Localization: English / Simplified Chinese / Traditional Chinese

---

## Features

- **Four-tier chest chain**: Black Metal → Eitr → Flametal → Crystal, capacities `2x6 / 3x6 / 3x8 / 4x8` (configurable).
- **Character-level data**: contents live in the character file (`Player.m_customData`) and follow you across worlds and servers; the chest item is just a "key".
- **Equip to open**: uses a Utility slot (compatible with ExtraSlots extra slots), default hotkey `B` (configurable).
- **Weight limit**: defaults `100 / 150 / 300 / 800` (0 = unlimited, configurable).
  - Drag / Shift-move over the limit **auto-splits** (stores only what fits).
  - When full, items **bounce back** (no silent loss).
  - Container weight is shown as `current/max`.
  - Nearby / remote storage is **not** limited by weight.
- **Nearby storage**: one click stores backpack items into all containers within 30 m (configurable).
  - Automatically ignores: the first hotbar row, equipped items, ExtraSlots dedicated slots (quick/ammo/food/misc/extra equipment), and containers in use by other players.
- **Guard-stone remote deposit**: while away from home, deposit materials remotely into **all containers** inside the territory of a guard stone you have access to.
- **Configurable**: capacities, weights, ranges, filters, remote unlock, hotkey, timeouts, caching (ConfigurationManager supported).

## Recipe

| Tier | Item | Capacity (default) | Materials | Station |
|---|---|---|---|---|
| 1 | Black Metal Void Chest | 2x6 | Dragon Tear x5 + Fine Wood x10 + Black Metal x20 | Forge |
| 2 | Eitr Void Chest | 3x6 | Black Metal Void Chest x1 + Refined Eitr x10 + Yagluth Thing x2 | Forge |
| 3 | Flametal Void Chest | 3x8 | Eitr Void Chest x1 + Flametal x10 + Fader Relic x2 | Black Forge |
| 4 | Crystal Void Chest | 4x8 | Flametal Void Chest x1 + Blood Gold x10 + Liquid Frost x10 | Black Forge |

## Usage

1. Equip the Void Chest (Utility slot).
2. Press `B` to open the interface.
3. Interface buttons (single button, toggles):
   - **Nearby Storage**: store into surrounding containers;
   - **Remote Deposit**: store into home containers inside a guard stone's territory (requires Eitr/L2 or higher chest; can be set to always available in config).
4. Vanilla "Stack All / Take All" buttons still work.

## Remote Deposit Notes

- You must be the guard stone's **creator** or be on its **permitted** list.
- Range is the guard stone's own territory radius (XZ circle).
- **Works for multiplayer clients**: both the server (dedicated or host) and the client must have this mod installed.
  - The client sends a filtered backpack snapshot to the server; the server scans and stores, then returns the result;
  - The client removes exactly the items the server reports — safe even if the inventory changed while waiting;
  - If the server does not have the mod, a "no response" message appears after ~30 s;
  - Server can disable it with `RemoteStore.Enabled = false`.
- Scan results are cached (default 300 s, per player); repeated deposits complete in seconds. Removed containers are skipped automatically.

## Configuration

File: `BepInEx/config/trigger.valheim.voidchest.cfg` (ConfigurationManager supported).

| Section | Key | Default | Description |
|---|---|---|---|
| General | OpenHotkey | B | Open/close hotkey |
| General | DebugLog | true | Debug logging |
| Capacity | BlackMetal / Magic / Flame / Crystal Rows/Cols | 2x6 / 3x6 / 3x8 / 4x8 | Capacity per tier (1-8) |
| Weight | BlackMetal / Magic / Flame / Crystal Max | 100 / 150 / 300 / 800 | Weight limit (0-9999, 0 = unlimited) |
| NearbyStore | Enabled | true | Enable nearby storage |
| NearbyStore | Range | 30 | Search radius (1-100 m) |
| NearbyStore | CheckWard | true | Skip containers in territories you lack access to |
| NearbyStore | IgnoreHotbar | true | Do not store hotbar items |
| NearbyStore | IgnoreFood / IgnoreAmmo / IgnoreMead | false | Do not store food / ammo / mead |
| NearbyStore | TimeoutSeconds | 2 | Per-container response timeout |
| RemoteStore | Enabled | true | Enable remote deposit |
| RemoteStore | AlwaysAvailable | false | Remote always available (false = unlocked by Eitr/L2) |
| RemoteStore | CacheSeconds | 300 | Scan cache time (0 = rescan every time) |

## Installation

1. Install [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
2. Install [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
3. Drop `VoidChest.dll` into `BepInEx/plugins/VoidChest/`.

## Dependencies

- BepInExPack Valheim 5.4.2350+
- Jotunn 2.29.2+
- Optional: [Valheim Plus](https://thunderstore.io/c/valheim/p/Grantapher/ValheimPlus_Grantapher_Temporary/) (compatible; AutoStack spread is suppressed while nearby storage runs)
- Optional: [ExtraSlots](https://thunderstore.io/c/valheim/p/shudnal/ExtraSlots/) (extra Utility slot support)

## Repository

https://github.com/1264600905/valheim-voidchest

## Build

```powershell
cd VoidChest
dotnet build .\VoidChest.csproj -c Release
```

Output: `BepInEx/plugins/VoidChest/VoidChest.dll` (output path is preconfigured).

## Known Limitations

- Remote deposit requires the **same mod version on server and client** (otherwise you get the "no response" message).
- After changing capacities, items beyond the visible range keep their data but are hidden (visible again with a larger capacity).
- Uninstalling the mod makes the chest item inert; character data is preserved (reinstall to recover).

## Changelog

- **0.5.0**: Remote deposit supports multiplayer clients (custom RPC: client snapshot → server processing → diff-based removal); scan cache per player.
- **0.4.x**: Configurable capacity/weight; remote unlock (Eitr/L2 by default); single toggle button; localized item descriptions (EN/CN/TW); L3 recipe material fix.
- **0.3.2**: Remote storage performance (full snapshot classification, caching, 8 ms frame budget, staged perf stats).
- **0.3.1**: Fixed ZDO object pool reuse risk (store only ZDOIDs).
- **0.3.0**: P2 guard-stone remote warehouse.
- **0.2.0**: P1 nearby storage, ExtraSlots compatibility, V+ gate, UI buttons.
- **0.1.x**: P0 basics (item/recipe/virtual container/save/hotkey).

---

## 简体中文

一个 Valheim 模组：一件随角色旅行的魔法容器。

装备后按热键打开专属容器；内容绑定在**角色存档**上——丢进箱子、交易、掉落、合成升级都不会丢失或转移。支持四级升级链、附近快速存储与「守护石」远程仓库。

- **四级宝箱链**：黑金属 → 魔能 → 烈焰 → 水晶，容量 `2×6 / 3×6 / 3×8 / 4×8`（可配置）。
- **角色级数据**：内容存于角色档案，跨世界、跨服务器随身携带。
- **装备打开**：占用 Utility 槽（兼容 ExtraSlots 额外槽），默认热键 `B`。
- **重量上限**：`100 / 150 / 300 / 800`（0 = 无限制）；超额自动分堆，满了物品回弹。
- **附近存储**：一键存入周围 30m 内所有箱子（忽略快捷栏/已装备/ExtraSlots 专用槽/被占用箱子）。
- **守护石远程仓库**：远程存入有权限守护石领地内的家箱（联机需服务端同版本）。
- **可配置**：容量、重量、范围、过滤、远程解锁、热键、超时与缓存。

| 等级 | 物品 | 容量（默认） | 配方材料 | 工作台 |
|---|---|---|---|---|
| 1 | 黑金属虚空宝箱 | 2×6 | 龙之泪×5 + 细木×10 + 黑金属×20 | 熔炉 |
| 2 | 魔能虚空宝箱 | 3×6 | 黑金属虚空宝箱×1 + 埃达精华×10 + 裂魂×2 | 熔炉 |
| 3 | 烈焰虚空宝箱 | 3×8 | 魔能虚空宝箱×1 + 焰金属×10 + 青焰龙王圣物×2 | 黑熔炉 |
| 4 | 水晶虚空宝箱 | 4×8 | 烈焰虚空宝箱×1 + 血金×10 + 液态冰霜×10 | 黑熔炉 |

---

## 繁體中文

一個 Valheim 模組：一件隨角色旅行的魔法容器。

裝備後按熱鍵打開專屬容器；內容綁定在**角色存檔**上——丟進箱子、交易、掉落、合成升級都不會遺失或轉移。支援四級升級鏈、附近快速存儲與「守護石」遠端倉庫。

- **四級寶箱鏈**：黑金屬 → 魔能 → 烈焰 → 水晶，容量 `2×6 / 3×6 / 3×8 / 4×8`（可設定）。
- **角色級資料**：內容存於角色檔案，跨世界、跨伺服器隨身攜帶。
- **裝備打開**：佔用 Utility 槽（相容 ExtraSlots 額外槽），預設熱鍵 `B`。
- **重量上限**：`100 / 150 / 300 / 800`（0 = 無限制）；超額自動分堆，滿了物品回彈。
- **附近存儲**：一鍵存入周圍 30m 內所有箱子（忽略快捷欄/已裝備/ExtraSlots 專用槽/被佔用箱子）。
- **守護石遠端倉庫**：遠端存入有權限守護石領地內的家箱（連線需伺服器同版本）。
- **可設定**：容量、重量、範圍、過濾、遠端解鎖、熱鍵、逾時與快取。

| 等級 | 物品 | 容量（預設） | 配方材料 | 工作台 |
|---|---|---|---|---|
| 1 | 黑金屬虛空寶箱 | 2×6 | 龍之淚×5 + 細木×10 + 黑金屬×20 | 熔爐 |
| 2 | 魔能虛空寶箱 | 3×6 | 黑金屬虛空寶箱×1 + 埃達精華×10 + 裂魂×2 | 熔爐 |
| 3 | 烈焰虛空寶箱 | 3×8 | 魔能虛空寶箱×1 + 焰金屬×10 + 青焰龍王聖物×2 | 黑熔爐 |
| 4 | 水晶虛空寶箱 | 4×8 | 烈焰虛空寶箱×1 + 血金×10 + 液態冰霜×10 | 黑熔爐 |
