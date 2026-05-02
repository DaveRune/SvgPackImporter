using System;
using System.Collections.Generic;

namespace KnightForge.IconImporter
{
    [Serializable]
    public class IconEntry
    {
        public string name;
        public string variant;
        public List<string> aliases = new();
    }
}