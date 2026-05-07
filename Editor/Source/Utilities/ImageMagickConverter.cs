using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using KnightForge.SvgPackImporter.Editor.Data;
using KnightForge.SvgPackImporter.Providers;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace KnightForge.SvgPackImporter.Editor.Utilities
{
    internal static class ImageMagickConverter
    {
        private static string _cachedExecutablePath;
        private static bool _cacheValid;

        public static void InvalidateDetectionCache()
        {
            _cachedExecutablePath = null;
            _cacheValid = false;
        }

        public static bool TryDetectImageMagick(out string executablePath)
        {
            if (_cacheValid)
            {
                executablePath = _cachedExecutablePath;
                return !string.IsNullOrEmpty(executablePath);
            }

            var found = TryDetectInternal(out executablePath);

            if (found)
            {
                var settings = IconImporterSettings.Instance;
                if (settings.ImageMagickPath != executablePath || !settings.ImageMagickDetected)
                {
                    settings.ImageMagickPath = executablePath;
                    settings.ImageMagickDetected = true;
                    IconImporterSettings.Save();
                }
            }

            _cachedExecutablePath = found ? executablePath : null;
            _cacheValid = true;
            return found;
        }

        private static bool TryDetectInternal(out string executablePath)
        {
            executablePath = "";

            var savedPath = IconImporterSettings.Instance.ImageMagickPath;
            if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
            {
                executablePath = savedPath;
                return true;
            }

#if UNITY_EDITOR_WIN
            var fixedCandidates = new[]
            {
                @"C:\Program Files\ImageMagick\magick.exe",
                @"C:\Program Files\ImageMagick\convert.exe",
                @"C:\Program Files (x86)\ImageMagick\magick.exe",
                @"C:\Program Files (x86)\ImageMagick\convert.exe"
            };

            foreach (var candidate in fixedCandidates)
            {
                if (!File.Exists(candidate)) continue;
                executablePath = candidate;
                return true;
            }

            var searchRoots = new[] { @"C:\Program Files", @"C:\Program Files (x86)" };
            foreach (var root in searchRoots)
            {
                if (!Directory.Exists(root))
                    continue;

                foreach (var dir in Directory.GetDirectories(root, "ImageMagick*"))
                foreach (var exe in new[] { "magick.exe", "convert.exe" })
                {
                    var path = Path.Combine(dir, exe);
                    if (!File.Exists(path)) continue;
                    executablePath = path;
                    return true;
                }
            }

            return false;
#elif UNITY_EDITOR_OSX
            var candidates = new[]
            {
                "/usr/local/bin/magick",
                "/opt/homebrew/bin/magick",
                "/usr/local/bin/convert",
                "/opt/homebrew/bin/convert"
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    executablePath = candidate;
                    return true;
                }
            }

            if (TryExecuteCommand("which magick", out var output) || TryExecuteCommand("which convert", out output))
            {
                executablePath = output.Trim();
                return true;
            }

            return false;
#else
            if (File.Exists("/usr/bin/magick")) { executablePath = "/usr/bin/magick"; return true; }
            if (File.Exists("/usr/bin/convert")) { executablePath = "/usr/bin/convert"; return true; }

            if (TryExecuteCommand("which magick", out var output) || TryExecuteCommand("which convert", out output))
            {
                executablePath = output.Trim();
                return true;
            }

            return false;
#endif
        }

        public static bool TryGeneratePreviewFromContent(string svgPath, string outputPngPath, int size, IconProvider provider, string variant)
        {
            if (!TryDetectImageMagick(out var magickPath))
                return false;

            var tempSvgPath = outputPngPath + ".tmp.svg";
            try
            {
                var content = File.ReadAllText(svgPath);
                content = provider.PreprocessSvg(content, variant, "#FFFFFF", 2f);
                File.WriteAllText(tempSvgPath, content, Encoding.UTF8);

                var density = provider.GetDensity(size, variant);
                var args = $"-background none -density {density} \"{tempSvgPath}\" \"{outputPngPath}\"";
                return ExecuteCommand(magickPath, args);
            }
            finally
            {
                if (File.Exists(tempSvgPath))
                    File.Delete(tempSvgPath);
            }
        }

        public static IEnumerator ConvertSvgsToPngs(
            List<ImportedIcon> selectedIcons,
            Func<ImportedIcon, string> resolveSvgPath,
            int size, float strokeWidth, Color color,
            string outputFolder, Action<string> progressCallback)
        {
            if (!TryDetectImageMagick(out var convertPath))
            {
                progressCallback?.Invoke("ImageMagick not found. Please install it first.");
                yield break;
            }

            Directory.CreateDirectory(outputFolder);

            var tempFolder = Path.Combine(Path.GetTempPath(), "IconImporterTemp");
            Directory.CreateDirectory(tempFolder);

            var colorHex = ColorToHex(color);
            var successCount = 0;

            for (var i = 0; i < selectedIcons.Count; i++)
            {
                var icon = selectedIcons[i];
                var svgPath = resolveSvgPath(icon);
                var pngName = $"{IconNaming.AssetName(icon.provider, icon.iconName, icon.variant)}.png";
                var pngPath = Path.Combine(outputFolder, pngName);

                progressCallback?.Invoke($"Converting {icon.iconName} ({icon.variant})... ({i + 1}/{selectedIcons.Count})");

                if (!File.Exists(svgPath))
                {
                    Debug.LogWarning($"SVG not found: '{svgPath}'.");
                    continue;
                }

                var tempSvgPath = Path.Combine(tempFolder, pngName.Replace(".png", "_temp.svg"));
                var svgContent = File.ReadAllText(svgPath);
                var processedContent = icon.provider.PreprocessSvg(svgContent, icon.variant, colorHex, strokeWidth);
                File.WriteAllText(tempSvgPath, processedContent, Encoding.UTF8);

                var density = icon.provider.GetDensity(size, icon.variant);
                var args = $"-background none -density {density} \"{tempSvgPath}\" \"{pngPath}\"";

                if (ExecuteCommand(convertPath, args))
                    successCount++;
                else
                    Debug.LogError($"Failed to convert '{icon.iconName}' ({icon.variant}).");

                File.Delete(tempSvgPath);

                yield return null;
            }

            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);

            progressCallback?.Invoke($"Import complete! Converted {successCount}/{selectedIcons.Count} icons.");
            AssetDatabase.Refresh();
        }

        private static bool ExecuteCommand(string command, string args)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                process.WaitForExit();

                if (process.ExitCode == 0)
                    return true;

                var error = process.StandardError.ReadToEnd();
                if (!string.IsNullOrEmpty(error))
                    Debug.LogError($"ImageMagick: {error}");

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error executing ImageMagick: {ex.Message}");
                return false;
            }
        }

        private static bool TryExecuteCommand(string command, out string output)
        {
            output = "";
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "sh",
                    Arguments = $"-c \"{command}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static string ColorToHex(Color color)
        {
            var r = Mathf.RoundToInt(color.r * 255);
            var g = Mathf.RoundToInt(color.g * 255);
            var b = Mathf.RoundToInt(color.b * 255);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}
