using HarmonyLib;

namespace VoidChest
{
    /// <summary>
    /// 当自研"附近存储"启用时，屏蔽 Valheim Plus 的 AutoStack 自动扩散（避免双重/无过滤扩散）。
    /// 通过反射定位 V+ 内部类，失败时降级为仅日志提示。
    /// </summary>
    internal static class VPlusGate
    {
        private static bool _installed;

        internal static void Install(Harmony harmony)
        {
            if (_installed)
            {
                return;
            }

            var type = AccessTools.TypeByName("ValheimPlus.Inventory_StackAll_Patch");
            if (type == null)
            {
                VLog.Info("未检测到 Valheim Plus AutoStack，跳过门控。");
                return;
            }

            var target = AccessTools.Method(type, "Postfix");
            if (target == null)
            {
                VLog.Warn("检测到 Valheim Plus 但未找到 Inventory_StackAll_Patch.Postfix，跳过门控（建议关闭 V+ [AutoStack]）。");
                return;
            }

            var prefix = AccessTools.Method(typeof(VPlusGate), nameof(GatePrefix));
            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            _installed = true;
            VLog.Info("已安装 V+ AutoStack 门控：启用附近存储时屏蔽 V+ 扩散。");
        }

        private static bool GatePrefix()
        {
            bool nearbyStoreEnabled = VoidChestPlugin.EnableNearbyStore != null &&
                                      VoidChestPlugin.EnableNearbyStore.Value;
            return !nearbyStoreEnabled;
        }
    }
}
