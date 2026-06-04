using System;
using HarmonyLib;
using UnityEngine;

namespace MappingExtensions.HarmonyPatches
{
    // Temporary diagnostics for invisible/absent Mapping Extensions walls.
    // Set Enabled to false or delete this file after collecting logs.
    internal static class ObstacleDebugPatches
    {
        internal static readonly bool Enabled = true;
        private const int MaxLogLines = 300;
        private static int logLines;

        internal static bool ShouldLog(ObstacleData obstacleData)
        {
            return Enabled
                   && (Plugin.active
                       || MappingExtensionsData.IsPrecisionValue(obstacleData.lineIndex)
                       || MappingExtensionsData.IsPrecisionValue(obstacleData.width)
                       || MappingExtensionsData.IsPrecisionValue((int)obstacleData.lineLayer)
                       || MappingExtensionsData.IsPrecisionValue(obstacleData.height)
                       || obstacleData.duration < 0f);
        }

        internal static void Info(string message)
        {
            if (!Enabled)
            {
                return;
            }

            try
            {
                if (logLines++ >= MaxLogLines)
                {
                    return;
                }

                Plugin.Log.Info($"[ObstacleDebug] {message}");
            }
            catch
            {
                // Diagnostics must not affect gameplay.
            }
        }

        internal static void Error(string context, Exception exception)
        {
            if (!Enabled)
            {
                return;
            }

            try
            {
                if (logLines++ >= MaxLogLines)
                {
                    return;
                }

                Plugin.Log.Error($"[ObstacleDebug] {context}: {exception}");
            }
            catch
            {
                // Diagnostics must not affect gameplay.
            }
        }

        internal static string ObstacleDataString(ObstacleData obstacleData)
        {
            return $"time={obstacleData.time:F3}, lineIndex={obstacleData.lineIndex}, lineLayer={obstacleData.lineLayer}, width={obstacleData.width}, height={obstacleData.height}, duration={obstacleData.duration:F3}";
        }

        internal static string SpawnDataString(ObstacleSpawnData spawnData)
        {
            return $"moveOffset={VectorString(spawnData.moveOffset)}, obstacleWidth={spawnData.obstacleWidth:F3}, obstacleHeight={spawnData.obstacleHeight:F3}";
        }

        internal static string VectorString(Vector3 value)
        {
            return $"({value.x:F3}, {value.y:F3}, {value.z:F3})";
        }
    }

    [HarmonyPatch(typeof(BeatmapDataLoaderVersion2_6_0AndEarlier.BeatmapDataLoader.ObstacleConverter), nameof(BeatmapDataLoaderVersion2_6_0AndEarlier.BeatmapDataLoader.ObstacleConverter.GetHeightForObstacleType))]
    internal static class ObstacleDebugGetHeightForObstacleTypePatch
    {
        private static void Postfix(ref int __result, BeatmapSaveDataVersion2_6_0AndEarlier.ObstacleType obstacleType)
        {
        }

        private static void Finalizer(BeatmapSaveDataVersion2_6_0AndEarlier.ObstacleType obstacleType, Exception __exception)
        {
            if (__exception != null)
            {
                ObstacleDebugPatches.Error($"GetHeightForObstacleType failed type={(int)obstacleType}", __exception);
            }
        }
    }

    [HarmonyPatch(typeof(BeatmapDataLoaderVersion2_6_0AndEarlier.BeatmapDataLoader.ObstacleConverter), nameof(BeatmapDataLoaderVersion2_6_0AndEarlier.BeatmapDataLoader.ObstacleConverter.GetLayerForObstacleType))]
    internal static class ObstacleDebugGetLayerForObstacleTypePatch
    {
        private static void Postfix(ref int __result, BeatmapSaveDataVersion2_6_0AndEarlier.ObstacleType obstacleType)
        {
        }

        private static void Finalizer(BeatmapSaveDataVersion2_6_0AndEarlier.ObstacleType obstacleType, Exception __exception)
        {
            if (__exception != null)
            {
                ObstacleDebugPatches.Error($"GetLayerForObstacleType failed type={(int)obstacleType}", __exception);
            }
        }
    }

    [HarmonyPatch(typeof(BeatmapObjectSpawnMovementData), nameof(BeatmapObjectSpawnMovementData.GetObstacleSpawnData))]
    internal static class ObstacleDebugGetObstacleSpawnDataPatch
    {
        private static void Postfix(ref ObstacleSpawnData __result, ObstacleData obstacleData)
        {
            if (!ObstacleDebugPatches.ShouldLog(obstacleData))
            {
                return;
            }

            ObstacleDebugPatches.Info($"GetObstacleSpawnData data=[{ObstacleDebugPatches.ObstacleDataString(obstacleData)}], spawn={ObstacleDebugPatches.SpawnDataString(__result)}");
        }

        private static void Finalizer(ObstacleData obstacleData, Exception __exception)
        {
            if (__exception != null)
            {
                ObstacleDebugPatches.Error($"GetObstacleSpawnData failed data=[{ObstacleDebugPatches.ObstacleDataString(obstacleData)}]", __exception);
            }
        }
    }

    [HarmonyPatch(typeof(ObstacleController), nameof(ObstacleController.Init))]
    internal static class ObstacleDebugObstacleControllerInitPatch
    {
        private static void Prefix(ObstacleController __instance, ObstacleData obstacleData, ObstacleSpawnData obstacleSpawnData)
        {
            if (!ObstacleDebugPatches.ShouldLog(obstacleData))
            {
                return;
            }

            ObstacleDebugPatches.Info($"ObstacleController.Init prefix data=[{ObstacleDebugPatches.ObstacleDataString(obstacleData)}], spawn=[{ObstacleDebugPatches.SpawnDataString(obstacleSpawnData)}], controllerPre length={__instance._length:F3}, width={__instance._width:F3}, height={__instance._height:F3}, active={Plugin.active}");
        }

        private static void Postfix(ObstacleController __instance, ObstacleData obstacleData, ObstacleSpawnData obstacleSpawnData)
        {
            if (!ObstacleDebugPatches.ShouldLog(obstacleData))
            {
                return;
            }

            var stretchableObstacle = __instance._stretchableObstacle;
            ObstacleDebugPatches.Info($"ObstacleController.Init postfix data=[{ObstacleDebugPatches.ObstacleDataString(obstacleData)}], spawn=[{ObstacleDebugPatches.SpawnDataString(obstacleSpawnData)}], controllerPost length={__instance._length:F3}, duration={__instance._obstacleDuration:F3}, width={__instance._width:F3}, height={__instance._height:F3}, stretchable={(stretchableObstacle == null ? "null" : stretchableObstacle.name)}");
        }

        private static void Finalizer(ObstacleData obstacleData, Exception __exception)
        {
            if (__exception != null)
            {
                ObstacleDebugPatches.Error($"ObstacleController.Init failed data=[{ObstacleDebugPatches.ObstacleDataString(obstacleData)}]", __exception);
            }
        }
    }

    [HarmonyPatch(typeof(StretchableObstacle), "CalculateObstacleTransformProperties")]
    internal static class ObstacleDebugCalculateObstacleTransformPropertiesPatch
    {
        private static void Postfix(
            float width,
            float height,
            float length,
            ref Vector3 localPosition,
            ref Vector3 size,
            ref Vector3 scale)
        {
            if (length < 0f || length <= StretchableObstacleNegativeLengthPatch.NegativeWallVisualLength)
            {
                ObstacleDebugPatches.Info($"CalculateObstacleTransformProperties width={width:F3}, height={height:F3}, length={length:F3}, localPosition={ObstacleDebugPatches.VectorString(localPosition)}, size={ObstacleDebugPatches.VectorString(size)}, scale={ObstacleDebugPatches.VectorString(scale)}");
            }
        }

        private static void Finalizer(float width, float height, float length, Exception __exception)
        {
            if (__exception != null)
            {
                ObstacleDebugPatches.Error($"CalculateObstacleTransformProperties failed width={width:F3}, height={height:F3}, length={length:F3}", __exception);
            }
        }
    }

    [HarmonyPatch(typeof(StretchableObstacle), nameof(StretchableObstacle.SetAllProperties))]
    internal static class ObstacleDebugSetAllPropertiesPatch
    {
        private static void Postfix(StretchableObstacle __instance, float width, float height, float length)
        {
            if (length < 0f || length <= StretchableObstacleNegativeLengthPatch.NegativeWallVisualLength)
            {
                ObstacleDebugPatches.Info($"SetAllProperties name={__instance.name}, width={width:F3}, height={height:F3}, length={length:F3}, frameLength={__instance._obstacleFrame.length:F3}, framePos={ObstacleDebugPatches.VectorString(__instance._obstacleFrame.localPosition)}, glow={(__instance._obstacleFakeGlow == null ? "null" : __instance._obstacleFakeGlow.length.ToString("F3"))}");
            }
        }

        private static void Finalizer(StretchableObstacle __instance, float width, float height, float length, Exception __exception)
        {
            if (__exception != null)
            {
                ObstacleDebugPatches.Error($"StretchableObstacle.SetAllProperties failed name={__instance.name}, width={width:F3}, height={height:F3}, length={length:F3}", __exception);
            }
        }
    }

    [HarmonyPatch(typeof(StretchableObstacle), nameof(StretchableObstacle.SetSizeAndOffset))]
    internal static class ObstacleDebugSetSizeAndOffsetPatch
    {
        private static void Postfix(StretchableObstacle __instance, float width, float height, float length, float offset)
        {
            if (length < 0f || length <= StretchableObstacleNegativeLengthPatch.NegativeWallVisualLength)
            {
                ObstacleDebugPatches.Info($"SetSizeAndOffset name={__instance.name}, width={width:F3}, height={height:F3}, length={length:F3}, offset={offset:F3}, frameLength={__instance._obstacleFrame.length:F3}, framePos={ObstacleDebugPatches.VectorString(__instance._obstacleFrame.localPosition)}, glow={(__instance._obstacleFakeGlow == null ? "null" : __instance._obstacleFakeGlow.length.ToString("F3"))}");
            }
        }

        private static void Finalizer(StretchableObstacle __instance, float width, float height, float length, float offset, Exception __exception)
        {
            if (__exception != null)
            {
                ObstacleDebugPatches.Error($"StretchableObstacle.SetSizeAndOffset failed name={__instance.name}, width={width:F3}, height={height:F3}, length={length:F3}, offset={offset:F3}", __exception);
            }
        }
    }
}
