// Defines the read-only presentation surface consumed by the HUD presenter.

using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Presentation.UI
{
    public interface IHudView
    {
        void Render(MatchSnapshot snapshot);
    }
}
