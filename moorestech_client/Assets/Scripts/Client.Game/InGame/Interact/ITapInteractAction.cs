using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
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

        // UI遷移を伴わないアクションはnullを返す
        // Actions without a UI transition return null
        UITransitContext Execute();
    }
}
