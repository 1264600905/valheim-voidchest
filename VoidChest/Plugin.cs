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
        public const string PluginVersion = "0.2.0";

        internal static VoidChestPlugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<KeyCode> OpenHotkey;
        internal static ConfigEntry<bool> DebugLog;

        internal static ConfigEntry<bool> EnableNearbyStore;
        internal static ConfigEntry<float> NearbyStoreRange;
        internal static ConfigEntry<bool> NearbyStoreCheckWard;
        internal static ConfigEntry<bool> NearbyStoreIgnoreHotbar;
        internal static ConfigEntry<bool> NearbyStoreIgnoreFood;
        internal static ConfigEntry<bool> NearbyStoreIgnoreAmmo;
        internal static ConfigEntry<bool> NearbyStoreIgnoreMead;
        internal static ConfigEntry<float> NearbyStoreTimeout;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            OpenHotkey = Config.Bind("General", "OpenHotkey", KeyCode.B,
                "打开/关闭虚空宝箱容器界面的热键。");
            DebugLog = Config.Bind("General", "DebugLog", true,
                "输出详细调试日志（诊断用，默认开启）。");

            EnableNearbyStore = Config.Bind("NearbyStore", "Enabled", true,
                "启用虚空宝箱界面上的\"附近存储\"按钮（不影响原版与 V+ 的堆叠按钮行为）。");
            NearbyStoreRange = Config.Bind("NearbyStore", "Range", 10f,
                "附近存储搜索半径（米）。");
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

            VLog.Info($"{PluginName} v{PluginVersion} 初始化中...");

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(VoidChestPlugin).Assembly);

            foreach (var method in _harmony.GetPatchedMethods())
            {
                VLog.Info($"  Harmony patch: {method.DeclaringType?.Name}.{method.Name}");
            }

            VPlusGate.Install(_harmony);
            VoidChestItems.Register();

            VLog.Info($"{PluginName} v{PluginVersion} 初始化完成。热键={OpenHotkey.Value}, Debug={DebugLog.Value}, 附近存储={EnableNearbyStore.Value}");
        }

        private void Update()
        {
            VoidChestNearbyStore.Update();

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
