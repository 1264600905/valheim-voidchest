using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VoidChest
{
    /// <summary>
    /// 物品身份标识：用于跨端匹配同一件物品。
    /// 服务端按客户端快照处理物品，客户端凭标识从实时背包扣除已存入物品。
    /// 标识包含全部持久化字段（词条/耐久/品质/制作者等），忽略堆叠数量与格子位置。
    /// </summary>
    internal static class VoidChestItemIdentity
    {
        internal static string Of(ItemDrop.ItemData item)
        {
            var sb = new StringBuilder(96);
            sb.Append(item.m_dropPrefab != null ? item.m_dropPrefab.name : "?").Append('|');
            sb.Append(item.m_quality).Append('|');
            sb.Append(item.m_variant).Append('|');
            sb.Append(item.m_worldLevel).Append('|');
            sb.Append(Mathf.RoundToInt(item.m_durability * 100f)).Append('|');
            sb.Append(item.m_crafterID).Append('|');
            sb.Append(item.m_crafterName).Append('|');
            sb.Append(item.m_pickedUp ? '1' : '0').Append('|');
            sb.Append(item.m_cheated ? '1' : '0');

            var custom = item.m_customData;
            if (custom != null && custom.Count > 0)
            {
                var keys = new List<string>(custom.Keys);
                keys.Sort(StringComparer.Ordinal);
                foreach (var key in keys)
                {
                    var value = custom[key] ?? string.Empty;
                    sb.Append('|').Append(key.Length).Append(':').Append(key)
                      .Append('=').Append(value.Length).Append(':').Append(value);
                }
            }

            return sb.ToString();
        }

        /// <summary>按标识统计物品数量（单位：件）。</summary>
        internal static Dictionary<string, int> Totals(Inventory inv)
        {
            var result = new Dictionary<string, int>();
            if (inv == null)
            {
                return result;
            }

            foreach (var item in inv.GetAllItems())
            {
                if (item == null || item.m_shared == null)
                {
                    continue;
                }

                var key = Of(item);
                int current;
                result.TryGetValue(key, out current);
                result[key] = current + item.m_stack;
            }

            return result;
        }
    }
}
