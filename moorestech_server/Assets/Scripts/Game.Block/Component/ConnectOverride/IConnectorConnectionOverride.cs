using System.Collections.Generic;
using Game.Block.Interface.Component;
using UnityEngine;

namespace Game.Block.Component.ConnectOverride
{
    internal interface IConnectorConnectionOverride<TTarget> where TTarget : IBlockComponent
    {
        IReadOnlyList<Vector3Int> ObservationPositions { get; }
        void ApplyTo(Dictionary<TTarget, ConnectedInfo> connectedTargets);
    }

    internal sealed class DefaultConnectionOverride<TTarget> : IConnectorConnectionOverride<TTarget>
        where TTarget : IBlockComponent
    {
        public IReadOnlyList<Vector3Int> ObservationPositions { get; } = new Vector3Int[0];
        public void ApplyTo(Dictionary<TTarget, ConnectedInfo> connectedTargets) { }
    }
}
