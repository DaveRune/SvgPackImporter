using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor
{
    public static class IconImportProcessor
    {
        public static IEnumerator EmbedIconsAsSubassets(IconPack targetPack, List<ImportedIcon> selectedIcons, string outputFolder)
        {
            var assetPath = AssetDatabase.GetAssetPath(targetPack);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError($"IconPackSO '{targetPack.name}' has no asset path. Save the asset first.");
                yield break;
            }

            // Cache existing subassets by name so we can update them in-place and preserve Unity local
            // file IDs — this keeps references from UI Image components intact across re-imports.
            var existingTextures = new Dictionary<string, Texture2D>();
            var existingSprites = new Dictionary<string, Sprite>();

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is Texture2D tex) existingTextures[tex.name] = tex;
                else if (asset is Sprite spr) existingSprites[spr.name] = spr;
            }

            var keepAssets = new HashSet<UnityEngine.Object> { targetPack };
            targetPack.icons.Clear();

            var embedded = 0;

            foreach (var selected in selectedIcons)
            {
                // PNG name matches the output produced by ImageMagickConverter
                var assetName = $"{selected.iconName}-{selected.variant}";
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
                        UnityEngine.Object.DestroyImmediate(texture);
                        continue;
                    }

                    AssetDatabase.AddObjectToAsset(texture, targetPack);
                }

                keepAssets.Add(texture);

                // Reuse the sprite only if its rect still matches the texture dimensions.
                // If iconSize changed between imports, the rect is stale and must be recreated.
                existingSprites.TryGetValue(assetName, out var existingSpr);
                var rectMatches = existingSpr != null
                    && (int)existingSpr.rect.width == texture.width
                    && (int)existingSpr.rect.height == texture.height;

                Sprite sprite;
                if (rectMatches)
                {
                    sprite = existingSpr;
                }
                else
                {
                    if (existingSpr != null)
                        AssetDatabase.RemoveObjectFromAsset(existingSpr);

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
                    texture = texture,
                    sprite = sprite
                });

                embedded++;
            }

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (!keepAssets.Contains(asset))
                    AssetDatabase.RemoveObjectFromAsset(asset);
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
