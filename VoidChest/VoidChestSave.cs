using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace VoidChest
{
    /// <summary>
    /// 虚空宝箱内容持久化：存于 Player.m_customData（角色级，随 .fch 保存）。
    /// </summary>
    internal static class VoidChestSave
    {
        internal const string CustomKey = "liu_VoidChest";
        private const int CurrentVersion = 1;

        // 性能统计
        internal static int SaveCount;
        internal static double SaveTotalMs;

        internal static void ResetStats()
        {
            SaveCount = 0;
            SaveTotalMs = 0;
        }

        [Serializable]
        private class Blob
        {
            public int version;
            public string data;
        }

        internal static void LoadInto(Inventory inv, Player player)
        {
            try
            {
                if (inv == null)
                {
                    VLog.Warn("LoadInto: inventory 为 null。");
                    return;
                }

                // 先取出存档文本，避免清空库存触发的回调覆盖存档
                string json = null;
                player?.m_customData?.TryGetValue(CustomKey, out json);

                inv.RemoveAll();

                if (string.IsNullOrEmpty(json))
                {
                    VLog.Debug("LoadInto: 存档中没有虚空宝箱数据（首次使用）。");
                    return;
                }

                var blob = JsonUtility.FromJson<Blob>(json);
                if (blob == null)
                {
                    VLog.Warn("LoadInto: 存档 JSON 解析为 null，放弃加载。");
                    return;
                }

                if (blob.version != CurrentVersion)
                {
                    VLog.Warn($"LoadInto: 存档版本 {blob.version} != {CurrentVersion}，放弃加载。");
                    return;
                }

                if (string.IsNullOrEmpty(blob.data))
                {
                    VLog.Debug("LoadInto: 存档数据为空。");
                    return;
                }

                var sw = Stopwatch.StartNew();
                var bytes = Convert.FromBase64String(blob.data);
                inv.Load(new ZPackage(bytes));
                sw.Stop();

                VLog.Info($"LoadInto: 存档 JSON {json.Length} 字符，Base64 {blob.data.Length} 字符，加载 {inv.NrOfItems()} 件物品，耗时 {sw.Elapsed.TotalMilliseconds:F2}ms。");

                if (VLog.DebugEnabled)
                {
                    foreach (var item in inv.GetAllItems())
                    {
                        VLog.Debug($"  - {item.m_shared.m_name} x{item.m_stack} @({item.m_gridPos.x},{item.m_gridPos.y})");
                    }
                }
            }
            catch (Exception e)
            {
                VLog.Error("加载虚空宝箱数据失败: ", e);
            }
        }

        internal static void SaveFrom(Inventory inv, Player player)
        {
            try
            {
                if (inv == null || player == null)
                {
                    return;
                }

                var sw = Stopwatch.StartNew();

                var pkg = new ZPackage();
                inv.Save(pkg);

                var blob = new Blob
                {
                    version = CurrentVersion,
                    data = Convert.ToBase64String(pkg.GetArray())
                };

                if (player.m_customData == null)
                {
                    player.m_customData = new Dictionary<string, string>();
                }

                player.m_customData[CustomKey] = JsonUtility.ToJson(blob);

                sw.Stop();
                SaveCount++;
                SaveTotalMs += sw.Elapsed.TotalMilliseconds;

                VLog.Debug($"SaveFrom: 保存 {inv.NrOfItems()} 件物品，Base64 {blob.data.Length} 字符，耗时 {sw.Elapsed.TotalMilliseconds:F2}ms（累计 {SaveCount} 次 {SaveTotalMs:F1}ms）。");
            }
            catch (Exception e)
            {
                VLog.Error("保存虚空宝箱数据失败: ", e);
            }
        }
    }
}
