using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.Blueprint.Planning
{
    public enum BlueprintPasteLineKind { ElectricWire, GearChain }
    public enum BlueprintPasteLineFailureReason { None, InvalidTarget, OutOfRange, ConnectionLimit, AlreadyConnected }

    public readonly struct BlueprintPasteLine
    {
        public readonly BlueprintPasteLineKind Kind;
        public readonly int ElementIndexA;
        public readonly int ElementIndexB;
        public readonly Vector3Int PositionA;
        public readonly Vector3Int PositionB;
        public readonly Guid ConnectToolGuid;
        public readonly BlueprintPasteLineFailureReason FailureReason;
        public bool IsConnectable => FailureReason == BlueprintPasteLineFailureReason.None;
        public readonly IReadOnlyList<ConnectToolMaterialCost> Materials;

        internal BlueprintPasteLine(BlueprintPasteLineKind kind, int elementIndexA, int elementIndexB,
            Vector3Int positionA, Vector3Int positionB, Guid connectToolGuid, IReadOnlyList<ConnectToolMaterialCost> materials, BlueprintPasteLineFailureReason failureReason)
        {
            Kind = kind;
            FailureReason = failureReason;
            ElementIndexA = elementIndexA;
            ElementIndexB = elementIndexB;
            PositionA = positionA;
            PositionB = positionB;
            ConnectToolGuid = connectToolGuid;
            Materials = Array.AsReadOnly(materials.ToArray());
        }
    }
}
