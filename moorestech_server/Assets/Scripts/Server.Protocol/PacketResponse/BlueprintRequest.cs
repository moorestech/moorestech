using System;
using System.Collections.Generic;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    [MessagePackObject]
    public class BlueprintRequest : ProtocolMessagePackBase
    {
        public const int MaxPasteOrigins = 64;

        [Key(2)] public BlueprintOperation Operation { get; set; }
        [Key(3)] public string Name { get; set; }

        // コピー範囲はXYZバウンディングボックスで指定
        // Copy area is uniquely specified as a full XYZ bounding box
        [Key(4)] public Vector3IntMessagePack Min { get; set; }
        [Key(5)] public Vector3IntMessagePack Max { get; set; }

        // 削除・貼り付けの対象は名前ではなくGuidで指定する
        // Delete and paste identify the target by GUID rather than name
        [Key(6)] public string BlueprintGuidStr { get; set; }

        // 列の各原点と共通の回転を送る
        // Send each run origin and the shared rotation
        [Key(7)] public int RotationStep;
        [Key(8)] public List<Vector3IntMessagePack> Origins;

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlueprintRequest() { Tag = BlueprintProtocol.ProtocolTag; }

        // Operationごとに必要フィールドのみ設定
        // Private constructor; static factories below set only the fields each Operation needs
        private BlueprintRequest(BlueprintOperation operation, string name, Vector3IntMessagePack min, Vector3IntMessagePack max, string blueprintGuidStr, int rotationStep, List<Vector3IntMessagePack> origins)
        {
            Tag = BlueprintProtocol.ProtocolTag;
            Operation = operation;
            Name = name;
            Min = min;
            Max = max;
            BlueprintGuidStr = blueprintGuidStr;
            RotationStep = rotationStep;
            Origins = origins;
        }

        public static BlueprintRequest CreateCreateRequest(string name, Vector3Int min, Vector3Int max)
        {
            return new BlueprintRequest(BlueprintOperation.Create, name, new Vector3IntMessagePack(min), new Vector3IntMessagePack(max), null, 0, null);
        }

        public static BlueprintRequest CreateGetAllRequest()
        {
            return new BlueprintRequest(BlueprintOperation.GetAll, null, null, null, null, 0, null);
        }

        public static BlueprintRequest CreatePasteRequest(Guid blueprintGuid, int rotationStep, List<Vector3Int> origins)
        {
            return new BlueprintRequest(BlueprintOperation.Paste, null, null, null, blueprintGuid.ToString(),
                rotationStep, origins.ConvertAll(origin => new Vector3IntMessagePack(origin)));
        }

        // 共有上限で区切り、始点からの素材判定順を保つ
        // Split at the shared limit while preserving material evaluation order
        public static IEnumerable<BlueprintRequest> CreatePasteRequests(Guid blueprintGuid, int rotationStep, List<Vector3Int> origins)
        {
            for (var offset = 0; offset < origins.Count; offset += MaxPasteOrigins)
            {
                var count = Math.Min(MaxPasteOrigins, origins.Count - offset);
                yield return CreatePasteRequest(blueprintGuid, rotationStep, origins.GetRange(offset, count));
            }
        }

        public static BlueprintRequest CreateDeleteRequest(Guid blueprintGuid)
        {
            return new BlueprintRequest(BlueprintOperation.Delete, null, null, null, blueprintGuid.ToString(), 0, null);
        }
    }

}
