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

            if (InventoryGui.instance.IsContainerOpen())
            {
                if (IsVoidChestOpen())
                {
                    InventoryGui.instance.Hide();
                }
                return;
            }

            var item = GetEquippedChest(player);
            if (item == null)
            {
                if (VoidChestPlugin.DebugLog.Value)
                {
                    VoidChestPlugin.Log.LogInfo("未装备虚空宝箱，无法打开。");
                }
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

            foreach (var item in inv.GetEquippedItems())
            {
                if (item?.m_dropPrefab != null && item.m_dropPrefab.name.StartsWith(PrefabPrefix))
                {
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
            VoidChestPlugin.Log.LogInfo("虚拟容器已创建。");
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
            vc.SetInventory(inv);
            VoidChestSave.LoadInto(inv, player);

            _openItem = item;
            InventoryGui.instance.Show(vc);

            VoidChestPlugin.Log.LogInfo($"打开虚空宝箱: {name} ({size.rows}x{size.cols})");
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
                VoidChestSave.SaveFrom(inv, player);
            }
        }
    }
}
