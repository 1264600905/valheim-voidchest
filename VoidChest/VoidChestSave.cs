using System;
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
                    return;
                }

                // 先取出存档文本，避免清空库存触发的回调覆盖存档
                string json = null;
                player?.m_customData?.TryGetValue(CustomKey, out json);

                inv.RemoveAll();

                if (string.IsNullOrEmpty(json))
                {
                    return;
                }

                var blob = JsonUtility.FromJson<Blob>(json);
                if (blob == null || blob.version != CurrentVersion || string.IsNullOrEmpty(blob.data))
                {
                    return;
                }

                var bytes = Convert.FromBase64String(blob.data);
                inv.Load(new ZPackage(bytes));
            }
            catch (Exception e)
            {
                VoidChestPlugin.Log.LogError("加载虚空宝箱数据失败: " + e);
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

                var pkg = new ZPackage();
                inv.Save(pkg);

                var blob = new Blob
                {
                    version = CurrentVersion,
                    data = Convert.ToBase64String(pkg.GetArray())
                };

                if (player.m_customData == null)
                {
                    player.m_customData = new System.Collections.Generic.Dictionary<string, string>();
                }

                player.m_customData[CustomKey] = JsonUtility.ToJson(blob);
            }
            catch (Exception e)
            {
                VoidChestPlugin.Log.LogError("保存虚空宝箱数据失败: " + e);
            }
        }
    }
}
