using System.Collections.Generic;
using Core.Master;
using Game.Fluid;
using Game.Fluid.Simulation;
using UnityEngine;

namespace Game.Block.Blocks.Fluid
{
    // liveパイプ登録・セーブ用面速度収集・機械からの流入記録と、パイプが自分の中身を読む網を公開する。dirty再構築はtick末尾で具象datastore経由（MasterTickUpdater）
    // Exposes live pipe registration, face-velocity collection for saving, machine-inflow recording and the network pipes read their contents from; the dirty rebuild runs at tick end via the concrete datastore (MasterTickUpdater)
    public interface IFluidNetworkDatastore
    {
        // 現在の網。パイプは自分の番号でここから中身を読み書きする。再構築で別のインスタンスに置き換わる
        // The current network; pipes read and write their contents here by index, and a rebuild swaps in a new instance
        FluidSimNetwork Network { get; }
        void AddPipe(FluidPipeComponent pipe);
        void RemovePipe(FluidPipeComponent pipe);
        void CollectOwnedFaceVelocities(FluidPipeComponent pipe, List<(Vector3Int direction, double velocity)> buffer);

        // 機械がパイプへ入れた量を網へ足し、受け入れた量をそのtickの差分に残す。受け入れられなかった残りを返す
        // Add what a machine pushed into a pipe to the network, keep the accepted amount in this tick's diff, and return the remainder
        FluidStack AddInflow(FluidPipeComponent pipe, FluidStack fluidStack);
    }
}
