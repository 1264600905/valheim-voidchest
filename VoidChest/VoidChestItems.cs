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
        }

        private static void RegisterItems()
        {
            if (_registered)
            {
                return;
            }

            try
            {
                Add(BlackMetal, "黑金属虚空宝箱", "forge", new[]
                {
                    new RequirementConfig("DragonTear", 5),
                    new RequirementConfig("FineWood", 10),
                    new RequirementConfig("BlackMetal", 20),
                });

                Add(Magic, "魔能虚空宝箱", "forge", new[]
                {
                    new RequirementConfig(BlackMetal, 1),
                    new RequirementConfig("Eitr", 10),
                    new RequirementConfig("YagluthDrop", 2),
                });

                Add(Flame, "烈焰虚空宝箱", "blackforge", new[]
                {
                    new RequirementConfig(Magic, 1),
                    new RequirementConfig("Flametal", 10),
                    new RequirementConfig("FaderDrop", 2),
                });

                Add(Crystal, "水晶虚空宝箱", "blackforge", new[]
                {
                    new RequirementConfig(Flame, 1),
                    new RequirementConfig("Gold", 10),
                    new RequirementConfig("FrozenFuel", 10),
                });

                _registered = true;
                VoidChestPlugin.Log.LogInfo("虚空宝箱物品与配方注册完成。");
            }
            catch (Exception e)
            {
                VoidChestPlugin.Log.LogError("注册虚空宝箱失败: " + e);
            }
        }

        private static void Add(string prefabName, string displayName, string station, RequirementConfig[] requirements)
        {
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
            Sanitize(item, prefabName, displayName);
            ItemManager.Instance.AddItem(item);

            VoidChestPlugin.Log.LogInfo($"已注册物品: {prefabName} ({displayName}) @ {station}");
        }

        private static void Sanitize(CustomItem item, string prefabName, string displayName)
        {
            var prefab = item.ItemPrefab;
            if (prefab == null)
            {
                VoidChestPlugin.Log.LogError($"[{prefabName}] 克隆失败：prefab 为空。");
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
            VoidChestPlugin.Log.LogInfo($"[{prefabName}] 组件结构: {string.Join(", ", names)}");

            foreach (var c in components)
            {
                if (c == null)
                {
                    continue;
                }

                if (c is Container || c is Piece || c is WearNTear || c is Destructible || c is PrivateArea)
                {
                    UnityEngine.Object.DestroyImmediate(c);
                }
            }

            var drop = item.ItemDrop;
            if (drop == null)
            {
                VoidChestPlugin.Log.LogError($"[{prefabName}] 没有 ItemDrop 组件。");
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
            shared.m_description = "一个随身携带的虚空宝箱。内容绑定在你的角色存档上，装备后按热键打开。";

            var tint = Tints[prefabName];

            if (shared.m_icons != null)
            {
                for (int i = 0; i < shared.m_icons.Length; i++)
                {
                    shared.m_icons[i] = RecolorSprite(shared.m_icons[i], tint);
                }
                VoidChestPlugin.Log.LogInfo($"[{prefabName}] 图标数量: {shared.m_icons.Length}");
            }
            else
            {
                VoidChestPlugin.Log.LogWarning($"[{prefabName}] 没有图标。");
            }

            RecolorRenderers(prefab, tint);
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

        private static void RecolorRenderers(GameObject root, Color tint)
        {
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
                    }

                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", tint * 0.35f);
                    }
                }
            }
        }
    }
}
