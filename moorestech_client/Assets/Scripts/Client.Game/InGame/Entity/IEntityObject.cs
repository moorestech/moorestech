using UnityEngine;

namespace Client.Game.Entity
{
    public interface IEntityObject
    {
        public void SetDirectPosition(Vector3 position);
        public void SetInterpolationPosition(Vector3 position);
        public void Destroy();
    }
}