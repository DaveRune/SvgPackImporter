using System.Collections.Generic;
using UnityEngine;

namespace KnightForge.IconImporter.Providers.BuiltIn
{
    [CreateAssetMenu(fileName = "Heroicons", menuName = IconImporterConstants.IconProviders + "Heroicons", order = IconImporterConstants.IconProvidersBuiltIn)]
    public sealed class HeroIconProvider : RepoIconProvider
    {
        public override string DefaultRepoUrl => "https://github.com/tailwindlabs/heroicons";
        protected override string DefaultSvgRootFolder => "Heroicons";

        protected override string GenerateStableId() => "heroicons";

        private static readonly Dictionary<string, VariantDescriptor> Paths = new()
        {
            { "outline", new VariantDescriptor("optimized/24/outline/", IconStyle.Stroke) },
            { "solid", new VariantDescriptor("optimized/24/solid/", IconStyle.Fill) }
        };

        public override IReadOnlyDictionary<string, VariantDescriptor> VariantPaths => Paths;
    }
}