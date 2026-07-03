using Core.Master;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts
{
    /// <summary>
    /// ポール設置・延長プロトコルの送信指示。FromPosがnullなら孤立設置となる。
    /// Send command for the pole place/extend protocol. Null FromPos means isolated placement.
    /// </summary>
    public readonly struct GearChainPoleExtendSendCommand
    {
        public readonly Vector3Int? FromPos;
        public readonly int PoleSlot;
        public readonly PlaceInfo PlaceInfo;
        public readonly ItemId ChainItemId;

        // 設置直後に接続上限へ達するポールは次の起点に引き継がない
        // Do not hand over the placed pole as the next source when it reaches its connection limit immediately
        public readonly bool CanContinueExtension;

        public GearChainPoleExtendSendCommand(Vector3Int? fromPos, int poleSlot, PlaceInfo placeInfo, ItemId chainItemId, bool canContinueExtension)
        {
            FromPos = fromPos;
            PoleSlot = poleSlot;
            PlaceInfo = placeInfo;
            ChainItemId = chainItemId;
            CanContinueExtension = canContinueExtension;
        }
    }

    /// <summary>
    /// 既存ポール同士のチェーン接続プロトコルの送信指示
    /// Send command for the chain connect protocol between existing poles
    /// </summary>
    public readonly struct GearChainConnectSendCommand
    {
        public readonly Vector3Int FromPos;
        public readonly Vector3Int ToPos;
        public readonly ItemId ChainItemId;

        public GearChainConnectSendCommand(Vector3Int fromPos, Vector3Int toPos, ItemId chainItemId)
        {
            FromPos = fromPos;
            ToPos = toPos;
            ChainItemId = chainItemId;
        }
    }
}
