using Client.Common.Server;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Presenter.Player
{
    public class PlayerPositionSender : ITickable
    {
        private float _timer;
        
        
        /// <summary>
        ///     Updateと同じタイミングで呼ばれる
        /// </summary>
        public void Tick()
        {
            _timer += Time.deltaTime;
            if (_timer < NetworkConst.UpdateIntervalSeconds) return;
            _timer = 0;
            
            var playerObjectController = PlayerSystemContainer.Instance.PlayerObjectController;
            ClientContext.VanillaApi.SendOnly.SendPlayerPosition(playerObjectController.Position);
        }
    }
}