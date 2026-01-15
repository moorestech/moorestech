using UnityEngine;

namespace Client.Game.InGame.Entity
{
    public interface IEntityObject
    {
        long EntityId { get; }
        void Initialize(long entityId);
        void SetDirectPosition(Vector3 position);
        void SetPositionWithLerp(Vector3 position);
        void Destroy();
        void SetEntityData(byte[] entityEntityData);
    }
}