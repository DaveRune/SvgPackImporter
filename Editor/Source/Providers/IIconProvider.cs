using System.Collections.Generic;

namespace KnightForge.IconImporter.Editor
{
    public interface IIconProvider
    {
        string ProviderName { get; }
        IReadOnlyList<string> AvailableVariants { get; }
        IconManifest LoadManifest();
        string GetSvgPath(string iconName, string variant);
    }
}
