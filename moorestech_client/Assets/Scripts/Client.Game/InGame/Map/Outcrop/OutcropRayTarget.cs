using UnityEngine;

namespace Client.Game.InGame.Map.Outcrop
{
    /// <summary>
    ///     露頭コライダに付与する採掘レイキャスト用マーカー
    ///     Mining raycast marker attached to outcrop colliders
    /// </summary>
    public class OutcropRayTarget : MonoBehaviour
    {
        public OutcropGameObject OutcropGameObject { get; private set; }

        public void Initialize(OutcropGameObject outcropGameObject)
        {
            OutcropGameObject = outcropGameObject;
        }
    }
}
