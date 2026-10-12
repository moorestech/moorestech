using UnityEngine;

namespace Client.Game.InGame.BeltTransport.View
{
    // 置いたモデルのGameObjectをそのまま表示物にする
    // Uses the placed model GameObject directly as the displayed object
    public sealed class GameObjectBeltItemObject : IBeltItemObject
    {
        private readonly GameObject _gameObject;

        public GameObjectBeltItemObject(GameObject gameObject)
        {
            _gameObject = gameObject;
        }

        public void SetPosition(Vector3 position)
        {
            _gameObject.transform.position = position;
        }

        public void Destroy()
        {
            Object.Destroy(_gameObject);
        }
    }
}
