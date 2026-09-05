using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Selection;
using UnityEngine;

namespace Client.Game.InGame.Map.Outcrop
{
    /// <summary>
    ///     露頭コライダに付与するレイキャスト用マーカー
    ///     Raycast marker attached to outcrop colliders
    /// </summary>
    public class OutcropRayTarget : MonoBehaviour, IInteractRayTarget
    {
        public OutcropGameObject OutcropGameObject { get; private set; }

        public IInteractable Interactable => OutcropGameObject;

        public void Initialize(OutcropGameObject outcropGameObject)
        {
            OutcropGameObject = outcropGameObject;
        }
    }
}
