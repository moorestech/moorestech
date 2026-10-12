using System;
using System.Collections.Generic;
using System.Linq;
using Game.Blueprint;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    [MessagePackObject]
    public class BlueprintResponse : ProtocolMessagePackBase
    {
        [Key(2)] public bool Success { get; set; }
        [Key(3)] public BlueprintFailureReason FailureReason { get; set; }

        // Create成功時に発行されたGuid（他Operationではnull）
        // The GUID issued on a successful Create (null for other operations)
        [Key(4)] public string RegisteredGuidStr { get; set; }
        [Key(5)] public List<BlueprintMessagePack> Blueprints { get; set; }
        [Key(6)] public bool HasCostShortage { get; set; }
        [Key(7)] public List<BlueprintPlacedCellMessagePack> PlacedCells { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlueprintResponse() { }

        public BlueprintResponse(bool success, BlueprintFailureReason failureReason, string registeredGuidStr, List<BlueprintMessagePack> blueprints)
        {
            Tag = BlueprintProtocol.ProtocolTag;
            Success = success;
            FailureReason = failureReason;
            RegisteredGuidStr = registeredGuidStr;
            Blueprints = blueprints;
            PlacedCells = new List<BlueprintPlacedCellMessagePack>();
        }

        public BlueprintResponse(bool success, BlueprintFailureReason failureReason,
            bool hasCostShortage, List<BlueprintPlacedCellMessagePack> placedCells)
        {
            Tag = BlueprintProtocol.ProtocolTag;
            Success = success;
            FailureReason = failureReason;
            HasCostShortage = hasCostShortage;
            PlacedCells = placedCells;
        }
    }

    [MessagePackObject]
    public class BlueprintPlacedCellMessagePack
    {
        [Key(0)] public Vector3IntMessagePack Position { get; set; }
        [Key(1)] public int Direction { get; set; }
        [Key(2)] public int BlockId { get; set; }
        [Key(3)] public int BlockInstanceId { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlueprintPlacedCellMessagePack() { }

        public BlueprintPlacedCellMessagePack(Vector3Int position, int direction, int blockId, int blockInstanceId)
        {
            Position = new Vector3IntMessagePack(position);
            Direction = direction;
            BlockId = blockId;
            BlockInstanceId = blockInstanceId;
        }
    }

    [MessagePackObject]
    public class BlueprintMessagePack
    {
        [Key(0)] public string Name { get; set; }
        [Key(1)] public List<BlueprintBlockMessagePack> Blocks { get; set; }

        // 識別子はGuidに一本化。名前は表示専用でありもう識別には使わない
        // The identity is unified to a GUID; the name is display-only and no longer used for identification
        [Key(2)] public string BlueprintGuidStr { get; set; }
        [Key(3)] public List<BlueprintLineMessagePack> Wires { get; set; }
        [Key(4)] public List<BlueprintLineMessagePack> Chains { get; set; }
        [IgnoreMember] public Guid BlueprintGuid => Guid.Parse(BlueprintGuidStr);

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlueprintMessagePack() { }

        public BlueprintMessagePack(BlueprintJsonObject jsonObject)
        {
            Name = jsonObject.Name;
            Blocks = jsonObject.Blocks.Select(b => new BlueprintBlockMessagePack(b)).ToList();
            BlueprintGuidStr = jsonObject.BlueprintGuidStr;
            Wires = jsonObject.Wires.Select(w => new BlueprintLineMessagePack(w)).ToList();
            Chains = jsonObject.Chains.Select(c => new BlueprintLineMessagePack(c)).ToList();
        }

        // クライアントがBP実データ（貼り付け計算の入力）へ戻す口。Guidも保持したまま渡す
        // Converts back to the domain model used by paste calculation, preserving the GUID
        public BlueprintJsonObject ToJsonObject()
        {
            return new BlueprintJsonObject(Name, Blocks.Select(b => b.ToJsonObject()).ToList(),
                Wires.Select(w => w.ToJsonObject()).ToList(), Chains.Select(c => c.ToJsonObject()).ToList(), BlueprintGuid);
        }
    }

    [MessagePackObject]
    public class BlueprintBlockMessagePack
    {
        [Key(0)] public Vector3IntMessagePack Offset { get; set; }
        [Key(1)] public string BlockGuidStr { get; set; }
        [Key(2)] public int Direction { get; set; }
        [Key(3)] public Dictionary<string, string> Settings { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlueprintBlockMessagePack() { }

        public BlueprintBlockMessagePack(BlueprintBlockJsonObject jsonObject)
        {
            Offset = new Vector3IntMessagePack(jsonObject.Offset);
            BlockGuidStr = jsonObject.BlockGuidStr;
            Direction = jsonObject.Direction;
            Settings = jsonObject.Settings;
        }

        public BlueprintBlockJsonObject ToJsonObject()
        {
            return new BlueprintBlockJsonObject(Offset.Vector3Int, BlockGuidStr, Direction, Settings);
        }
    }

    public enum BlueprintOperation
    {
        Create = 0,
        GetAll = 1,
        Delete = 2,
        Paste = 3,
    }

    public enum BlueprintFailureReason
    {
        None = 0,
        InvalidName = 1,
        EmptyArea = 2,
        NotFound = 3,
        UnknownOperation = 4,
        InvalidRequest = 5,
        NotUnlocked = 6,
        PasteCostShortage = 7,
        PasteNotUnlocked = 8,
        PasteLineFailed = 9,
        PastePlacementFailed = 10,
    }
}
