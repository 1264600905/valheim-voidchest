using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace VoidChest
{
    internal static class VoidChestManager
    {
        internal const string PrefabPrefix = "VoidChest";

        private static readonly AccessTools.FieldRef<InventoryGui, Container> CurrentContainerRef =
            AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");

        private static VirtualContainer _container;
        private static ItemDrop.ItemData _openItem;

        internal static void Toggle(Player player)
        {
            if (InventoryGui.instance == null)
            {
                return;
            }

            bool containerOpen = InventoryGui.instance.IsContainerOpen();
            bool isOurContainer = containerOpen && IsVoidChestOpen();
            VLog.Debug($"Toggle: containerOpen={containerOpen}, isOurContainer={isOurContainer}");

            if (containerOpen)
            {
                if (isOurContainer)
                {
                    InventoryGui.instance.Hide();
                    VLog.Info("关闭虚空宝箱界面。");
                }
                return;
            }

            var item = GetEquippedChest(player);
            if (item == null)
            {
                VLog.Debug("未装备虚空宝箱，无法打开。");
                return;
            }

            Open(player, item);
        }

        internal static bool IsVoidChestOpen()
        {
            return InventoryGui.instance != null &&
                   CurrentContainerRef(InventoryGui.instance) is VirtualContainer;
        }

        internal static ItemDrop.ItemData GetEquippedChest(Player player)
        {
            var inv = player.GetInventory();
            if (inv == null)
            {
                return null;
            }

            var equipped = inv.GetEquippedItems();
            if (VLog.DebugEnabled)
            {
                var names = new List<string>();
                foreach (var e in equipped)
                {
                    names.Add(e?.m_shared?.m_name ?? "?");
                }
                VLog.Debug($"已装备物品: [{string.Join(", ", names)}]");
            }

            foreach (var item in equipped)
            {
                if (item?.m_dropPrefab != null && item.m_dropPrefab.name.StartsWith(PrefabPrefix))
                {
                    VLog.Debug($"检测到虚空宝箱: prefab={item.m_dropPrefab.name}, name={item.m_shared.m_name}");
                    return item;
                }
            }

            return null;
        }

        private static VirtualContainer EnsureContainer(Player player)
        {
            if (_container != null)
            {
                return _container;
            }

            var go = new GameObject("VoidChest_VirtualContainer");
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = Vector3.zero;

            _container = go.AddComponent<VirtualContainer>();
            VLog.Info("虚拟容器已创建。");
            return _container;
        }

        internal static void Open(Player player, ItemDrop.ItemData item)
        {
            var vc = EnsureContainer(player);
            var size = GetSize(item);
            var name = item.m_shared.m_name;

            vc.m_name = name;
            vc.m_width = size.cols;
            vc.m_height = size.rows;

            var inv = new Inventory(name, null, size.cols, size.rows);

            vc.SuppressSave = true;
            int itemCount;
            try
            {
                vc.SetInventory(inv);
                VoidChestSave.LoadInto(inv, player);
                itemCount = inv.NrOfItems();
            }
            finally
            {
                vc.SuppressSave = false;
            }

            _openItem = item;
            InventoryGui.instance.Show(vc);

            VLog.Info($"打开虚空宝箱: {name} ({size.rows}行x{size.cols}列)，加载 {itemCount} 件物品。");
        }

        internal static (int rows, int cols) GetSize(ItemDrop.ItemData item)
        {
            var prefabName = item?.m_dropPrefab != null ? item.m_dropPrefab.name : "";

            switch (prefabName)
            {
                case VoidChestItems.Magic:
                    return (3, 6);
                case VoidChestItems.Flame:
                    return (3, 8);
                case VoidChestItems.Crystal:
                    return (4, 8);
                default:
                    return (2, 6);
            }
        }

        internal static void FlushActive(Player player)
        {
            if (player == null || _container == null)
            {
                return;
            }

            if (player != Player.m_localPlayer)
            {
                return;
            }

            var inv = _container.GetInventory();
            if (inv != null)
            {
                VLog.Debug($"Player.Save 触发 flush，当前库存 {inv.NrOfItems()} 件。");
                VoidChestSave.SaveFrom(inv, player);
            }
        }
    }
}
