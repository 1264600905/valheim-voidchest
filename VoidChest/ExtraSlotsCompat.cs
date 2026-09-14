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

        private static bool _slotLookupInitialized;
        private static MethodInfo _getItemSlot;
        private static bool _slotLookupWarned;

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

        private static MethodInfo GetItemSlotMethod()
        {
            if (_slotLookupInitialized)
            {
                return _getItemSlot;
            }

            _slotLookupInitialized = true;

            var type = AccessTools.TypeByName("ExtraSlots.Slots");
            if (type == null)
            {
                return null;
            }

            _getItemSlot = AccessTools.Method(type, "GetItemSlot", new[] { typeof(ItemDrop.ItemData) });

            if (_getItemSlot == null)
            {
                VLog.Warn("未找到 ExtraSlots.Slots.GetItemSlot，专用槽位物品过滤降级。");
            }

            return _getItemSlot;
        }

        /// <summary>物品是否位于 ExtraSlots 的任意专用槽位（快捷/弹药/食物/杂项/额外装备/自定义）。</summary>
        internal static bool IsInExtraSlot(ItemDrop.ItemData item)
        {
            if (item == null)
            {
                return false;
            }

            var method = GetItemSlotMethod();
            if (method == null)
            {
                return false;
            }

            try
            {
                return method.Invoke(null, new object[] { item }) != null;
            }
            catch (Exception e)
            {
                if (!_slotLookupWarned)
                {
                    _slotLookupWarned = true;
                    VLog.Warn("ExtraSlots.GetItemSlot 调用失败，专用槽位过滤降级: " + e.Message);
                }

                return false;
            }
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
