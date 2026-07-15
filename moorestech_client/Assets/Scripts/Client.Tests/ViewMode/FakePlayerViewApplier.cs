using System.Collections.Generic;
using Client.Game.InGame.Control.ViewMode;

namespace Client.Tests.ViewMode
{
    public class FakePlayerViewApplier : IPlayerViewApplier
    {
        public readonly List<PlayerViewMode> AppliedModes = new();

        public void SetViewMode(PlayerViewMode mode)
        {
            AppliedModes.Add(mode);
        }
    }
}
