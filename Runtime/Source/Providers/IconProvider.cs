using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace KnightForge.SvgPackImporter.Providers
{
    [Icon("Packages/com.KnightForge.SvgPackImporter/Editor/Icons/IconProvider.png")]
    public abstract class IconProvider : ScriptableObject
    {
        private const string ProvidersRoot = "IconProviders";
        internal const float SvgBaseDpi = 96f;

        [HideInInspector] [SerializeField] private string _stableId;

        [SerializeField] protected string svgRootFolder = "My Local Icons";
        [SerializeField] private List<string> _variants = new();

        public string StableId => _stableId;
        public virtual IReadOnlyList<string> Variants => _variants;
        public virtual bool SupportsStroke => false;
        protected static Vector2Int DefaultViewBoxSize => new(24, 24);

        protected virtual void Reset()
        {
            if (string.IsNullOrEmpty(_stableId))
                _stableId = GenerateStableId();
        }

        protected virtual string GenerateStableId()
        {
            return Guid.NewGuid().ToString("N")[..8];
        }

        public string GetSvgPath(string iconName, string variant)
        {
            return string.IsNullOrEmpty(variant)
                ? Path.Combine(GetRootPath(), $"{iconName}.svg")
                : Path.Combine(GetRootPath(), variant, $"{iconName}.svg");
        }

        public bool HasSourceFor(string iconName, string variant)
        {
            var path = GetSvgPath(iconName, variant);
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        public string GetRootPath()
        {
            return Path.Combine(GetProvidersRoot(), svgRootFolder);
        }

        public static string GetProvidersRoot()
        {
            var projectPath = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectPath, ProvidersRoot);
        }

        public int GetDensity(int targetSize, string variant)
        {
            var vb = GetViewBoxSizeForVariant(variant);
            return Mathf.RoundToInt(targetSize * SvgBaseDpi / vb.x);
        }

        public virtual string PreprocessSvg(string content, string variant, string colorHex, float strokeWidth)
        {
            content = EnsureExplicitDimensions(content, GetViewBoxSizeForVariant(variant));
            return content.Replace("currentColor", colorHex);
        }

        public IconManifest LoadManifest()
        {
            var manifestPath = Path.Combine(GetRootPath(), "manifest.json");
            return !File.Exists(manifestPath)
                ? null
                : JsonUtility.FromJson<IconManifest>(File.ReadAllText(manifestPath));
        }

        public IconManifest BuildManifest(string versionOverride = null)
        {
            var root = GetRootPath();

            var manifest = new IconManifest
            {
                providerName = name,
                version = versionOverride ?? GetManifestVersion(),
                cachedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                icons = new List<IconEntry>()
            };

            var aliasMap = LoadAliases(root);
            var variants = Variants;

            if (variants == null || variants.Count == 0)
                ScanVariant(root, string.Empty, manifest, aliasMap);
            else
                foreach (var variant in variants)
                    ScanVariant(root, variant, manifest, aliasMap);

            SaveManifest(root, manifest);
            return manifest;
        }

        protected virtual string GetManifestVersion()
        {
            return "local";
        }

        protected virtual Vector2Int GetViewBoxSizeForVariant(string variant)
        {
            return DefaultViewBoxSize;
        }

        protected virtual Dictionary<string, List<string>> LoadAliases(string root)
        {
            return new Dictionary<string, List<string>>();
        }

        // Injects explicit width/height on the root <svg> if absent. ImageMagick requires these
        // to correctly interpret the -density flag; without them it can render at the wrong scale.
        private static string EnsureExplicitDimensions(string content, Vector2Int vb)
        {
            return Regex.Replace(content, @"(<svg\b)([^>]*>)", m =>
            {
                var attrs = m.Groups[2].Value;
                var hasWidth = Regex.IsMatch(attrs, @"\bwidth=""[\d.]+""");
                var hasHeight = Regex.IsMatch(attrs, @"\bheight=""[\d.]+""");
                if (hasWidth && hasHeight) return m.Value;
                var inject = "";
                if (!hasWidth) inject += $" width=\"{vb.x}\"";
                if (!hasHeight) inject += $" height=\"{vb.y}\"";
                return m.Groups[1].Value + inject + m.Groups[2].Value;
            });
        }

        private static void ScanVariant(string root, string variant, IconManifest manifest, Dictionary<string, List<string>> aliasMap)
        {
            var variantPath = string.IsNullOrEmpty(variant) ? root : Path.Combine(root, variant);

            if (!Directory.Exists(variantPath))
            {
                if (!string.IsNullOrEmpty(variant))
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
                    aliases = aliasMap.TryGetValue(fileName, out var aliases)
                        ? new List<string>(aliases)
                        : new List<string>()
                });
            }
        }

        private static void SaveManifest(string root, IconManifest manifest)
        {
            try
            {
                Directory.CreateDirectory(root);
                var manifestPath = Path.Combine(root, "manifest.json");
                File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
                Debug.Log($"Manifest saved to '{manifestPath}' ({manifest.icons.Count} icons).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save manifest: {ex.Message}");
            }
        }
    }
}