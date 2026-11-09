using System;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// パネルの取得失敗（Web の error: openFailed / containerMissing / trainCarMissing）
    /// Panel fetch failures (Web error: openFailed / containerMissing / trainCarMissing)
    /// </summary>
    public enum BlockInventoryPanelError
    {
        None,
        OpenFailed,
        ContainerMissing,
        TrainCarMissing,
    }

    public static class BlockInventoryPanelErrorText
    {
        // Web BlockInventoryPanel の TrainErrorKeys と同じ対応
        // Same mapping as TrainErrorKeys in the Web BlockInventoryPanel
        // None は false（エラー行を出さない）。外殻のエラー行の出し入れはこの戻り値 1 本で決める（D-36）
        // None gives false (no error row); the shell's error row is shown or hidden by this return value alone (D-36)
        public static bool TryGetLocalizationKey(BlockInventoryPanelError error, out LocalizationKey key)
        {
            switch (error)
            {
                case BlockInventoryPanelError.None:
                    key = default;
                    return false;
                case BlockInventoryPanelError.OpenFailed:
                    key = LocalizationKeys.Ui.BlockInventory.InventoryOpenFailed;
                    return true;
                case BlockInventoryPanelError.ContainerMissing:
                    key = LocalizationKeys.Ui.BlockInventory.TrainNoItemContainer;
                    return true;
                case BlockInventoryPanelError.TrainCarMissing:
                    key = LocalizationKeys.Ui.BlockInventory.TrainNotFound;
                    return true;
                default:
                    // enum に値を足して文言を足し忘れた開発時の誤り（空の文で出さない）
                    // A dev-time mistake: a new enum value without text (never show an empty row)
                    throw new ArgumentOutOfRangeException(nameof(error), error, null);
            }
        }
    }
}
