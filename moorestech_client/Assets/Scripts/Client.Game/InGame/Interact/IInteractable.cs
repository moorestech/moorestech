using UnityEngine;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     Fキー操作対象の共通契約。駆動側は種別を知らない
    ///     Shared contract for anything the interact key targets; the driver never learns the concrete kind
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
