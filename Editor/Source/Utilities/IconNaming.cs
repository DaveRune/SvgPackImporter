using KnightForge.IconImporter.Providers;

namespace KnightForge.IconImporter.Editor.Utilities
{
    /// Single source of truth for asset names and dictionary keys derived from a provider/icon/variant tuple.
    internal static class IconNaming
    {
        private const string UnknownDisplayName = "Unknown";
        private const string UnknownStableId = "unknown";

        // Human-readable name used for the actual subasset and PNG file. Includes the renameable
        // provider display name. Not safe to use as a long-lived lookup key — use StableKey for that.
        internal static string AssetName(IconProvider provider, string iconName, string variant)
        {
            var displayName = provider ? provider.name : UnknownDisplayName;
            var stableId = StableIdOrFallback(provider);
            return string.IsNullOrEmpty(variant)
                ? $"{displayName}-{iconName}-{stableId}"
                : $"{displayName}-{iconName}-{variant}-{stableId}";
        }

        // Rename-invariant lookup key. Drops the renameable provider display name so dictionaries
        // built from existing subassets remain valid after a provider asset is renamed.
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
