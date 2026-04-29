using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace KnightForge.IconImporter.Editor
{
    public static class ImageMagickConverter
    {
        private static string _convertPath;

        public static bool TryDetectImageMagick(out string executablePath)
        {
            executablePath = "";

            #if UNITY_EDITOR_WIN
            var candidates = new[] {
                @"C:\Program Files\ImageMagick\convert.exe",
                @"C:\Program Files (x86)\ImageMagick\convert.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"ImageMagick\convert.exe")
            };
            #elif UNITY_EDITOR_OSX
            var candidates = new[] {
                "/usr/local/bin/convert",
                "/opt/homebrew/bin/convert"
            };
            #else
            var candidates = new[] { "/usr/bin/convert" };
            #endif

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    executablePath = candidate;
                    return true;
                }
            }

            if (TryExecuteCommand("which convert", out var output))
            {
                executablePath = output.Trim();
                return true;
            }

            return false;
        }

        public static IEnumerator ConvertSvgsToPngs(List<string> iconNames, string variant, int size, float strokeWidth, Color color, string outputFolder, System.Action<string> progressCallback)
        {
            if (!TryDetectImageMagick(out var convertPath))
            {
                progressCallback?.Invoke("ImageMagick not found. Please install it first.");
                yield break;
            }

            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string tablerPath = Path.Combine(projectPath, "~TablerIcons", variant);

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            string colorHex = ColorToHex(color);
            int successCount = 0;

            for (int i = 0; i < iconNames.Count; i++)
            {
                string iconName = iconNames[i];
                string svgPath = Path.Combine(tablerPath, $"{iconName}.svg");
                string pngPath = Path.Combine(outputFolder, $"{iconName}.png");

                progressCallback?.Invoke($"Converting {iconName}... ({i + 1}/{iconNames.Count})");

                if (!File.Exists(svgPath))
                {
                    Debug.LogWarning($"SVG not found: {svgPath}");
                    continue;
                }

                string args = $"\"{svgPath}\" -density 300 -background none -size {size}x{size} -resize {size}x{size} -fill \"{colorHex}\" -fuzz 10% -fill \"{colorHex}\" \"{pngPath}\"";

                if (ExecuteCommand(convertPath, args))
                {
                    successCount++;
                }
                else
                {
                    Debug.LogError($"Failed to convert {iconName}");
                }

                yield return null;
            }

            progressCallback?.Invoke($"Import complete! Converted {successCount}/{iconNames.Count} icons");
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
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error executing command: {ex.Message}");
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
            int r = Mathf.RoundToInt(color.r * 255);
            int g = Mathf.RoundToInt(color.g * 255);
            int b = Mathf.RoundToInt(color.b * 255);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}
