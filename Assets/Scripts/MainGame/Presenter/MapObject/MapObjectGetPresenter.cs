using System.Threading;
using Cysharp.Threading.Tasks;
using MainGame.Network.Send;
using MainGame.UnityView.Control;
using MainGame.UnityView.MapObject;
using MainGame.UnityView.UI.Inventory.View.HotBar;
using MainGame.UnityView.UI.UIState;
using MainGame.UnityView.Util;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MainGame.Presenter.MapObject
{
    /// <summary>
    /// マップオブジェクトのUIの表示や削除の判定を担当する
    /// </summary>
    public class MapObjectGetPresenter : MonoBehaviour 
    {
        [SerializeField] private MiningObjectHelper miningObjectHelper;
        
        private UIStateControl _uiStateControl;
        private SendGetMapObjectProtocolProtocol _sendGetMapObjectProtocolProtocol;
        private CancellationTokenSource _miningCancellationTokenSource = new();

        [Inject]
        public void Constructor(UIStateControl uiStateControl,SendGetMapObjectProtocolProtocol sendGetMapObjectProtocolProtocol)
        {
            _uiStateControl = uiStateControl;
            _sendGetMapObjectProtocolProtocol = sendGetMapObjectProtocolProtocol;
        }
        
        private MapObjectGameObject _lastForcesMapObjectGameObject;

        private void Update()
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
                
                _miningCancellationTokenSource.Cancel();
            }
            else if (_lastForcesMapObjectGameObject == null && forceMapObject != null)
            {
                //フォーカスが当たったのでアウトラインを表示する
                forceMapObject.OutlineEnable(true);
            }
            else if (_lastForcesMapObjectGameObject != null && forceMapObject != null &&
                     _lastForcesMapObjectGameObject != forceMapObject)
            {
                //フォーカスが切り替わったのでアウトラインを切り替える
                _lastForcesMapObjectGameObject.OutlineEnable(false);
                forceMapObject.OutlineEnable(true);
                
                _miningCancellationTokenSource.Cancel();
            }

            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && forceMapObject != null && !miningObjectHelper.IsMining
            {
                StartMining(5,forceMapObject.InstanceId).Forget();
            }
            
            _lastForcesMapObjectGameObject = forceMapObject;
        }
        
        
        private async UniTask StartMining(float miningTime,int instanceId)
        {
            _miningCancellationTokenSource.Cancel();
            _miningCancellationTokenSource = new CancellationTokenSource();
            
            if(await miningObjectHelper.StartMining(miningTime,_miningCancellationTokenSource.Token))
            {
                _sendGetMapObjectProtocolProtocol.Send(instanceId);
                _miningCancellationTokenSource.Dispose();
                _miningCancellationTokenSource = new CancellationTokenSource();
            }
        }
    }
}