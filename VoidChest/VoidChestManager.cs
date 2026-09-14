using System;
using System.Collections.Generic;
using System.Reflection;
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

        private static bool _extraSlotsInitialized;
        private static MethodInfo _extraSlotsGetEquipped;

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

            InitExtraSlots();

            if (_extraSlotsGetEquipped != null)
            {
                try
                {
                    var list = _extraSlotsGetEquipped.Invoke(null, new object[] { player }) as List<ItemDrop.ItemData>;
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            if (IsVoidChestItem(item))
                            {
                                VLog.Debug($"ExtraSlots 额外槽检测到虚空宝箱: prefab={item.m_dropPrefab.name}, name={item.m_shared.m_name}");
                                return item;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    VLog.Warn("ExtraSlots 装备检测失败，降级为原版槽检测: " + e.Message);
                    _extraSlotsGetEquipped = null;
                }
            }

            return null;
        }

        private static bool IsVoidChestItem(ItemDrop.ItemData item)
        {
            return item?.m_dropPrefab != null && item.m_dropPrefab.name.StartsWith(PrefabPrefix);
        }

        private static void InitExtraSlots()
        {
            if (_extraSlotsInitialized)
            {
                return;
            }

            _extraSlotsInitialized = true;

            var type = AccessTools.TypeByName("ExtraSlots.ExtraUtilitySlots");
            if (type == null)
            {
                VLog.Debug("未检测到 ExtraSlots，仅使用原版装备槽。");
                return;
            }

            _extraSlotsGetEquipped = AccessTools.Method(type, "GetEquippedItems", new[] { typeof(Humanoid) });

            if (_extraSlotsGetEquipped != null)
            {
                VLog.Info("检测到 ExtraSlots，额外 Utility 槽参与装备检测。");
            }
            else
            {
                VLog.Warn("检测到 ExtraSlots 但未找到 ExtraUtilitySlots.GetEquippedItems，跳过额外槽检测。");
            }
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
