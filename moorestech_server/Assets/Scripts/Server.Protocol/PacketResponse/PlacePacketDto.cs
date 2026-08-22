using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using MessagePack;
using Newtonsoft.Json;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    [MessagePackObject]
    public class PlaceInfoMessagePack
    {
        [Key(0)] public Vector3IntMessagePack Position { get; set; }

        [Key(1)] public BlockDirection Direction { get; set; }

        [Key(2)] public BlockVerticalDirection VerticalDirection { get; set; }
        [Key(3)] public BlockCreateParamMessagePack[] BlockCreateParams { get; set; }

        [Key(4)] public int BlockIdInt { get; set; }
        [IgnoreMember] public BlockId BlockId => new(BlockIdInt);

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PlaceInfoMessagePack() { }

        public PlaceInfoMessagePack(PlaceInfo placeInfo)
        {
            BlockCreateParams = placeInfo.CreateParams.Select(v => new BlockCreateParamMessagePack(v)).ToArray();
            Position = new Vector3IntMessagePack(placeInfo.Position);
            Direction = placeInfo.Direction;
            VerticalDirection = placeInfo.VerticalDirection;
            BlockIdInt = placeInfo.BlockId.AsPrimitive();
        }
    }

    [MessagePackObject]
    public class BlockCreateParamMessagePack
    {
        [Key(0)] public string Key { get; set; }
        [Key(1)] public byte[] Value { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlockCreateParamMessagePack() { }

        public BlockCreateParamMessagePack(BlockCreateParam param)
        {
            Key = param.Key;
            Value = param.Value;
        }
    }

    /// <summary>
    ///     セルが設置不可になった原因。判定した側が立て、表示側が文言へ写像する
    ///     Why a cell became unplaceable; set by whoever judged it and mapped to wording by the presentation side
    /// </summary>
    public enum PlacementBlockCause
    {
        None,
        ExistingBlock,
        ImpossibleOverpass,
        SlopeBlockMissing,
    }

    public class PlaceInfo
    {
        public Vector3Int Position { get; set; }
        public BlockDirection Direction { get; set; }
        public BlockVerticalDirection VerticalDirection { get; set; }
        public BlockId BlockId;

        public bool Placeable { get; set; }

        // Placeableをfalseにした原因（クライアント表示専用。Placeable=trueのときは常にNone）
        // The cause that set Placeable to false (client display only; always None while Placeable is true)
        public PlacementBlockCause BlockCause { get; set; }

        public BlockCreateParam[] CreateParams { get; set; } = Array.Empty<BlockCreateParam>();

        [JsonIgnore] public Dictionary<string, byte[]> CreateParamDictionary => CreateParams.ToDictionary(v => v.Key, v => v.Value);
    }
}
