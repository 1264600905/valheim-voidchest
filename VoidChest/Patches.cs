using HarmonyLib;

namespace VoidChest
{
    [HarmonyPatch(typeof(Container), "Awake")]
    internal static class ContainerAwakePatch
    {
        private static bool Prefix(Container __instance)
        {
            if (__instance is VirtualContainer vc)
            {
                vc.InitVirtual();
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.IsOwner))]
    internal static class ContainerIsOwnerPatch
    {
        private static bool Prefix(Container __instance, ref bool __result)
        {
            if (__instance is VirtualContainer)
            {
                __result = true;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.SetInUse))]
    internal static class ContainerSetInUsePatch
    {
        private static bool Prefix(Container __instance)
        {
            return !(__instance is VirtualContainer);
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.IsInUse))]
    internal static class ContainerIsInUsePatch
    {
        private static bool Prefix(Container __instance, ref bool __result)
        {
            if (__instance is VirtualContainer)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Container), "CheckForChanges")]
    internal static class ContainerCheckForChangesPatch
    {
        private static bool Prefix(Container __instance)
        {
            return !(__instance is VirtualContainer);
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.Save))]
    internal static class PlayerSavePatch
    {
        private static void Prefix(Player __instance)
        {
            VoidChestManager.FlushActive(__instance);
        }
    }

    /// <summary>附近存储流程中，用过滤版本替换原版 Inventory.StackAll。</summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll))]
    internal static class InventoryStackAllPatch
    {
        private static bool Prefix(Inventory __instance, Inventory fromInventory, ref int __result)
        {
            if (!VoidChestNearbyStore.FilterActive)
            {
                return true;
            }

            __result = VoidChestNearbyStore.FilteredStackAll(__instance, fromInventory, false);
            return false;
        }
    }

    /// <summary>捕获容器堆叠 RPC 响应（含授权结果），推进附近存储队列。</summary>
    [HarmonyPatch(typeof(Container), "RPC_StackResponse")]
    internal static class ContainerRpcStackResponsePatch
    {
        private static void Postfix(long uid, bool granted)
        {
            VoidChestNearbyStore.OnStackResponse(granted);
        }
    }

    /// <summary>容器界面显示后刷新自定义按钮显隐。</summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    internal static class InventoryGuiShowPatch
    {
        private static void Postfix(Container container)
        {
            VoidChestUi.OnContainerShown(container);
        }
    }
}
