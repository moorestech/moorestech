
using MainGame.UnityView.UI.Tutorial;
using MainGame.UnityView.UI.UIState;
using UnityEngine;

namespace MainGame.Presenter.Tutorial.ExecutableTutorials
{
    
    /// <summary>
    /// 鉄の採掘をさせるチュートリアル
    /// </summary>
    public class IronMiningTutorial : IExecutableTutorial
    {
        public bool IsFinishTutorial { get; private set; }

        private readonly UIStateControl _uiStateControl;
        
        public IronMiningTutorial(UIStateControl uiStateControl)
        {
            _uiStateControl = uiStateControl;
        }

        public void StartTutorial()
        {
        }

        public void Update()
        {
            //TODO カーソルにGボタンをおすというのを出す
            
            //TODO　破壊、採掘モードになったら近くの鉱石をハイライトする
            
            //TODO 左クリック長押しで採掘するように伝える

            //TODO　鉱石を採掘したら終了
        }

        public void EndTutorial()
        {
        }
    }
}