using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    internal static class SubInventoryStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Close),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.ShiftLeftClick, LocalizationKeys.Ui.KeyHint.Text.BulkMove),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.RightClick, LocalizationKeys.Ui.KeyHint.Text.HalveOrPlaceOne),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.LeftDrag, LocalizationKeys.Ui.KeyHint.Text.DistributeEvenly),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.DoubleClick, LocalizationKeys.Ui.KeyHint.Text.GatherSameItem),
        };
    }
}
