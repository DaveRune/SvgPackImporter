using System.Collections.Generic;
using UnityEngine;

namespace KnightForge.SvgPackImporter.Providers.BuiltIn
{
    [CreateAssetMenu(fileName = "CSS.GG Icons", menuName = IconImporterConstants.IconProviders + "css.gg", order = IconImporterConstants.IconProvidersBuiltIn)]
    public sealed class CssGgIconProvider : RepoIconProvider
    {
        public override string DefaultRepoUrl => "https://github.com/astrit/css.gg";
        protected override string DefaultSvgRootFolder => "CssGg";

        protected override string GenerateStableId() => "cssgg";

        private static readonly Dictionary<string, VariantDescriptor> Paths = new()
        {
            { "", new VariantDescriptor("icons/svg/", IconStyle.Fill) }
        };

        public override IReadOnlyDictionary<string, VariantDescriptor> VariantPaths => Paths;
    }
}