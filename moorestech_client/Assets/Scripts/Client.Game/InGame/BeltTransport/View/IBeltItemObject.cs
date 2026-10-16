using UnityEngine;

namespace Client.Game.InGame.BeltTransport.View
{
    // ベルト上のアイテム1個の表示物。位置を置くことと消すことだけができる
    // The displayed object of one belt item; it can only be positioned and destroyed
    public interface IBeltItemObject
    {
        void SetPosition(Vector3 position);
        void Destroy();
    }
}
