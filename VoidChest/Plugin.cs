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
        public const string PluginVersion = "0.1.1";

        internal static VoidChestPlugin Instance;
        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyCode> OpenHotkey;
        internal static ConfigEntry<bool> DebugLog;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            OpenHotkey = Config.Bind("General", "OpenHotkey", KeyCode.B,
                "打开/关闭虚空宝箱容器界面的热键。");
            DebugLog = Config.Bind("General", "DebugLog", true,
                "输出详细调试日志（诊断用，默认开启）。");

            VLog.Info($"{PluginName} v{PluginVersion} 初始化中...");

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(VoidChestPlugin).Assembly);

            foreach (var method in _harmony.GetPatchedMethods())
            {
                VLog.Info($"  Harmony patch: {method.DeclaringType?.Name}.{method.Name}");
            }

            VoidChestItems.Register();

            VLog.Info($"{PluginName} v{PluginVersion} 初始化完成。热键={OpenHotkey.Value}, Debug={DebugLog.Value}");
        }

        private void Update()
        {
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
