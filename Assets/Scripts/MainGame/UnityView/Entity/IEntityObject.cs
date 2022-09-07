using UnityEngine;

namespace MainGame.UnityView.Entity
{
    public interface IEntityObject
    {
        public void SetPosition(Vector3 position);
        public void Destroy();
    }
}