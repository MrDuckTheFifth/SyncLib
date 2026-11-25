using System;
using HarmonyLib;
using UnityEngine;

namespace SyncLib {
    [HarmonyPatch(typeof(GetComponentInParentAttribute), "GetComponent")]
    public static class FixGetComponentInParentPrefabBugPatch {
        private static bool Prefix(Transform transform, Type type, ref Component __result) {
            Transform transform2 = transform;
            while (transform2 != null) {
                Component component = transform2.GetComponent(type);
                if (component != null) {
                    __result = component;
                    return false;
                }
                transform2 = transform2.parent;
            }
            __result = null;
            return false;
        }
    }
}