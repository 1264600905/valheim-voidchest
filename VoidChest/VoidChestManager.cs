using HarmonyLib;
using UnityEngine;

namespace VoidChest
{
    internal static class VoidChestManager
    {
        internal const string PrefabPrefix = "VoidChest";

        internal static readonly AccessTools.FieldRef<InventoryGui, Container> CurrentContainerRef =
            AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");

        private static VirtualContainer _container;
        private static ItemDrop.ItemData _openItem;

        internal static VirtualContainer CurrentContainer => _container;

        internal static Container CurrentOpenedContainer(InventoryGui gui)
        {
            return gui != null ? CurrentContainerRef(gui) : null;
        }

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
            if (inv != null)
            {
                foreach (var item in inv.GetEquippedItems())
                {
                    if (IsVoidChestItem(item))
                    {
                        VLog.Debug($"原版装备槽检测到虚空宝箱: prefab={item.m_dropPrefab.name}, name={item.m_shared.m_name}");
                        return item;
                    }
                }
            }

            var extraSlots = ExtraSlotsCompat.GetEquippedItems(player);
            if (extraSlots != null)
            {
                foreach (var item in extraSlots)
                {
                    if (IsVoidChestItem(item))
                    {
                        VLog.Debug($"ExtraSlots 额外槽检测到虚空宝箱: prefab={item.m_dropPrefab.name}, name={item.m_shared.m_name}");
                        return item;
                    }
                }
            }

            return null;
        }

        private static bool IsVoidChestItem(ItemDrop.ItemData item)
        {
            return item?.m_dropPrefab != null && item.m_dropPrefab.name.StartsWith(PrefabPrefix);
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

            vc.MaxWeight = GetMaxWeight(item);

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
                    return (VoidChestPlugin.CapacityMagicRows.Value, VoidChestPlugin.CapacityMagicCols.Value);
                case VoidChestItems.Flame:
                    return (VoidChestPlugin.CapacityFlameRows.Value, VoidChestPlugin.CapacityFlameCols.Value);
                case VoidChestItems.Crystal:
                    return (VoidChestPlugin.CapacityCrystalRows.Value, VoidChestPlugin.CapacityCrystalCols.Value);
                default:
                    return (VoidChestPlugin.CapacityBlackMetalRows.Value, VoidChestPlugin.CapacityBlackMetalCols.Value);
            }
        }

        internal static float GetMaxWeight(ItemDrop.ItemData item)
        {
            var prefabName = item?.m_dropPrefab != null ? item.m_dropPrefab.name : "";

            switch (prefabName)
            {
                case VoidChestItems.Magic:
                    return VoidChestPlugin.WeightMagic.Value;
                case VoidChestItems.Flame:
                    return VoidChestPlugin.WeightFlame.Value;
                case VoidChestItems.Crystal:
                    return VoidChestPlugin.WeightCrystal.Value;
                default:
                    return VoidChestPlugin.WeightBlackMetal.Value;
            }
        }

        /// <summary>宝箱等级：黑金属=1 / 魔能=2 / 烈焰=3 / 水晶=4。</summary>
        internal static int GetTier(ItemDrop.ItemData item)
        {
            var prefabName = item?.m_dropPrefab != null ? item.m_dropPrefab.name : "";

            switch (prefabName)
            {
                case VoidChestItems.Magic:
                    return 2;
                case VoidChestItems.Flame:
                    return 3;
                case VoidChestItems.Crystal:
                    return 4;
                default:
                    return 1;
            }
        }

        /// <summary>远程存储是否对当前装备的宝箱可用（受 AlwaysAvailable 与等级解锁影响）。</summary>
        internal static bool CanUseRemote(Player player)
        {
            if (VoidChestPlugin.EnableRemoteStore == null || !VoidChestPlugin.EnableRemoteStore.Value)
            {
                return false;
            }

            if (VoidChestPlugin.RemoteStoreAlwaysAvailable != null &&
                VoidChestPlugin.RemoteStoreAlwaysAvailable.Value)
            {
                return true;
            }

            var item = GetEquippedChest(player);
            return item != null && GetTier(item) >= 2;
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

        internal static void Message(Player player, string text)
        {
            if (player == null || MessageHud.instance == null)
            {
                return;
            }

            player.Message(MessageHud.MessageType.Center, text);
        }
    }
}
