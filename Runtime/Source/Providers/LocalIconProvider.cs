using UnityEngine;

namespace KnightForge.SvgPackImporter.Providers
{
    /// Concrete provider for icons sourced from a local project folder, with no remote download workflow.
    [Icon("Packages/com.knightforge.svgpackimporter/Editor/Icons/IconProvider.png")]
    [CreateAssetMenu(fileName = "Local Icons", menuName = SvgPackImporterConstants.IconProviders + "Local", order = 1)]
    public sealed class LocalIconProvider : IconProvider
    {
    }
}
