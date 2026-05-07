using System.Collections.Generic;
using UnityEngine;

namespace KnightForge.SvgPackImporter.Providers.BuiltIn
{
    [CreateAssetMenu(fileName = "Feather Icons", menuName = IconImporterConstants.IconProviders + "Feather", order = IconImporterConstants.IconProvidersBuiltIn)]
    public sealed class FeatherIconProvider : RepoIconProvider
    {
        public override string DefaultRepoUrl => "https://github.com/feathericons/feather";
        protected override string DefaultSvgRootFolder => "Feather";

        protected override string GenerateStableId() => "feather";

        private static readonly Dictionary<string, VariantDescriptor> Paths = new()
        {
            { "", new VariantDescriptor("icons/", IconStyle.Stroke) }
        };

        public override IReadOnlyDictionary<string, VariantDescriptor> VariantPaths => Paths;
    }
}