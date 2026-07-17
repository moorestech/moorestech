using System.Collections.Generic;
using UnityEngine;

namespace Game.Block.Blocks.Fluid
{
    // liveパイプ登録とセーブ用面速度収集を公開する。dirty再構築はtick先頭で具象datastore経由（MasterTickUpdater）
    // Exposes live pipe registration and face-velocity collection for saving; the dirty rebuild runs at tick head via the concrete datastore (MasterTickUpdater)
    public interface IFluidNetworkDatastore
    {
        void AddPipe(FluidPipeComponent pipe);
        void RemovePipe(FluidPipeComponent pipe);
        void CollectOwnedFaceVelocities(FluidPipeComponent pipe, List<(Vector3Int direction, double velocity)> buffer);
    }
}
