using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace VoidChest
{
    /// <summary>ExtraSlots 软依赖：额外装备槽检测（反射调用，失败自动降级）。</summary>
    internal static class ExtraSlotsCompat
    {
        private static bool _initialized;
        private static MethodInfo _getEquipped;
        private static bool _warned;

        private static MethodInfo GetMethod()
        {
            if (_initialized)
            {
                return _getEquipped;
            }

            _initialized = true;

            var type = AccessTools.TypeByName("ExtraSlots.ExtraUtilitySlots");
            if (type == null)
            {
                VLog.Debug("未检测到 ExtraSlots。");
                return null;
            }

            _getEquipped = AccessTools.Method(type, "GetEquippedItems", new[] { typeof(Humanoid) });

            if (_getEquipped != null)
            {
                VLog.Info("检测到 ExtraSlots，额外 Utility 槽参与检测。");
            }
            else
            {
                VLog.Warn("检测到 ExtraSlots 但未找到 ExtraUtilitySlots.GetEquippedItems，跳过额外槽检测。");
            }

            return _getEquipped;
        }

        internal static List<ItemDrop.ItemData> GetEquippedItems(Humanoid humanoid)
        {
            var method = GetMethod();
            if (method == null || humanoid == null)
            {
                return null;
            }

            try
            {
                return method.Invoke(null, new object[] { humanoid }) as List<ItemDrop.ItemData>;
            }
            catch (Exception e)
            {
                if (!_warned)
                {
                    _warned = true;
                    VLog.Warn("ExtraSlots 检测失败，降级跳过额外槽: " + e.Message);
                }

                return null;
            }
        }
    }
}
