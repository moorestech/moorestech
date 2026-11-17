using Client.Ui.State.Commands;

namespace Client.Ui.State.Toast
{
    /// <summary>
    /// コマンド結果をトーストへ渡す口。結果を作る Commands が自分で呼ぶ（View は呼ばない）
    /// Port that hands command results to toasts; the Commands that produce a result call it themselves (views never do)
    /// </summary>
    public interface IUiCommandResultToaster
    {
        void Report(string commandName, UiCommandResult result);
    }
}
