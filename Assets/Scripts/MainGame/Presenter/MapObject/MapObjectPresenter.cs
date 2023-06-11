﻿using MainGame.Network.Send;
using MainGame.UnityView.Control;
using MainGame.UnityView.MapObject;
using MainGame.UnityView.UI.UIState;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Presenter.MapObject
{
    /// <summary>
    /// マップオブジェクトのUIの表示や削除の判定を担当する
    /// </summary>
    public class MapObjectPresenter : ITickable
    {
        private readonly UIStateControl _uiStateControl;
        private readonly SendGetMapObjectProtocolProtocol _sendGetMapObjectProtocolProtocol;

        public MapObjectPresenter(UIStateControl uiStateControl,SendGetMapObjectProtocolProtocol sendGetMapObjectProtocolProtocol)
        {
            _uiStateControl = uiStateControl;
            _sendGetMapObjectProtocolProtocol = sendGetMapObjectProtocolProtocol;
        }
        
        private MapObjectGameObject _lastForcesMapObjectGameObject;

        public void Tick()
        {
            if (_uiStateControl.CurrentState != UIStateEnum.GameScreen)
            {
                return;
            }
            
            //スクリーンからマウスの位置にRayを飛ばす
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 1000)) return;
            
            //アウトラインの制御
            var forceMapObject = hit.collider.gameObject.GetComponent<MapObjectGameObject>();
            if (_lastForcesMapObjectGameObject != null && forceMapObject == null)
            {
                //フォーカスが外れたのでアウトラインを消す
                _lastForcesMapObjectGameObject.OutlineEnable(false);
            }
            else if (_lastForcesMapObjectGameObject == null && forceMapObject != null)
            {
                //フォーカスが当たったのでアウトラインを表示する
                _lastForcesMapObjectGameObject.OutlineEnable(true);
            }
            else if (_lastForcesMapObjectGameObject != forceMapObject)
            {
                //フォーカスが切り替わったのでアウトラインを切り替える
                _lastForcesMapObjectGameObject.OutlineEnable(false);
                forceMapObject.OutlineEnable(true);
            }

            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && forceMapObject != null)
            {
                //クリックしたら取得プロトコルを送信する
                _sendGetMapObjectProtocolProtocol.Send(forceMapObject.InstanceId);
            }
            
            _lastForcesMapObjectGameObject = forceMapObject;
        }
    }
}