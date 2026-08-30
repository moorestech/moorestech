using System.Collections.Generic;
using Client.Input;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     毎フレーム1件のインタラクト対象を決める役。テストは実装を差し替える
    ///     Decides the single interact target each frame; tests substitute their own implementation
    /// </summary>
    public interface IInteractTargetSelector
    {
        IInteractable Select();

        // 主対象が応じないキーの押下を、そのキーに応じる最良候補へ回すための問い合わせ
        // Asks which candidate answers a key the primary target does not offer
        IInteractable SelectRespondingTo(InputKey key);

        // 候補が提示するキーの集合。駆動側が特定のキーを名指ししないための出所
        // The keys the candidates offer, so the driver never has to name a key itself
        void CollectCandidateKeys(List<InputKey> keys);
    }
}
