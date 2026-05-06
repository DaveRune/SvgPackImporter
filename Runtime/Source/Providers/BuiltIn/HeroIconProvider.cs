using System.Collections.Generic;
using UnityEngine;

namespace KnightForge.IconImporter.Providers.BuiltIn
{
    [CreateAssetMenu(fileName = "HeroIcon", menuName = IconImporterConstants.IconProviders + "Heroicons", order = IconImporterConstants.IconProvidersBuiltIn)]
    public sealed class HeroIconProvider : RepoIconProvider
    {
        public override string DefaultRepoUrl => "https://github.com/tailwindlabs/heroicons";
        protected override string DefaultSvgRootFolder => "Heroicons";

        public override IReadOnlyDictionary<string, VariantDescriptor> VariantPaths =>
            new Dictionary<string, VariantDescriptor>
            {
                { "outline", new VariantDescriptor("optimized/24/outline/", IconStyle.Stroke) },
                { "solid", new VariantDescriptor("optimized/24/solid/", IconStyle.Fill) }
            };
    }
}