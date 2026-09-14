using System;
using HarmonyLib;
using UnityEngine;

namespace VoidChest
{
    /// <summary>
    /// 无网络的虚拟容器：由 VoidChestManager 打开到原版容器 UI。
    /// Container.Awake 被 Harmony Prefix 拦截（不做 ZNetView 初始化）。
    /// </summary>
    public class VirtualContainer : Container
    {
        private static readonly AccessTools.FieldRef<Container, Inventory> InventoryRef =
            AccessTools.FieldRefAccess<Container, Inventory>("m_inventory");

        internal void InitVirtual()
        {
            SetInventory(new Inventory("Void Chest", null, 6, 2));
        }

        internal void SetInventory(Inventory inv)
        {
            var old = GetInventory();
            if (old != null)
            {
                old.m_onChanged = (Action)Delegate.Remove(old.m_onChanged, new Action(OnInventoryChanged));
            }

            InventoryRef(this) = inv;

            if (inv != null)
            {
                inv.m_onChanged = (Action)Delegate.Combine(inv.m_onChanged, new Action(OnInventoryChanged));
            }
        }

        private void OnInventoryChanged()
        {
            var player = Player.m_localPlayer;
            var inv = GetInventory();
            if (player != null && inv != null)
            {
                VoidChestSave.SaveFrom(inv, player);
            }
        }
    }
}
