using System.Collections.Generic;
using Client.Game.InGame.Interact.Tap;
using UnityEngine;

namespace Client.Tests.Interact
{
    // 渡されたアクションをそのまま提示する単押し対象
    // A tap target that offers exactly the actions it was given
    internal sealed class StubTapInteractable : ITapInteractable
    {
        public GameObject GameObject { get; }
        public bool IsInteractAvailable => true;
        public IReadOnlyList<ITapInteractAction> Actions { get; }

        public StubTapInteractable(GameObject gameObject, params ITapInteractAction[] actions)
        {
            GameObject = gameObject;
            Actions = actions;
        }

        public void SetHighlighted(bool highlighted)
        {
        }
    }
}
