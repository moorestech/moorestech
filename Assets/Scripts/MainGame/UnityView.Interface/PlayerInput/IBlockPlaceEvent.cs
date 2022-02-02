using UnityEngine;

namespace MainGame.UnityView.Interface.PlayerInput
{
    public interface IBlockPlaceEvent
    {
        public delegate void OnBlockPlace(Vector2Int position,short hotBarIndex);

        public void Subscribe(OnBlockPlace onBlockPlace);
    }
}