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
        private bool _isSending;

        // 地形構築の完了後にFinalizerが呼ぶ。落下中の座標をサーバーへ書き込ませない
        // Called by the finalizer after the terrain is built so falling coordinates never reach the server
        public void StartSending()
        {
            _isSending = true;
        }

        /// <summary>
        ///     Updateと同じタイミングで呼ばれる
        /// </summary>
        public void Tick()
        {
            if (!_isSending) return;

            _timer += Time.deltaTime;
            if (_timer < NetworkConst.UpdateIntervalSeconds) return;
            _timer = 0;
            
            var playerObjectController = PlayerSystemContainer.Instance.PlayerObjectController;
            ClientContext.VanillaApi.SendOnly.SendPlayerPosition(playerObjectController.Position);
        }
    }
}