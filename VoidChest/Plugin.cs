using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace VoidChest
{
    [BepInPlugin(Guid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class VoidChestPlugin : BaseUnityPlugin
    {
        public const string Guid = "liu.valheim.voidchest";
        public const string PluginName = "Void Chest";
        public const string PluginVersion = "0.4.0";

        internal static VoidChestPlugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<KeyCode> OpenHotkey;
        internal static ConfigEntry<bool> DebugLog;

        // 容量（行/列）
        internal static ConfigEntry<int> CapacityBlackMetalRows;
        internal static ConfigEntry<int> CapacityBlackMetalCols;
        internal static ConfigEntry<int> CapacityMagicRows;
        internal static ConfigEntry<int> CapacityMagicCols;
        internal static ConfigEntry<int> CapacityFlameRows;
        internal static ConfigEntry<int> CapacityFlameCols;
        internal static ConfigEntry<int> CapacityCrystalRows;
        internal static ConfigEntry<int> CapacityCrystalCols;

        // 重量上限（0 = 无限制）
        internal static ConfigEntry<float> WeightBlackMetal;
        internal static ConfigEntry<float> WeightMagic;
        internal static ConfigEntry<float> WeightFlame;
        internal static ConfigEntry<float> WeightCrystal;

        // 附近存储
        internal static ConfigEntry<bool> EnableNearbyStore;
        internal static ConfigEntry<float> NearbyStoreRange;
        internal static ConfigEntry<bool> NearbyStoreCheckWard;
        internal static ConfigEntry<bool> NearbyStoreIgnoreHotbar;
        internal static ConfigEntry<bool> NearbyStoreIgnoreFood;
        internal static ConfigEntry<bool> NearbyStoreIgnoreAmmo;
        internal static ConfigEntry<bool> NearbyStoreIgnoreMead;
        internal static ConfigEntry<float> NearbyStoreTimeout;

        // 远程存储
        internal static ConfigEntry<bool> EnableRemoteStore;
        internal static ConfigEntry<bool> RemoteStoreAlwaysAvailable;
        internal static ConfigEntry<float> RemoteStoreCacheSeconds;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            OpenHotkey = Config.Bind("General", "OpenHotkey", KeyCode.B,
                "打开/关闭虚空宝箱容器界面的热键。");
            DebugLog = Config.Bind("General", "DebugLog", true,
                "输出详细调试日志（诊断用，默认开启）。");

            var capacityRange = new AcceptableValueRange<int>(1, 8);

            CapacityBlackMetalRows = Config.Bind("Capacity", "BlackMetal.Rows", 2,
                new ConfigDescription("黑金属虚空宝箱：行数。", capacityRange));
            CapacityBlackMetalCols = Config.Bind("Capacity", "BlackMetal.Cols", 6,
                new ConfigDescription("黑金属虚空宝箱：列数。", capacityRange));
            CapacityMagicRows = Config.Bind("Capacity", "Magic.Rows", 3,
                new ConfigDescription("魔能虚空宝箱：行数。", capacityRange));
            CapacityMagicCols = Config.Bind("Capacity", "Magic.Cols", 6,
                new ConfigDescription("魔能虚空宝箱：列数。", capacityRange));
            CapacityFlameRows = Config.Bind("Capacity", "Flame.Rows", 3,
                new ConfigDescription("烈焰虚空宝箱：行数。", capacityRange));
            CapacityFlameCols = Config.Bind("Capacity", "Flame.Cols", 8,
                new ConfigDescription("烈焰虚空宝箱：列数。", capacityRange));
            CapacityCrystalRows = Config.Bind("Capacity", "Crystal.Rows", 4,
                new ConfigDescription("水晶虚空宝箱：行数。", capacityRange));
            CapacityCrystalCols = Config.Bind("Capacity", "Crystal.Cols", 8,
                new ConfigDescription("水晶虚空宝箱：列数。", capacityRange));

            var weightRange = new AcceptableValueRange<float>(0f, 9999f);

            WeightBlackMetal = Config.Bind("Weight", "BlackMetal.Max", 100f,
                new ConfigDescription("黑金属虚空宝箱：重量上限（0 = 无限制）。", weightRange));
            WeightMagic = Config.Bind("Weight", "Magic.Max", 150f,
                new ConfigDescription("魔能虚空宝箱：重量上限（0 = 无限制）。", weightRange));
            WeightFlame = Config.Bind("Weight", "Flame.Max", 300f,
                new ConfigDescription("烈焰虚空宝箱：重量上限（0 = 无限制）。", weightRange));
            WeightCrystal = Config.Bind("Weight", "Crystal.Max", 800f,
                new ConfigDescription("水晶虚空宝箱：重量上限（0 = 无限制）。", weightRange));

            EnableRemoteStore = Config.Bind("RemoteStore", "Enabled", true,
                "启用虚空宝箱界面上的远程存入功能（守护石仓库）。仅单机/主机模式可用。");
            RemoteStoreAlwaysAvailable = Config.Bind("RemoteStore", "AlwaysAvailable", false,
                "远程存储是否一直可用。false = 需要装备魔能（L2）及以上宝箱才解锁；true = 初始即可用。");
            RemoteStoreCacheSeconds = Config.Bind("RemoteStore", "CacheSeconds", 300f,
                new ConfigDescription(
                    "远程存入的扫描结果缓存时间（秒）。0 = 每次重新扫描（慢）。",
                    new AcceptableValueRange<float>(0f, 3600f)));

            EnableNearbyStore = Config.Bind("NearbyStore", "Enabled", true,
                "启用附近存储功能（不影响原版与 V+ 的堆叠按钮行为）。");
            NearbyStoreRange = Config.Bind("NearbyStore", "Range", 30f,
                new ConfigDescription(
                    "附近存储搜索半径（米）。范围 1-100，默认 30。",
                    new AcceptableValueRange<float>(1f, 100f)));
            NearbyStoreCheckWard = Config.Bind("NearbyStore", "CheckWard", true,
                "跳过无权限的领地（守护石）内的容器。");
            NearbyStoreIgnoreHotbar = Config.Bind("NearbyStore", "IgnoreHotbar", true,
                "不存储物品栏第一排（快捷栏）的物品（通常放置装备）。");
            NearbyStoreIgnoreFood = Config.Bind("NearbyStore", "IgnoreFood", false,
                "不存储食物。");
            NearbyStoreIgnoreAmmo = Config.Bind("NearbyStore", "IgnoreAmmo", false,
                "不存储弹药。");
            NearbyStoreIgnoreMead = Config.Bind("NearbyStore", "IgnoreMead", false,
                "不存储蜜酒/药水。");
            NearbyStoreTimeout = Config.Bind("NearbyStore", "TimeoutSeconds", 2f,
                "单个容器等待 RPC 响应超时（秒），超时后跳过。");

            // 旧默认值 10 -> 新默认值 30（仅当配置仍是旧默认值时迁移）
            if (Mathf.Approximately(NearbyStoreRange.Value, 10f))
            {
                NearbyStoreRange.Value = 30f;
                VLog.Info("附近存储范围已从旧默认 10 更新为 30（可在 ConfigurationManager 中调整）。");
            }

            VLog.Info($"{PluginName} v{PluginVersion} 初始化中...");

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(VoidChestPlugin).Assembly);

            foreach (var method in _harmony.GetPatchedMethods())
            {
                VLog.Info($"  Harmony patch: {method.DeclaringType?.Name}.{method.Name}");
            }

            VPlusGate.Install(_harmony);
            VoidChestItems.Register();

            VLog.Info($"{PluginName} v{PluginVersion} 初始化完成。热键={OpenHotkey.Value}, Debug={DebugLog.Value}, 附近存储={EnableNearbyStore.Value}, 远程存入={EnableRemoteStore.Value}(Always={RemoteStoreAlwaysAvailable.Value})");
        }

        private void Update()
        {
            VoidChestNearbyStore.Update();
            VoidChestRemoteStore.Update();

            var player = Player.m_localPlayer;
            if (player == null || InventoryGui.instance == null)
            {
                return;
            }

            if (Input.GetKeyDown(OpenHotkey.Value))
            {
                VoidChestManager.Toggle(player);
            }
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
        }
    }
}
