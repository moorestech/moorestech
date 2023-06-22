using System.Threading;
using Cysharp.Threading.Tasks;
using MainGame.Network.Send;
using MainGame.UnityView.Control;
using MainGame.UnityView.MapObject;
using MainGame.UnityView.UI.Inventory.View.HotBar;
using MainGame.UnityView.UI.UIState;
using MainGame.UnityView.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;
using VContainer.Unity;

namespace MainGame.Presenter.MapObject
{
    /// <summary>
    /// マップオブジェクトのUIの表示や削除の判定を担当する
    /// </summary>
    public class MapObjectGetPresenter : MonoBehaviour
    {
        private const float MiningTime = 5f;
        
        [SerializeField] private MiningObjectHelper miningObjectHelper;
        
        private UIStateControl _uiStateControl;
        private SendGetMapObjectProtocolProtocol _sendGetMapObjectProtocolProtocol;
        private CancellationTokenSource _miningCancellationTokenSource = new();
        
        private CancellationToken _gameObjectCancellationToken;

        [Inject]
        public void Constructor(UIStateControl uiStateControl,SendGetMapObjectProtocolProtocol sendGetMapObjectProtocolProtocol)
        {
            _uiStateControl = uiStateControl;
            _sendGetMapObjectProtocolProtocol = sendGetMapObjectProtocolProtocol;
            _gameObjectCancellationToken = this.GetCancellationTokenOnDestroy();
            
            WhileUpdate().Forget();
        }
        

        private async UniTask WhileUpdate()
        {
            while (true)
            {
                await MiningUpdate(_gameObjectCancellationToken);
                await UniTask.Yield(_gameObjectCancellationToken);
            }
        }

        private async UniTask MiningUpdate(CancellationToken cancellationToken)
        {
            if (_uiStateControl.CurrentState != UIStateEnum.GameScreen)
            {
                return;
            }

            var forcesMapObject = GetOnMouseMapObject();
            if (!InputManager.Playable.ScreenLeftClick.GetKeyDown || forcesMapObject == null)
            {
                return;
            }
            
            forcesMapObject.OutlineEnable(true);
            
            _miningCancellationTokenSource.Cancel();
            _miningCancellationTokenSource = new CancellationTokenSource();
            miningObjectHelper.StartMining(MiningTime,_miningCancellationTokenSource.Token).Forget();

            var isMiningCanceled = false;
            
            //map objectがフォーカスされ、クリックされているので採掘を行う
            //採掘中はこのループの中にいる
            //採掘時間分ループする
            var nowTime = 0f;
            while (nowTime < MiningTime)
            {
                await UniTask.Yield(PlayerLoopTiming.Update,cancellationToken);
                nowTime += Time.deltaTime;


                //クリックが離されたら採掘を終了する
                //map objectが変わったら採掘を終了する
                if (InputManager.Playable.ScreenLeftClick.GetKeyUp || forcesMapObject != GetOnMouseMapObject())
                { 
                    isMiningCanceled = true;
                }
            }

            if (!isMiningCanceled)
            {
                _sendGetMapObjectProtocolProtocol.Send(forcesMapObject.InstanceId);
            }

            forcesMapObject.OutlineEnable(false);
            _miningCancellationTokenSource.Cancel();
        }

        private MapObjectGameObject GetOnMouseMapObject()
        {
            //スクリーンからマウスの位置にRayを飛ばす
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 1000)) return null;
            if (EventSystem.current.IsPointerOverGameObject()) return null;
            
            //マップオブジェクトを取得する
            return hit.collider.gameObject.GetComponent<MapObjectGameObject>();
        }
    }
}