using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.Fluid.Simulation
{
    /// <summary>
    ///     リープフロッグ2フェーズの一括tick処理。n tickの充填率差からn+1/2の面速度を求め、その速度でn+1の内容量を確定する。
    ///     速度→流量の変換時に「送り側残量・受け側空き容量・面上限」で比例クランプし、面ごとに引く量と足す量を同一値にすることで質量保存を厳密に守る。
    ///     クランプで流せなかった運動量は速度から取り除く（速度フィードバック）。これにより詰まり時の速度ワインドアップと解消時の非物理的バーストを防ぐ。
    ///     全面の速度更新→全面の流量適用の2段一括処理のため、結果は面の登録順に依存しない（浮動小数点の丸めを除く）。
    ///
    ///     Batched two-phase leapfrog tick: fill-rate differences at tick n yield face velocities at n+1/2, which settle amounts at n+1.
    ///     Fluxes are proportionally clamped by giver amount, receiver free capacity and per-face cap; each face subtracts and adds the same value, keeping mass conservation exact.
    ///     Momentum that could not flow is removed from the velocity (velocity feedback), preventing wind-up while blocked and unphysical bursts on release.
    ///     Velocities update for all faces before any flux applies, so results do not depend on face registration order (up to floating-point rounding).
    /// </summary>
    public static class FluidSimulationStepper
    {
        public static void Step(IReadOnlyList<FluidSimNode> nodes, IReadOnlyList<FluidSimFace> faces, IReadOnlyList<IFluidBoundaryPort> boundaryPorts)
        {
            ResetScratch();

            // Phase1: n tickの充填率差 → n+1/2の面速度（リープフロッグ）
            // Phase1: fill-rate differences at tick n → face velocities at n+1/2 (leapfrog)
            UpdateFaceVelocities();
            UpdateBoundaryPortVelocities();

            // Phase2: n+1/2の速度 → クランプ済み流量 → n+1の内容量
            // Phase2: velocities at n+1/2 → clamped fluxes → amounts at n+1
            AccumulateOutflow();
            ScaleOutflowAndAccumulateInflow();
            ApplyFaceFluxes();
            DeliverToBoundaryPorts();

            CleanupNodes();

            #region Internal

            void ResetScratch()
            {
                foreach (var node in nodes)
                {
                    node.OutflowSum = 0;
                    node.InflowSum = 0;
                    node.OutflowScale = 1;
                    node.InflowScale = 1;
                }
            }

            void UpdateFaceVelocities()
            {
                foreach (var face in faces)
                {
                    // 異種流体が向かい合う面は閉面として速度を殺す
                    // A face between mismatched fluids is closed and its velocity killed
                    if (IsClosedFace(face))
                    {
                        face.Velocity = 0;
                        continue;
                    }

                    // 圧力スケールは隣接容量の小さい方。容量が異なっても充填率（水位）で釣り合う
                    // Pressure scale is the smaller neighbor capacity; unequal capacities still balance by fill rate (water level)
                    var pressureScale = Math.Min(face.NodeA.Capacity, face.NodeB.Capacity);
                    var pressureDelta = face.NodeA.FillRate - face.NodeB.FillRate;
                    face.Velocity = (face.Velocity + FluidSimulationConstants.PressureGain * pressureDelta * pressureScale) * (1 - FluidSimulationConstants.Damping);
                }
            }

            void UpdateBoundaryPortVelocities()
            {
                foreach (var port in boundaryPorts)
                {
                    // 境界の圧力は0扱い（受け入れ可否はDeliverの残量で表現される）。吸い戻しはしないため0未満にはならない
                    // Boundary pressure counts as zero (acceptance shows up as Deliver's remainder); no suction, so never below zero
                    var node = port.PipeNode;
                    var velocity = (port.Velocity + FluidSimulationConstants.PressureGain * node.FillRate * node.Capacity) * (1 - FluidSimulationConstants.Damping);
                    port.Velocity = Math.Max(0, velocity);
                }
            }

            void AccumulateOutflow()
            {
                // 各ノードの流出希望合計を集計し、残量を超える場合の比例縮小率を決める
                // Sum each node's desired outflow and derive the proportional scale when it exceeds the available amount
                foreach (var face in faces)
                {
                    var desired = ClampByFaceCap(face);
                    if (desired > 0) face.NodeA.OutflowSum += desired;
                    else face.NodeB.OutflowSum += -desired;
                }

                foreach (var port in boundaryPorts)
                {
                    port.PipeNode.OutflowSum += Math.Min(port.Velocity, port.FlowCapacityPerTick);
                }

                foreach (var node in nodes)
                {
                    if (node.OutflowSum > node.Amount)
                    {
                        node.OutflowScale = node.OutflowSum > 0 ? node.Amount / node.OutflowSum : 0;
                    }
                }
            }

            void ScaleOutflowAndAccumulateInflow()
            {
                // 送り側縮小後の暫定流量を確定し、受け側の空き容量による縮小率を決める
                // Fix tentative fluxes after giver-side scaling, then derive receiver-side scales from free capacity
                foreach (var face in faces)
                {
                    var desired = ClampByFaceCap(face);
                    var giver = desired > 0 ? face.NodeA : face.NodeB;
                    var receiver = desired > 0 ? face.NodeB : face.NodeA;
                    face.TentativeFlux = desired * giver.OutflowScale;
                    receiver.InflowSum += Math.Abs(face.TentativeFlux);
                }

                foreach (var node in nodes)
                {
                    var freeCapacity = Math.Max(0, node.Capacity - node.Amount);
                    if (node.InflowSum > freeCapacity)
                    {
                        node.InflowScale = node.InflowSum > 0 ? freeCapacity / node.InflowSum : 0;
                    }
                }
            }

            void ApplyFaceFluxes()
            {
                foreach (var face in faces)
                {
                    var tentative = face.TentativeFlux;
                    var giver = tentative >= 0 ? face.NodeA : face.NodeB;
                    var receiver = tentative >= 0 ? face.NodeB : face.NodeA;

                    // 同tick内で先行する面が受け側に別流体を入れた場合は閉面扱いにする
                    // Treat as closed when an earlier face already filled the receiver with a different fluid this tick
                    var receiverHasFluid = receiver.Amount > FluidSimulationConstants.AmountEpsilon;
                    if (receiverHasFluid && receiver.FluidId != giver.FluidId && Math.Abs(tentative) > 0)
                    {
                        face.Velocity = 0;
                        continue;
                    }

                    // 受け側空き容量の縮小を掛けた確定流量を、両ノードへ同一値で適用する（厳密保存）
                    // Apply the settled flux, scaled by receiver free capacity, with one shared value on both nodes (exact conservation)
                    var settled = tentative * receiver.InflowScale;
                    face.NodeA.Amount -= settled;
                    face.NodeB.Amount += settled;

                    if (Math.Abs(settled) > FluidSimulationConstants.AmountEpsilon && !receiverHasFluid)
                    {
                        receiver.FluidId = giver.FluidId;
                    }

                    // 速度フィードバック: 実際に流れた量へ速度を収束させる（非クランプ時は恒等）
                    // Velocity feedback: converge velocity onto the settled flux (identity when unclamped)
                    face.Velocity = settled;
                }
            }

            void DeliverToBoundaryPorts()
            {
                foreach (var port in boundaryPorts)
                {
                    var node = port.PipeNode;
                    var desired = Math.Min(port.Velocity, port.FlowCapacityPerTick);
                    var attempted = desired * node.OutflowScale;

                    var actual = 0.0;
                    if (attempted > FluidSimulationConstants.AmountEpsilon && node.FluidId != FluidMaster.EmptyFluidId)
                    {
                        // 境界が受け取らなかった残量ぶんだけ流量を縮め、ノードから確定分のみを減らす
                        // Shrink the flux by whatever the boundary rejected and subtract only the settled amount from the node
                        var remain = port.Deliver(new FluidStack(attempted, node.FluidId));
                        actual = attempted - remain.Amount;
                        node.Amount -= actual;
                    }

                    port.Velocity = actual;
                }
            }

            void CleanupNodes()
            {
                foreach (var node in nodes)
                {
                    node.CleanupIfEmpty();
                }
            }

            double ClampByFaceCap(FluidSimFace face)
            {
                // 許可されていない向きは0でクランプする（一方向パイプ対応）
                // Directions that are not allowed clamp to zero (one-way pipe support)
                var min = face.AllowBToA ? -face.FlowCapacityPerTick : 0;
                var max = face.AllowAToB ? face.FlowCapacityPerTick : 0;
                return Math.Clamp(face.Velocity, min, max);
            }

            bool IsClosedFace(FluidSimFace face)
            {
                var aHasFluid = face.NodeA.Amount > FluidSimulationConstants.AmountEpsilon;
                var bHasFluid = face.NodeB.Amount > FluidSimulationConstants.AmountEpsilon;
                return aHasFluid && bHasFluid && face.NodeA.FluidId != face.NodeB.FluidId;
            }

            #endregion
        }
    }
}
