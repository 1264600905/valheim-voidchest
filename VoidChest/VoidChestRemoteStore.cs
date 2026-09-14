using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

namespace VoidChest
{
    /// <summary>
    /// P2 守护石远程仓库：扫描玩家有权限的守护石领地范围内的所有箱子，远程堆入背包物品。
    /// 仅在主机/单机模式执行（服务端 ZDO 权威）；专用服务器需服务端安装本 mod。
    /// 优先使用反射全量快照（一次遍历所有 ZDO），失败时降级为按 prefab 名分帧扫描。
    ///
    /// 重要：全程只保存 ZDOID，使用时用 ZDOMan.GetZDO(id) 重新获取实时对象。
    /// ZDO 对象会被对象池复用，长期持有对象引用可能指向被复用的新对象（数据污染风险）。
    /// 读-改-写在同一主线程 Update 内原子完成，不会被其他玩家操作打断。
    /// </summary>
    internal static class VoidChestRemoteStore
    {
        private enum Phase
        {
            Idle,
            FullScan,   // 反射全量快照分类（守护石 + 容器候选）
            ChestFilter,// 容器候选筛选（守护石范围内 / 未占用）
            Guards,     // 降级：按守护石 prefab 名扫描
            Chests,     // 降级：按容器 prefab 名扫描
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

        // 反射全量快照（仅存 ID，使用时重新解析）
        private static bool _objectsByIdInit;
        private static AccessTools.FieldRef<ZDOMan, Dictionary<ZDOID, ZDO>> _objectsByIdRef;
        private static List<ZDOID> _snapshot;
        private static int _cursor;
        private static readonly List<ZDOID> _chestCandidates = new List<ZDOID>();
        private static int _candidateCursor;
        private static readonly Dictionary<int, byte> _prefabKind = new Dictionary<int, byte>();

        // 降级：prefab 名扫描
        private static List<string> _prefabNames = new List<string>();
        private static int _prefabIndex;
        private static readonly List<ZDO> _found = new List<ZDO>();
        private static int _iter;

        // 结果（只保存 ID / 值，不保存对象引用）
        private static readonly List<GuardInfo> _guards = new List<GuardInfo>();
        private static readonly List<ZDOID> _guardIds = new List<ZDOID>();
        private static readonly List<ZDOID> _chests = new List<ZDOID>();
        private static readonly HashSet<ZDOID> _chestSeen = new HashSet<ZDOID>();
        private static int _chestIndex;

        private static int _moved;
        private static int _processed;
        private static int _skipped;

        // 缓存
        private static float _cacheTime = -99999f;
        private static readonly List<ZDOID> _cachedGuardIds = new List<ZDOID>();
        private static readonly List<ZDOID> _cachedChestIds = new List<ZDOID>();

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
                VLog.Info("远程存入：已有流程进行中，忽略本次点击。");
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

            _player = player;
            _playerId = player.GetPlayerID();

            _guards.Clear();
            _guardIds.Clear();
            _chests.Clear();
            _chestSeen.Clear();
            _chestCandidates.Clear();
            _found.Clear();

            _moved = 0;
            _processed = 0;
            _skipped = 0;
            _chestIndex = 0;
            _candidateCursor = 0;

            VoidChestPerf.Reset();

            // 1) 尝试缓存
            if (TryUseCache())
            {
                _phase = Phase.Stacking;
                VLog.Info($"远程存入开始：缓存命中（守护石 {_guards.Count}，箱子 {_chests.Count}）。");
                return;
            }

            // 2) 反射全量快照优先
            if (EnsureObjectsByIdRef())
            {
                _snapshot = SnapshotZdos();
                _cursor = 0;
                _phase = Phase.FullScan;
                VLog.Info($"远程存入开始：全量快照 {(_snapshot != null ? _snapshot.Count : 0)} 个 ZDO（玩家 ID {_playerId}）。");
                return;
            }

            // 3) 降级：按 prefab 名扫描
            var guardPrefabs = CollectPrefabs(go => go.GetComponent<PrivateArea>() != null);
            if (guardPrefabs.Count == 0)
            {
                VoidChestManager.Message(player, "世界中未找到守护石。");
                VLog.Info("远程存入：未找到守护石 prefab。");
                _phase = Phase.Idle;
                return;
            }

            _prefabNames = guardPrefabs;
            _prefabIndex = 0;
            _iter = 0;
            _phase = Phase.Guards;
            VLog.Info($"远程存入开始：降级 prefab 扫描模式（守护石 prefab {guardPrefabs.Count} 种，玩家 ID {_playerId}）。");
        }

        internal static void Update()
        {
            if (_phase == Phase.Idle)
            {
                return;
            }

            float budgetEnd = Time.realtimeSinceStartup + 0.004f;

            if (_phase == Phase.FullScan)
            {
                UpdateFullScan(budgetEnd);
                return;
            }

            if (_phase == Phase.ChestFilter)
            {
                UpdateChestFilter(budgetEnd);
                return;
            }

            if (_phase == Phase.Guards)
            {
                UpdateGuards(budgetEnd);
                return;
            }

            if (_phase == Phase.Chests)
            {
                UpdateChests(budgetEnd);
                return;
            }

            if (_phase == Phase.Stacking)
            {
                while (Time.realtimeSinceStartup < budgetEnd)
                {
                    if (_chestIndex >= _chests.Count)
                    {
                        Finish(null);
                        return;
                    }

                    ProcessChest(_chests[_chestIndex++]);
                }
            }
        }

        // ---------------- 反射全量快照 ----------------

        private static bool EnsureObjectsByIdRef()
        {
            if (_objectsByIdInit)
            {
                return _objectsByIdRef != null;
            }

            _objectsByIdInit = true;

            try
            {
                _objectsByIdRef = AccessTools.FieldRefAccess<ZDOMan, Dictionary<ZDOID, ZDO>>("m_objectsByID");
            }
            catch (Exception e)
            {
                _objectsByIdRef = null;
                VLog.Warn("ZDOMan.m_objectsByID 反射失败，降级为 prefab 扫描模式: " + e.Message);
            }

            return _objectsByIdRef != null;
        }

        private static List<ZDOID> SnapshotZdos()
        {
            var sw = Stopwatch.StartNew();
            var dict = _objectsByIdRef(ZDOMan.instance);
            var list = new List<ZDOID>(dict.Count);

            foreach (var kv in dict)
            {
                list.Add(kv.Key);
            }

            sw.Stop();
            VoidChestPerf.AddScan(sw.Elapsed.TotalMilliseconds);

            return list;
        }

        private static void UpdateFullScan(float budgetEnd)
        {
            if (_snapshot == null)
            {
                _phase = Phase.ChestFilter;
                return;
            }

            int processed = 0;

            while (_cursor < _snapshot.Count)
            {
                var id = _snapshot[_cursor++];
                processed++;

                var zdo = ZDOMan.instance.GetZDO(id);
                if (zdo != null)
                {
                    byte kind;
                    try
                    {
                        kind = ClassifyPrefab(zdo.GetPrefab());
                    }
                    catch
                    {
                        kind = 0;
                    }

                    if (kind == 1)
                    {
                        if (HasAccess(zdo))
                        {
                            _guards.Add(new GuardInfo
                            {
                                Pos = zdo.GetPosition(),
                                Radius = GetGuardRadius(zdo.GetPrefab())
                            });
                            _guardIds.Add(id);
                        }
                    }
                    else if (kind == 2)
                    {
                        _chestCandidates.Add(id);
                    }
                }

                if ((processed & 1023) == 0 && Time.realtimeSinceStartup >= budgetEnd)
                {
                    break;
                }
            }

            if (_cursor >= _snapshot.Count)
            {
                _snapshot = null;

                if (_guards.Count == 0)
                {
                    Finish("没有找到你有权限的守护石");
                    return;
                }

                _candidateCursor = 0;
                _phase = Phase.ChestFilter;
                VLog.Info($"远程存入：快照分类完成，守护石 {_guards.Count}，容器候选 {_chestCandidates.Count}。");
            }
        }

        private static void UpdateChestFilter(float budgetEnd)
        {
            int processed = 0;

            while (_candidateCursor < _chestCandidates.Count)
            {
                var id = _chestCandidates[_candidateCursor++];
                processed++;

                if (!_chestSeen.Contains(id))
                {
                    var zdo = ZDOMan.instance.GetZDO(id);
                    if (zdo != null && IsInAnyGuard(zdo.GetPosition()))
                    {
                        if (zdo.GetInt(ZDOVars.s_inUse) == 1)
                        {
                            _skipped++;
                        }
                        else
                        {
                            _chestSeen.Add(id);
                            _chests.Add(id);
                        }
                    }
                }

                if ((processed & 1023) == 0 && Time.realtimeSinceStartup >= budgetEnd)
                {
                    break;
                }
            }

            if (_candidateCursor >= _chestCandidates.Count)
            {
                _chestCandidates.Clear();
                _chestIndex = 0;
                UpdateCache();
                _phase = Phase.Stacking;
                VLog.Info($"远程存入：目标箱子 {_chests.Count} 个（有权限守护石 {_guards.Count} 个）。");
            }
        }

        /// <summary>prefab 分类：0=忽略, 1=守护石, 2=可存容器（缓存）。</summary>
        private static byte ClassifyPrefab(int prefabHash)
        {
            if (_prefabKind.TryGetValue(prefabHash, out var cached))
            {
                return cached;
            }

            byte kind = 0;
            var go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabHash) : null;

            if (go != null)
            {
                if (go.GetComponent<PrivateArea>() != null)
                {
                    kind = 1;
                }
                else if (go.GetComponent<Container>() != null &&
                         go.GetComponent<Ship>() == null &&
                         go.GetComponent<Vagon>() == null &&
                         !go.name.ToLower().Contains("tombstone"))
                {
                    kind = 2;
                }
            }

            _prefabKind[prefabHash] = kind;
            return kind;
        }

        // ---------------- 降级：prefab 名扫描 ----------------

        private static void UpdateGuards(float budgetEnd)
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
                        if (!zdo.IsValid() || !HasAccess(zdo))
                        {
                            continue;
                        }

                        _guards.Add(new GuardInfo
                        {
                            Pos = zdo.GetPosition(),
                            Radius = GetGuardRadius(zdo.GetPrefab())
                        });
                        _guardIds.Add(zdo.m_uid);
                    }

                    _found.Clear();
                    _iter = 0;
                    _prefabIndex++;
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

                if (go.GetComponent<Ship>() != null || go.GetComponent<Vagon>() != null)
                {
                    return false;
                }

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

        private static void UpdateChests(float budgetEnd)
        {
            while (Time.realtimeSinceStartup < budgetEnd)
            {
                if (_prefabIndex >= _prefabNames.Count)
                {
                    _chestIndex = 0;
                    UpdateCache();
                    _phase = Phase.Stacking;
                    VLog.Info($"远程存入：目标箱子 {_chests.Count} 个（有权限守护石 {_guards.Count} 个）。");
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
                        if (!zdo.IsValid() || _chestSeen.Contains(zdo.m_uid) || !IsInAnyGuard(zdo.GetPosition()))
                        {
                            continue;
                        }

                        if (zdo.GetInt(ZDOVars.s_inUse) == 1)
                        {
                            _skipped++;
                            continue;
                        }

                        _chestSeen.Add(zdo.m_uid);
                        _chests.Add(zdo.m_uid);
                    }

                    _found.Clear();
                    _iter = 0;
                    _prefabIndex++;
                }
            }
        }

        // ---------------- 缓存 ----------------

        private static bool TryUseCache()
        {
            if (VoidChestPlugin.RemoteStoreCacheSeconds.Value <= 0f)
            {
                return false;
            }

            if (_cachedGuardIds.Count == 0 && _cachedChestIds.Count == 0)
            {
                return false;
            }

            if (Time.realtimeSinceStartup - _cacheTime > VoidChestPlugin.RemoteStoreCacheSeconds.Value)
            {
                return false;
            }

            foreach (var id in _cachedGuardIds)
            {
                var zdo = ZDOMan.instance.GetZDO(id);
                if (zdo == null || !zdo.IsValid())
                {
                    continue;
                }

                _guards.Add(new GuardInfo
                {
                    Pos = zdo.GetPosition(),
                    Radius = GetGuardRadius(zdo.GetPrefab())
                });
                _guardIds.Add(id);
            }

            if (_guards.Count == 0)
            {
                return false;
            }

            foreach (var id in _cachedChestIds)
            {
                var zdo = ZDOMan.instance.GetZDO(id);
                if (zdo == null || !zdo.IsValid())
                {
                    continue;
                }

                if (zdo.GetInt(ZDOVars.s_inUse) == 1)
                {
                    _skipped++;
                    continue;
                }

                if (_chestSeen.Add(id))
                {
                    _chests.Add(id);
                }
            }

            if (_chests.Count == 0)
            {
                return false;
            }

            VLog.Info($"远程存入：缓存命中（缓存于 {Time.realtimeSinceStartup - _cacheTime:F0}s 前，守护石 {_guards.Count}，箱子 {_chests.Count}）。");
            return true;
        }

        private static void UpdateCache()
        {
            _cachedGuardIds.Clear();
            _cachedGuardIds.AddRange(_guardIds);

            _cachedChestIds.Clear();
            _cachedChestIds.AddRange(_chests);

            _cacheTime = Time.realtimeSinceStartup;

            VLog.Debug($"远程存入：缓存已更新（守护石 {_cachedGuardIds.Count}，箱子 {_cachedChestIds.Count}）。");
        }

        // ---------------- 堆叠 / 收尾 ----------------

        private static void ProcessChest(ZDOID id)
        {
            var playerInv = _player != null ? _player.GetInventory() : null;
            if (playerInv == null)
            {
                return;
            }

            try
            {
                // 重新获取实时对象（对象池可能已复用，绝不能持有旧引用）
                var zdo = ZDOMan.instance.GetZDO(id);
                if (zdo == null || !zdo.IsValid())
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
                VLog.Warn($"远程存入：处理箱子 {id} 失败: {e.Message}");
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

        // ---------------- 辅助 ----------------

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
