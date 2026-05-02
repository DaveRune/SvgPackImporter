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

            var toImport = pack.icons
                .Where(icon => icon.provider)
                .Select(icon => new ImportedIcon { iconName = icon.iconName, variant = icon.variant, provider = icon.provider })
                .ToList();

            EditorUtility.DisplayProgressBar("Updating Icons", "Starting conversion...", 0);

            var convertRoutine = ImageMagickConverter.ConvertSvgsToPngs(
                toImport,
                icon => icon.provider.GetSvgPath(icon.iconName, icon.variant),
                pack.iconSize,
                pack.strokeWidth,
                pack.iconColor,
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

            var keepAssets = new HashSet<Object> { targetPack };
            targetPack.icons.Clear();

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

                keepAssets.Add(texture);

                // Reuse the sprite only if its rect still matches the texture dimensions.
                // If iconSize changed between imports, the rect is stale and must be recreated.
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
                    if (existingSprite)
                        AssetDatabase.RemoveObjectFromAsset(existingSprite);

                    sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f
                    );
                    sprite.name = assetName;
                    AssetDatabase.AddObjectToAsset(sprite, targetPack);
                }

                keepAssets.Add(sprite);

                targetPack.icons.Add(new IconPack.PackedIcon
                {
                    iconName = selected.iconName,
                    variant = selected.variant,
                    provider = selected.provider,
                    texture = texture,
                    sprite = sprite
                });

                embedded++;
            }

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                if (!keepAssets.Contains(asset))
                    AssetDatabase.RemoveObjectFromAsset(asset);

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