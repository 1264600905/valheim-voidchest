using HarmonyLib;
using UnityEngine;

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

            __result = VoidChestFilter.StackAllFiltered(__instance, fromInventory);
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

    /// <summary>容器界面显示后刷新自定义按钮显隐；同时输出配方诊断。</summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    internal static class InventoryGuiShowPatch
    {
        private static void Postfix(Container container)
        {
            VoidChestUi.OnContainerShown(container);
            VoidChestDiagnostics.DumpRecipesAndInventory();
        }
    }

    /// <summary>重量上限：拖拽/分堆对话框存入（带数量）时按剩余额度分堆，完全存不下则无反应。</summary>
    [HarmonyPatch]
    internal static class InventoryMoveItemToThisAmountPatch
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Inventory), "MoveItemToThis",
                new[] { typeof(Inventory), typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int) });
        }

        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref int amount)
        {
            if (item == null || !VoidChestWeight.IsVirtualInventory(__instance))
            {
                return true;
            }

            int allowed = VoidChestWeight.AllowedAmount(__instance, item, amount);
            if (allowed <= 0)
            {
                return false; // 重量已满：无反应
            }

            amount = allowed; // 只能存入部分时自动分堆
            return true;
        }
    }

    /// <summary>重量上限：Shift 快速移动（整堆）时检查，超限改为部分移动。</summary>
    [HarmonyPatch]
    internal static class InventoryMoveItemToThisAllPatch
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Inventory), "MoveItemToThis",
                new[] { typeof(Inventory), typeof(ItemDrop.ItemData) });
        }

        private static bool Prefix(Inventory __instance, Inventory fromInventory, ItemDrop.ItemData item)
        {
            if (item == null || fromInventory == null || !VoidChestWeight.IsVirtualInventory(__instance))
            {
                return true;
            }

            int allowed = VoidChestWeight.AllowedAmount(__instance, item, item.m_stack);
            if (allowed <= 0)
            {
                return false;
            }

            if (allowed >= item.m_stack)
            {
                return true; // 整堆可存，走原版逻辑
            }

            VoidChestWeight.MovePartialToContainer(__instance, fromInventory, item, allowed);
            return false;
        }
    }

    /// <summary>虚空宝箱重量显示为"当前/上限"（0 = 仅显示当前）。</summary>
    [HarmonyPatch(typeof(InventoryGui), "UpdateContainerWeight")]
    internal static class InventoryGuiContainerWeightPatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            var vc = VoidChestManager.CurrentContainer;
            if (vc == null || __instance == null)
            {
                return;
            }

            if (VoidChestManager.CurrentOpenedContainer(__instance) != vc)
            {
                return; // 当前打开的不是虚空宝箱，保持原版显示
            }

            var inv = vc.GetInventory();
            if (inv == null)
            {
                return;
            }

            int current = Mathf.CeilToInt(inv.GetTotalWeight());
            __instance.m_containerWeight.text = vc.MaxWeight > 0f
                ? $"{current}/{Mathf.RoundToInt(vc.MaxWeight)}"
                : current.ToString();
        }
    }
}
