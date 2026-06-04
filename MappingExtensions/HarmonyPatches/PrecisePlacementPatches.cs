using HarmonyLib;
using UnityEngine;

namespace MappingExtensions.HarmonyPatches
{
    [HarmonyPatch(typeof(BeatmapObjectSpawnMovementData), nameof(BeatmapObjectSpawnMovementData.GetNoteOffset))]
    internal static class BeatmapObjectSpawnMovementDataGetNoteOffsetPatch
    {
        private static void Postfix(BeatmapObjectSpawnMovementData __instance, ref Vector3 __result, int noteLineIndex, NoteLineLayer noteLineLayer)
        {
            if (MappingExtensionsData.IsPrecisionValue(noteLineIndex))
            {
                noteLineIndex = MappingExtensionsData.NormalizePrecisionLineIndex(noteLineIndex);

                // TODO: Find a better name for this variable.
                var num = -(__instance._noteLinesCount - 1f) * 0.5f;
                num += noteLineIndex * StaticBeatmapObjectSpawnMovementData.kNoteLinesDistance / 1000;
                __result = __instance._rightVec * num + new Vector3(0f, StaticBeatmapObjectSpawnMovementData.LineYPosForLineLayer(noteLineLayer), 0f);
            }
        }
    }

    [HarmonyPatch(typeof(BeatmapObjectSpawnMovementData), nameof(BeatmapObjectSpawnMovementData.GetObstacleOffset))]
    internal static class BeatmapObjectSpawnMovementDataGetObstacleOffsetPatch
    {
        private static void Postfix(BeatmapObjectSpawnMovementData __instance, ref Vector3 __result, int noteLineIndex, NoteLineLayer noteLineLayer)
        {
            var hasPrecisionLineIndex = MappingExtensionsData.IsPrecisionValue(noteLineIndex);
            var hasPrecisionLineLayer = MappingExtensionsData.IsPrecisionValue((int)noteLineLayer);

            if (hasPrecisionLineIndex)
            {
                noteLineIndex = MappingExtensionsData.NormalizePrecisionLineIndex(noteLineIndex);

                // TODO: Find a better name for this variable.
                var num = -(__instance._noteLinesCount - 1f) * 0.5f;
                num += noteLineIndex * StaticBeatmapObjectSpawnMovementData.kNoteLinesDistance / 1000;
                var y = StaticBeatmapObjectSpawnMovementData.LineYPosForLineLayer(noteLineLayer) + StaticBeatmapObjectSpawnMovementData.kObstacleVerticalOffset;
                if (hasPrecisionLineLayer)
                {
                    y -= __instance._jumpOffsetYProvider.jumpOffsetY;
                }

                __result = __instance._rightVec * num + new Vector3(0f, y, 0f);
            }
            else if (hasPrecisionLineLayer)
            {
                var y = StaticBeatmapObjectSpawnMovementData.LineYPosForLineLayer(noteLineLayer) + StaticBeatmapObjectSpawnMovementData.kObstacleVerticalOffset - __instance._jumpOffsetYProvider.jumpOffsetY;
                __result = new Vector3(__result.x, y, __result.z);
            }
        }
    }

    [HarmonyPatch(typeof(BeatmapObjectSpawnMovementData), nameof(BeatmapObjectSpawnMovementData.HighestJumpPosYForLineLayer))]
    internal static class BeatmapObjectSpawnMovementDataHighestJumpPosYForLineLayerPatch
    {
        private static void Postfix(BeatmapObjectSpawnMovementData __instance, ref float __result, NoteLineLayer lineLayer)
        {
            var delta = __instance._topLinesHighestJumpPosY - __instance._upperLinesHighestJumpPosY;
            var layer = (int)lineLayer;
            if (MappingExtensionsData.IsPrecisionValue(layer))
            {
                __result = __instance._upperLinesHighestJumpPosY - delta - delta + __instance._jumpOffsetYProvider.jumpOffsetY + layer * delta / 1000;
            }
            else if (Plugin.active && (layer is > 2 or < 0))
            {
                __result = __instance._upperLinesHighestJumpPosY - delta + __instance._jumpOffsetYProvider.jumpOffsetY + layer * delta;
            }
        }
    }

    [HarmonyPatch(typeof(StaticBeatmapObjectSpawnMovementData), nameof(StaticBeatmapObjectSpawnMovementData.LineYPosForLineLayer))]
    internal static class StaticBeatmapObjectSpawnMovementDataLineYPosForLineLayerPatch
    {
        private static void Postfix(ref float __result, NoteLineLayer lineLayer)
        {
            const float delta = StaticBeatmapObjectSpawnMovementData.kNoteLinesDistance;
            const float upperLinesYPos = StaticBeatmapObjectSpawnMovementData.kBaseLinesYPos + StaticBeatmapObjectSpawnMovementData.kNoteLinesDistance;
            var layer = (int)lineLayer;
            if (MappingExtensionsData.IsPrecisionValue(layer))
            {
                __result = upperLinesYPos - delta - delta + layer * delta / 1000;
            }
            else if (Plugin.active && (layer is > 2 or < 0))
            {
                __result = upperLinesYPos - delta + layer * delta;
            }
        }
    }

    [HarmonyPatch(typeof(StaticBeatmapObjectSpawnMovementData), nameof(StaticBeatmapObjectSpawnMovementData.Get2DNoteOffset))]
    internal static class StaticBeatmapObjectSpawnMovementDataGet2DNoteOffsetPatch
    {
        private static void Postfix(ref Vector2 __result, int noteLineIndex, int noteLinesCount, NoteLineLayer noteLineLayer)
        {
            if (MappingExtensionsData.IsPrecisionValue(noteLineIndex))
            {
                noteLineIndex = MappingExtensionsData.NormalizePrecisionLineIndex(noteLineIndex);

                // TODO: Find a better name for this variable.
                var num = -(noteLinesCount - 1f) * 0.5f;
                var x = num + noteLineIndex * StaticBeatmapObjectSpawnMovementData.kNoteLinesDistance / 1000;
                var y = StaticBeatmapObjectSpawnMovementData.LineYPosForLineLayer(noteLineLayer);
                __result = new Vector2(x, y);
            }
        }
    }
}
