using Client.Game.InGame.Mining;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    [RequireComponent(typeof(Collider))]
    public class MapObjectRayTarget : MonoBehaviour, IMiningRayTarget
    {
        public MapObjectGameObject MapObjectGameObject { get; private set; }

        public IMiningTargetObject MiningTargetObject => MapObjectGameObject;

        // 相互作用可否はコライダー有効/無効で表す。歩行用は別objectで影響なし
        // Interactability is expressed by the collider's enabled state; the walking collider is a separate object
        // 無効化のみの片道にして、後から誰かが有効化し直す順序制約を構造的に消す
        // Disabling only, so no later call can re-enable it and no ordering constraint exists
        public void Initialize(MapObjectGameObject mapObjectGameObject, bool interactable)
        {
            MapObjectGameObject = mapObjectGameObject;
            if (!interactable) GetComponent<Collider>().enabled = false;
        }
    }
}
