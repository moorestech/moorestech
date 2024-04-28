using UnityEngine;

namespace Client.Game.InGame.Entity
{
    public interface IEntityObject
    {
        public void SetDirectPosition(Vector3 position);
        public void SetInterpolationPosition(Vector3 position);
        public void Destroy();
    }
}