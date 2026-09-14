using System;
using Jotunn.Managers;
using UnityEngine;

namespace VoidChest
{
    /// <summary>
    /// 本地化：English / Chinese（简体）/ Chinese_Trad（繁体）。
    /// token 以 $ 前缀，注册到 Jotunn 的 CustomLocalization，游戏按当前语言自动显示。
    /// </summary>
    internal static class VoidChestLocalization
    {
        internal const string ItemBlackMetal = "$item_voidchest_blackmetal";
        internal const string ItemBlackMetalDesc = "$item_voidchest_blackmetal_desc";
        internal const string ItemMagic = "$item_voidchest_magic";
        internal const string ItemMagicDesc = "$item_voidchest_magic_desc";
        internal const string ItemFlame = "$item_voidchest_flame";
        internal const string ItemFlameDesc = "$item_voidchest_flame_desc";
        internal const string ItemCrystal = "$item_voidchest_crystal";
        internal const string ItemCrystalDesc = "$item_voidchest_crystal_desc";

        internal const string StoreNearby = "$vc_store_nearby";
        internal const string StoreRemote = "$vc_store_remote";

        internal const string NearbyInProgress = "$vc_nearby_in_progress";
        internal const string NearbyNone = "$vc_nearby_none";
        internal const string NearbyStored = "$vc_nearby_stored";
        internal const string NearbyNothing = "$vc_nearby_nothing";
        internal const string NearbyRejected = "$vc_nearby_rejected";
        internal const string NearbyTimeout = "$vc_nearby_timeout";

        internal const string RemoteInProgress = "$vc_remote_in_progress";
        internal const string RemoteHostOnly = "$vc_remote_host_only";
        internal const string RemoteNoGuardstone = "$vc_remote_no_guardstone";
        internal const string RemoteNoAccess = "$vc_remote_no_access";
        internal const string RemoteStored = "$vc_remote_stored";
        internal const string RemoteNothing = "$vc_remote_nothing";
        internal const string RemoteSkipped = "$vc_remote_skipped";

        private static readonly ValueTuple<string, string, string, string>[] Translations =
        {
            // token, English, Chinese, Chinese_Trad
            (ItemBlackMetal, "Black Metal Void Chest", "黑金属虚空宝箱", "黑金屬虛空寶箱"),
            (ItemBlackMetalDesc,
                "Mistland smiths folded a shard of the void into dark iron. Only its bearer may look upon what it holds.",
                "雾之国的工匠将一片虚空折进黑铁。匣中之物，唯持匣者得以相见。",
                "霧之國的工匠將一片虛空折進黑鐵。匣中之物，唯持匣者得以相見。"),

            (ItemMagic, "Eitr Void Chest", "魔能虚空宝箱", "魔能虛空寶箱"),
            (ItemMagicDesc,
                "Forged with refined eitr, a faint glow now stirs within. The gods are long gone — only this chest answers your call.",
                "铸入埃达精华后，匣中浮现幽光。诸神早已远去，唯有此匣应你呼唤。",
                "鑄入埃達精華後，匣中浮現幽光。諸神早已遠去，唯有此匣應你呼喚。"),

            (ItemFlame, "Flametal Void Chest", "烈焰虚空宝箱", "烈焰虛空寶箱"),
            (ItemFlameDesc,
                "Flames lick its edges yet never devour what lies within. The Emerald Flame acknowledges your will.",
                "火焰舔舐匣缘，却从不吞噬其中之物。青焰认可了你的意志。",
                "火焰舔舐匣緣，卻從不吞噬其中之物。青焰認可了你的意志。"),

            (ItemCrystal, "Crystal Void Chest", "水晶虚空宝箱", "水晶虛空寶箱"),
            (ItemCrystalDesc,
                "Born of blood-gold and liquid frost, a gate beyond the Nine Worlds. It keeps your past — and the roads you have yet to walk.",
                "霜与血的造物，诸界之外的门扉。它收纳你的过往，也收纳你尚未走完的路。",
                "霜與血的造物，諸界之外的門扉。它收納你的過往，也收納你尚未走完的路。"),

            (StoreNearby, "Nearby Storage", "附近存储", "附近存儲"),
            (StoreRemote, "Remote Deposit", "远程存入", "遠端存入"),

            (NearbyInProgress, "Nearby storage is in progress, please try again later.",
                "附近存储正在进行中，请稍后再试。", "附近儲存正在進行中，請稍後再試。"),
            (NearbyNone, "No containers nearby to store into.",
                "附近没有可存储的箱子。", "附近沒有可儲存的箱子。"),
            (NearbyStored, "Stored {0} items into {1} containers",
                "已存入 {0} 件物品到 {1} 个箱子", "已存入 {0} 件物品到 {1} 個箱子"),
            (NearbyNothing, "Nothing to store (scanned {0} containers)",
                "没有可存入的物品（扫描 {0} 个箱子）", "沒有可存入的物品（掃描 {0} 個箱子）"),
            (NearbyRejected, "({0} in use / rejected)",
                "（{0} 个使用中/被拒绝）", "（{0} 個使用中/被拒絕）"),
            (NearbyTimeout, "({0} timed out)",
                "（{0} 个超时）", "（{0} 個超時）"),

            (RemoteInProgress, "Remote deposit is already in progress...",
                "远程存入正在进行中...", "遠端存入正在進行中..."),
            (RemoteHostOnly, "Remote deposit is only available in singleplayer or as the host.",
                "远程存入仅支持单机/主机模式", "遠端存入僅支援單機/主機模式"),
            (RemoteNoGuardstone, "No guard stone found in the world.",
                "世界中未找到守护石。", "世界中未找到守護石。"),
            (RemoteNoAccess, "You have access to no guard stone.",
                "没有找到你有权限的守护石", "沒有找到你有權限的守護石"),
            (RemoteStored, "Deposited {0} items into your home containers ({1} containers)",
                "已远程存入 {0} 件物品到家的箱子（{1} 个箱子）", "已遠端存入 {0} 件物品到家的箱子（{1} 個箱子）"),
            (RemoteNothing, "Nothing to deposit (scanned {0} containers)",
                "没有可远程存入的物品（扫描 {0} 个箱子）", "沒有可遠端存入的物品（掃描 {0} 個箱子）"),
            (RemoteSkipped, "({0} in use / invalid skipped)",
                "（跳过 {0} 个使用中/无效箱子）", "（跳過 {0} 個使用中/無效箱子）"),
        };

        internal static void Register()
        {
            try
            {
                var localization = LocalizationManager.Instance.GetLocalization();

                foreach (var entry in Translations)
                {
                    localization.AddTranslation("English", entry.Item1, entry.Item2);
                    localization.AddTranslation("Chinese", entry.Item1, entry.Item3);
                    localization.AddTranslation("Chinese_Trad", entry.Item1, entry.Item4);
                }

                VLog.Info($"本地化已注册：{Translations.Length} 个条目 × 3 种语言（English/Chinese/Chinese_Trad）。");
            }
            catch (Exception e)
            {
                VLog.Error("本地化注册失败: ", e);
            }
        }

        /// <summary>本地化 token（无参数）。</summary>
        internal static string L(string token)
        {
            var loc = Localization.instance;
            return loc != null ? loc.Localize(token) : token;
        }

        /// <summary>本地化 token 并替换 {0} {1} ... 占位符。</summary>
        internal static string L(string token, params object[] args)
        {
            var text = L(token);

            if (args == null)
            {
                return text;
            }

            for (int i = 0; i < args.Length; i++)
            {
                text = text.Replace("{" + i + "}", args[i] != null ? args[i].ToString() : "");
            }

            return text;
        }
    }
}
