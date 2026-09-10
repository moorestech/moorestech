using System.Collections.Generic;
using Client.Input;

namespace Client.Game.InGame.Interact.Selection
{
    /// <summary>
    ///     1フレーム分の走査結果。主対象もキー別の候補も同じ走査の上で答える
    ///     One frame's scan result; the primary target and the per-key candidates answer on that same scan
    /// </summary>
    public interface IInteractSelection
    {
        // そのフレームで唯一選ばれた対象
        // The single target picked for the frame
        IInteractable Primary { get; }

        // 主対象が応じないキーを最良候補へ回す
        // Asks which candidate answers a key the primary target does not offer
        IInteractable SelectRespondingTo(InputKey key);

        // 駆動側がキーを名指ししないための候補キー集合
        // The keys the candidates offer, so the driver never has to name a key itself
        void CollectCandidateKeys(List<InputKey> keys);
    }
}
