using KnightForge.IconImporter.Providers;

namespace KnightForge.IconImporter.Editor.Utilities
{
    /// Single source of truth for asset names and dictionary keys derived from a provider/icon/variant tuple.
    internal static class IconNaming
    {
        internal static string AssetName(IconProvider provider, string iconName, string variant)
        {
            var providerName = provider ? provider.name : "Unknown";
            return string.IsNullOrEmpty(variant)
                ? $"{providerName}-{iconName}"
                : $"{providerName}-{iconName}-{variant}";
        }
    }
}
