using System;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.Player
{
    public class PlayerGrabItemManager : MonoBehaviour
    {
        // 手持ちアイテムの差し替え通知。新しいRendererが増えるため自機の表示状態を再適用する購読者がいる
        // Fires when the grab item is swapped; subscribers re-apply the player model visibility to the new renderers
        public IObservable<Unit> OnGrabItemChanged => _onGrabItemChanged;
        private readonly Subject<Unit> _onGrabItemChanged = new();

        [SerializeField] private Transform leftHandParent;
        [SerializeField] private Transform rightHandParent;

        public void SetItem(GameObject item, bool isLeft, Vector3 position = default, Quaternion rotation = default)
        {
            var parent = isLeft ? leftHandParent : rightHandParent;
            item.transform.SetParent(parent);
            item.transform.localPosition = position;
            item.transform.localRotation = rotation;

            _onGrabItemChanged.OnNext(Unit.Default);
        }
    }
}
