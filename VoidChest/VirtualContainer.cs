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

        /// <summary>加载存档数据期间禁止回写，避免 RemoveAll 触发回调覆盖存档。</summary>
        internal bool SuppressSave;

        internal void InitVirtual()
        {
            SetInventory(new Inventory("Void Chest", null, 6, 2));
            VLog.Debug("VirtualContainer.InitVirtual: 占位 Inventory 6x2 已创建。");
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

            VLog.Debug($"VirtualContainer.SetInventory: {(inv != null ? inv.GetWidth() + "x" + inv.GetHeight() : "null")}");
        }

        private void OnInventoryChanged()
        {
            if (SuppressSave)
            {
                VLog.Debug("OnInventoryChanged: SuppressSave=true，跳过保存。");
                return;
            }

            var player = Player.m_localPlayer;
            var inv = GetInventory();
            if (player != null && inv != null)
            {
                VLog.Debug($"OnInventoryChanged: 库存变更 -> {inv.NrOfItems()} 件，保存。");
                VoidChestSave.SaveFrom(inv, player);
            }
        }
    }
}
