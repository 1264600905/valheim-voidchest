using System.Collections.Generic;
using System.Diagnostics;

namespace VoidChest
{
    /// <summary>存储过滤与堆叠（附近存储 / 远程存储共用）。</summary>
    internal static class VoidChestFilter
    {
        /// <summary>
        /// 是否跳过该物品：
        /// 1) 物品栏第一排（快捷栏）；
        /// 2) ExtraSlots 专用槽位（快捷/弹药/食物/杂项/额外装备）；
        /// 3) 可选：弹药 / 食物 / 蜜酒。
        /// 已装备物品由调用方通过 Player.IsItemEquiped 排除。
        /// </summary>
        internal static bool ShouldSkip(ItemDrop.ItemData item)
        {
            if (VoidChestPlugin.NearbyStoreIgnoreHotbar.Value && item.m_gridPos.y == 0)
            {
                VLog.Debug($"存储过滤：跳过快捷栏物品 {item.m_shared.m_name}");
                return true;
            }

            if (ExtraSlotsCompat.IsInExtraSlot(item))
            {
                VLog.Debug($"存储过滤：跳过额外槽位物品 {item.m_shared.m_name}");
                return true;
            }

            var shared = item.m_shared;
            var type = shared.m_itemType;

            if (VoidChestPlugin.NearbyStoreIgnoreAmmo.Value &&
                (type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable))
            {
                return true;
            }

            if (type == ItemDrop.ItemData.ItemType.Consumable)
            {
                bool isFood = shared.m_food > 0f;
                if (isFood && VoidChestPlugin.NearbyStoreIgnoreFood.Value)
                {
                    return true;
                }

                if (!isFood && VoidChestPlugin.NearbyStoreIgnoreMead.Value)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 不做本地规则判断的堆叠（服务端处理客户端预过滤快照时使用；装备排除已在快照构建时完成）。
        /// 返回移动的堆叠数。
        /// </summary>
        internal static int StackAllRaw(Inventory container, Inventory from)
        {
            var sw = Stopwatch.StartNew();
            var items = new List<ItemDrop.ItemData>(from.GetAllItems());
            int moved = 0;

            foreach (var item in items)
            {
                if (item == null || item.m_shared == null)
                {
                    continue;
                }

                if (!container.ContainsItemByName(item.m_shared.m_name))
                {
                    continue;
                }

                if (container.AddItem(item))
                {
                    from.RemoveItem(item);
                    moved++;
                }
            }

            sw.Stop();
            VoidChestPerf.AddStack(sw.Elapsed.TotalMilliseconds);

            VLog.Debug($"StackAllRaw: {container.GetName()} 移动 {moved} 堆叠，耗时 {sw.Elapsed.TotalMilliseconds:F2}ms");

            return moved;
        }

        /// <summary>把玩家背包中与容器同名的物品按过滤规则堆入容器，返回移动的堆叠数。</summary>
        internal static int StackAllFiltered(Inventory container, Inventory from)
        {
            var sw = Stopwatch.StartNew();
            var items = new List<ItemDrop.ItemData>(from.GetAllItems());
            int moved = 0;
            var player = Player.m_localPlayer;

            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                if (!container.ContainsItemByName(item.m_shared.m_name))
                {
                    continue;
                }

                if (player != null && player.IsItemEquiped(item))
                {
                    continue;
                }

                if (ShouldSkip(item))
                {
                    continue;
                }

                if (container.AddItem(item))
                {
                    from.RemoveItem(item);
                    moved++;
                }
            }

            sw.Stop();
            VoidChestPerf.AddStack(sw.Elapsed.TotalMilliseconds);

            VLog.Debug($"StackAllFiltered: {container.GetName()} 移动 {moved} 堆叠，耗时 {sw.Elapsed.TotalMilliseconds:F2}ms");

            return moved;
        }
    }
}
