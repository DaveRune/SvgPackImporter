using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KnightForge.SvgPackImporter.Providers.BuiltIn
{
    [Icon("Packages/com.knightforge.svgpackimporter/Editor/Icons/IconProvider.png")]
    [CreateAssetMenu(fileName = "Tabler Icons", menuName = IconImporterConstants.IconProviders + "Tabler", order = IconImporterConstants.IconProvidersBuiltIn)]
    public sealed class TablerIconProvider : RepoIconProvider
    {
        public override string DefaultRepoUrl => "https://github.com/tabler/tabler-icons";
        protected override string DefaultSvgRootFolder => "Tabler";
        public override string AliasesZipPath => "aliases.json";

        protected override string GenerateStableId() => "tabler";

        private static readonly Dictionary<string, VariantDescriptor> Paths = new()
        {
            { "outline", new VariantDescriptor("icons/outline/", IconStyle.Stroke) },
            { "filled", new VariantDescriptor("icons/filled/", IconStyle.Fill) }
        };

        public override IReadOnlyDictionary<string, VariantDescriptor> VariantPaths => Paths;

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