using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using UnityEngine;

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
        // 押し込まれるベルコンのblockごとに受け口をまとめ、機械の押し込みをそのblockの数件だけで照合する
        // Supply ports grouped by the pushed belt block, so a machine push is matched against that block's few ports only
        private readonly Dictionary<BlockInstanceId, List<BeltMachineSupplyPort>> _supplyPortsByBeltBlock = new();

        public BeltTransportAssembly(BeltSegmentLayout[] layouts, BeltConveyorSegment[] segments, List<BeltMachineSupplyPort> supplyPorts)
        {
            Layouts = layouts;
            Segments = segments;
            Simulation = new BeltSimulation(segments);
            foreach (var port in supplyPorts)
            {
                if (!_supplyPortsByBeltBlock.TryGetValue(port.BeltBlockInstanceId, out var ports))
                {
                    ports = new List<BeltMachineSupplyPort>(1);
                    _supplyPortsByBeltBlock.Add(port.BeltBlockInstanceId, ports);
                }
                ports.Add(port);
            }
        }

        // 機械の押し込みを、接続に対応する受け口へ進入距離1で入れる。受け口が無い押し込みは接続の食い違いなので拒否してログを出す
        // Push from a machine into the port matching its connection at entry length 1; a push with no port is a connection mismatch, so reject it and log
        public bool TrySupplyFromMachine(BlockInstanceId beltBlockInstanceId, in InsertItemContext context, ItemId itemId, ItemInstanceId itemInstanceId)
        {
            if (_supplyPortsByBeltBlock.TryGetValue(beltBlockInstanceId, out var ports))
                for (var i = 0; i < ports.Count; i++)
                {
                    var port = ports[i];
                    if (!port.Matches(beltBlockInstanceId, context)) continue;
                    var item = new BeltItem(itemId, itemInstanceId, port.EntryDirection);
                    return port.Receiver.TryReceive(port.Direction, MachineEntryLength, item);
                }
            Debug.LogError($"[BeltTransport] No supply port for machine {context.SourceBlockInstanceId} pushing into belt {beltBlockInstanceId}; the push is rejected.");
            return false;
        }
    }
}
