using System;

namespace KnightForge.IconImporter.Editor.Providers
{
    public static class IconProviderFactory
    {
        public static IIconProvider Create(IconProvider provider)
        {
            if (provider is TablerIconProvider)
                return new Providers.Tabler.TablerIconProvider(provider.svgRootFolder);

            throw new NotSupportedException($"Unknown icon provider type: '{provider.GetType().Name}'.");
        }
    }
}