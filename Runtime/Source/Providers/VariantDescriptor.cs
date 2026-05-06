using UnityEngine;

namespace KnightForge.IconImporter.Providers
{
    public enum IconStyle
    {
        Fill,
        Stroke
    }

    public struct VariantDescriptor
    {
        public string Path;
        public IconStyle Style;
        public Vector2Int? ViewBoxSize;

        public VariantDescriptor(string path, IconStyle style, Vector2Int? viewBoxSize = null)
        {
            Path = path;
            Style = style;
            ViewBoxSize = viewBoxSize;
        }
    }
}