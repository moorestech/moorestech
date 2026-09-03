using System.Collections.Generic;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     単押しアクションを1つ以上持つインタラクト対象
    ///     An interactable exposing one or more tap actions
    /// </summary>
    public interface ITapInteractable : IInteractable
    {
        IReadOnlyList<ITapInteractAction> Actions { get; }
    }
}
