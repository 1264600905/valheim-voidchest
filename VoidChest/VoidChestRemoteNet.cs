using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoidChest
{
    /// <summary>
    /// 远程存入网络层（客户端 ↔ 服务端，需双方安装本 mod）：
    /// - 客户端把"按本地规则过滤后的背包快照"发给服务端；
    /// - 服务端沿用 VoidChestRemoteStore 的扫描/堆叠流程处理该快照；
    /// - 服务端回传实际移动的物品差异（标识 + 数量），客户端据此从实时背包精确扣除，
    ///   避免快照合并、加载失败或并发变动导致的误删。
    /// </summary>
    internal static class VoidChestRemoteNet
    {
        internal const string RequestRpc = "VoidChest_RemoteDepositRequest";
        internal const string ResultRpc = "VoidChest_RemoteDepositResult";

        /// <summary>协议版本：客户端与服务端不一致时拒绝处理。</summary>
        private const int ProtocolVersion = 1;

        /// <summary>等待服务端响应的超时（秒）；超时通常意味着服务端未安装本 mod。</summary>
        private const float ResponseTimeout = 30f;

        internal enum Status
        {
            Ok = 0,
            Busy = 1,
            NoGuardstone = 2,
            NoAccess = 3,
            Disabled = 4,
            Invalid = 5,
        }

        private static ZRoutedRpc _registeredOn;
        private static bool _pending;
        private static long _activeSeq;
        private static float _sentAt;
        private static bool _timeoutWarned;

        internal static void Update()
        {
            EnsureRegistered();

            if (!_pending || Time.realtimeSinceStartup - _sentAt < ResponseTimeout)
            {
                return;
            }

            _pending = false;

            if (_timeoutWarned)
            {
                return;
            }

            _timeoutWarned = true;
            var player = Player.m_localPlayer;
            if (player != null)
            {
                VoidChestManager.Message(player, BuildStatusMessage(Status.Invalid, 0, 0, 0));
            }

            VLog.Warn($"远程存入：等待服务端响应超过 {ResponseTimeout:F0} 秒（服务端可能未安装本 mod）。");
        }

        private static void EnsureRegistered()
        {
            // ZRoutedRpc 实例在每次进入世界时重建，必须按实例重新注册
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || _registeredOn == rpc)
            {
                return;
            }

            try
            {
                rpc.Register<ZPackage>(RequestRpc, OnRequest);
                rpc.Register<ZPackage>(ResultRpc, OnResult);
                _registeredOn = rpc;
                _pending = false;
                _timeoutWarned = false;
                VLog.Info("远程存入 RPC 已注册。");
            }
            catch (Exception e)
            {
                _registeredOn = rpc; // 避免每帧重试刷日志
                VLog.Error("远程存入 RPC 注册失败: ", e);
            }
        }

        internal static string BuildStatusMessage(Status status, int moved, int processed, int skipped)
        {
            string message;
            switch (status)
            {
                case Status.Ok:
                    message = moved > 0
                        ? VoidChestLocalization.L(VoidChestLocalization.RemoteStored, moved, processed)
                        : VoidChestLocalization.L(VoidChestLocalization.RemoteNothing, processed);
                    if (skipped > 0)
                    {
                        message += VoidChestLocalization.L(VoidChestLocalization.RemoteSkipped, skipped);
                    }
                    break;
                case Status.Busy:
                    message = VoidChestLocalization.L(VoidChestLocalization.RemoteInProgress);
                    break;
                case Status.NoGuardstone:
                    message = VoidChestLocalization.L(VoidChestLocalization.RemoteNoGuardstone);
                    break;
                case Status.NoAccess:
                    message = VoidChestLocalization.L(VoidChestLocalization.RemoteNoAccess);
                    break;
                case Status.Disabled:
                    message = VoidChestLocalization.L(VoidChestLocalization.RemoteDisabled);
                    break;
                default:
                    message = VoidChestLocalization.L(VoidChestLocalization.RemoteServerMissing);
                    break;
            }

            return message;
        }

        // ---------------- 客户端 ----------------

        internal static void RequestRemote(Player player)
        {
            if (player == null)
            {
                return;
            }

            if (_pending)
            {
                VoidChestManager.Message(player, VoidChestLocalization.L(VoidChestLocalization.RemoteInProgress));
                return;
            }

            var rpc = ZRoutedRpc.instance;
            if (rpc == null)
            {
                VoidChestManager.Message(player, BuildStatusMessage(Status.Invalid, 0, 0, 0));
                return;
            }

            Inventory snapshot;
            try
            {
                snapshot = BuildFilteredSnapshot(player);
            }
            catch (Exception e)
            {
                VLog.Error("远程存入：构建背包快照失败: ", e);
                VoidChestManager.Message(player, BuildStatusMessage(Status.Invalid, 0, 0, 0));
                return;
            }

            var seq = ++_activeSeq;
            _pending = true;
            _timeoutWarned = false;
            _sentAt = Time.realtimeSinceStartup;

            try
            {
                var invPkg = new ZPackage();
                snapshot.Save(invPkg);

                var payload = new ZPackage();
                payload.Write(player.GetPlayerID());
                payload.Write(seq);
                payload.Write(ProtocolVersion);
                payload.Write(snapshot.GetWidth());
                payload.Write(snapshot.GetHeight());
                payload.Write(invPkg.GetArray());

                rpc.InvokeRoutedRPC(RequestRpc, payload);
                VoidChestManager.Message(player, VoidChestLocalization.L(VoidChestLocalization.RemoteRequested));
                VLog.Info($"远程存入：已向服务端发送请求 #{seq}（玩家 {player.GetPlayerID()}，物品 {snapshot.NrOfItems()} 件，背包数据 {invPkg.GetArray().Length} 字节）。");
            }
            catch (Exception e)
            {
                _pending = false;
                VLog.Error("远程存入：发送请求失败: ", e);
                VoidChestManager.Message(player, BuildStatusMessage(Status.Invalid, 0, 0, 0));
            }
        }

        /// <summary>构建"客户端过滤后"的背包快照（服务端不重复过滤，规则以客户端为准）。</summary>
        private static Inventory BuildFilteredSnapshot(Player player)
        {
            var inv = player.GetInventory();
            int width = Mathf.Clamp(inv.GetWidth(), 1, 64);
            int height = Mathf.Clamp(inv.GetHeight(), 1, 64);
            var snapshot = new Inventory("VoidChestRemoteDeposit", null, width, height);

            foreach (var item in inv.GetAllItems())
            {
                if (item == null || item.m_shared == null)
                {
                    continue;
                }

                if (player.IsItemEquiped(item))
                {
                    continue;
                }

                if (VoidChestFilter.ShouldSkip(item))
                {
                    continue;
                }

                snapshot.AddItem(item.Clone());
            }

            return snapshot;
        }

        private static void OnResult(long sender, ZPackage payload)
        {
            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                return;
            }

            long seq;
            Status status;
            int moved;
            int processed;
            int skipped;

            try
            {
                seq = payload.ReadLong();
                status = (Status)payload.ReadInt();
                moved = payload.ReadInt();
                processed = payload.ReadInt();
                skipped = payload.ReadInt();
            }
            catch (Exception e)
            {
                VLog.Error("远程存入：解析服务端结果头失败: ", e);
                return;
            }

            if (seq != _activeSeq)
            {
                VLog.Info($"远程存入：忽略过期的服务端结果 #{seq}（当前 #{_activeSeq}）。");
                return;
            }

            _pending = false;
            _timeoutWarned = false;

            var player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            if (status == Status.Ok)
            {
                try
                {
                    int removed = ApplyMovedItems(player, payload);
                    VLog.Info($"远程存入：服务端结果 #{seq} 移动 {moved} 堆叠，本地扣除 {removed} 件。");
                }
                catch (Exception e)
                {
                    VLog.Error("远程存入：应用服务端结果失败: ", e);
                }
            }

            VoidChestManager.Message(player, BuildStatusMessage(status, moved, processed, skipped));
            VLog.Info($"远程存入：服务端结果 #{seq} status={status} 移动={moved} 箱子={processed} 跳过={skipped}。");
        }

        /// <summary>按服务端回传的移动差异，从实时背包中扣除对应数量的物品。</summary>
        private static int ApplyMovedItems(Player player, ZPackage payload)
        {
            int count = payload.ReadInt();
            if (count <= 0)
            {
                return 0;
            }

            var live = player.GetInventory();
            var liveItems = new List<ItemDrop.ItemData>(live.GetAllItems());
            int removed = 0;

            for (int i = 0; i < count; i++)
            {
                var identity = payload.ReadString();
                int amount = payload.ReadInt();
                if (amount <= 0)
                {
                    continue;
                }

                int remaining = amount;
                foreach (var item in liveItems)
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    if (item == null || item.m_shared == null || item.m_stack <= 0)
                    {
                        continue;
                    }

                    if (!string.Equals(VoidChestItemIdentity.Of(item), identity, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int take = Mathf.Min(remaining, item.m_stack);
                    live.RemoveItem(item, take);
                    remaining -= take;
                    removed += take;
                }

                if (remaining > 0)
                {
                    VLog.Warn($"远程存入：本地背包未找到可扣除的物品（还差 {remaining} 件，物品可能已被移动/消耗）。");
                }
            }

            return removed;
        }

        // ---------------- 服务端 ----------------

        private static void OnRequest(long sender, ZPackage payload)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer())
            {
                return;
            }

            try
            {
                long playerId = payload.ReadLong();
                long seq = payload.ReadLong();
                int protocol = payload.ReadInt();
                int width = Mathf.Clamp(payload.ReadInt(), 1, 64);
                int height = Mathf.Clamp(payload.ReadInt(), 1, 64);
                var bytes = payload.ReadByteArray();

                if (protocol != ProtocolVersion || playerId == 0L || bytes == null || bytes.Length == 0)
                {
                    SendResult(sender, seq, Status.Invalid, 0, 0, 0, null);
                    return;
                }

                if (!VoidChestPlugin.EnableRemoteStore.Value)
                {
                    SendResult(sender, seq, Status.Disabled, 0, 0, 0, null);
                    return;
                }

                var snapshot = new Inventory("VoidChestRemoteRequest", null, width, height);
                snapshot.Load(new ZPackage(bytes));

                VoidChestRemoteStore.StartRemoteJob(sender, seq, playerId, snapshot);
            }
            catch (Exception e)
            {
                VLog.Warn("远程存入：处理服务端请求失败: " + e.Message);
            }
        }

        internal static void SendResult(long peer, long seq, Status status, int moved, int processed, int skipped,
            List<KeyValuePair<string, int>> movedEntries)
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || peer == 0L)
            {
                return;
            }

            try
            {
                var payload = new ZPackage();
                payload.Write(seq);
                payload.Write((int)status);
                payload.Write(moved);
                payload.Write(processed);
                payload.Write(skipped);

                int count = movedEntries != null ? movedEntries.Count : 0;
                payload.Write(count);
                for (int i = 0; i < count; i++)
                {
                    payload.Write(movedEntries[i].Key);
                    payload.Write(movedEntries[i].Value);
                }

                rpc.InvokeRoutedRPC(peer, ResultRpc, payload);
            }
            catch (Exception e)
            {
                VLog.Warn($"远程存入：发送结果到 {peer} 失败: {e.Message}");
            }
        }
    }
}
