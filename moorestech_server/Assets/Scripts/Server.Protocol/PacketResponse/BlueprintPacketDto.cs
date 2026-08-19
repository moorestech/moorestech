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
    public class BlueprintRequest : ProtocolMessagePackBase
    {
        [Key(2)] public BlueprintOperation Operation { get; set; }
        [Key(3)] public string Name { get; set; }

        // コピー範囲はXYZバウンディングボックスで指定
        // Copy area is uniquely specified as a full XYZ bounding box
        [Key(4)] public Vector3IntMessagePack Min { get; set; }
        [Key(5)] public Vector3IntMessagePack Max { get; set; }

        // Delete専用。削除対象は名前ではなくGuidで指定する
        // Delete only; the target is identified by GUID, not by name
        [Key(6)] public string BlueprintGuidStr { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlueprintRequest() { Tag = BlueprintProtocol.ProtocolTag; }

        // Operationごとに必要フィールドのみ設定
        // Private constructor; static factories below set only the fields each Operation needs
        private BlueprintRequest(BlueprintOperation operation, string name, Vector3IntMessagePack min, Vector3IntMessagePack max, string blueprintGuidStr)
        {
            Tag = BlueprintProtocol.ProtocolTag;
            Operation = operation;
            Name = name;
            Min = min;
            Max = max;
            BlueprintGuidStr = blueprintGuidStr;
        }

        public static BlueprintRequest CreateCreateRequest(string name, Vector3Int min, Vector3Int max)
        {
            return new BlueprintRequest(BlueprintOperation.Create, name, new Vector3IntMessagePack(min), new Vector3IntMessagePack(max), null);
        }

        public static BlueprintRequest CreateGetAllRequest()
        {
            return new BlueprintRequest(BlueprintOperation.GetAll, null, null, null, null);
        }

        public static BlueprintRequest CreateDeleteRequest(Guid blueprintGuid)
        {
            return new BlueprintRequest(BlueprintOperation.Delete, null, null, null, blueprintGuid.ToString());
        }
    }

    [MessagePackObject]
    public class BlueprintResponse : ProtocolMessagePackBase
    {
        [Key(2)] public bool Success { get; set; }
        [Key(3)] public BlueprintFailureReason FailureReason { get; set; }

        // Create成功時に発行されたGuid（他Operationではnull）
        // The GUID issued on a successful Create (null for other operations)
        [Key(4)] public string RegisteredGuidStr { get; set; }
        [Key(5)] public List<BlueprintMessagePack> Blueprints { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlueprintResponse() { }

        public BlueprintResponse(bool success, BlueprintFailureReason failureReason, string registeredGuidStr, List<BlueprintMessagePack> blueprints)
        {
            Tag = BlueprintProtocol.ProtocolTag;
            Success = success;
            FailureReason = failureReason;
            RegisteredGuidStr = registeredGuidStr;
            Blueprints = blueprints;
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
        [IgnoreMember] public Guid BlueprintGuid => Guid.Parse(BlueprintGuidStr);

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlueprintMessagePack() { }

        public BlueprintMessagePack(BlueprintJsonObject jsonObject)
        {
            Name = jsonObject.Name;
            Blocks = jsonObject.Blocks.Select(b => new BlueprintBlockMessagePack(b)).ToList();
            BlueprintGuidStr = jsonObject.BlueprintGuidStr;
        }

        // クライアントがBP実データ（貼り付け計算の入力）へ戻す口。Guidも保持したまま渡す
        // Converts back to the domain model used by paste calculation, preserving the GUID
        public BlueprintJsonObject ToJsonObject()
        {
            return new BlueprintJsonObject(Name, Blocks.Select(b => b.ToJsonObject()).ToList(), BlueprintGuid);
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
    }
}
