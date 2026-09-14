using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

namespace VoidChest
{
    /// <summary>
    /// 自研"附近存储"：把背包物品堆进周围箱子。
    /// 走原版 Container.StackAll() RPC 链路（联机安全），串行队列 + 超时。
    /// 堆叠时通过 FilterActive + Inventory.StackAll patch 应用过滤（VoidChestFilter）。
    /// </summary>
    internal static class VoidChestNearbyStore
    {
        internal static bool FilterActive { get; private set; }
        internal static bool IsRunning => _running;

        private static readonly List<Container> _queue = new List<Container>();
        private static readonly HashSet<Container> _seen = new HashSet<Container>();
        private static readonly AccessTools.FieldRef<Container, ZNetView> NViewRef =
            AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");

        private static bool _running;
        private static Container _current;
        private static float _waitTimer;
        private static int _beforeCount;
        private static int _processed;
        private static int _timedOut;
        private static int _rejected;
        private static float _currentSentAt;

        internal static void Start(Player player)
        {
            if (player == null)
            {
                return;
            }

            if (_running || VoidChestRemoteStore.IsRunning)
            {
                VoidChestManager.Message(player, "已有存储操作正在进行中...");
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
            _rejected = 0;
            _running = true;
            FilterActive = true;
            _waitTimer = 0f;
            _current = null;

            VoidChestPerf.Reset();

            VLog.Info($"附近存储开始：{containers.Count} 个容器，背包物品 {_beforeCount}。");
            SendNext();
        }

        internal static void Update()
        {
            if (!_running)
            {
                return;
            }

            // 主循环驱动队列推进；单机/主机同步 RPC 时在时间预算内连续处理多个容器
            float budgetEnd = Time.realtimeSinceStartup + VoidChestPerf.FrameBudgetSeconds;

            while (true)
            {
                if (_current == null)
                {
                    if (_queue.Count == 0)
                    {
                        Finish();
                        return;
                    }

                    SendNext();

                    if (_current != null)
                    {
                        // 等待网络响应
                        break;
                    }

                    if (Time.realtimeSinceStartup >= budgetEnd)
                    {
                        // 本帧预算用尽，下一帧继续
                        break;
                    }

                    continue;
                }

                _waitTimer += Time.deltaTime;
                if (_waitTimer > Timeout())
                {
                    VLog.Warn($"附近存储：{_current.gameObject.name} 响应超时，跳过。");
                    _timedOut++;
                    _current = null;
                    continue;
                }

                break;
            }
        }

        internal static void OnStackResponse(bool granted)
        {
            if (!_running || _current == null)
            {
                return;
            }

            VoidChestPerf.NoteStep((Time.realtimeSinceStartup - _currentSentAt) * 1000.0);

            _processed++;

            if (!granted)
            {
                _rejected++;
                VLog.Debug($"附近存储：{_current.gameObject.name} 被拒绝（使用中/无权限）。");
            }

            _current = null;
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
            _currentSentAt = Time.realtimeSinceStartup;

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

            if (_rejected > 0)
            {
                message += $"（{_rejected} 个使用中/被拒绝）";
            }

            if (_timedOut > 0)
            {
                message += $"（{_timedOut} 个超时）";
            }

            if (player != null)
            {
                VoidChestManager.Message(player, message);
            }

            VLog.Info($"附近存储完成：移动 {moved} 件，容器 {_processed}，拒绝 {_rejected}，超时 {_timedOut}。");
            VLog.Info(VoidChestPerf.Summary("附近存储", moved, _processed, _rejected, _timedOut));
        }

        private static List<Container> FindContainers(Player player)
        {
            var sw = Stopwatch.StartNew();
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

                if (IsContainerBusy(container, out var busyReason))
                {
                    VLog.Debug($"附近存储：跳过容器 {container.gameObject.name}（{busyReason}）");
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

            sw.Stop();
            VoidChestPerf.AddScan(sw.Elapsed.TotalMilliseconds);

            return result;
        }

        /// <summary>
        /// 判断容器是否被占用：
        /// 1) 本地状态（单机/主机/自己使用中）；
        /// 2) ZDO 的 InUse 字段（其他玩家正在打开该箱子，客户端同步值）。
        /// 原版 RPC 授权（owner 端检查）仍作为最终兜底。
        /// </summary>
        private static bool IsContainerBusy(Container container, out string reason)
        {
            reason = null;

            if (container.IsInUse())
            {
                reason = "本地使用中";
                return true;
            }

            try
            {
                var nview = NViewRef(container);
                var zdo = nview != null ? nview.GetZDO() : null;
                if (zdo != null && zdo.GetInt(ZDOVars.s_inUse) == 1)
                {
                    reason = "其他玩家使用中(ZDO)";
                    return true;
                }
            }
            catch (Exception e)
            {
                VLog.Debug("ZDO 占用检查失败: " + e.Message);
            }

            return false;
        }

        private static float Timeout()
        {
            return Mathf.Max(0.2f, VoidChestPlugin.NearbyStoreTimeout.Value);
        }
    }
}
