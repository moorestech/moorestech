using System.Data.Services;
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

        public MapObjectPresenter(UIStateControl uiStateControl)
        {
            _uiStateControl = uiStateControl;
        }

        public void Tick()
        {
            if (_uiStateControl.CurrentState != UIStateEnum.GameScreen)
            {
                return;
            }
            
            //スクリーンからマウスの位置にRayを飛ばす
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 1000)) return;
            
            //Rayが当たったオブジェクトがMapObjectGameObjectでなければreturn
            var mapObjectGameObject = hit.collider.gameObject.GetComponent<MapObjectGameObject>();
            if (mapObjectGameObject == null) return;
            
            
        }
    }
}