using System.Collections.Generic;
using Client.Input;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     単押しで実行するアクション。キー・ヒント・実行を対象側が定義し、駆動側は従うだけ
    ///     A tap action: the target defines key, hint and execution, the driver only follows
    /// </summary>
    public interface ITapInteractAction
    {
        InputKey Key { get; }
        LocalizationKey HintKey { get; }
        IReadOnlyList<string> HintParams { get; }

        // 実行した事実は結果型が保持する。UI遷移の有無はHandled/Transitで表す
        // The result type carries the fact that it ran; Handled and Transit tell whether a UI transition follows
        InteractExecuteResult Execute();
    }
}
