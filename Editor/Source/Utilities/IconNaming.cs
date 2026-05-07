using KnightForge.IconImporter.Providers;

namespace KnightForge.IconImporter.Editor.Utilities
{
    /// Single source of truth for asset names and dictionary keys derived from a provider/icon/variant tuple.
    internal static class IconNaming
    {
        private const string UnknownDisplayName = "Unknown";
        private const string UnknownStableId = "unknown";

        internal static string AssetName(IconProvider provider, string iconName, string variant)
        {
            var displayName = provider ? provider.name : UnknownDisplayName;
            var stableId = StableIdOrFallback(provider);
            return string.IsNullOrEmpty(variant)
                ? $"{displayName}-{iconName}-{stableId}"
                : $"{displayName}-{iconName}-{variant}-{stableId}";
        }

        internal static string StableKey(IconProvider provider, string iconName, string variant)
        {
            var stableId = StableIdOrFallback(provider);
            return string.IsNullOrEmpty(variant)
                ? $"{stableId}-{iconName}"
                : $"{stableId}-{iconName}-{variant}";
        }

        private static string StableIdOrFallback(IconProvider provider)
        {
            if (!provider) return UnknownStableId;
            var id = provider.StableId;
            return string.IsNullOrEmpty(id) ? UnknownStableId : id;
        }
    }
}
