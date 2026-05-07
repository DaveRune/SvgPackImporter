using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace KnightForge.SvgPackImporter.Providers.BuiltIn
{
    [CreateAssetMenu(fileName = "Simple Icons", menuName = SvgPackImporterConstants.IconProviders + "Simple Icons", order = SvgPackImporterConstants.IconProvidersBuiltIn)]
    public sealed class SimpleIconsProvider : RepoIconProvider
    {
        public override string DefaultRepoUrl => "https://github.com/simple-icons/simple-icons";
        protected override string DefaultSvgRootFolder => "SimpleIcons";

        protected override string GenerateStableId() => "simpleicons";

        private static readonly Dictionary<string, VariantDescriptor> Paths = new()
        {
            { "", new VariantDescriptor("icons/", IconStyle.Fill) }
        };

        public override IReadOnlyDictionary<string, VariantDescriptor> VariantPaths => Paths;

        public override string PreprocessSvg(string content, string variant, string colorHex, float strokeWidth)
        {
            // <title> elements can be rendered as text by ImageMagick's Windows SVG renderer.
            content = Regex.Replace(content, @"<title>[^<]*</title>", "");

            // Paths have no fill attribute and default to black. Inject fill="currentColor"
            // on the root <svg> so all paths inherit the tint color.
            content = Regex.Replace(content, @"(<svg\b)([^>]*>)", m =>
            {
                var attrs = m.Groups[2].Value;
                return attrs.Contains("fill=")
                    ? m.Value
                    : m.Groups[1].Value + @" fill=""currentColor""" + attrs;
            });

            return base.PreprocessSvg(content, variant, colorHex, strokeWidth);
        }
    }
}