using UnityEngine;

namespace KnightForge.IconImporter.Providers
{
    /// Concrete provider for icons sourced from a local project folder, with no remote download workflow.
    [Icon("Packages/com.knightforge.iconimporter/Editor/Icons/IconProvider.png")]
    [CreateAssetMenu(fileName = "Local Icons", menuName = IconImporterConstants.IconProviders + "Local", order = 1)]
    public sealed class LocalIconProvider : IconProvider
    {
    }
}
