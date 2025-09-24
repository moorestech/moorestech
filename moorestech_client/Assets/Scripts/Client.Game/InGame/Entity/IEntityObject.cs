using UnityEngine;

namespace Client.Game.InGame.Entity
{
    public interface IEntityObject
    {
        public long EntityId { get; }
        public void Initialize(long entityId);
        public void SetDirectPosition(Vector3 position);
        public void SetInterpolationPosition(Vector3 position);
        public void Destroy();
    }
}