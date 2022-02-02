using MainGame.UnityView.Interface.PlayerInput;
using UnityEngine;
using static MainGame.UnityView.Interface.PlayerInput.IBlockPlaceEvent;

namespace MainGame.UnityView.ControllerInput.Event
{
    public class BlockPlaceEvent : IBlockPlaceEvent
    {
        public event OnBlockPlace OnBlockPlaceEvent;
        public void Subscribe(OnBlockPlace onBlockPlace)
        {
            OnBlockPlaceEvent += onBlockPlace;
        }

        public virtual void OnOnBlockPlaceEvent(Vector2Int position, short hotBarIndex)
        {
            OnBlockPlaceEvent?.Invoke(position, hotBarIndex);
        }
    }
}