using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KnightForge.IconImporter.Providers.BuiltIn
{
    [CreateAssetMenu(menuName = IconImporterConstants.IconProviders + "Tabler", order = IconImporterConstants.IconProvidersBuiltIn)]
    public sealed class TablerIconProvider : RepoIconProvider
    {
        public override IReadOnlyDictionary<string, string> VariantPaths => new Dictionary<string, string>
        {
            { "outline", "icons/outline/" },
            { "filled", "icons/filled/" }
        };

        public override string AliasesZipPath => "aliases.json";

        protected override void Reset()
        {
            SetDefaults();
        }

        public void SetDefaults()
        {
            base.Reset();
            _svgRootFolder = "Tabler";
            _repoUrl = "https://github.com/tabler/tabler-icons";
        }

        protected override Dictionary<string, List<string>> LoadAliases(string root)
        {
            var aliasMap = new Dictionary<string, List<string>>();
            var aliasesPath = Path.Combine(root, "aliases.json");

            if (!File.Exists(aliasesPath))
                return aliasMap;

            try
            {
                var json = File.ReadAllText(aliasesPath);
                var aliasData = JsonUtility.FromJson<AliasData>("{\"data\":" + json + "}");

                if (aliasData?.data != null)
                    foreach (var entry in aliasData.data)
                    {
                        if (!aliasMap.ContainsKey(entry.name))
                            aliasMap[entry.name] = new List<string>();
                        if (entry.aliases != null)
                            aliasMap[entry.name].AddRange(entry.aliases);
                    }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error loading Tabler aliases: {ex.Message}");
            }

            return aliasMap;
        }

        [Serializable]
        private class AliasData
        {
            public AliasEntry[] data;
        }

        [Serializable]
        private class AliasEntry
        {
            public string name;
            public string[] aliases;
        }
    }
}