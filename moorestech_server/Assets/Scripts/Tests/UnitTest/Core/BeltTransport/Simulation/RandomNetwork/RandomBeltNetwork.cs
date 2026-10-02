using System;
using System.Collections.Generic;
using Core.BeltTransport;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Simulation.BeltSimulationTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Simulation.RandomNetwork
{
    // シードから参照実装の配線規則を満たす小さな網を生成する
    // 通常: 搬入元1つまで。合流: 通常segment・bufferから2～3搬入、bufferの搬出先1つ。分岐: 搬入1つ、bufferの搬出先2～3
    // Generates a small network from a seed that follows the reference wiring rules
    // Normal: at most one input. Merge: 2-3 inputs from normals/buffers, one buffer output. Branch: one input, 2-3 buffer outputs
    public sealed class RandomBeltNetwork
    {
        public readonly List<BeltConveyorSegment> Segments = new();
        public readonly List<RandomBeltMachineSink> Sinks = new();
        public readonly HashSet<long> InsertedSerials = new();
        private readonly List<RandomBeltMachineSource> _sources = new();
        private readonly List<BeltOpenEnd> _openEnds = new();
        private readonly RandomBeltChoices _random;
        private long _nextSerial = 1;

        public RandomBeltNetwork(int seed)
        {
            // 機械から供給される通常segmentを起点に、延長・分岐・合流・輪を積み上げる
            // Start from machine-fed normal segments and stack extensions, branches, merges and loops
            _random = new RandomBeltChoices(seed);
            for (var i = _random.Next(2, 5); i > 0; i--)
            {
                var belt = AddNormal();
                _sources.Add(new RandomBeltMachineSource(_random.NextSeed(), belt));
                _openEnds.Add(new BeltOpenEnd(belt, null, BeltDirection.None));
            }
            for (var step = _random.Next(4, 11); step > 0; step--) AddRandomPart();

            // 残った出口は機械へ、通常segmentは一部を未接続のまま残す
            // Close remaining exits into machines, leaving some normal exits unconnected
            foreach (var end in _openEnds)
            {
                if (!end.IsBuffer && _random.Next(5) == 0) continue;
                var sink = new RandomBeltMachineSink(_random.NextSeed());
                Sinks.Add(sink);
                end.ConnectTo(sink, BeltDirection.Front);
            }
            _openEnds.Clear();
            foreach (var segment in Segments) RestoreInitialItems(segment);
            _random.Shuffle(Segments);
        }

        // tick境界の操作: 機械からの供給、機械の開閉、まれな速度変更
        // Tick-boundary operations: machine supply, machine open/close and rare speed changes
        public void PrepareTick()
        {
            foreach (var source in _sources)
            {
                if (source.TryInsert(_nextSerial)) InsertedSerials.Add(_nextSerial);
                _nextSerial++;
            }
            foreach (var sink in Sinks) sink.RerollAtTickBoundary();
            foreach (var segment in Segments)
                if (_random.Next(50) == 0) segment.SetSpeed(_random.Speed());
        }

        private void AddRandomPart()
        {
            switch (_random.Next(5))
            {
                case 0: Extend(); break;
                case 1: AddBranch(); break;
                case 2: AddMerge(); break;
                case 3: AddClosedLoop(); break;
                default: AddMergeBranchLoop(); break;
            }
        }

        private void Extend()
        {
            var belt = AddNormal();
            TakeOpenEnd(_random.Next(_openEnds.Count)).ConnectTo(belt, BeltDirection.Front);
            _openEnds.Add(new BeltOpenEnd(belt, null, BeltDirection.None));
        }

        private void AddBranch()
        {
            // 分岐の入力側は直進の反対。bufferからの入力なら直進方向はそのbuffer方向に固定される
            // The branch input side is opposite its straight direction; from a buffer the straight direction is that slot
            var end = TakeOpenEnd(_random.Next(_openEnds.Count));
            var forward = end.IsBuffer ? end.BufferDirection : _random.Direction();
            var candidates = _random.ShuffledCandidates(forward);
            var branch = new BeltBranchSegment(_random.Next(1, 4), _random.Speed(),
                Order(candidates[0], candidates[1], candidates[2]), forward);
            Segments.Add(branch);
            end.ConnectTo(branch, forward);
            for (var i = _random.Next(2, 4) - 1; i >= 0; i--) _openEnds.Add(new BeltOpenEnd(null, branch.Buffer, candidates[i]));
        }

        private void AddMerge()
        {
            // bufferの入力方向は固定。重なるか直進方向が残らなければ今回は作らない
            // Buffer inputs have fixed directions. Skip when they collide or leave no straight direction
            if (_openEnds.Count < 2) return;
            var ends = new List<BeltOpenEnd>();
            for (var i = Math.Min(_openEnds.Count, _random.Next(2, 4)); i > 0; i--) ends.Add(TakeOpenEnd(_random.Next(_openEnds.Count)));
            var used = 0;
            foreach (var end in ends)
            {
                if (!end.IsBuffer) continue;
                var bit = 1 << (int)BeltDirections.Opposite(end.BufferDirection);
                if ((used & bit) != 0) { _openEnds.AddRange(ends); return; }
                used |= bit;
            }
            var forward = _random.PickUnused(used);
            used |= 1 << (int)forward;

            var candidates = _random.ShuffledCandidates(BeltDirections.Opposite(forward));
            var merge = new BeltMergeSegment(_random.Speed(), Order(candidates[0], candidates[1], candidates[2]), forward);
            Segments.Add(merge);
            foreach (var end in ends)
            {
                if (end.IsBuffer) { end.ConnectTo(merge, end.BufferDirection); continue; }
                var input = _random.PickUnused(used);
                used |= 1 << (int)input;
                end.ConnectTo(merge, BeltDirections.Opposite(input));
            }
            _openEnds.Add(new BeltOpenEnd(null, merge.Buffer, forward));
        }

        private void AddClosedLoop()
        {
            var first = AddNormal();
            var last = first;
            for (var i = _random.Next(0, 3); i > 0; i--)
            {
                var next = AddNormal();
                last.ConnectTo(next, BeltDirection.Front);
                last = next;
            }
            last.ConnectTo(first, BeltDirection.Front);
        }

        // 通常segment→合流(Left)→通常→分岐→通常→合流(Back)の輪。分岐の横出口は外へ開く
        // A loop: normal → merge(Left) → normal → branch → normal → merge(Back). The branch's side exits stay open
        private void AddMergeBranchLoop()
        {
            var index = _openEnds.FindIndex(e => !e.IsBuffer);
            if (index < 0) return;
            var merge = new BeltMergeSegment(_random.Speed(), BeltPriority.InitializeFromDirection, BeltDirection.Front);
            var branch = new BeltBranchSegment(_random.Next(1, 4), _random.Speed(), BeltPriority.InitializeFromDirection, BeltDirection.Front);
            Segments.Add(merge);
            Segments.Add(branch);
            var toBranch = AddNormal();
            var toMerge = AddNormal();
            TakeOpenEnd(index).ConnectTo(merge, BeltDirection.Right);
            merge.Buffer.ConnectTo(toBranch, BeltDirection.Front);
            toBranch.ConnectTo(branch, BeltDirection.Front);
            branch.Buffer.ConnectTo(toMerge, BeltDirection.Front);
            toMerge.ConnectTo(merge, BeltDirection.Front);
            _openEnds.Add(new BeltOpenEnd(null, branch.Buffer, BeltDirection.Left));
            if (_random.Next(2) == 0) _openEnds.Add(new BeltOpenEnd(null, branch.Buffer, BeltDirection.Right));
        }

        // 出口側から距離がW以上離れるように、Length未満の位置へアイテムを置く
        // Place items below Length so that consecutive distances differ by at least W
        private void RestoreInitialItems(BeltConveyorSegment segment)
        {
            if (segment is BeltBufferedSegment buffered && _random.Next(10) < 3) buffered.Buffer.RestoreItem(MakeItem(NewInitialSerial()));
            if (_random.Next(2) == 0) return;
            var states = new List<BeltItemState>();
            for (var distance = _random.Next(0, W); distance < segment.Capacity * W; distance += W + _random.Next(0, 200))
                if (_random.Next(3) != 0) states.Add(new BeltItemState(MakeItem(NewInitialSerial()), distance));
            segment.RestoreItems(states.ToArray());
        }

        private long NewInitialSerial()
        {
            InsertedSerials.Add(_nextSerial);
            return _nextSerial++;
        }

        private BeltNormalSegment AddNormal()
        {
            var belt = CreateNormal(_random.Next(1, 5), _random.Speed());
            Segments.Add(belt);
            return belt;
        }

        private BeltOpenEnd TakeOpenEnd(int index)
        {
            var end = _openEnds[index];
            _openEnds.RemoveAt(index);
            return end;
        }
    }
}
