using System.Collections.Generic;
using UnityEngine;

namespace KnightForge.IconImporter.Providers.BuiltIn
{
    [CreateAssetMenu(fileName = "Iconoir", menuName = IconImporterConstants.IconProviders + "Iconoir", order = IconImporterConstants.IconProvidersBuiltIn)]
    public sealed class IconoirIconProvider : RepoIconProvider
    {
        public override string DefaultRepoUrl => "https://github.com/iconoir-icons/iconoir";
        protected override string DefaultSvgRootFolder => "Iconoir";

        public override IReadOnlyDictionary<string, VariantDescriptor> VariantPaths =>
            new Dictionary<string, VariantDescriptor>
            {
                { "regular", new VariantDescriptor("icons/regular/", IconStyle.Stroke) },
                { "solid", new VariantDescriptor("icons/solid/", IconStyle.Fill) }
            };
    }
}