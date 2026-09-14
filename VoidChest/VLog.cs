using System;

namespace VoidChest
{
    internal static class VLog
    {
        internal static bool DebugEnabled
        {
            get { return VoidChestPlugin.DebugLog == null || VoidChestPlugin.DebugLog.Value; }
        }

        internal static void Debug(string message)
        {
            if (DebugEnabled)
            {
                VoidChestPlugin.Log.LogInfo("[Debug] " + message);
            }
        }

        internal static void Info(string message)
        {
            VoidChestPlugin.Log.LogInfo(message);
        }

        internal static void Warn(string message)
        {
            VoidChestPlugin.Log.LogWarning(message);
        }

        internal static void Error(string message, Exception e = null)
        {
            VoidChestPlugin.Log.LogError(e == null ? message : message + Environment.NewLine + e);
        }
    }
}
