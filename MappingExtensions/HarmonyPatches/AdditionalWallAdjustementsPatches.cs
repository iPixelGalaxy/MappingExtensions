using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SongCore.Utilities;
using UnityEngine;

namespace MappingExtensions.HarmonyPatches
{
    [HarmonyPatch(typeof(BeatmapDataLoaderVersion2_6_0AndEarlier.BeatmapDataLoader.ObstacleConverter), nameof(BeatmapDataLoaderVersion2_6_0AndEarlier.BeatmapDataLoader.ObstacleConverter.GetHeightForObstacleType))]
    internal static class BeatmapDataLoaderObstacleConverterGetHeightForObstacleTypePatch
    {
        private enum Mode
        {
            PreciseHeight,
            PreciseHeightStart
        }

        private static void Postfix(ref int __result, BeatmapSaveDataVersion2_6_0AndEarlier.ObstacleType obstacleType)
        {
            var type = (int)obstacleType;

            if (type is < 1000 or > 4005000)
            {
                return;
            }

            int obsHeight;

            var mode = type is >= 4001 and <= 4005000 ? Mode.PreciseHeightStart : Mode.PreciseHeight;
            if (mode == Mode.PreciseHeightStart)
            {
                type -= 4001;
                obsHeight = type / 1000;
            }
            else
            {
                obsHeight = type - 1000;
            }

            __result = (int)(obsHeight / 1000f * 5 * 1000 + 1000);
        }
    }

    [HarmonyPatch(typeof(BeatmapDataLoaderVersion2_6_0AndEarlier.BeatmapDataLoader.ObstacleConverter), nameof(BeatmapDataLoaderVersion2_6_0AndEarlier.BeatmapDataLoader.ObstacleConverter.GetLayerForObstacleType))]
    internal static class BeatmapDataLoaderObstacleConverterGetLayerForObstacleTypePatch
    {
        private enum Mode
        {
            PreciseHeight,
            PreciseHeightStart
        }

        private static void Postfix(ref int __result, BeatmapSaveDataVersion2_6_0AndEarlier.ObstacleType obstacleType)
        {
            var type = (int)obstacleType;

            if (type is < 1000 or > 4005000)
            {
                return;
            }

            var startHeight = 0;

            var mode = type is >= 4001 and <= 4005000 ? Mode.PreciseHeightStart : Mode.PreciseHeight;
            if (mode == Mode.PreciseHeightStart)
            {
                type -= 4001;
                startHeight = type % 1000;
            }

            // Math that is accurate in shape/proportions but has walls being too high.
            __result = (int)(startHeight / 750f * 5 * 1000 + 1334);
        }
    }

    [HarmonyPatch(typeof(BeatmapObjectSpawnMovementData), nameof(BeatmapObjectSpawnMovementData.GetObstacleSpawnData))]
    internal static class BeatmapObjectSpawnMovementDataGetObstacleSpawnDataPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Replaces the obstacle width.
            return new CodeMatcher(instructions)
                .MatchEndForward(
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt && ((MethodBase)i.operand).Name == $"get_{nameof(ObstacleController.width)}"),
                    new CodeMatch(OpCodes.Conv_R4),
                    new CodeMatch())
                .ThrowIfInvalid("Could not find obstacle width access in BeatmapObjectSpawnMovementData.GetObstacleSpawnData")
                .Insert(Transpilers.EmitDelegate<Func<float, float>>(obstacleWidth =>
                {
                    if (!MappingExtensionsData.TryDecodePrecisionWidth(obstacleWidth, out var width))
                    {
                        return obstacleWidth;
                    }

                    return width;
                }))
                .InstructionEnumeration();
        }
    }

    [HarmonyPatch(typeof(ObstacleController), nameof(ObstacleController.Init))]
    internal static class ObstacleControllerInitPatch
    {
        private static void Prefix(ObstacleData obstacleData, ref ObstacleSpawnData obstacleSpawnData)
        {
            if (!MappingExtensionsData.TryDecodePrecisionHeight(obstacleData.height, out var obstacleHeight))
            {
                if (!Plugin.active || obstacleData.height <= 2)
                {
                    return;
                }

                obstacleHeight = obstacleData.height;
            }

            obstacleHeight *= StaticBeatmapObjectSpawnMovementData.kNoteLinesDistance;

            obstacleSpawnData = new ObstacleSpawnData(obstacleSpawnData.moveOffset, obstacleSpawnData.obstacleWidth, obstacleHeight);
        }

        private static void Postfix(ObstacleController __instance, ObstacleData obstacleData)
        {
            if (obstacleData.duration >= 0f || __instance._length >= 0f)
            {
                return;
            }

            var lengthPerBeat = __instance._length / obstacleData.duration;
            if (lengthPerBeat <= Mathf.Epsilon)
            {
                return;
            }

            var positiveLength = StretchableObstacleNegativeLengthPatch.GetNegativeWallVisualLength(__instance._length);
            __instance._length = positiveLength;
            __instance._obstacleDuration = positiveLength / lengthPerBeat;
        }
    }

    [HarmonyPatch(typeof(StretchableObstacle), "CalculateObstacleTransformProperties")]
    internal static class StretchableObstacleNegativeLengthPatch
    {
        internal const float NegativeWallMinimumVisualLength = 35f;

        private static void Postfix(
            StretchableObstacle __instance,
            float width,
            float height,
            float length,
            ref Vector3 localPosition,
            ref Vector3 size,
            ref Vector3 scale)
        {
            if (length >= 0f)
            {
                return;
            }

            var positiveLength = GetNegativeWallVisualLength(length);
            size = new Vector3(width, height, positiveLength);
            localPosition = new Vector3(0f, height * 0.5f, positiveLength * -0.5f);
            scale = size - __instance._coreOffset;
        }

        internal static float GetNegativeWallVisualLength(float length)
        {
            return Mathf.Max(-length, NegativeWallMinimumVisualLength);
        }
    }

    [HarmonyPatch(typeof(StretchableObstacle))]
    internal static class StretchableObstacleNegativeLengthRendererPatch
    {
        [HarmonyPatch(nameof(StretchableObstacle.SetAllProperties))]
        [HarmonyPostfix]
        private static void SetAllPropertiesPostfix(StretchableObstacle __instance, float height, float length)
        {
            NormalizeRendererLength(__instance, height, length);
        }

        [HarmonyPatch(nameof(StretchableObstacle.SetSizeAndOffset))]
        [HarmonyPostfix]
        private static void SetSizeAndOffsetPostfix(StretchableObstacle __instance, float height, float length)
        {
            NormalizeRendererLength(__instance, height, length);
        }

        private static void NormalizeRendererLength(StretchableObstacle stretchableObstacle, float height, float length)
        {
            if (length >= 0f)
            {
                return;
            }

            var positiveLength = StretchableObstacleNegativeLengthPatch.GetNegativeWallVisualLength(length);
            var localPosition = new Vector3(0f, height * 0.5f, positiveLength * -0.5f);

            stretchableObstacle._obstacleFrame.length = positiveLength;
            stretchableObstacle._obstacleFrame.localPosition = localPosition;
            stretchableObstacle._obstacleFrame.Refresh();

            if (stretchableObstacle._obstacleFakeGlow != null)
            {
                stretchableObstacle._obstacleFakeGlow.length = positiveLength + stretchableObstacle._fakeGlowOffset.z;
                stretchableObstacle._obstacleFakeGlow.localPosition = localPosition;
                stretchableObstacle._obstacleFakeGlow.Refresh();
            }
        }
    }
}
