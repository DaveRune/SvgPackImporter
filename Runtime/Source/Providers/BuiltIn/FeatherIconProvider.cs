using System.Collections.Generic;
using UnityEngine;

namespace KnightForge.IconImporter.Providers.BuiltIn
{
    [CreateAssetMenu(fileName = "Feather Icons", menuName = IconImporterConstants.IconProviders + "Feather", order = IconImporterConstants.IconProvidersBuiltIn)]
    public sealed class FeatherIconProvider : RepoIconProvider
    {
        public override string DefaultRepoUrl => "https://github.com/feathericons/feather";
        protected override string DefaultSvgRootFolder => "Feather";

        protected override string GenerateStableId() => "feather";

        public override IReadOnlyDictionary<string, VariantDescriptor> VariantPaths =>
            new Dictionary<string, VariantDescriptor>
            {
                { "", new VariantDescriptor("icons/", IconStyle.Stroke) }
            };
    }
}