using UnityEngine;

namespace VoidChest
{
    /// <summary>存储流程（附近/远程）共享性能统计。</summary>
    internal static class VoidChestPerf
    {
        internal static float StartRealtime;
        internal static float ScanMs;
        internal static int ScanBatches;
        internal static int StackCalls;
        internal static double StackMs;
        internal static float MaxStepMs;
        internal static int DataWriteCount;
        internal static double DataWriteMs;

        internal static void Reset()
        {
            StartRealtime = Time.realtimeSinceStartup;
            ScanMs = 0f;
            ScanBatches = 0;
            StackCalls = 0;
            StackMs = 0.0;
            MaxStepMs = 0f;
            DataWriteCount = 0;
            DataWriteMs = 0.0;
        }

        internal static void AddScan(double ms)
        {
            ScanMs += (float)ms;
            ScanBatches++;
        }

        internal static void AddStack(double ms)
        {
            StackCalls++;
            StackMs += ms;
        }

        internal static void AddDataWrite(double ms)
        {
            DataWriteCount++;
            DataWriteMs += ms;
        }

        internal static void NoteStep(double ms)
        {
            if (ms > MaxStepMs)
            {
                MaxStepMs = (float)ms;
            }
        }

        internal static double TotalMs()
        {
            return (Time.realtimeSinceStartup - StartRealtime) * 1000.0;
        }

        internal static string Summary(string tag, int moved, int containers, int rejected, int skipped)
        {
            return $"{tag}性能：总耗时 {TotalMs():F0}ms | 扫描 {ScanMs:F1}ms({ScanBatches}批) | 容器 {containers}(拒绝{rejected}/跳过{skipped}) | 堆叠 {StackCalls} 次 {StackMs:F1}ms | 最长步骤 {MaxStepMs:F0}ms | 数据写入 {DataWriteCount} 次 {DataWriteMs:F1}ms | 移动 {moved} 件";
        }
    }
}
