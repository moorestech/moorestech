using System.Collections.Generic;
using Client.Input;

namespace Client.Game.InGame.Interact.Selection
{
    /// <summary>
    ///     毎フレーム1件の対象を決める役。テストは実装を差し替える
    ///     Decides the single interact target each frame; tests substitute their own implementation
    /// </summary>
    public interface IInteractTargetSelector
    {
        IInteractable Select();

        // 主対象が応じないキーを最良候補へ回す
        // Asks which candidate answers a key the primary target does not offer
        IInteractable SelectRespondingTo(InputKey key);

        // 駆動側がキーを名指ししないための候補キー集合
        // The keys the candidates offer, so the driver never has to name a key itself
        void CollectCandidateKeys(List<InputKey> keys);
    }
}
