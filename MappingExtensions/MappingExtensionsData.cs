namespace MappingExtensions
{
    internal static class MappingExtensionsData
    {
        internal static bool IsPrecisionValue(int value)
        {
            return value is >= 1000 or <= -1000;
        }

        internal static bool IsPrecisionValue(float value)
        {
            return value is >= 1000f or <= -1000f;
        }

        internal static int NormalizePrecisionLineIndex(int lineIndex)
        {
            return lineIndex <= -1000 ? lineIndex + 2000 : lineIndex;
        }

        internal static bool TryDecodePrecisionWidth(float encodedWidth, out float width)
        {
            if (!IsPrecisionValue(encodedWidth))
            {
                width = encodedWidth;
                return false;
            }

            if (encodedWidth <= -1000f)
            {
                encodedWidth += 2000f;
            }

            width = (encodedWidth - 1000f) / 1000f;
            return true;
        }

        internal static bool TryDecodePrecisionHeight(int encodedHeight, out float height)
        {
            switch (encodedHeight)
            {
                case <= -1000:
                    height = (encodedHeight + 2000f) / 1000f;
                    return true;
                case >= 1000:
                    height = (encodedHeight - 1000f) / 1000f;
                    return true;
                default:
                    height = encodedHeight;
                    return false;
            }
        }
    }
}
