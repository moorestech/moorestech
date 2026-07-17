using System;
using Core.Master;
using UnityEngine;

namespace Game.Fluid.Simulation
{
    /// <summary>
    ///     流体シミュレーションの1ノード（パイプ1本分の流体状態）。内容量と流体IDを保持し、FluidSimFaceがノード間を結ぶ。
    ///     サーバー・クライアントで同一計算を行う前提のため、Unityエンジン機能や乱数・時刻に依存しない純粋なデータとして扱う。
    ///
    ///     One node of the fluid simulation (fluid state of a single pipe). Holds amount and fluid id; FluidSimFace links nodes.
    ///     Designed to run identically on server and client, so it stays pure data with no engine, RNG, or wall-clock dependency.
    /// </summary>
    public class FluidSimNode
    {
        // 決定論的な走査順を保証するためのソートキー
        // Sort key guaranteeing a deterministic iteration order
        public readonly Vector3Int Position;
        public readonly double Capacity;

        public double Amount;
        public FluidId FluidId;

        // Stepperが1tick内でのみ使う一時集計値
        // Per-tick scratch values used only inside the stepper
        internal double OutflowSum;
        internal double InflowSum;
        internal double OutflowScale;
        internal double InflowScale;

        public double FillRate => Amount / Capacity;

        public FluidSimNode(Vector3Int position, double capacity)
        {
            Position = position;
            Capacity = capacity;
            FluidId = FluidMaster.EmptyFluidId;
        }

        // 境界（機械のpush等）からの流入。流体ID整合と空き容量でクランプし、受け取れなかった残りを返す
        // External inflow from boundaries (machine push etc.); clamp by fluid-id match and free capacity, return the remainder
        public FluidStack AddExternal(FluidStack fluidStack)
        {
            if (fluidStack.Amount <= 0) return fluidStack;

            // 異種流体は拒否（空ノードは受け入れて流体IDを引き継ぐ）
            // Reject mismatched fluids; an empty node accepts and adopts the incoming id
            var hasFluid = Amount > FluidSimulationConstants.AmountEpsilon;
            if (hasFluid && FluidId != fluidStack.FluidId) return fluidStack;
            if (!hasFluid) FluidId = fluidStack.FluidId;

            var accepted = Math.Min(fluidStack.Amount, Capacity - Amount);
            if (accepted <= 0) return fluidStack;

            Amount += accepted;
            return new FluidStack(fluidStack.Amount - accepted, fluidStack.FluidId);
        }

        // 空になったら流体IDを解除する
        // Release the fluid id once the node is empty
        internal void CleanupIfEmpty()
        {
            if (Amount > FluidSimulationConstants.AmountEpsilon) return;
            Amount = 0;
            FluidId = FluidMaster.EmptyFluidId;
        }
    }
}
