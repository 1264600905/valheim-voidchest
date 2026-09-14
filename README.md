# 虚空宝箱（Void Chest）

一个 Valheim 模组：一件随角色旅行的魔法容器。

装备后按热键打开专属容器；内容绑定在**角色存档**上 —— 丢进箱子、交易、掉落、合成升级都不会丢失或转移。支持四级升级链、附近快速存储与「守护石」远程仓库。

> 本地化：English / 简体中文 / 繁體中文

---

## 功能

- **四级宝箱链**：黑金属 → 魔能 → 烈焰 → 水晶，容量依次 `2×6 / 3×6 / 3×8 / 4×8`（可配置）。
- **角色级数据**：内容存于角色档案（`Player.m_customData`），跨世界、跨服务器随身携带；宝箱物品只是"钥匙"。
- **装备打开**：占用 Utility 槽（兼容 ExtraSlots 额外槽），默认热键 `B`（可配置）。
- **重量上限**：默认 `100 / 150 / 300 / 800`（0 = 无限制，可配置）。
  - 拖拽 / Shift 快速移动超额时**自动分堆**（只存入可容纳数量）。
  - 重量已满时**无反应**（物品回弹）。
  - 容器重量显示为 `当前/上限`。
  - 附近 / 远程存储**不受重量限制**。
- **附近存储**：一键把背包物品存进周围 30m（可配置）内所有箱子。
  - 自动忽略：物品栏第一排（快捷栏）、已装备物品、ExtraSlots 专用槽位（快捷/弹药/食物/杂项/额外装备）、被其他玩家占用的箱子。
- **守护石远程仓库**：出门在外，把物资远程存进「你有权限的守护石」领地范围内的**家里所有箱子**。
- **可配置**：容量、重量、范围、过滤规则、远程解锁方式、热键、超时与缓存（支持 ConfigurationManager）。

## 制作配方

| 等级 | 物品 | 容量（默认） | 配方材料 | 工作台 |
|---|---|---|---|---|
| 1 | 黑金属虚空宝箱 | 2×6 | 龙之泪×5 + 细木×10 + 黑金属×20 | 熔炉 |
| 2 | 魔能虚空宝箱 | 3×6 | 黑金属虚空宝箱×1 + 埃达精华×10 + 裂魂×2 | 熔炉 |
| 3 | 烈焰虚空宝箱 | 3×8 | 魔能虚空宝箱×1 + 焰金属×10 + 青焰龙王圣物×2 | 黑熔炉 |
| 4 | 水晶虚空宝箱 | 4×8 | 烈焰虚空宝箱×1 + 血金×10 + 液态冰霜×10 | 黑熔炉 |

## 使用

1. 装备虚空宝箱（Utility 槽）。
2. 按 `B` 打开界面。
3. 界面按钮（单按钮动态切换）：
   - **附近存储**：存入周围箱子；
   - **远程存入**：存入守护石领地内的家箱（需要装备魔能/L2 及以上宝箱解锁；或在配置中开启"始终可用"）。
4. 原版"全部堆叠 / 全部取出"按钮同样可用。

## 远程仓库说明

- 需要玩家是守护石的**创建者**或已在它的 **permitted（允许）名单**中。
- 范围使用守护石自带的领地半径（XZ 圆形）。
- **仅单机 / 主机模式可用**；专用服务器需要服务端安装本模组。
- 扫描结果会缓存（默认 300 秒），重复存入秒级完成；被拆的箱子自动跳过。

## 配置

配置文件：`BepInEx/config/liu.valheim.voidchest.cfg`（支持 ConfigurationManager 图形界面）。

| 配置节 | 键 | 默认 | 说明 |
|---|---|---|---|
| General | OpenHotkey | B | 打开/关闭热键 |
| General | DebugLog | true | 调试日志 |
| Capacity | BlackMetal / Magic / Flame / Crystal 的 Rows/Cols | 2×6 / 3×6 / 3×8 / 4×8 | 每级容量（1-8） |
| Weight | BlackMetal / Magic / Flame / Crystal 的 Max | 100 / 150 / 300 / 800 | 重量上限（0-9999，0=无限制） |
| NearbyStore | Enabled | true | 启用附近存储 |
| NearbyStore | Range | 30 | 搜索半径（1-100 米） |
| NearbyStore | CheckWard | true | 跳过无权限领地内的容器 |
| NearbyStore | IgnoreHotbar | true | 不存储快捷栏物品 |
| NearbyStore | IgnoreFood / IgnoreAmmo / IgnoreMead | false | 不存储食物 / 弹药 / 蜜酒 |
| NearbyStore | TimeoutSeconds | 2 | 单容器响应超时 |
| RemoteStore | Enabled | true | 启用远程存入 |
| RemoteStore | AlwaysAvailable | false | 远程是否始终可用（false = 魔能/L2 解锁） |
| RemoteStore | CacheSeconds | 300 | 扫描缓存时间（0=每次重扫） |

## 安装

1. 安装 [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)。
2. 安装 [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/)。
3. 将 `VoidChest.dll` 放入 `BepInEx/plugins/VoidChest/`。

## 依赖

- BepInExPack Valheim 5.4.2350+
- Jotunn 2.29.2+
- 可选：[Valheim Plus](https://thunderstore.io/c/valheim/p/Grantapher/ValheimPlus_Grantapher_Temporary/)（兼容，启用附近存储时自动屏蔽其 AutoStack 扩散）
- 可选：[ExtraSlots](https://thunderstore.io/c/valheim/p/shudnal/ExtraSlots/)（额外 Utility 槽装备支持）

## 构建

```powershell
cd VoidChest
dotnet build .\VoidChest.csproj -c Release
```

输出：`BepInEx/plugins/VoidChest/VoidChest.dll`（工程内已配置输出路径）。

## 已知限制

- 远程存入仅单机/主机模式（专用服务器需服务端安装，尚未支持）。
- 调整容量后，超出显示范围的物品数据会保留但不可见（换回更大容量可见）。
- 卸载模组后宝箱物品会失效；角色档案中的数据仍保留（重装可恢复）。

## 更新日志

- **0.4.0**：容量/重量可配置；远程解锁机制（默认 L2 解锁）；单按钮动态切换；物品描述本地化（英/简/繁）。
- **0.3.2**：远程存储性能优化（全量快照分类、缓存、8ms 帧预算、分阶段性能统计）。
- **0.3.1**：修复 ZDO 对象池复用风险（只保存 ZDOID）。
- **0.3.0**：P2 守护石远程仓库。
- **0.2.0**：P1 附近存储、ExtraSlots 兼容、V+ 门控、界面按钮。
- **0.1.x**：P0 基础（物品/配方/虚拟容器/存档/热键）。

## 仓库

https://github.com/1264600905/valheim-voidchest （Private，开发中）
