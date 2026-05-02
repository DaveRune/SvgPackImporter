using System.Collections.Generic;

namespace KnightForge.IconImporter.Editor.Providers
{
    public interface IIconProvider
    {
        IReadOnlyList<string> AvailableVariants { get; }
        IconManifest LoadManifest();
        string GetSvgPath(string iconName, string variant);
    }
}