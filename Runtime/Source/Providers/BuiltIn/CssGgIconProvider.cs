using System.Collections.Generic;
using UnityEngine;

namespace KnightForge.IconImporter.Providers.BuiltIn
{
    [CreateAssetMenu(fileName = "css gg", menuName = IconImporterConstants.IconProviders + "css.gg", order = IconImporterConstants.IconProvidersBuiltIn)]
    public sealed class CssGgIconProvider : RepoIconProvider
    {
        public override string DefaultRepoUrl => "https://github.com/astrit/css.gg";
        protected override string DefaultSvgRootFolder => "CssGg";

        public override IReadOnlyDictionary<string, VariantDescriptor> VariantPaths =>
            new Dictionary<string, VariantDescriptor>
            {
                { "", new VariantDescriptor("icons/svg/", IconStyle.Fill) }
            };
    }
}