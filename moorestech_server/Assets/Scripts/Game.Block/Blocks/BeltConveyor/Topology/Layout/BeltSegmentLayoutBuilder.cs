using System;
using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Topology.Layout
{
    // マス一覧からsegmentの構成を作る純粋な計算。2入力以上のマスは合流、分配器は接続数によらず分岐、それ以外は最大限まとめた通常
    // Pure computation from the cell list to segment layouts: cells with two or more inputs become merges, splitters always branches, the rest maximal normals
    // 機械・分岐bufferから合流への入力には、間に表示しない1マス分の内部segmentを置く。合流buffer・通常segmentからの入力には置かない
    // Inputs into a merge from a machine or a branch buffer get a hidden one-cell internal segment in between; merge buffers and normal segments connect directly
    public static class BeltSegmentLayoutBuilder
    {
        public static BeltSegmentLayout[] Build(List<BeltTopologyCell> cells)
        {
            var cellIndexByBlock = new Dictionary<BlockInstanceId, int>(cells.Count);
            for (var i = 0; i < cells.Count; i++) cellIndexByBlock.Add(cells[i].BlockInstanceId, i);
            var chains = BeltSegmentChainWalker.Walk(cells, cellIndexByBlock);

            // 連鎖の順に番号を振り、合流の直後にその内部segmentを入力方向順で続ける
            // Number chains in order, following each merge immediately with its internal segments in input direction order
            var segmentIndexOfCell = new int[cells.Count];
            var internalIndexOfInput = new Dictionary<int, int>();
            var nextIndex = 0;
            foreach (var chain in chains)
            {
                foreach (var cellIndex in chain) segmentIndexOfCell[cellIndex] = nextIndex;
                nextIndex++;
                var last = chain[chain.Length - 1];
                if (!IsMerge(cells[last]) || cells[last].IsSplitter) continue;
                for (var i = 0; i < cells[last].Inputs.Length; i++)
                    if (NeedsInternal(cells[last].Inputs[i])) internalIndexOfInput.Add(InputKey(last, i), nextIndex++);
            }

            // 番号が確定してから接続を解決し、構成を番号順に並べる
            // Resolve links once every index is known, then list the layouts in index order
            var layouts = new BeltSegmentLayout[nextIndex];
            foreach (var chain in chains)
            {
                var layout = CreateChainLayout(chain);
                layouts[layout.Index] = layout;
                if (layout.Kind != BeltSegmentKind.Merge) continue;
                for (var i = 0; i < layout.Inputs.Length; i++)
                    if (NeedsInternal(layout.Inputs[i].Connection)) layouts[layout.Inputs[i].PartnerSegmentIndex] = CreateInternalLayout(layout, layout.Inputs[i]);
            }
            return layouts;

            #region Internal

            BeltSegmentLayout CreateChainLayout(int[] chain)
            {
                var head = cells[chain[0]];
                var last = cells[chain[chain.Length - 1]];
                var kind = KindOf(chain, last);
                var chainCells = new BeltTopologyCell[chain.Length];
                for (var i = 0; i < chain.Length; i++) chainCells[i] = cells[chain[i]];
                return new BeltSegmentLayout(segmentIndexOfCell[chain[0]], kind, false, chainCells, last.BeltSpeedPerTick, last.Forward,
                    ResolveInputs(chain[0], head, kind), ResolveOutputs(last, kind));
            }

            BeltSegmentKind KindOf(int[] chain, BeltTopologyCell last)
            {
                // 分配器に2入力以上はマスタ上ありえない。起きたら分岐として扱い、入力は捨てる
                // A splitter with two or more inputs cannot exist per master; if it does, treat it as a branch and drop its inputs
                if (last.IsSplitter && IsMerge(last))
                    Debug.LogError($"[BeltSegmentLayout] Splitter {last.BlockInstanceId}@{last.Position} has {last.Inputs.Length} inputs; treating it as a branch without inputs.");
                if (last.IsSplitter) return BeltSegmentKind.Branch;
                return chain.Length == 1 && IsMerge(last) ? BeltSegmentKind.Merge : BeltSegmentKind.Normal;
            }

            BeltSegmentLayoutLink[] ResolveInputs(int headIndex, BeltTopologyCell head, BeltSegmentKind kind)
            {
                if (kind != BeltSegmentKind.Merge && head.Inputs.Length != 1) return Array.Empty<BeltSegmentLayoutLink>();
                var links = new BeltSegmentLayoutLink[head.Inputs.Length];
                for (var i = 0; i < links.Length; i++)
                {
                    ref readonly var input = ref head.Inputs[i];
                    var partner = kind == BeltSegmentKind.Merge && NeedsInternal(input) ? internalIndexOfInput[InputKey(headIndex, i)] : PartnerIndex(input);
                    links[i] = new BeltSegmentLayoutLink(input.Direction, input.EntryDirection, partner, input);
                }
                return links;
            }

            BeltSegmentLayoutLink[] ResolveOutputs(BeltTopologyCell last, BeltSegmentKind kind)
            {
                // 通常・合流の出力は1本まで。辺ごとの接続解決が保証するので、2本以上は異常として先頭だけ残す
                // Normal and merge outputs are at most one; edge resolution guarantees it, so more than one is an anomaly and only the first is kept
                var count = last.Outputs.Length;
                if (kind != BeltSegmentKind.Branch && 1 < count)
                {
                    Debug.LogError($"[BeltSegmentLayout] {kind} cell {last.BlockInstanceId}@{last.Position} has {count} outputs; keeping only the first.");
                    count = 1;
                }
                var links = new BeltSegmentLayoutLink[count];
                for (var i = 0; i < count; i++)
                {
                    ref readonly var output = ref last.Outputs[i];
                    var partner = BeltSegmentLayoutLink.Machine;
                    if (output.PartnerKind == BeltTopologyPartnerKind.Belt)
                    {
                        // 分岐bufferから合流へ出すときは、合流が持つ内部segmentへつなぐ
                        // A branch buffer feeding a merge connects to the merge's internal segment instead
                        var partnerCellIndex = cellIndexByBlock[output.PartnerBlock.BlockInstanceId];
                        partner = kind == BeltSegmentKind.Branch ? InternalIndexFor(partnerCellIndex, last.BlockInstanceId) : segmentIndexOfCell[partnerCellIndex];
                    }
                    links[i] = new BeltSegmentLayoutLink(output.Direction, output.EntryDirection, partner, output);
                }
                return links;
            }

            int InternalIndexFor(int targetCellIndex, BlockInstanceId sourceBlock)
            {
                // 相手が合流でなければそのsegment。合流なら、送り元の入力は対で作られているので必ず見つかる
                // A non-merge target is its own segment; for a merge, the source's input always exists because links are built in pairs
                var target = cells[targetCellIndex];
                if (!IsMerge(target) || target.IsSplitter) return segmentIndexOfCell[targetCellIndex];
                var inputIndex = 0;
                while (target.Inputs[inputIndex].PartnerBlock.BlockInstanceId != sourceBlock) inputIndex++;
                return internalIndexOfInput[InputKey(targetCellIndex, inputIndex)];
            }

            bool NeedsInternal(in BeltTopologyConnection input)
            {
                // 機械か分配器(分岐buffer)からの入力は、合流へ直接つながず内部segmentを挟む。分配器判定はマス側の計算済みの値を読む
                // Inputs from a machine or a splitter (branch buffer) never connect to a merge directly; the splitter flag is read from the computed cell
                return input.PartnerKind == BeltTopologyPartnerKind.Machine || cells[cellIndexByBlock[input.PartnerBlock.BlockInstanceId]].IsSplitter;
            }

            BeltSegmentLayout CreateInternalLayout(BeltSegmentLayout merge, in BeltSegmentLayoutLink mergeInput)
            {
                // 内部segmentは入力側の反対へ進んで合流へ入る。速度は素材によらず上限値
                // An internal segment travels away from its input side into the merge, at the maximum speed regardless of material
                var forward = BeltDirections.Opposite(mergeInput.Direction);
                var inputs = new[] { new BeltSegmentLayoutLink(mergeInput.Direction, mergeInput.EntryDirection, PartnerIndex(mergeInput.Connection), mergeInput.Connection) };
                var outputs = new[] { new BeltSegmentLayoutLink(forward, mergeInput.EntryDirection, merge.Index, mergeInput.Connection) };
                return new BeltSegmentLayout(mergeInput.PartnerSegmentIndex, BeltSegmentKind.Normal, true, Array.Empty<BeltTopologyCell>(),
                    BeltConstants.MaxSpeed, forward, inputs, outputs);
            }

            int PartnerIndex(in BeltTopologyConnection connection)
            {
                return connection.PartnerKind == BeltTopologyPartnerKind.Belt
                    ? segmentIndexOfCell[cellIndexByBlock[connection.PartnerBlock.BlockInstanceId]] : BeltSegmentLayoutLink.Machine;
            }

            #endregion
        }

        private static bool IsMerge(BeltTopologyCell cell)
        {
            return 2 <= cell.Inputs.Length;
        }

        // 1マスの入力は辺ごとに1本なので最大4本。4を基数にすればマスと入力の組を1つの整数で表せる
        // A cell has at most four inputs, one per edge, so radix 4 packs the cell and input pair into one integer
        private static int InputKey(int cellIndex, int inputIndex)
        {
            return cellIndex * 4 + inputIndex;
        }
    }
}
