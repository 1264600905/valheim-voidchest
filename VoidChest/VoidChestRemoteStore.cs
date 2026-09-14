using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace VoidChest
{
    /// <summary>
    /// P2 守护石远程仓库：扫描玩家有权限的守护石领地范围内的所有箱子，远程堆入背包物品。
    /// 仅在主机/单机模式执行（服务端 ZDO 权威）；专用服务器需服务端安装本 mod。
    /// 全程按 ZDO 数据操作（远处箱子 GameObject 未加载）。
    /// </summary>
    internal static class VoidChestRemoteStore
    {
        private enum Phase
        {
            Idle,
            Guards,
            Chests,
            Stacking
        }

        private struct GuardInfo
        {
            public Vector3 Pos;
            public float Radius;
        }

        internal static bool IsRunning => _phase != Phase.Idle;

        private static Phase _phase = Phase.Idle;
        private static Player _player;
        private static long _playerId;

        private static List<string> _prefabNames = new List<string>();
        private static int _prefabIndex;
        private static readonly List<ZDO> _found = new List<ZDO>();
        private static int _iter;

        private static readonly List<GuardInfo> _guards = new List<GuardInfo>();
        private static readonly Dictionary<ZDOID, ZDO> _chests = new Dictionary<ZDOID, ZDO>();
        private static readonly List<ZDO> _chestList = new List<ZDO>();
        private static int _chestIndex;

        private static int _moved;
        private static int _processed;
        private static int _skipped;

        private static readonly Dictionary<int, float> _guardRadiusCache = new Dictionary<int, float>();
        private static readonly Dictionary<int, Vector2i> _containerSizeCache = new Dictionary<int, Vector2i>();

        internal static void Start(Player player)
        {
            if (player == null)
            {
                return;
            }

            if (_phase != Phase.Idle)
            {
                VoidChestManager.Message(player, "远程存入正在进行中...");
                return;
            }

            if (VoidChestNearbyStore.IsRunning)
            {
                VoidChestManager.Message(player, "附近存储正在进行中，请稍后再试。");
                return;
            }

            if (ZNet.instance == null || !ZNet.instance.IsServer())
            {
                VoidChestManager.Message(player, "远程存入仅支持单机/主机模式");
                VLog.Info("远程存入：非主机模式，已取消。");
                return;
            }

            var guardPrefabs = CollectPrefabs(go => go.GetComponent<PrivateArea>() != null);
            if (guardPrefabs.Count == 0)
            {
                VoidChestManager.Message(player, "世界中未找到守护石。");
                VLog.Info("远程存入：未找到守护石 prefab。");
                return;
            }

            _player = player;
            _playerId = player.GetPlayerID();

            _guards.Clear();
            _chests.Clear();
            _chestList.Clear();
            _found.Clear();

            _prefabNames = guardPrefabs;
            _prefabIndex = 0;
            _iter = 0;

            _moved = 0;
            _processed = 0;
            _skipped = 0;

            VoidChestPerf.Reset();
            _phase = Phase.Guards;

            VLog.Info($"远程存入开始：守护石 prefab {guardPrefabs.Count} 种，玩家 ID {_playerId}。");
        }

        internal static void Update()
        {
            if (_phase == Phase.Idle)
            {
                return;
            }

            float budgetEnd = Time.realtimeSinceStartup + 0.004f;

            if (_phase == Phase.Guards)
            {
                while (Time.realtimeSinceStartup < budgetEnd)
                {
                    if (_prefabIndex >= _prefabNames.Count)
                    {
                        if (_guards.Count == 0)
                        {
                            Finish("没有找到你有权限的守护石");
                            return;
                        }

                        BeginChestScan();
                        break;
                    }

                    var name = _prefabNames[_prefabIndex];
                    var sw = Stopwatch.StartNew();
                    bool done = ZDOMan.instance.GetAllZDOsWithPrefabIterative(name, _found, ref _iter);
                    sw.Stop();
                    VoidChestPerf.AddScan(sw.Elapsed.TotalMilliseconds);

                    if (done)
                    {
                        foreach (var zdo in _found)
                        {
                            if (!zdo.IsValid())
                            {
                                continue;
                            }

                            if (!HasAccess(zdo))
                            {
                                continue;
                            }

                            _guards.Add(new GuardInfo
                            {
                                Pos = zdo.GetPosition(),
                                Radius = GetGuardRadius(zdo.GetPrefab())
                            });
                        }

                        _found.Clear();
                        _iter = 0;
                        _prefabIndex++;
                    }
                }

                return;
            }

            if (_phase == Phase.Chests)
            {
                while (Time.realtimeSinceStartup < budgetEnd)
                {
                    if (_prefabIndex >= _prefabNames.Count)
                    {
                        _chestList.AddRange(_chests.Values);
                        _chestIndex = 0;
                        _phase = Phase.Stacking;
                        VLog.Info($"远程存入：目标箱子 {_chestList.Count} 个（有权限守护石 {_guards.Count} 个）。");
                        break;
                    }

                    var name = _prefabNames[_prefabIndex];
                    var sw = Stopwatch.StartNew();
                    bool done = ZDOMan.instance.GetAllZDOsWithPrefabIterative(name, _found, ref _iter);
                    sw.Stop();
                    VoidChestPerf.AddScan(sw.Elapsed.TotalMilliseconds);

                    if (done)
                    {
                        foreach (var zdo in _found)
                        {
                            if (!zdo.IsValid())
                            {
                                continue;
                            }

                            if (_chests.ContainsKey(zdo.m_uid))
                            {
                                continue;
                            }

                            if (!IsInAnyGuard(zdo.GetPosition()))
                            {
                                continue;
                            }

                            if (zdo.GetInt(ZDOVars.s_inUse) == 1)
                            {
                                _skipped++;
                                continue;
                            }

                            _chests[zdo.m_uid] = zdo;
                        }

                        _found.Clear();
                        _iter = 0;
                        _prefabIndex++;
                    }
                }

                return;
            }

            if (_phase == Phase.Stacking)
            {
                while (Time.realtimeSinceStartup < budgetEnd)
                {
                    if (_chestIndex >= _chestList.Count)
                    {
                        Finish(null);
                        return;
                    }

                    ProcessChest(_chestList[_chestIndex++]);
                }
            }
        }

        private static void BeginChestScan()
        {
            _prefabNames = CollectPrefabs(go =>
            {
                if (go.GetComponent<Container>() == null)
                {
                    return false;
                }

                // 排除船、车
                if (go.GetComponent<Ship>() != null || go.GetComponent<Vagon>() != null)
                {
                    return false;
                }

                // 排除墓碑
                if (go.name.ToLower().Contains("tombstone"))
                {
                    return false;
                }

                return true;
            });

            _prefabIndex = 0;
            _found.Clear();
            _iter = 0;
            _phase = Phase.Chests;

            VLog.Info($"远程存入：开始扫描容器（{_prefabNames.Count} 种 prefab），有权限守护石 {_guards.Count} 个。");
        }

        private static void ProcessChest(ZDO zdo)
        {
            var playerInv = _player != null ? _player.GetInventory() : null;
            if (playerInv == null)
            {
                return;
            }

            try
            {
                if (!zdo.IsValid())
                {
                    _skipped++;
                    return;
                }

                if (zdo.GetInt(ZDOVars.s_inUse) == 1)
                {
                    _skipped++;
                    return;
                }

                var sw = Stopwatch.StartNew();

                var size = GetContainerSize(zdo.GetPrefab());
                var temp = new Inventory("RemoteContainer", null, size.x, size.y);

                var bytes = zdo.GetByteArray(ZDOVars.s_items);
                if (bytes != null)
                {
                    temp.Load(new ZPackage(bytes));
                }

                int moved = VoidChestFilter.StackAllFiltered(temp, playerInv);

                if (moved > 0)
                {
                    var writeSw = Stopwatch.StartNew();
                    var pkg = new ZPackage();
                    temp.Save(pkg);
                    zdo.Set(ZDOVars.s_items, pkg.GetArray());
                    writeSw.Stop();
                    VoidChestPerf.AddDataWrite(writeSw.Elapsed.TotalMilliseconds);
                    _moved += moved;
                }

                _processed++;
                sw.Stop();
                VoidChestPerf.NoteStep(sw.Elapsed.TotalMilliseconds);
            }
            catch (Exception e)
            {
                VLog.Warn($"远程存入：处理箱子 {zdo.m_uid} 失败: {e.Message}");
                _skipped++;
            }
        }

        private static void Finish(string overrideMessage)
        {
            _phase = Phase.Idle;

            string message;
            if (!string.IsNullOrEmpty(overrideMessage))
            {
                message = overrideMessage;
            }
            else if (_moved > 0)
            {
                message = $"已远程存入 {_moved} 件物品到家的箱子（{_processed} 个箱子）";
            }
            else
            {
                message = $"没有可远程存入的物品（扫描 {_processed} 个箱子）";
            }

            if (_skipped > 0)
            {
                message += $"（跳过 {_skipped} 个使用中/无效箱子）";
            }

            if (_player != null)
            {
                VoidChestManager.Message(_player, message);
            }

            VLog.Info($"远程存入完成：移动 {_moved} 件，箱子 {_processed}，跳过 {_skipped}，守护石 {_guards.Count}。");
            VLog.Info(VoidChestPerf.Summary("远程存储", _moved, _processed, 0, _skipped));

            _player = null;
        }

        private static List<string> CollectPrefabs(Func<GameObject, bool> predicate)
        {
            var result = new List<string>();
            var scene = ZNetScene.instance;
            if (scene == null)
            {
                return result;
            }

            foreach (var go in scene.m_prefabs)
            {
                if (go == null)
                {
                    continue;
                }

                try
                {
                    if (predicate(go))
                    {
                        result.Add(go.name);
                    }
                }
                catch (Exception)
                {
                    // 忽略单个 prefab 的检查异常
                }
            }

            return result;
        }

        private static bool HasAccess(ZDO guard)
        {
            if (guard.GetLong(ZDOVars.s_creator, 0L) == _playerId)
            {
                return true;
            }

            int count = guard.GetInt(ZDOVars.s_permitted);
            for (int i = 0; i < count; i++)
            {
                if (guard.GetLong("pu_id" + i, 0L) == _playerId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInAnyGuard(Vector3 pos)
        {
            foreach (var guard in _guards)
            {
                float dx = pos.x - guard.Pos.x;
                float dz = pos.z - guard.Pos.z;
                if (dx * dx + dz * dz <= guard.Radius * guard.Radius)
                {
                    return true;
                }
            }

            return false;
        }

        private static float GetGuardRadius(int prefabHash)
        {
            if (_guardRadiusCache.TryGetValue(prefabHash, out var cached))
            {
                return cached;
            }

            float radius = 10f;
            var go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabHash) : null;
            if (go != null)
            {
                var area = go.GetComponent<PrivateArea>();
                if (area != null)
                {
                    radius = area.m_radius;
                }
            }

            _guardRadiusCache[prefabHash] = radius;
            return radius;
        }

        private static Vector2i GetContainerSize(int prefabHash)
        {
            if (_containerSizeCache.TryGetValue(prefabHash, out var cached))
            {
                return cached;
            }

            var size = new Vector2i(8, 4);
            var go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabHash) : null;
            if (go != null)
            {
                var container = go.GetComponent<Container>();
                if (container != null)
                {
                    size = new Vector2i(container.m_width, container.m_height);
                }
            }

            _containerSizeCache[prefabHash] = size;
            return size;
        }
    }
}
