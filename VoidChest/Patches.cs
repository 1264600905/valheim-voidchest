using HarmonyLib;

namespace VoidChest
{
    [HarmonyPatch(typeof(Container), "Awake")]
    internal static class ContainerAwakePatch
    {
        private static bool Prefix(Container __instance)
        {
            if (__instance is VirtualContainer vc)
            {
                vc.InitVirtual();
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.IsOwner))]
    internal static class ContainerIsOwnerPatch
    {
        private static bool Prefix(Container __instance, ref bool __result)
        {
            if (__instance is VirtualContainer)
            {
                __result = true;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.SetInUse))]
    internal static class ContainerSetInUsePatch
    {
        private static bool Prefix(Container __instance)
        {
            return !(__instance is VirtualContainer);
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.IsInUse))]
    internal static class ContainerIsInUsePatch
    {
        private static bool Prefix(Container __instance, ref bool __result)
        {
            if (__instance is VirtualContainer)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Container), "CheckForChanges")]
    internal static class ContainerCheckForChangesPatch
    {
        private static bool Prefix(Container __instance)
        {
            return !(__instance is VirtualContainer);
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.Save))]
    internal static class PlayerSavePatch
    {
        private static void Prefix(Player __instance)
        {
            VoidChestManager.FlushActive(__instance);
        }
    }
}
