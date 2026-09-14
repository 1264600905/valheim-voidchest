using System;
using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace VoidChest
{
    internal static class VoidChestItems
    {
        internal const string BlackMetal = "VoidChestBlackMetal";
        internal const string Magic = "VoidChestMagic";
        internal const string Flame = "VoidChestFlame";
        internal const string Crystal = "VoidChestCrystal";

        private const string BasePrefab = "chest_hildir2";

        private static bool _registered;

        internal static readonly Dictionary<string, Color> Tints = new Dictionary<string, Color>
        {
            { BlackMetal, new Color32(0xD0, 0x78, 0xFF, 0xFF) },
            { Magic, new Color32(0x18, 0xE7, 0xA9, 0xFF) },
            { Flame, new Color32(0xFF, 0xAC, 0x59, 0xFF) },
            { Crystal, new Color32(0xFF, 0x45, 0x45, 0xFF) },
        };

        internal static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += RegisterItems;
            VLog.Info("已订阅 PrefabManager.OnVanillaPrefabsAvailable");
        }

        private static void RegisterItems()
        {
            if (_registered)
            {
                VLog.Debug("物品已注册过，跳过。");
                return;
            }

            VLog.Info("原版 prefab 可用，开始注册虚空宝箱...");

            try
            {
                Add(BlackMetal, VoidChestLocalization.ItemBlackMetal, VoidChestLocalization.ItemBlackMetalDesc,
                    "forge", new[]
                {
                    new RequirementConfig("DragonTear", 5),
                    new RequirementConfig("FineWood", 10),
                    new RequirementConfig("BlackMetal", 20),
                });

                Add(Magic, VoidChestLocalization.ItemMagic, VoidChestLocalization.ItemMagicDesc,
                    "forge", new[]
                {
                    new RequirementConfig(BlackMetal, 1),
                    new RequirementConfig("Eitr", 10),
                    new RequirementConfig("YagluthDrop", 2),
                });

                Add(Flame, VoidChestLocalization.ItemFlame, VoidChestLocalization.ItemFlameDesc,
                    "blackforge", new[]
                {
                    new RequirementConfig(Magic, 1),
                    new RequirementConfig("Flametal", 10),
                    new RequirementConfig("FaderDrop", 2),
                });

                Add(Crystal, VoidChestLocalization.ItemCrystal, VoidChestLocalization.ItemCrystalDesc,
                    "blackforge", new[]
                {
                    new RequirementConfig(Flame, 1),
                    new RequirementConfig("Gold", 10),
                    new RequirementConfig("FrozenFuel", 10),
                });

                _registered = true;
                VLog.Info("虚空宝箱物品与配方注册完成。");
            }
            catch (Exception e)
            {
                VLog.Error("注册虚空宝箱失败: ", e);
            }
        }

        private static void Add(string prefabName, string displayName, string description, string station, RequirementConfig[] requirements)
        {
            var reqText = new List<string>();
            foreach (var r in requirements)
            {
                reqText.Add($"{r.Item}x{r.Amount}");
            }
            VLog.Info($"注册 [{prefabName}] 名称={displayName} 工作台={station} 材料={string.Join(", ", reqText)}");

            var config = new ItemConfig
            {
                Name = prefabName,
                Description = prefabName,
                CraftingStation = station,
                MinStationLevel = 1,
                Amount = 1,
                Requirements = requirements,
                Weight = 2f,
            };

            var item = new CustomItem(prefabName, BasePrefab, config);

            if (item.ItemPrefab == null)
            {
                VLog.Error($"[{prefabName}] CustomItem.ItemPrefab 为空（克隆 {BasePrefab} 失败）");
            }

            Sanitize(item, prefabName, displayName, description);
            ItemManager.Instance.AddItem(item);

            var recipe = item.Recipe;
            if (recipe == null)
            {
                VLog.Warn($"[{prefabName}] 配方未生成（Recipe 为空）。");
            }
            else
            {
                VLog.Info($"[{prefabName}] 配方已生成: {recipe.Recipe?.m_resources?.Length ?? 0} 项材料, 工作台={recipe.Recipe?.m_craftingStation?.name}");
            }
        }

        private static void Sanitize(CustomItem item, string prefabName, string displayName, string description)
        {
            var prefab = item.ItemPrefab;
            if (prefab == null)
            {
                VLog.Error($"[{prefabName}] 克隆失败：prefab 为空。");
                return;
            }

            var components = prefab.GetComponentsInChildren<Component>(true);
            var names = new List<string>();
            foreach (var c in components)
            {
                if (c != null)
                {
                    names.Add(c.GetType().Name);
                }
            }
            VLog.Info($"[{prefabName}] 组件结构: {string.Join(", ", names)}");

            foreach (var c in components)
            {
                if (c == null)
                {
                    continue;
                }

                if (c is Container || c is Piece || c is WearNTear || c is Destructible || c is PrivateArea)
                {
                    VLog.Info($"[{prefabName}] 移除组件: {c.GetType().Name}");
                    UnityEngine.Object.DestroyImmediate(c);
                }
            }

            var drop = item.ItemDrop;
            if (drop == null)
            {
                VLog.Error($"[{prefabName}] 没有 ItemDrop 组件，无法作为物品。");
                return;
            }

            var shared = drop.m_itemData.m_shared;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Utility;
            shared.m_maxStackSize = 1;
            shared.m_weight = 2f;
            shared.m_teleportable = true;
            shared.m_questItem = false;

            // 直接写入中文名与描述（不走 Jotunn 的本地化 token 处理）
            shared.m_name = displayName;
            shared.m_description = description;

            VLog.Info($"[{prefabName}] ItemDrop: name={shared.m_name}, type={shared.m_itemType}, weight={shared.m_weight}, icons={(shared.m_icons?.Length ?? 0)}");

            var tint = Tints[prefabName];

            if (shared.m_icons != null && shared.m_icons.Length > 0)
            {
                for (int i = 0; i < shared.m_icons.Length; i++)
                {
                    var source = shared.m_icons[i];
                    var recolored = RecolorSprite(source, tint);
                    shared.m_icons[i] = recolored;
                    VLog.Info($"[{prefabName}] 图标[{i}] {SpriteInfo(source)} -> {SpriteInfo(recolored)}");
                }
            }
            else
            {
                VLog.Warn($"[{prefabName}] 没有图标（m_icons 为空）。");
            }

            var recoloredMaterials = RecolorRenderers(prefab, tint);
            VLog.Info($"[{prefabName}] 材质换色: {recoloredMaterials} 个材质设置 tint={ColorUtility.ToHtmlStringRGB(tint)}");
        }

        private static string SpriteInfo(Sprite sprite)
        {
            if (sprite == null)
            {
                return "null";
            }

            var tex = sprite.texture;
            var rect = sprite.textureRect;
            return $"{(tex != null ? tex.width + "x" + tex.height : "no-tex")}/rect({rect.x},{rect.y},{rect.width},{rect.height})/ppu{sprite.pixelsPerUnit}";
        }

        private static Sprite RecolorSprite(Sprite src, Color tint)
        {
            if (src == null)
            {
                return null;
            }

            var srcTex = src.texture;
            if (srcTex == null)
            {
                return src;
            }

            // 图标可能来自 SpriteAtlas：必须按 textureRect 读取子区域
            var rect = src.textureRect;
            int rw = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            int rh = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            int tw = Mathf.Max(1, srcTex.width);
            int th = Mathf.Max(1, srcTex.height);

            var rt = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;

            try
            {
                Graphics.Blit(srcTex, rt);
                RenderTexture.active = rt;

                var tex = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(rect.x, rect.y, rw, rh), 0, 0);
                tex.Apply();

                var px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    var p = px[i];
                    if (p.a == 0)
                    {
                        continue;
                    }

                    // 用原像素亮度保留明暗层次，替换为目标品质色
                    float v = Mathf.Max(p.r, Mathf.Max(p.g, p.b)) / 255f;
                    v = Mathf.Clamp01(0.2f + 0.8f * v);

                    px[i] = new Color32(
                        (byte)(tint.r * 255f * v),
                        (byte)(tint.g * 255f * v),
                        (byte)(tint.b * 255f * v),
                        p.a);
                }

                tex.SetPixels32(px);
                tex.Apply();

                return Sprite.Create(tex, new Rect(0, 0, rw, rh), new Vector2(0.5f, 0.5f),
                    src.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static int RecolorRenderers(GameObject root, Color tint)
        {
            int count = 0;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.materials;
                foreach (var mat in materials)
                {
                    if (mat == null)
                    {
                        continue;
                    }

                    if (mat.HasProperty("_Color"))
                    {
                        mat.SetColor("_Color", tint);
                        count++;
                    }

                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", tint * 0.35f);
                    }
                }
            }

            return count;
        }
    }
}
