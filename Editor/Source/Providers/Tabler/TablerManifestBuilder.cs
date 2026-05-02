using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KnightForge.IconImporter.Editor.Providers.Tabler
{
    public static class TablerManifestBuilder
    {
        public static IconManifest BuildManifest(string tablerPath, string version)
        {
            var manifest = new IconManifest
            {
                providerName = "Tabler",
                version = version,
                cachedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                icons = new List<IconEntry>()
            };

            var aliasMap = LoadAliases(Path.Combine(tablerPath, "aliases.json"));

            ScanVariant(tablerPath, "outline", manifest, aliasMap);
            ScanVariant(tablerPath, "filled", manifest, aliasMap);

            SaveManifest(tablerPath, manifest);
            return manifest;
        }

        private static Dictionary<string, List<string>> LoadAliases(string aliasesPath)
        {
            var aliasMap = new Dictionary<string, List<string>>();

            if (!File.Exists(aliasesPath))
            {
                Debug.LogWarning($"aliases.json not found at '{aliasesPath}'.");
                return aliasMap;
            }

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
                Debug.LogWarning($"Error loading aliases: {ex.Message}");
            }

            return aliasMap;
        }

        private static void ScanVariant(string tablerPath, string variant, IconManifest manifest, Dictionary<string, List<string>> aliasMap)
        {
            var variantPath = Path.Combine(tablerPath, variant);

            if (!Directory.Exists(variantPath))
            {
                Debug.LogWarning($"Variant folder not found: '{variantPath}'.");
                return;
            }

            foreach (var svgFile in Directory.GetFiles(variantPath, "*.svg"))
            {
                var fileName = Path.GetFileNameWithoutExtension(svgFile);
                manifest.icons.Add(new IconEntry
                {
                    name = fileName,
                    variant = variant,
                    aliases = aliasMap.ContainsKey(fileName) ? new List<string>(aliasMap[fileName]) : new List<string>()
                });
            }
        }

        private static void SaveManifest(string tablerPath, IconManifest manifest)
        {
            var manifestPath = Path.Combine(tablerPath, "manifest.json");

            try
            {
                File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
                Debug.Log($"Manifest saved to '{manifestPath}' ({manifest.icons.Count} icons).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving manifest: {ex.Message}");
            }
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