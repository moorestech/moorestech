using System;
using Game.Fluid.Sync.State;
using MessagePack;

namespace Game.Fluid.Sync.Message
{
    // 流体網の全量の通信形。再構築tickの送信では連番を消費し、途中参加の送信では発行済み最新の連番をウォーターマークとして載せる
    // Wire form of the fluid network full state; a rebuild-tick send consumes a sequence id, a join-time send carries the latest issued id as a watermark
    [MessagePackObject]
    public class FluidNetworkFullStateMessagePack
    {
        [Key(0)] public uint ServerTick { get; set; }
        [Key(1)] public uint TickSequenceId { get; set; }
        [Key(2)] public FluidNodeStateMessagePack[] Nodes { get; set; }
        [Key(3)] public FluidFaceStateMessagePack[] Faces { get; set; }
        [Key(4)] public FluidPortStateMessagePack[] Ports { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public FluidNetworkFullStateMessagePack() { }

        public FluidNetworkFullStateMessagePack(uint serverTick, uint tickSequenceId, FluidNetworkFullState fullState)
        {
            ServerTick = serverTick;
            TickSequenceId = tickSequenceId;
            Nodes = new FluidNodeStateMessagePack[fullState.Nodes.Length];
            for (var i = 0; i < Nodes.Length; i++) Nodes[i] = new FluidNodeStateMessagePack(fullState.Nodes[i]);
            Faces = new FluidFaceStateMessagePack[fullState.Faces.Length];
            for (var i = 0; i < Faces.Length; i++) Faces[i] = new FluidFaceStateMessagePack(fullState.Faces[i]);
            Ports = new FluidPortStateMessagePack[fullState.Ports.Length];
            for (var i = 0; i < Ports.Length; i++) Ports[i] = new FluidPortStateMessagePack(fullState.Ports[i]);
        }

        public FluidNetworkFullState ToFullState()
        {
            var nodes = new FluidNodeState[Nodes.Length];
            for (var i = 0; i < nodes.Length; i++) nodes[i] = Nodes[i].ToState();
            var faces = new FluidFaceState[Faces.Length];
            for (var i = 0; i < faces.Length; i++) faces[i] = Faces[i].ToState();
            var ports = new FluidPortState[Ports.Length];
            for (var i = 0; i < ports.Length; i++) ports[i] = Ports[i].ToState();
            return new FluidNetworkFullState(nodes, faces, ports);
        }
    }
}
