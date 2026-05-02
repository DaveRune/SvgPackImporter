using System.Collections.Generic;
using UnityEngine;

namespace KnightForge.IconImporter.Providers
{
    public abstract class RepoIconProvider : IconProvider
    {
        [SerializeField] protected string _repoUrl;
        [SerializeField] protected string _version = "latest";

        public string RepoUrl => _repoUrl;
        public string Version => _version;

        // Key: local variant folder name. Value: path fragment to match in ZIP entries.
        public abstract IReadOnlyDictionary<string, string> VariantPaths { get; }

        // Path fragment matching the aliases file entry in the ZIP. Null means no aliases file.
        public virtual string AliasesZipPath => null;

        protected virtual void Reset()
        {
            _variants = new List<string>(VariantPaths.Keys);
        }

        protected override string GetManifestVersion()
        {
            return _version;
        }

        public override IconManifest BuildManifest(string versionOverride = null)
        {
            if (_variants == null || _variants.Count == 0)
                _variants = new List<string>(VariantPaths.Keys);
            return base.BuildManifest(versionOverride);
        }
    }
}