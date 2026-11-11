using Client.Ui.Components.Controls;
using Client.Ui.State.Text;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Platform.Edit;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Parts
{
    /// <summary>
    /// 積込/卸しの切替（Web TrainPlatformSection の写し）。押下は編集のコマンドを直接呼び、選択はサーバーの状態でだけ替わる
    /// Load/unload switch (copy of Web TrainPlatformSection); presses call the edit commands directly and the selection follows the server only
    /// </summary>
    public sealed class TrainPlatformModeSection : VisualElement
    {
        private readonly ModeSwitch<TrainPlatformTransferMode> _mode;

        public TrainPlatformModeSection(TrainPlatformEditCommands commands)
        {
            // 選択肢の testid は Web に無いので -load / -unload を足す（D-7）
            // Web options carry no testids, so -load / -unload are added (D-7)
            name = "train-platform-section";
            _mode = new ModeSwitch<TrainPlatformTransferMode>("train-platform-mode", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, new[]
            {
                new ModeSwitchOption<TrainPlatformTransferMode>("train-platform-mode-load", TrainPlatformTransferMode.LoadToTrain, new UiText(LocalizationKeys.Ui.BlockInventory.TrainPlatformLoad)),
                new ModeSwitchOption<TrainPlatformTransferMode>("train-platform-mode-unload", TrainPlatformTransferMode.UnloadToPlatform, new UiText(LocalizationKeys.Ui.BlockInventory.TrainPlatformUnload)),
            });
            _mode.OnOptionSelected.Subscribe(mode => commands.SelectTransferMode(mode));
            Add(_mode);
        }

        public void Render(TrainPlatformTransferMode mode)
        {
            _mode.SetSelected(mode);
        }
    }
}
