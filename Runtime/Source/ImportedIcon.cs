using System;
using KnightForge.SvgPackImporter.Providers;

namespace KnightForge.SvgPackImporter
{
    [Serializable]
    public sealed class ImportedIcon
    {
        public string iconName;
        public string variant;
        public IconProvider provider;
    }
}