using System;

namespace KnightForge.IconImporter.Editor
{
    public static class IconProviderFactory
    {
        public static IIconProvider Create(IconProviderSO providerSO)
        {
            if (providerSO is TablerIconProviderSO)
                return new TablerIconProvider(providerSO.svgRootFolder);

            throw new NotSupportedException($"Unknown icon provider type: '{providerSO.GetType().Name}'.");
        }
    }
}
