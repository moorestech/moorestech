using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.View
{
    // メインカメラの位置を基準点にする。カメラの切り替えは毎回読み直すので購読は要らない
    // Uses the main camera's position as the reference point; it is re-read every time, so camera switches need no subscription
    public sealed class CameraBeltItemViewpoint : IBeltItemViewpoint
    {
        public bool TryGetPosition(out Vector3 position)
        {
            var camera = CameraManager.MainCamera?.Camera;
            if (camera == null)
            {
                position = default;
                return false;
            }
            position = camera.transform.position;
            return true;
        }
    }
}
