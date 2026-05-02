using System;
using System.Collections.Generic;

namespace KnightForge.IconImporter
{
    [Serializable]
    public class IconManifest
    {
        public string providerName;
        public string version;
        public long cachedTimestamp;
        public List<IconEntry> icons = new();
    }
}