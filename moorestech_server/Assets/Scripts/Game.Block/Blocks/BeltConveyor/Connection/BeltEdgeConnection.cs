using System;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.BeltConveyor.Connection
{
    internal sealed class BeltEdgeConnection : IEquatable<BeltEdgeConnection>
    {
        internal readonly BlockConnectorComponent<IBlockInventory, DefaultConnectJudge> Source;
        internal readonly IBlockInventory Target;
        internal readonly ConnectedInfo Info;

        internal BeltEdgeConnection(IBlock source, IBlock target, IBlockConnector output, IBlockConnector input)
            : this(source.ComponentManager.GetComponent<BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>>(),
                target.ComponentManager.GetComponent<IBlockInventory>(), new ConnectedInfo(output, input, target)) { }

        internal BeltEdgeConnection(BlockConnectorComponent<IBlockInventory, DefaultConnectJudge> source, IBlockInventory target, ConnectedInfo info)
        {
            Source = source;
            Target = target;
            Info = info;
        }

        // portの実体も比較し、同じtargetへのport差替を検出する
        // Compare port identities too, detecting port replacement for the same target
        public bool Equals(BeltEdgeConnection other) => other != null && ReferenceEquals(Source, other.Source) &&
            ReferenceEquals(Target, other.Target) && ReferenceEquals(Info.TargetBlock, other.Info.TargetBlock) &&
            ReferenceEquals(Info.SelfConnector, other.Info.SelfConnector) && ReferenceEquals(Info.TargetConnector, other.Info.TargetConnector);
        public override bool Equals(object obj) => obj is BeltEdgeConnection other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Source, Target, Info.SelfConnector, Info.TargetConnector);
    }
}
