using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Block.GearRotation.Gpu
{
    /// <summary>
    /// フレームの終わりに、歯車の回転状態の表と時刻をGPUへ送る
    /// Sends the gear rotation table and time to the GPU at the end of each frame
    /// </summary>
    public sealed class GearRotationGpuUploader : ILateTickable
    {
        public void LateTick()
        {
            // CPUで回していたときと同じく、Time.deltaTimeが進む時計(一時停止で止まる)を使う
            // Use the clock that Time.deltaTime advances (stops while paused), as the CPU rotation did
            GearRotationGpuTable.Upload(Time.time);
        }
    }
}
