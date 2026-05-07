using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace KnightForge.SvgPackImporter.Editor.Utilities
{
    internal static class IconImportProcessor
    {
        public static void StartUpdate(IconPack pack, object coroutineOwner, Action onComplete = null)
        {
            if (!ImageMagickConverter.TryDetectImageMagick(out _))
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
                .Where(icon => icon.provider && icon.provider.HasSourceFor(icon.iconName, icon.variant))
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

            // Snapshot current pack entries before clearing, so we can re-add any icons
            // whose source SVG was missing and were excluded from selectedIcons.
            var originalPackedIcons = targetPack.Icons.ToList();

            // Keyed by StableKey so updates preserve Unity local file IDs across asset renames.
            var existingTextures = new Dictionary<string, Texture2D>();
            var existingSprites = new Dictionary<string, Sprite>();

            foreach (var packed in originalPackedIcons)
            {
                if (!packed.provider) continue;
                var key = IconNaming.StableKey(packed.provider, packed.iconName, packed.variant);
                if (packed.texture) existingTextures[key] = packed.texture;
                if (packed.sprite) existingSprites[key] = packed.sprite;
            }

            var selectedStableKeys = new HashSet<string>(
                selectedIcons.Select(i => IconNaming.StableKey(i.provider, i.iconName, i.variant)));

            var keepAssets = new HashSet<Object> { targetPack };
            targetPack.ClearIcons();

            var embedded = 0;

            foreach (var selected in selectedIcons)
            {
                var assetName = IconNaming.AssetName(selected.provider, selected.iconName, selected.variant);
                var stableKey = IconNaming.StableKey(selected.provider, selected.iconName, selected.variant);
                var pngPath = Path.Combine(outputFolder, $"{assetName}.png");

                if (!File.Exists(pngPath))
                {
                    Debug.LogWarning($"PNG not found for icon '{assetName}' at '{pngPath}'.");
                    continue;
                }

                var bytes = File.ReadAllBytes(pngPath);

                Texture2D texture;
                if (existingTextures.TryGetValue(stableKey, out var existingTex))
                {
                    texture = existingTex;
                    texture.LoadImage(bytes);
                    if (texture.name != assetName)
                        texture.name = assetName;
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

                existingSprites.TryGetValue(stableKey, out var existingSprite);
                var rectMatches = existingSprite
                                  && (int)existingSprite.rect.width == texture.width
                                  && (int)existingSprite.rect.height == texture.height;

                Sprite sprite;
                if (rectMatches)
                {
                    sprite = existingSprite;
                    if (sprite.name != assetName)
                        sprite.name = assetName;
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
                        sprite.name = assetName;
                    }
                    else
                    {
                        sprite = newSprite;
                        AssetDatabase.AddObjectToAsset(sprite, targetPack);
                    }
                }

                sprite.hideFlags = HideFlags.HideInHierarchy;
                keepAssets.Add(sprite);

                targetPack.AddIcon(new IconPack.PackedIcon
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
                var stableKey = IconNaming.StableKey(original.provider, original.iconName, original.variant);
                if (selectedStableKeys.Contains(stableKey)) continue;

                if (original.texture) keepAssets.Add(original.texture);
                if (original.sprite) keepAssets.Add(original.sprite);
                targetPack.AddIcon(original);
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
