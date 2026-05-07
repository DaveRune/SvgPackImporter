using System.Collections.Generic;
using UnityEngine;

namespace KnightForge.SvgPackImporter.Providers.BuiltIn
{
    [CreateAssetMenu(fileName = "Iconoir Icons", menuName = IconImporterConstants.IconProviders + "Iconoir", order = IconImporterConstants.IconProvidersBuiltIn)]
    public sealed class IconoirIconProvider : RepoIconProvider
    {
        public override string DefaultRepoUrl => "https://github.com/iconoir-icons/iconoir";
        protected override string DefaultSvgRootFolder => "Iconoir";

        protected override string GenerateStableId() => "iconoir";

        private static readonly Dictionary<string, VariantDescriptor> Paths = new()
        {
            { "regular", new VariantDescriptor("icons/regular/", IconStyle.Stroke) },
            { "solid", new VariantDescriptor("icons/solid/", IconStyle.Fill) }
        };

        public override IReadOnlyDictionary<string, VariantDescriptor> VariantPaths => Paths;
    }
}