using Client.Ui.State.Commands;

namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 駅名の入力と決定・移送モードの切替。View が直接呼ぶ（設計書 A-8）。サーバーの結果は実装がトーストへ渡す
    /// Station name input and apply, and transfer mode selection; the View calls them directly (design doc A-8) and the implementation toasts server results
    /// </summary>
    public interface ITrainPlatformEditCommands
    {
        void EditStationName(string input);
        UiCommandResult ApplyStationName();
        UiCommandResult SelectTransferMode(TrainPlatformTransferMode mode);
    }
}
