using System;
using System.Collections.Generic;

namespace KnightForge.SvgPackImporter
{
    [Serializable]
    public class IconEntry
    {
        public string name;
        public string variant;
        public List<string> aliases = new();
    }
}