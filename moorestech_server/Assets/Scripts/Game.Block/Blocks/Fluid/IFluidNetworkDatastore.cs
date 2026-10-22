using System.Collections.Generic;
using Core.Master;
using UnityEngine;

namespace Game.Block.Blocks.Fluid
{
    // liveパイプ登録・セーブ用面速度収集・機械からの流入記録を公開する。dirty再構築はtick末尾で具象datastore経由（MasterTickUpdater）
    // Exposes live pipe registration, face-velocity collection for saving and machine-inflow recording; the dirty rebuild runs at tick end via the concrete datastore (MasterTickUpdater)
    public interface IFluidNetworkDatastore
    {
        void AddPipe(FluidPipeComponent pipe);
        void RemovePipe(FluidPipeComponent pipe);
        void CollectOwnedFaceVelocities(FluidPipeComponent pipe, List<(Vector3Int direction, double velocity)> buffer);

        // 機械がパイプへ入れてノードが受け入れた量を、そのtickの差分に残す
        // Keep the amount a machine pushed into a pipe and the node accepted in this tick's diff
        void RecordInflow(FluidPipeComponent pipe, FluidId fluidId, double acceptedAmount);
    }
}
