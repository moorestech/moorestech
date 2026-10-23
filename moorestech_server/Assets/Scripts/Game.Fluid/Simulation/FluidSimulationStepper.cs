using System;
using Core.Master;
using Game.Fluid.Sync.State;

namespace Game.Fluid.Simulation
{
    /// <summary>
    ///     リープフロッグ2フェーズの一括tick処理。n tickの充填率差からn+1/2の面速度を求め、その速度でn+1の内容量を確定する。
    ///     速度→流量の変換時に「送り側残量・受け側空き容量・面上限」で比例クランプし、面ごとに引く量と足す量を同一値にすることで質量保存を厳密に守る。
    ///     クランプで流せなかった運動量は速度から取り除く（速度フィードバック）。これにより詰まり時の速度ワインドアップと解消時の非物理的バーストを防ぐ。
    ///     全面の速度更新→全面の流量適用の2段一括処理のため、結果は面の登録順に依存しない（浮動小数点の丸めを除く）。
    ///     状態は網の配列を番号でなめ、最終パスで要素ハッシュを足して網へ残す(ハッシュのための別走査を持たない)。
    ///
    ///     Batched two-phase leapfrog tick: fill-rate differences at tick n yield face velocities at n+1/2, which settle amounts at n+1.
    ///     Fluxes are proportionally clamped by giver amount, receiver free capacity and per-face cap; each face subtracts and adds the same value, keeping mass conservation exact.
    ///     Momentum that could not flow is removed from the velocity (velocity feedback), preventing wind-up while blocked and unphysical bursts on release.
    ///     Velocities update for all faces before any flux applies, so results do not depend on face registration order (up to floating-point rounding).
    ///     State is walked by index over the network arrays, and the final passes fold the element hash into the network (no separate hashing walk).
    /// </summary>
    public static class FluidSimulationStepper
    {
        public static void Step(FluidSimNetwork network, FluidSimulationScratch scratch)
        {
            var amounts = network.NodeAmounts;
            var capacities = network.NodeCapacities;
            var fluidIds = network.NodeFluidIds;
            var faceA = network.FaceNodeA;
            var faceB = network.FaceNodeB;
            var faceCaps = network.FaceFlowCapacities;
            var velocities = network.FaceVelocities;
            var allowAToB = network.FaceAllowAToB;
            var allowBToA = network.FaceAllowBToA;
            var ports = network.Ports;
            var outflowSum = scratch.OutflowSum;
            var inflowSum = scratch.InflowSum;
            var outflowScale = scratch.OutflowScale;
            var inflowScale = scratch.InflowScale;
            var tentativeFlux = scratch.TentativeFlux;
            var hash = FluidNetworkStateHash.Counts(amounts.Length, faceA.Length, ports.Length);

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
            network.SetLastStateHash(hash);

            #region Internal

            void ResetScratch()
            {
                for (var i = 0; i < amounts.Length; i++)
                {
                    outflowSum[i] = 0;
                    inflowSum[i] = 0;
                    outflowScale[i] = 1;
                    inflowScale[i] = 1;
                }
            }

            void UpdateFaceVelocities()
            {
                for (var f = 0; f < faceA.Length; f++)
                {
                    var a = faceA[f];
                    var b = faceB[f];

                    // 異種流体が向かい合う面は閉面として速度を殺す
                    // A face between mismatched fluids is closed and its velocity killed
                    if (IsClosedFace(a, b))
                    {
                        velocities[f] = 0;
                        continue;
                    }

                    // 圧力スケールは隣接容量の小さい方。容量が異なっても充填率（水位）で釣り合う
                    // Pressure scale is the smaller neighbor capacity; unequal capacities still balance by fill rate (water level)
                    var pressureScale = Math.Min(capacities[a], capacities[b]);
                    var pressureDelta = amounts[a] / capacities[a] - amounts[b] / capacities[b];
                    velocities[f] = (velocities[f] + FluidSimulationConstants.PressureGain * pressureDelta * pressureScale) * (1 - FluidSimulationConstants.Damping);
                }
            }

            void UpdateBoundaryPortVelocities()
            {
                foreach (var port in ports)
                {
                    // 境界の圧力は0扱い（受け入れ可否はDeliverの残量で表現される）。吸い戻しはしないため0未満にはならない
                    // Boundary pressure counts as zero (acceptance shows up as Deliver's remainder); no suction, so never below zero
                    var node = port.NodeIndex;
                    var velocity = (port.Velocity + FluidSimulationConstants.PressureGain * amounts[node]) * (1 - FluidSimulationConstants.Damping);
                    port.Velocity = Math.Max(0, velocity);
                }
            }

            void AccumulateOutflow()
            {
                // 各ノードの流出希望合計を集計し、残量を超える場合の比例縮小率を決める
                // Sum each node's desired outflow and derive the proportional scale when it exceeds the available amount
                for (var f = 0; f < faceA.Length; f++)
                {
                    var desired = ClampByFaceCap(f);
                    if (0 < desired) outflowSum[faceA[f]] += desired;
                    else outflowSum[faceB[f]] += -desired;
                }

                foreach (var port in ports)
                {
                    outflowSum[port.NodeIndex] += Math.Min(port.Velocity, port.FlowCapacityPerTick);
                }

                for (var i = 0; i < amounts.Length; i++)
                {
                    if (amounts[i] < outflowSum[i])
                    {
                        outflowScale[i] = 0 < outflowSum[i] ? amounts[i] / outflowSum[i] : 0;
                    }
                }
            }

            void ScaleOutflowAndAccumulateInflow()
            {
                // 送り側縮小後の暫定流量を確定し、受け側の空き容量による縮小率を決める
                // Fix tentative fluxes after giver-side scaling, then derive receiver-side scales from free capacity
                for (var f = 0; f < faceA.Length; f++)
                {
                    var desired = ClampByFaceCap(f);
                    var giver = 0 < desired ? faceA[f] : faceB[f];
                    var receiver = 0 < desired ? faceB[f] : faceA[f];
                    tentativeFlux[f] = desired * outflowScale[giver];
                    inflowSum[receiver] += Math.Abs(tentativeFlux[f]);
                }

                for (var i = 0; i < amounts.Length; i++)
                {
                    var freeCapacity = Math.Max(0, capacities[i] - amounts[i]);
                    if (freeCapacity < inflowSum[i])
                    {
                        inflowScale[i] = 0 < inflowSum[i] ? freeCapacity / inflowSum[i] : 0;
                    }
                }
            }

            void ApplyFaceFluxes()
            {
                for (var f = 0; f < faceA.Length; f++)
                {
                    var tentative = tentativeFlux[f];
                    var giver = 0 <= tentative ? faceA[f] : faceB[f];
                    var receiver = 0 <= tentative ? faceB[f] : faceA[f];

                    // 同tick内で先行する面が受け側に別流体を入れた場合は閉面扱いにする
                    // Treat as closed when an earlier face already filled the receiver with a different fluid this tick
                    var receiverHasFluid = FluidSimulationConstants.AmountEpsilon < amounts[receiver];
                    if (receiverHasFluid && fluidIds[receiver] != fluidIds[giver] && 0 < Math.Abs(tentative))
                    {
                        velocities[f] = 0;
                        hash += FluidNetworkStateHash.FaceElement(f, 0);
                        continue;
                    }

                    // 受け側空き容量の縮小を掛けた確定流量を、両ノードへ同一値で適用する（厳密保存）
                    // Apply the settled flux, scaled by receiver free capacity, with one shared value on both nodes (exact conservation)
                    var settled = tentative * inflowScale[receiver];
                    amounts[faceA[f]] -= settled;
                    amounts[faceB[f]] += settled;

                    if (FluidSimulationConstants.AmountEpsilon < Math.Abs(settled) && !receiverHasFluid)
                    {
                        fluidIds[receiver] = fluidIds[giver];
                    }

                    // 速度フィードバック: 実際に流れた量へ速度を収束させる（非クランプ時は恒等）。確定した速度をそのまま要素ハッシュへ足す
                    // Velocity feedback: converge velocity onto the settled flux (identity when unclamped); the settled velocity goes straight into the element hash
                    velocities[f] = settled;
                    hash += FluidNetworkStateHash.FaceElement(f, settled);
                }
            }

            void DeliverToBoundaryPorts()
            {
                for (var p = 0; p < ports.Length; p++)
                {
                    var port = ports[p];
                    var node = port.NodeIndex;
                    var desired = Math.Min(port.Velocity, port.FlowCapacityPerTick);
                    var attempted = desired * outflowScale[node];

                    var actual = 0.0;
                    if (FluidSimulationConstants.AmountEpsilon < attempted && fluidIds[node] != FluidMaster.EmptyFluidId)
                    {
                        // 境界が受け取らなかった残量ぶんだけ流量を縮め、ノードから確定分のみを減らす
                        // Shrink the flux by whatever the boundary rejected and subtract only the settled amount from the node
                        var remain = port.Deliver(new FluidStack(attempted, fluidIds[node]));
                        actual = attempted - remain.Amount;
                        amounts[node] -= actual;
                    }

                    port.Velocity = actual;
                    hash += FluidNetworkStateHash.PortElement(p, actual);
                }
            }

            void CleanupNodes()
            {
                for (var i = 0; i < amounts.Length; i++)
                {
                    network.CleanupIfEmpty(i);
                    hash += FluidNetworkStateHash.NodeElement(i, amounts[i], fluidIds[i].AsPrimitive());
                }
            }

            double ClampByFaceCap(int f)
            {
                // 許可されていない向きは0でクランプする（一方向パイプ対応）
                // Directions that are not allowed clamp to zero (one-way pipe support)
                var min = allowBToA[f] ? -faceCaps[f] : 0;
                var max = allowAToB[f] ? faceCaps[f] : 0;
                return Math.Clamp(velocities[f], min, max);
            }

            bool IsClosedFace(int a, int b)
            {
                var aHasFluid = FluidSimulationConstants.AmountEpsilon < amounts[a];
                var bHasFluid = FluidSimulationConstants.AmountEpsilon < amounts[b];
                return aHasFluid && bHasFluid && fluidIds[a] != fluidIds[b];
            }

            #endregion
        }
    }
}
