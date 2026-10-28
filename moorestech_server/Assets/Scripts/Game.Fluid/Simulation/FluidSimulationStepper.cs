using System;
using Core.Master;
using Game.Fluid.Sync.State;

namespace Game.Fluid.Simulation
{
    /// <summary>
    ///     リープフロッグ2フェーズの一括tick処理。n tickの充填率差からn+1/2の面速度を求め、その速度でn+1の内容量を確定する。
    ///     量・速度は FluidUnits の整数。比例縮小は「希望 × 残量 / 希望合計」の整数の掛け割り(切り捨て)で行い、送り側の残量を超えず、面ごとに引く量と足す量が同一値なので質量保存は厳密。
    ///     クランプで流せなかった運動量は速度から取り除く（速度フィードバック）。これにより詰まり時の速度ワインドアップと解消時の非物理的バーストを防ぐ。
    ///     フェーズはノード範囲・面範囲・ポート範囲の関数で、各フェーズは自分の添字の要素だけに書き、前のフェーズの配列だけを読む(gather)。
    ///     そのため範囲をいくつに分けて回しても結果は同じで、ハッシュも範囲ごとの部分和の加算で一致する(ADR 0090。将来のスレッド配分の入口が partitionCount)。
    ///     整数の切り捨てで止まる「動けない量」(FluidSimNetwork.IsImmovable)は tick の最後に 0 にして名を外す(double 版の 1e-9 カットオフに当たる)。流れの計算は量の閾値を見ず、空(名なし)かどうかだけで面を開閉する。
    ///
    ///     Batched two-phase leapfrog tick: fill-rate differences at tick n yield face velocities at n+1/2, which settle amounts at n+1.
    ///     Amounts and velocities are FluidUnits integers. Proportional clamping is the integer multiply-divide "desired * amount / sum" (truncating), never exceeding the giver's amount; each face subtracts and adds the same value, so conservation is exact.
    ///     Momentum that could not flow is removed from the velocity (velocity feedback), preventing wind-up while blocked and unphysical bursts on release.
    ///     Phases are node-range, face-range and port-range functions; each writes only its own index and reads only arrays of earlier phases (gather).
    ///     Hence splitting the ranges into any number of parts gives the same result, and the hash is the sum of per-range partial sums (ADR 0090; partitionCount is the future thread-distribution entry).
    ///     An amount too small to move after integer truncation (FluidSimNetwork.IsImmovable) is cleared with its id at the end of the tick (the counterpart of the double version's 1e-9 cutoff). The flow computation never looks at amount thresholds; faces open or close purely on whether a side is empty (unnamed).
    /// </summary>
    public static class FluidSimulationStepper
    {
        public static void Step(FluidSimNetwork network, FluidSimulationScratch scratch)
        {
            Step(network, scratch, 1);
        }

        // partitionCount で各フェーズの範囲を等分して順に回す。1 なら全範囲を 1 回。結果は分割数に依らない
        // Splits every phase's range into partitionCount equal parts run in sequence; 1 runs the whole range once. The result does not depend on the count
        public static void Step(FluidSimNetwork network, FluidSimulationScratch scratch, int partitionCount)
        {
            if (partitionCount < 1) throw new ArgumentOutOfRangeException(nameof(partitionCount), partitionCount, "partitionCount must be at least 1");

            var nodeCount = network.NodeCount;
            var faceCount = network.FaceCount;
            var portCount = network.Ports.Length;
            var hash = FluidNetworkStateHash.Counts(nodeCount, faceCount, portCount);

            unchecked
            {
                // Phase1: n tickの充填率差 → n+1/2の面速度とポート速度（リープフロッグ）
                // Phase1: fill-rate differences at tick n → face and port velocities at n+1/2 (leapfrog)
                for (var p = 0; p < partitionCount; p++) UpdateFaceVelocities(network, scratch, RangeStart(faceCount, p, partitionCount), RangeStart(faceCount, p + 1, partitionCount));
                for (var p = 0; p < partitionCount; p++) UpdatePortVelocities(network, RangeStart(portCount, p, partitionCount), RangeStart(portCount, p + 1, partitionCount));

                // Phase2: 速度 → 送り側縮小 → 受け側縮小と異種流体の閉面 → 確定流量 → n+1の内容量と配達
                // Phase2: velocities → giver-side scaling → receiver-side scaling and mismatched-fluid closing → settled fluxes → amounts at n+1 and deliveries
                for (var p = 0; p < partitionCount; p++) AccumulateOutflow(network, scratch, RangeStart(nodeCount, p, partitionCount), RangeStart(nodeCount, p + 1, partitionCount));
                for (var p = 0; p < partitionCount; p++) ComputeTentativeFlux(network, scratch, RangeStart(faceCount, p, partitionCount), RangeStart(faceCount, p + 1, partitionCount));
                for (var p = 0; p < partitionCount; p++) ResolveInflow(network, scratch, RangeStart(nodeCount, p, partitionCount), RangeStart(nodeCount, p + 1, partitionCount));
                for (var p = 0; p < partitionCount; p++) hash += SettleFaces(network, scratch, RangeStart(faceCount, p, partitionCount), RangeStart(faceCount, p + 1, partitionCount));
                for (var p = 0; p < partitionCount; p++) ApplyNodeFluxes(network, scratch, RangeStart(nodeCount, p, partitionCount), RangeStart(nodeCount, p + 1, partitionCount));
                for (var p = 0; p < partitionCount; p++) hash += DeliverToBoundaryPorts(network, scratch, RangeStart(nodeCount, p, partitionCount), RangeStart(nodeCount, p + 1, partitionCount));
                for (var p = 0; p < partitionCount; p++) hash += CleanupNodes(network, RangeStart(nodeCount, p, partitionCount), RangeStart(nodeCount, p + 1, partitionCount));
            }

            network.SetLastStateHash(hash);
        }

        // 等分した範囲の part 番目の開始添字(part == partitionCount なら count)
        // Start index of the part-th equal range (count when part == partitionCount)
        private static int RangeStart(int count, int part, int partitionCount)
        {
            return (int)((long)count * part / partitionCount);
        }

        // 面範囲: 両端の充填率差から速度を更新し、向き許可と面上限で希望流量を切る。名の違う流体が向かい合う面は閉面として速度を殺す(空のノードは名が無いので開く)
        // Face range: update velocity from the fill-rate difference of both ends and clamp the desired flux by direction permission and face cap; a face between differently named fluids is closed and its velocity killed (an empty node has no name, so it stays open)
        private static void UpdateFaceVelocities(FluidSimNetwork network, FluidSimulationScratch scratch, int start, int end)
        {
            var amounts = network.NodeAmounts;
            var capacities = network.NodeCapacities;
            var fluidIds = network.NodeFluidIds;
            var velocities = network.FaceVelocities;
            for (var f = start; f < end; f++)
            {
                var a = network.FaceNodeA[f];
                var b = network.FaceNodeB[f];
                if (fluidIds[a] != fluidIds[b] && fluidIds[a] != FluidMaster.EmptyFluidId && fluidIds[b] != FluidMaster.EmptyFluidId)
                {
                    velocities[f] = 0;
                    scratch.FaceDesired[f] = 0;
                    continue;
                }

                // 圧力は充填率差 × 隣接容量の小さい方。容量が異なっても充填率（水位）で釣り合い、容量比の掛け割りは 128 ビットの途中値で正確に求める
                // Pressure is the fill-rate difference times the smaller neighbor capacity; unequal capacities still balance by fill rate, and the capacity-ratio multiply-divide is exact through a 128-bit intermediate
                var gain = FluidPressureMath.Gain(FluidPressureMath.PressureUnits(amounts[a], capacities[a], amounts[b], capacities[b]));
                var velocity = (velocities[f] + gain) * (1000 - FluidSimulationConstants.DampingPerMille) / 1000;
                velocities[f] = velocity;

                // 許可されていない向きは0でクランプする（一方向パイプ対応）
                // Directions that are not allowed clamp to zero (one-way pipe support)
                var cap = network.FaceFlowCapacities[f];
                var min = network.FaceAllowBToA[f] ? -cap : 0;
                var max = network.FaceAllowAToB[f] ? cap : 0;
                scratch.FaceDesired[f] = Math.Clamp(velocity, min, max);
            }
        }

        // ポート範囲: 境界の圧力は0扱い（受け入れ可否はDeliverの結果で表現される）。吸い戻しはしないため0未満にはならない
        // Port range: boundary pressure counts as zero (acceptance shows up in Deliver's result); no suction, so never below zero
        private static void UpdatePortVelocities(FluidSimNetwork network, int start, int end)
        {
            var amounts = network.NodeAmounts;
            for (var p = start; p < end; p++)
            {
                var port = network.Ports[p];
                var amount = amounts[port.NodeIndex];
                var gain = FluidPressureMath.Gain(amount);
                var velocity = (port.Velocity + gain) * (1000 - FluidSimulationConstants.DampingPerMille) / 1000;
                port.Velocity = Math.Max(0, velocity);
            }
        }

        // ノード範囲: 開始時点の量を控え、接続面とポートの流出希望を集める
        // Node range: record the starting amount and gather the desired outflow of the node's faces and ports
        private static void AccumulateOutflow(FluidSimNetwork network, FluidSimulationScratch scratch, int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                scratch.NodeAmountStart[i] = network.NodeAmounts[i];
                long sum = 0;
                for (var k = network.NodeFaceStart[i]; k < network.NodeFaceStart[i + 1]; k++)
                {
                    var f = network.NodeFaces[k];
                    var desired = scratch.FaceDesired[f];
                    if (0 < desired && network.FaceNodeA[f] == i) sum += desired;
                    else if (desired < 0 && network.FaceNodeB[f] == i) sum += -desired;
                }
                for (var k = network.NodePortStart[i]; k < network.NodePortStart[i + 1]; k++)
                {
                    var port = network.Ports[network.NodePorts[k]];
                    sum += Math.Min(port.Velocity, port.FlowCapacityPerTick);
                }
                scratch.NodeOutflowSum[i] = sum;
            }
        }

        // 面範囲: 送り側の残量を超える希望は「希望 × 残量 / 希望合計」で縮める(切り捨てなので合計が残量を超えない)
        // Face range: desires beyond the giver's amount shrink to "desired * amount / sum" (truncation keeps the total within the amount)
        private static void ComputeTentativeFlux(FluidSimNetwork network, FluidSimulationScratch scratch, int start, int end)
        {
            for (var f = start; f < end; f++)
            {
                var desired = scratch.FaceDesired[f];
                if (desired == 0)
                {
                    scratch.FaceTentative[f] = 0;
                    continue;
                }
                var giver = 0 < desired ? network.FaceNodeA[f] : network.FaceNodeB[f];
                scratch.FaceTentative[f] = ScaleByAvailable(desired, scratch.NodeAmountStart[giver], scratch.NodeOutflowSum[giver]);
            }
        }

        // ノード範囲: この tick に受け入れる流体を決め、その流体の流入希望を集める。名があればその流体、空なら番号最小の流入面の流体。別の流体の面は閉面になる
        // Node range: decide the fluid accepted this tick and gather the incoming desire of that fluid; the node keeps its own fluid when named, else takes the fluid of its lowest-index incoming face. Faces of other fluids become closed
        private static void ResolveInflow(FluidSimNetwork network, FluidSimulationScratch scratch, int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                var incoming = network.NodeFluidIds[i];
                long sum = 0;
                for (var k = network.NodeFaceStart[i]; k < network.NodeFaceStart[i + 1]; k++)
                {
                    var f = network.NodeFaces[k];
                    var tentative = scratch.FaceTentative[f];
                    if (tentative == 0) continue;
                    var receiver = 0 < tentative ? network.FaceNodeB[f] : network.FaceNodeA[f];
                    if (receiver != i) continue;
                    var giver = 0 < tentative ? network.FaceNodeA[f] : network.FaceNodeB[f];
                    var giverFluid = network.NodeFluidIds[giver];
                    if (incoming == FluidMaster.EmptyFluidId) incoming = giverFluid;
                    if (giverFluid != incoming) continue;
                    sum += Math.Abs(tentative);
                }
                scratch.NodeInflowSum[i] = sum;
                scratch.NodeIncomingFluid[i] = incoming;
            }
        }

        // 面範囲: 受け側の空き容量で縮めた確定流量を決め、速度を実際に流れた量へ収束させる(非クランプ時は恒等)。受け側が採らなかった流体の面は閉じる。確定した速度をそのまま要素ハッシュへ足す
        // Face range: settle the flux scaled by the receiver's free capacity and converge the velocity onto it (identity when unclamped); a face whose fluid the receiver did not adopt closes. The settled velocity goes straight into the element hash
        private static uint SettleFaces(FluidSimNetwork network, FluidSimulationScratch scratch, int start, int end)
        {
            uint hash = 0;
            unchecked
            {
                for (var f = start; f < end; f++)
                {
                    var tentative = scratch.FaceTentative[f];
                    long settled = 0;
                    if (tentative != 0)
                    {
                        var receiver = 0 < tentative ? network.FaceNodeB[f] : network.FaceNodeA[f];
                        var giver = 0 < tentative ? network.FaceNodeA[f] : network.FaceNodeB[f];
                        if (network.NodeFluidIds[giver] == scratch.NodeIncomingFluid[receiver])
                        {
                            var free = Math.Max(0, network.NodeCapacities[receiver] - scratch.NodeAmountStart[receiver]);
                            settled = ScaleByAvailable(tentative, free, scratch.NodeInflowSum[receiver]);
                        }
                    }
                    scratch.FaceSettled[f] = settled;
                    network.FaceVelocities[f] = settled;
                    hash += FluidNetworkStateHash.FaceElement(f, settled);
                }
            }
            return hash;
        }

        // ノード範囲: 接続面の確定流量を集めて量を更新する(正準側 A は引き、B は足す。同じ値なので厳密保存)。流入があったノードは採った流体の名になる(空は引き継ぎ、同じ流体なら不変)
        // Node range: gather the settled fluxes of the node's faces into its amount (canonical side A subtracts, B adds the same value, so conservation is exact). A node with inflow takes the accepted fluid id (empty adopts, the same fluid is unchanged)
        private static void ApplyNodeFluxes(FluidSimNetwork network, FluidSimulationScratch scratch, int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                long delta = 0;
                long inflow = 0;
                for (var k = network.NodeFaceStart[i]; k < network.NodeFaceStart[i + 1]; k++)
                {
                    var f = network.NodeFaces[k];
                    var contribution = network.FaceNodeA[f] == i ? -scratch.FaceSettled[f] : scratch.FaceSettled[f];
                    delta += contribution;
                    if (0 < contribution) inflow += contribution;
                }
                if (0 < inflow) network.NodeFluidIds[i] = scratch.NodeIncomingFluid[i];
                network.NodeAmounts[i] += delta;
            }
        }

        // ノード範囲: そのノードのポートを番号順に配達する。境界が受け取らなかった分だけ流量を縮め、ノードから確定分のみを減らす。同じノードのポート同士は順に引くので範囲を分けても衝突しない
        // Node range: deliver through the node's ports in index order, shrinking the flux by what the boundary rejected and subtracting only the settled amount; ports of one node are handled in sequence, so split ranges never collide
        private static uint DeliverToBoundaryPorts(FluidSimNetwork network, FluidSimulationScratch scratch, int start, int end)
        {
            uint hash = 0;
            unchecked
            {
                for (var i = start; i < end; i++)
                {
                    for (var k = network.NodePortStart[i]; k < network.NodePortStart[i + 1]; k++)
                    {
                        var portIndex = network.NodePorts[k];
                        var port = network.Ports[portIndex];
                        var desired = Math.Min(port.Velocity, port.FlowCapacityPerTick);
                        var attempted = ScaleByAvailable(desired, scratch.NodeAmountStart[i], scratch.NodeOutflowSum[i]);

                        long actual = 0;
                        if (0 < attempted && network.NodeFluidIds[i] != FluidMaster.EmptyFluidId)
                        {
                            actual = Math.Clamp(port.Deliver(attempted, network.NodeFluidIds[i]), 0, attempted);
                            network.NodeAmounts[i] -= actual;
                        }

                        port.Velocity = actual;
                        hash += FluidNetworkStateHash.PortElement(portIndex, actual);
                    }
                }
            }
            return hash;
        }

        // ノード範囲: 動けない量しか無いノードを 0 にして流体IDを解除し、ノードの要素ハッシュを足す。流れている量は閾値を超えるので触らない
        // Node range: clear nodes holding only an immovable amount with their fluid id and add the node element hash; an amount in flow exceeds the threshold and is left alone
        private static uint CleanupNodes(FluidSimNetwork network, int start, int end)
        {
            uint hash = 0;
            unchecked
            {
                for (var i = start; i < end; i++)
                {
                    network.ClearIfImmovable(i);
                    hash += FluidNetworkStateHash.NodeElement(i, network.NodeAmounts[i], network.NodeFluidIds[i].AsPrimitive());
                }
            }
            return hash;
        }

        // 希望合計が利用可能量を超えるときだけ「希望 × 利用可能量 / 希望合計」で縮める。符号は希望に従い、切り捨ては 0 へ向く。積は 128 ビットの途中値で溢れない
        // Shrinks to "desired * available / sum" only when the total desire exceeds what is available; the sign follows the desire and truncation goes toward zero. The product never overflows thanks to the 128-bit intermediate
        private static long ScaleByAvailable(long desired, long available, long sum)
        {
            if (sum <= available) return desired;
            if (sum <= 0 || available <= 0) return 0;
            return FluidMulDiv.MulDiv(desired, available, sum);
        }
    }
}
