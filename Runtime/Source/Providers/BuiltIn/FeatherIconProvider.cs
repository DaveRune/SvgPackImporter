using System.Collections.Generic;
using UnityEngine;

namespace KnightForge.IconImporter.Providers.BuiltIn
{
    [CreateAssetMenu(fileName = "Feather Icons", menuName = IconImporterConstants.IconProviders + "Feather", order = IconImporterConstants.IconProvidersBuiltIn)]
    public sealed class FeatherIconProvider : RepoIconProvider
    {
        protected override string DefaultRepoUrl => "https://github.com/feathericons/feather";
        protected override string DefaultSvgRootFolder => "Feather";

        public override IReadOnlyDictionary<string, VariantDescriptor> VariantPaths =>
            new Dictionary<string, VariantDescriptor>
            {
                { "", new VariantDescriptor("icons/", IconStyle.Stroke) }
            };
    }
}