using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace VoidChest
{
    /// <summary>
    /// 诊断输出：打开背包/容器界面时打印虚空宝箱配方的解锁状态、原因、工作台与玩家物品。
    /// </summary>
    internal static class VoidChestDiagnostics
    {
        private static readonly FieldInfo KnownRecipesField = AccessTools.Field(typeof(Player), "m_knownRecipes");
        private static readonly FieldInfo KnownStationsField = AccessTools.Field(typeof(Player), "m_knownStations");
        private static readonly FieldInfo KnownMaterialField = AccessTools.Field(typeof(Player), "m_knownMaterial");

        private static float _lastDump = -999f;

        internal static void DumpRecipesAndInventory()
        {
            if (VoidChestPlugin.DebugLog == null || !VoidChestPlugin.DebugLog.Value)
            {
                return;
            }

            // 节流：2 秒内不重复输出
            if (Time.realtimeSinceStartup - _lastDump < 2f)
            {
                return;
            }

            _lastDump = Time.realtimeSinceStartup;

            try
            {
                Dump();
            }
            catch (System.Exception e)
            {
                VLog.Warn("配方诊断失败: " + e.Message);
            }
        }

        private static void Dump()
        {
            var player = Player.m_localPlayer;
            var db = ObjectDB.instance;
            if (player == null || db == null)
            {
                return;
            }

            var knownRecipes = KnownRecipesField?.GetValue(player) as HashSet<string> ?? new HashSet<string>();
            var knownStations = KnownStationsField?.GetValue(player) as Dictionary<string, int> ?? new Dictionary<string, int>();
            var knownMaterial = KnownMaterialField?.GetValue(player) as HashSet<string> ?? new HashSet<string>();
            var inv = player.GetInventory();

            var sb = new StringBuilder(2048);
            sb.AppendLine("========== 虚空宝箱配方诊断 ==========");

            foreach (var recipe in db.m_recipes)
            {
                if (recipe == null || recipe.m_item == null)
                {
                    continue;
                }

                var prefabName = recipe.m_item.name;
                if (!prefabName.StartsWith("VoidChest"))
                {
                    continue;
                }

                var drop = recipe.m_item.GetComponent<ItemDrop>();
                var productKey = drop != null ? drop.m_itemData.m_shared.m_name : "?";
                var productLocal = Localization.instance.Localize(productKey);
                bool unlocked = knownRecipes.Contains(productKey);

                string stationPrefab = recipe.m_craftingStation != null ? recipe.m_craftingStation.name : "(无)";
                string stationLocal = recipe.m_craftingStation != null
                    ? Localization.instance.Localize(recipe.m_craftingStation.m_name)
                    : "(无)";
                int knownLevel = -1;
                if (recipe.m_craftingStation != null)
                {
                    knownStations.TryGetValue(recipe.m_craftingStation.m_name, out knownLevel);
                }
                string knownLevelText = knownLevel < 0 ? "未记录" : knownLevel.ToString();

                sb.AppendLine($"[{prefabName}] 成品={productLocal}({productKey})");
                sb.AppendLine($"    解锁={unlocked} | 工作台={stationLocal}({stationPrefab}) | 需要等级={recipe.m_minStationLevel} | 玩家已知该工作台等级={knownLevelText} | 只需一种材料={recipe.m_requireOnlyOneIngredient}");

                var missingKnown = new List<string>();
                if (recipe.m_resources != null)
                {
                    foreach (var req in recipe.m_resources)
                    {
                        if (req == null || req.m_resItem == null)
                        {
                            sb.AppendLine("    材料: <null 引用>");
                            continue;
                        }

                        var matKey = req.m_resItem.m_itemData.m_shared.m_name;
                        var matLocal = Localization.instance.Localize(matKey);
                        int inInv = inv.CountItems(matKey);
                        bool seen = knownMaterial.Contains(matKey);

                        sb.AppendLine($"    材料: {req.m_resItem.name}({matLocal}) 需要={req.m_amount} 背包数量={inInv} 已见过={seen}");

                        if (!seen)
                        {
                            missingKnown.Add(matLocal + "/" + req.m_resItem.name);
                        }
                    }
                }

                var reasons = new List<string>();
                if (!unlocked)
                {
                    if (missingKnown.Count > 0)
                    {
                        reasons.Add("材料未见过: " + string.Join(", ", missingKnown));
                    }

                    if (recipe.m_craftingStation != null && knownLevel < recipe.m_minStationLevel)
                    {
                        reasons.Add($"工作台等级不足/未知（玩家={knownLevelText}，需要={recipe.m_minStationLevel}）");
                    }
                }

                sb.AppendLine(unlocked
                    ? "    状态: 已解锁"
                    : $"    未解锁原因: {(reasons.Count > 0 ? string.Join("; ", reasons) : "未知（其他条件）")}");
            }

            var items = inv.GetAllItems();
            sb.AppendLine($"---- 玩家背包物品（{items.Count}）----");
            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                var itemPrefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "(无prefab)";
                sb.AppendLine($"    {itemPrefab} | {Localization.instance.Localize(item.m_shared.m_name)} | x{item.m_stack}");
            }

            sb.AppendLine($"---- 玩家已记录工作台（{knownStations.Count}）----");
            foreach (var kv in knownStations)
            {
                sb.AppendLine($"    {Localization.instance.Localize(kv.Key)} = {kv.Value}");
            }

            sb.Append("=======================================");

            VoidChestPlugin.Log.LogInfo(sb.ToString());
        }
    }
}
