using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace KnightForge.IconImporter.Editor
{
    public static class ManifestBuilder
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
                Debug.LogWarning($"aliases.json not found at {aliasesPath}");
                return aliasMap;
            }

            try
            {
                string json = File.ReadAllText(aliasesPath);
                var aliasData = JsonUtility.FromJson<AliasData>("{\"data\":" + json + "}");

                if (aliasData?.data != null)
                {
                    foreach (var entry in aliasData.data)
                    {
                        if (!aliasMap.ContainsKey(entry.name))
                        {
                            aliasMap[entry.name] = new List<string>();
                        }
                        if (entry.aliases != null)
                        {
                            aliasMap[entry.name].AddRange(entry.aliases);
                        }
                    }
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
            string variantPath = Path.Combine(tablerPath, variant);

            if (!Directory.Exists(variantPath))
            {
                Debug.LogWarning($"Variant folder not found: {variantPath}");
                return;
            }

            var svgFiles = Directory.GetFiles(variantPath, "*.svg");

            foreach (var svgFile in svgFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(svgFile);
                var entry = new IconEntry
                {
                    name = fileName,
                    variant = variant,
                    aliases = aliasMap.ContainsKey(fileName) ? new List<string>(aliasMap[fileName]) : new List<string>()
                };

                manifest.icons.Add(entry);
            }
        }

        private static void SaveManifest(string tablerPath, IconManifest manifest)
        {
            string manifestPath = Path.Combine(tablerPath, "manifest.json");

            try
            {
                string json = JsonUtility.ToJson(manifest, true);
                File.WriteAllText(manifestPath, json);
                Debug.Log($"Manifest saved to {manifestPath} ({manifest.icons.Count} icons)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving manifest: {ex.Message}");
            }
        }

        [System.Serializable]
        private class AliasData
        {
            public AliasEntry[] data;
        }

        [System.Serializable]
        private class AliasEntry
        {
            public string name;
            public string[] aliases;
        }
    }
}
