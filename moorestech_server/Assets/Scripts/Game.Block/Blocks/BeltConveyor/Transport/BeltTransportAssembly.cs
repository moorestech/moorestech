using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Blocks.BeltConveyor.Transport.Rebuild;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // 構成から生成した1組のCore segment・更新入口・機械からの受け口。構成が変わるたびに丸ごと作り直す
    // One generated set of Core segments, its tick entry and the machine supply ports; rebuilt as a whole whenever the layout changes
    public sealed class BeltTransportAssembly
    {
        // 機械からの搬入の進入距離。ほんのわずかにベルコンへ載る
        // Entry length of a machine push; the item barely enters the belt
        public const int MachineEntryLength = 1;

        // 添字はsegment番号(構成のIndex)と一致する
        // Indices equal the segment number (layout Index)
        public readonly BeltSegmentLayout[] Layouts;
        public readonly BeltConveyorSegment[] Segments;
        public readonly BeltSimulation Simulation;
        // blockがどのsegmentのどのマスかを引く。再構築の復元とセーブの切り出しが使う
        // Looks up which segment and cell a block is; used by rebuild restoration and save extraction
        public readonly BeltCellLocator Locator;
        // 面(押し込まれるベルコンのblock＋機械のblock)ごとに受け口を1つ持つ
        // One supply port per face (the pushed belt block plus the machine block)
        private readonly Dictionary<BeltMachineSupplyKey, BeltMachineSupplyPort> _supplyPortByFace;

        public BeltTransportAssembly(BeltSegmentLayout[] layouts, BeltConveyorSegment[] segments, Dictionary<BeltMachineSupplyKey, BeltMachineSupplyPort> supplyPortByFace)
        {
            Layouts = layouts;
            Segments = segments;
            Simulation = new BeltSimulation(segments);
            Locator = new BeltCellLocator(layouts);
            _supplyPortByFace = supplyPortByFace;
        }

        // 機械の押し込みを、その面の受け口へ進入距離1で入れる
        // Push from a machine into the port of its face at entry length 1
        // 受け口が無い面は接続解決が接続を作らなかった面(縦置きのベルコン等)で、通常の配置では起きないので無音で拒否する
        // A face without a port is one the connection resolver never connected (such as a vertically placed belt); it never occurs in normal placement, so reject silently
        public bool TrySupplyFromMachine(BlockInstanceId beltBlockInstanceId, in InsertItemContext context, ItemId itemId, ItemInstanceId itemInstanceId)
        {
            if (!_supplyPortByFace.TryGetValue(new BeltMachineSupplyKey(beltBlockInstanceId, context.SourceBlockInstanceId), out var port)) return false;
            var item = new BeltItem(itemId, itemInstanceId, port.EntryDirection);
            return port.Receiver.TryReceive(port.Direction, MachineEntryLength, item);
        }
    }
}
