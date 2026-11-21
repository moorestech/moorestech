using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Block.GearRotation
{
    /// <summary>
    /// 有効な歯車の回転を毎フレーム1ループで進める
    /// Advances the rotation of every enabled gear in one loop per frame
    /// </summary>
    public sealed class GearRotationTicker : ITickable
    {
        public void Tick()
        {
            GearRotationUpdateList.AdvanceAll(Time.deltaTime);
        }
    }
}
