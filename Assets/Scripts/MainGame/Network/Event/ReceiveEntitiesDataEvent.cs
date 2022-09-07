using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Entity.Interface;
using Server.Protocol.PacketResponse.MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

namespace MainGame.Network.Event
{
    public class ReceiveEntitiesDataEvent
    {
        public event Action<List<EntityProperties>> OnChunkUpdateEvent;

        internal async UniTask InvokeChunkUpdateEvent(EntitiesResponseMessagePack response)
        {
            await UniTask.SwitchToMainThread();

            var properties = new List<EntityProperties>();
            foreach (var entity in response.Entities)
            {
                properties.Add(new EntityProperties(entity));
            }
            
            OnChunkUpdateEvent?.Invoke(properties);
        }
    }

    public class EntityProperties
    {
        public readonly long InstanceId;
        public readonly string Type;
        public readonly Vector3 Position;
        
        public EntityProperties(EntityMessagePack entityMessagePack)
        {
            InstanceId = entityMessagePack.InstanceId;
            Type = entityMessagePack.Type;
            var x = entityMessagePack.Position.X;
            var y = entityMessagePack.Position.Y;
            var z = entityMessagePack.Position.Z;
            Position = new Vector3(x,y,z);
        }

    }
}