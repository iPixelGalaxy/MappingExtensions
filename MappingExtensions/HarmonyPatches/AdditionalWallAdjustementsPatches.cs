using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SongCore.Utilities;
using UnityEngine;

namespace MappingExtensions.HarmonyPatches
{
    internal static class LegacyObstacleTypeEncoding
    {
        private const int PrecisionUnit = 1000;
        private const int PrecisionHeightMarker = 1000;
        private const int StartHeightMarker = 4001;
        private const int MaxStartHeight = 999;
        private const int MaxWallHeight = 4000;
        internal const int MaxEncodedType = StartHeightMarker + MaxWallHeight * PrecisionUnit + MaxStartHeight;

        private const float WallHeightToGameHeightMultiplier = 5f;
        private const int EncodedLayerGroundOffset = 1000;
        private const float StartHeightToLayerDivisor = 750f;

        internal enum Mode
        {
            PrecisionHeight,
            PrecisionHeightWithStart
        }

        internal readonly struct DecodedType
        {
            internal readonly Mode mode;
            internal readonly int wallHeight;
            internal readonly int startHeight;

            internal DecodedType(Mode mode, int wallHeight, int startHeight)
            {
                this.mode = mode;
                this.wallHeight = wallHeight;
                this.startHeight = startHeight;
            }
        }

        internal static bool TryDecode(int encodedType, out DecodedType decodedType)
        {
            decodedType = default;
            if (encodedType is < PrecisionHeightMarker or > MaxEncodedType)
            {
                return false;
            }

            if (encodedType >= StartHeightMarker)
            {
                var encodedHeightAndStart = encodedType - StartHeightMarker;
                decodedType = new DecodedType(
                    Mode.PrecisionHeightWithStart,
                    encodedHeightAndStart / PrecisionUnit,
                    encodedHeightAndStart % PrecisionUnit);
                return true;
            }

            decodedType = new DecodedType(Mode.PrecisionHeight, encodedType - PrecisionHeightMarker, 0);
            return true;
        }

        internal static int EncodeHeight(DecodedType decodedType)
        {
            return (int)(decodedType.wallHeight / (float)PrecisionUnit * WallHeightToGameHeightMultiplier * PrecisionUnit + PrecisionHeightMarker);
        }

        internal static int EncodeLayer(DecodedType decodedType)
        {
            if (decodedType.mode != Mode.PrecisionHeightWithStart)
            {
                return 0;
            }

            // Legacy ME v2 expands authored start height into the precision layer space used by wall art maps.
            // An offset of 1000 keeps converted walls aligned to Beat Saber's 0.6m wall grid.
            return (int)(decodedType.startHeight / StartHeightToLayerDivisor * WallHeightToGameHeightMultiplier * PrecisionUnit + EncodedLayerGroundOffset);
        }
    }

    [HarmonyPatch(typeof(BeatmapDataLoaderVersion2_6_0AndEarlier.BeatmapDataLoader.ObstacleConverter), nameof(BeatmapDataLoaderVersion2_6_0AndEarlier.BeatmapDataLoader.ObstacleConverter.GetHeightForObstacleType))]
    internal static class BeatmapDataLoaderObstacleConverterGetHeightForObstacleTypePatch
    {
        private static void Postfix(ref int __result, BeatmapSaveDataVersion2_6_0AndEarlier.ObstacleType obstacleType)
        {
            if (!LegacyObstacleTypeEncoding.TryDecode((int)obstacleType, out var decodedType))
            {
                return;
            }

            __result = LegacyObstacleTypeEncoding.EncodeHeight(decodedType);
        }
    }

    [HarmonyPatch(typeof(BeatmapDataLoaderVersion2_6_0AndEarlier.BeatmapDataLoader.ObstacleConverter), nameof(BeatmapDataLoaderVersion2_6_0AndEarlier.BeatmapDataLoader.ObstacleConverter.GetLayerForObstacleType))]
    internal static class BeatmapDataLoaderObstacleConverterGetLayerForObstacleTypePatch
    {
        private static void Postfix(ref int __result, BeatmapSaveDataVersion2_6_0AndEarlier.ObstacleType obstacleType)
        {
            if (!LegacyObstacleTypeEncoding.TryDecode((int)obstacleType, out var decodedType))
            {
                return;
            }

            __result = LegacyObstacleTypeEncoding.EncodeLayer(decodedType);
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
            if (!ShouldNormalizeVisualLength(obstacleData, __instance._length))
            {
                return;
            }

            var positiveLength = StretchableObstacleNegativeLengthPatch.GetNormalizedVisualLength(__instance._length);
            __instance._length = positiveLength;
            __instance._stretchableObstacle.SetSizeAndOffset(__instance._width, __instance._height, positiveLength, __instance.manualUvOffset);
            if (obstacleData.duration < 0f)
            {
                __instance._obstacleDuration = -obstacleData.duration;
            }
        }

        private static bool ShouldNormalizeVisualLength(ObstacleData obstacleData, float length)
        {
            if (length < 0f)
            {
                return true;
            }

            return obstacleData.duration <= 0.25f
                   && (MappingExtensionsData.IsPrecisionValue(obstacleData.lineIndex)
                       || MappingExtensionsData.IsPrecisionValue(obstacleData.width)
                       || MappingExtensionsData.IsPrecisionValue((int)obstacleData.lineLayer)
                       || MappingExtensionsData.IsPrecisionValue(obstacleData.height));
        }
    }

    [HarmonyPatch(typeof(StretchableObstacle), "CalculateObstacleTransformProperties")]
    internal static class StretchableObstacleNegativeLengthPatch
    {
        internal const float NegativeWallVisualLength = 0.05f;

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

            var positiveLength = GetNormalizedVisualLength(length);
            size = new Vector3(width, height, positiveLength);
            localPosition = new Vector3(0f, height * 0.5f, positiveLength * -0.5f);
            scale = size - __instance._coreOffset;
        }

        internal static float GetNormalizedVisualLength(float length)
        {
            return length < 0f || length > NegativeWallVisualLength ? NegativeWallVisualLength : length;
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

            var positiveLength = StretchableObstacleNegativeLengthPatch.GetNormalizedVisualLength(length);
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
