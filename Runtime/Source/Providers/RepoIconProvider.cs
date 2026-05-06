using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace KnightForge.IconImporter.Providers
{
    public abstract class RepoIconProvider : IconProvider
    {
        [SerializeField] private string _repoUrl;
        [SerializeField] private string _version = "latest";

        public string RepoUrl => _repoUrl;
        public string Version => _version;

        public abstract IReadOnlyDictionary<string, VariantDescriptor> VariantPaths { get; }

        public virtual string AliasesZipPath => null;

        public abstract string DefaultRepoUrl { get; }
        protected abstract string DefaultSvgRootFolder { get; }

        public override IReadOnlyList<string> Variants => new List<string>(VariantPaths.Keys);

        public override bool SupportsStroke => VariantPaths.Values.Any(v => v.Style == IconStyle.Stroke);

        protected override void Reset()
        {
            base.Reset();
            _repoUrl = DefaultRepoUrl;
            svgRootFolder = DefaultSvgRootFolder;
            _version = "latest";
        }

        public override string PreprocessSvg(string content, string variant, string colorHex, float strokeWidth)
        {
            var vb = GetViewBoxSizeForVariant(variant);
            var result = base.PreprocessSvg(content, variant, colorHex, strokeWidth);

            if (!VariantPaths.TryGetValue(variant, out var descriptor) || descriptor.Style != IconStyle.Stroke)
                return result;

            result = Regex.Replace(result, @"stroke-width=""[^""]*""", $"stroke-width=\"{strokeWidth:F2}\"");

            var pad = strokeWidth / 2f;
            result = Regex.Replace(result, @"viewBox=""[^""]*""",
                $"viewBox=\"{-pad:F2} {-pad:F2} {vb.x + strokeWidth:F2} {vb.y + strokeWidth:F2}\"");

            return result;
        }

        protected override Vector2Int GetViewBoxSizeForVariant(string variant)
        {
            if (VariantPaths.TryGetValue(variant, out var descriptor) && descriptor.ViewBoxSize.HasValue)
                return descriptor.ViewBoxSize.Value;
            return DefaultViewBoxSize;
        }

        protected override string GetManifestVersion()
        {
            return _version;
        }
    }
}