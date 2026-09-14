using UnityEngine;

namespace VoidChest
{
    /// <summary>
    /// 虚空宝箱重量上限：仅对"手动存入"（拖拽 / Shift 快速移动）生效，支持分堆；附近/远程存储不受限。
    /// 上限 0 = 无限制。
    /// </summary>
    internal static class VoidChestWeight
    {
        internal static bool IsVirtualInventory(Inventory inv)
        {
            var vc = VoidChestManager.CurrentContainer;
            return vc != null && vc.GetInventory() == inv;
        }

        /// <summary>在重量上限内可存入的数量。</summary>
        internal static int AllowedAmount(Inventory container, ItemDrop.ItemData item, int requested)
        {
            var vc = VoidChestManager.CurrentContainer;
            float limit = vc != null ? vc.MaxWeight : 0f;

            if (limit <= 0f)
            {
                return requested; // 无限制
            }

            float perUnit = item != null ? item.m_shared.m_weight : 0f;
            if (perUnit <= 0f)
            {
                return requested; // 无重量物品不限制
            }

            float remaining = limit - container.GetTotalWeight();
            if (remaining <= 0f)
            {
                return 0;
            }

            int allowed = Mathf.FloorToInt(remaining / perUnit);
            return Mathf.Clamp(allowed, 0, requested);
        }

        /// <summary>
        /// 部分移动：优先合并到容器内同类堆叠，否则放入第一个空格。
        /// 内部调用带 amount 的 MoveItemToThis（会再次经过重量校验，但数量已在额度内，不会循环）。
        /// </summary>
        internal static bool MovePartialToContainer(Inventory to, Inventory from, ItemDrop.ItemData item, int amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            foreach (var existing in to.GetAllItems())
            {
                if (existing == null || !existing.IsSameType(item))
                {
                    continue;
                }

                int space = existing.m_shared.m_maxStackSize - existing.m_stack;
                if (space <= 0)
                {
                    continue;
                }

                int move = Mathf.Min(space, amount);
                if (move > 0)
                {
                    return to.MoveItemToThis(from, item, move, existing.m_gridPos.x, existing.m_gridPos.y);
                }
            }

            for (int y = 0; y < to.GetHeight(); y++)
            {
                for (int x = 0; x < to.GetWidth(); x++)
                {
                    if (to.GetItemAt(x, y) == null)
                    {
                        return to.MoveItemToThis(from, item, amount, x, y);
                    }
                }
            }

            return false;
        }
    }
}
