using System;
using Game.Train.RailGraph;
using MessagePack;
using UnityEngine;

namespace Server.Util.MessagePack
{
    /// <summary>
    ///     RailComponentIDをMessagePackで扱う薄いDTO
    /// </summary>
    [MessagePackObject]
    public class RailComponentIDMessagePack
    {
        [Key(0)] public Vector3IntMessagePack Position { get; set; }
        [Key(1)] public int ID { get; set; }

        [Obsolete("For serialization")]
        public RailComponentIDMessagePack() { }

        public RailComponentIDMessagePack(RailComponentID componentId)
        {
            Position = new Vector3IntMessagePack(componentId.Position);
            ID = componentId.ID;
        }

        public RailComponentID ToModel()
        {
            return new RailComponentID(Position.Vector3Int, ID);
        }
    }
}
