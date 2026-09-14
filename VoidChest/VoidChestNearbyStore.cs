using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoidChest
{
    /// <summary>
    /// 自研"附近存储"：把背包物品堆进周围箱子。
    /// 走原版 Container.StackAll() RPC 链路（联机安全），串行队列 + 超时。
    /// 堆叠时通过 FilterActive + Inventory.StackAll patch 应用可选过滤。
    /// </summary>
    internal static class VoidChestNearbyStore
    {
        internal static bool FilterActive { get; private set; }

        private static readonly List<Container> _queue = new List<Container>();
        private static readonly HashSet<Container> _seen = new HashSet<Container>();
        private static bool _running;
        private static Container _current;
        private static float _waitTimer;
        private static int _beforeCount;
        private static int _processed;
        private static int _timedOut;

        internal static void Start(Player player)
        {
            if (player == null)
            {
                return;
            }

            if (_running)
            {
                VoidChestManager.Message(player, "附近存储正在进行中...");
                return;
            }

            var containers = FindContainers(player);
            if (containers.Count == 0)
            {
                VoidChestManager.Message(player, "附近没有可存储的箱子。");
                VLog.Info("附近存储：未找到可用容器。");
                return;
            }

            _queue.Clear();
            _queue.AddRange(containers);
            _beforeCount = player.GetInventory().CountItems(null);
            _processed = 0;
            _timedOut = 0;
            _running = true;
            FilterActive = true;
            _waitTimer = 0f;
            _current = null;

            VLog.Info($"附近存储开始：{containers.Count} 个容器，背包物品 {_beforeCount}。");
            SendNext();
        }

        internal static void Update()
        {
            if (!_running)
            {
                return;
            }

            // 由主循环驱动队列推进（避免单机同步 RPC 造成的深递归）
            if (_current == null)
            {
                SendNext();
                return;
            }

            _waitTimer += Time.deltaTime;
            if (_waitTimer > Timeout())
            {
                VLog.Warn($"附近存储：{_current.gameObject.name} 响应超时，跳过。");
                _timedOut++;
                _current = null;
            }
        }

        internal static void OnStackResponse()
        {
            if (!_running || _current == null)
            {
                return;
            }

            _processed++;
            _current = null;
        }

        /// <summary>替换原版 Inventory.StackAll 的过滤版本（仅在附近存储流程中生效）。</summary>
        internal static int FilteredStackAll(Inventory container, Inventory from, bool message)
        {
            var items = new List<ItemDrop.ItemData>(from.GetAllItems());
            int moved = 0;
            var player = Player.m_localPlayer;

            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                if (!container.ContainsItemByName(item.m_shared.m_name))
                {
                    continue;
                }

                if (player != null && player.IsItemEquiped(item))
                {
                    continue;
                }

                if (ShouldSkip(item))
                {
                    if (VLog.DebugEnabled)
                    {
                        VLog.Debug($"附近存储：过滤 {item.m_shared.m_name}");
                    }
                    continue;
                }

                if (container.AddItem(item))
                {
                    from.RemoveItem(item);
                    moved++;
                }
            }

            if (VLog.DebugEnabled)
            {
                VLog.Debug($"FilteredStackAll: 容器={container.GetName()}，移动 {moved} 堆叠。");
            }

            return moved;
        }

        private static void SendNext()
        {
            if (!_running)
            {
                return;
            }

            if (_queue.Count == 0)
            {
                Finish();
                return;
            }

            _current = _queue[0];
            _queue.RemoveAt(0);
            _waitTimer = 0f;

            try
            {
                VLog.Debug($"附近存储：请求 {_current.gameObject.name} ({_current.m_name})");
                _current.StackAll();
            }
            catch (Exception e)
            {
                VLog.Warn($"附近存储：请求失败 {(_current != null ? _current.gameObject.name : "?")}: {e.Message}");
                _timedOut++;
                _current = null;
            }
        }

        private static void Finish()
        {
            _running = false;
            _current = null;
            FilterActive = false;

            var player = Player.m_localPlayer;
            int after = player != null ? player.GetInventory().CountItems(null) : _beforeCount;
            int moved = _beforeCount - after;

            string message;
            if (moved > 0)
            {
                message = $"已存入 {moved} 件物品到 {_processed} 个箱子";
            }
            else if (_processed > 0)
            {
                message = $"没有可存入的物品（扫描 {_processed} 个箱子）";
            }
            else
            {
                message = "附近没有可用的箱子";
            }

            if (_timedOut > 0)
            {
                message += $"（{_timedOut} 个超时）";
            }

            if (player != null)
            {
                VoidChestManager.Message(player, message);
            }

            VLog.Info($"附近存储完成：移动 {moved} 件，容器 {_processed}，超时 {_timedOut}。");
        }

        private static List<Container> FindContainers(Player player)
        {
            _seen.Clear();
            var result = new List<Container>();

            int mask = LayerMask.GetMask("piece", "item", "vehicle");
            var hits = Physics.OverlapSphere(player.transform.position, VoidChestPlugin.NearbyStoreRange.Value, mask);

            VLog.Debug($"附近存储：OverlapSphere 命中 {hits.Length} 个碰撞体，半径 {VoidChestPlugin.NearbyStoreRange.Value}。");

            foreach (var hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }

                var container = hit.GetComponentInParent<Container>();
                if (container == null || container is VirtualContainer)
                {
                    continue;
                }

                if (_seen.Contains(container))
                {
                    continue;
                }

                if (container.GetInventory() == null)
                {
                    continue;
                }

                if (container.IsInUse())
                {
                    VLog.Debug($"附近存储：跳过使用中的容器 {container.gameObject.name}");
                    continue;
                }

                if (VoidChestPlugin.NearbyStoreCheckWard.Value &&
                    !PrivateArea.CheckAccess(hit.transform.position, 0f, false))
                {
                    VLog.Debug($"附近存储：跳过无权限容器 {container.gameObject.name}");
                    continue;
                }

                _seen.Add(container);
                result.Add(container);
            }

            result.Sort((a, b) =>
                Vector3.Distance(player.transform.position, a.transform.position)
                    .CompareTo(Vector3.Distance(player.transform.position, b.transform.position)));

            return result;
        }

        private static bool ShouldSkip(ItemDrop.ItemData item)
        {
            var shared = item.m_shared;
            var type = shared.m_itemType;

            if (VoidChestPlugin.NearbyStoreIgnoreAmmo.Value &&
                (type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable))
            {
                return true;
            }

            if (VoidChestPlugin.NearbyStoreIgnoreEquipable.Value && IsEquipable(type))
            {
                return true;
            }

            if (type == ItemDrop.ItemData.ItemType.Consumable)
            {
                bool isFood = shared.m_food > 0f;
                if (isFood && VoidChestPlugin.NearbyStoreIgnoreFood.Value)
                {
                    return true;
                }

                if (!isFood && VoidChestPlugin.NearbyStoreIgnoreMead.Value)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsEquipable(ItemDrop.ItemData.ItemType type)
        {
            switch (type)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Hands:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.Torch:
                case ItemDrop.ItemData.ItemType.Shoulder:
                case ItemDrop.ItemData.ItemType.Utility:
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Attach_Atgeir:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Trinket:
                    return true;
                default:
                    return false;
            }
        }

        private static float Timeout()
        {
            return Mathf.Max(0.2f, VoidChestPlugin.NearbyStoreTimeout.Value);
        }
    }
}
