using System;
using KnightForge.IconImporter.Providers;

namespace KnightForge.IconImporter
{
    [Serializable]
    public sealed class ImportedIcon
    {
        public string iconName;
        public string variant;
        public IconProvider provider;
    }
}