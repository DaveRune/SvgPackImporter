using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using KnightForge.IconImporter.Editor.Data;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace KnightForge.IconImporter.Editor.Utilities
{
    public static class IconImportProcessor
    {
        public static void StartUpdate(IconPack pack, object coroutineOwner, Action onComplete = null)
        {
            var settings = IconImporterSettings.Instance;
            if (!settings.imageMagickDetected)
            {
                var installNow = EditorUtility.DisplayDialog(
                    "ImageMagick Not Found",
                    "ImageMagick is required to convert SVGs to PNGs. Install it, then restart Unity.",
                    "Open Website", "Cancel");

                if (installNow)
                    Process.Start("https://imagemagick.org/script/download.php");

                return;
            }

            var outputFolder = Path.Combine(Application.temporaryCachePath, $"{pack.name}_icons");

            // Only convert icons whose source SVG is still on disk.
            // Icons with missing sources are preserved through EmbedIconsAsSubassets unchanged.
            var toImport = pack.Icons
                .Where(icon => icon.provider)
                .Where(icon =>
                {
                    var svgPath = icon.provider.GetSvgPath(icon.iconName, icon.variant);
                    return !string.IsNullOrEmpty(svgPath) && File.Exists(svgPath);
                })
                .Select(icon => new ImportedIcon { iconName = icon.iconName, variant = icon.variant, provider = icon.provider })
                .ToList();

            EditorUtility.DisplayProgressBar("Updating Icons", "Starting conversion...", 0);

            var convertRoutine = ImageMagickConverter.ConvertSvgsToPngs(
                toImport,
                icon => icon.provider.GetSvgPath(icon.iconName, icon.variant),
                pack.IconSize,
                pack.StrokeWidth,
                pack.IconColor,
                outputFolder,
                progress => EditorUtility.DisplayProgressBar("Updating Icons", progress, 0.5f));

            EditorCoroutineUtility.StartCoroutine(RunPipeline(convertRoutine, pack, toImport, outputFolder, onComplete), coroutineOwner);
        }

        private static IEnumerator RunPipeline(IEnumerator convertRoutine, IconPack pack, List<ImportedIcon> importedIcons, string outputFolder, Action onComplete)
        {
            while (convertRoutine.MoveNext())
                yield return convertRoutine.Current;

            EditorUtility.ClearProgressBar();

            yield return EmbedIconsAsSubassets(pack, importedIcons, outputFolder);

            onComplete?.Invoke();
        }

        public static IEnumerator EmbedIconsAsSubassets(IconPack targetPack, List<ImportedIcon> selectedIcons, string outputFolder)
        {
            var assetPath = AssetDatabase.GetAssetPath(targetPack);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError($"IconPack '{targetPack.name}' has no asset path. Save the asset first.");
                yield break;
            }

            // Cache existing subassets by name so we can update them in-place and preserve Unity local
            // file IDs - this keeps references from UI Image components intact across re-imports.
            var existingTextures = new Dictionary<string, Texture2D>();
            var existingSprites = new Dictionary<string, Sprite>();

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                switch (asset)
                {
                    case Texture2D texture:
                        existingTextures[texture.name] = texture;
                        break;
                    case Sprite sprite:
                        existingSprites[sprite.name] = sprite;
                        break;
                }

            // Snapshot current pack entries before clearing, so we can re-add any icons
            // whose source SVG was missing and were excluded from selectedIcons.
            var originalPackedIcons = targetPack.Icons.ToList();

            var selectedAssetNames = new HashSet<string>(selectedIcons.Select(i =>
            {
                var pName = i.provider != null ? i.provider.name : "Unknown";
                return string.IsNullOrEmpty(i.variant)
                    ? $"{pName}-{i.iconName}"
                    : $"{pName}-{i.iconName}-{i.variant}";
            }));

            var keepAssets = new HashSet<Object> { targetPack };
            targetPack.Icons.Clear();

            var embedded = 0;

            foreach (var selected in selectedIcons)
            {
                var providerName = selected.provider != null ? selected.provider.name : "Unknown";
                var assetName = string.IsNullOrEmpty(selected.variant)
                    ? $"{providerName}-{selected.iconName}"
                    : $"{providerName}-{selected.iconName}-{selected.variant}";
                var pngPath = Path.Combine(outputFolder, $"{assetName}.png");

                if (!File.Exists(pngPath))
                {
                    Debug.LogWarning($"PNG not found for icon '{assetName}' at '{pngPath}'.");
                    continue;
                }

                var bytes = File.ReadAllBytes(pngPath);

                Texture2D texture;
                if (existingTextures.TryGetValue(assetName, out var existingTex))
                {
                    texture = existingTex;
                    texture.LoadImage(bytes);
                }
                else
                {
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    {
                        name = assetName,
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp
                    };

                    if (!texture.LoadImage(bytes))
                    {
                        Debug.LogError($"Failed to load PNG bytes for icon '{assetName}'.");
                        Object.DestroyImmediate(texture);
                        continue;
                    }

                    AssetDatabase.AddObjectToAsset(texture, targetPack);
                }

                texture.hideFlags = HideFlags.HideInHierarchy;
                keepAssets.Add(texture);

                existingSprites.TryGetValue(assetName, out var existingSprite);
                var rectMatches = existingSprite
                                  && (int)existingSprite.rect.width == texture.width
                                  && (int)existingSprite.rect.height == texture.height;

                Sprite sprite;
                if (rectMatches)
                {
                    sprite = existingSprite;
                }
                else
                {
                    var newRect = new Rect(0, 0, texture.width, texture.height);
                    var newSprite = Sprite.Create(texture, newRect, new Vector2(0.5f, 0.5f), 100f);
                    newSprite.name = assetName;

                    if (existingSprite)
                    {
                        // Copy the new sprite's data into the existing sprite object to update its
                        // rect while preserving its local file ID. This keeps Image component
                        // references valid when iconSize changes between updates.
                        EditorUtility.CopySerialized(newSprite, existingSprite);
                        Object.DestroyImmediate(newSprite);
                        sprite = existingSprite;
                    }
                    else
                    {
                        sprite = newSprite;
                        AssetDatabase.AddObjectToAsset(sprite, targetPack);
                    }
                }

                sprite.hideFlags = HideFlags.HideInHierarchy;
                keepAssets.Add(sprite);

                targetPack.Icons.Add(new IconPack.PackedIcon
                {
                    iconName = selected.iconName,
                    variant = selected.variant,
                    provider = selected.provider,
                    texture = texture,
                    sprite = sprite
                });

                embedded++;
            }

            // Re-add icons that were excluded from selectedIcons because their source SVG was
            // missing. Preserve their existing subassets so references remain intact.
            foreach (var original in originalPackedIcons)
            {
                if (!original.provider) continue;
                var provName = original.provider.name;
                var aName = string.IsNullOrEmpty(original.variant)
                    ? $"{provName}-{original.iconName}"
                    : $"{provName}-{original.iconName}-{original.variant}";
                if (selectedAssetNames.Contains(aName)) continue;

                if (original.texture) keepAssets.Add(original.texture);
                if (original.sprite) keepAssets.Add(original.sprite);
                targetPack.Icons.Add(original);
            }

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (keepAssets.Contains(asset)) continue;
                AssetDatabase.RemoveObjectFromAsset(asset);
                Object.DestroyImmediate(asset, true);
            }

            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);

            EditorUtility.SetDirty(targetPack);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorApplication.delayCall += SceneView.RepaintAll;
            EditorApplication.delayCall += EditorApplication.RepaintProjectWindow;
            EditorApplication.delayCall += EditorApplication.RepaintHierarchyWindow;

            Debug.Log($"Embedded {embedded}/{selectedIcons.Count} icons as subassets in '{targetPack.name}'.");
        }
    }
}
