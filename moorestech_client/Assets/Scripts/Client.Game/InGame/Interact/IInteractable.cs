using UnityEngine;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     Fキー操作対象の共通契約。単押しは駆動側が種別を知らずに扱うが、長押し（採掘）は今もInteractControllerが採掘FSMを直接持つ
    ///     Shared contract for interact-key targets; tap dispatch is kind-agnostic, while hold (mining) is still driven by the mining FSM InteractController owns directly
    /// </summary>
    public interface IInteractable
    {
        GameObject GameObject { get; }

        // 破壊済み・マスタ欠損等は対象外
        // Destroyed, master-less or non-openable things never become candidates
        bool IsInteractAvailable { get; }

        void SetHighlighted(bool highlighted);
    }
}
