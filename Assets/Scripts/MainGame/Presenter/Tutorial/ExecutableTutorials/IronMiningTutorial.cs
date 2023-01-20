using MainGame.UnityView.UI.Inventory.Control;
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
        private const ulong IronOreItemId = 1;
        
        public bool IsFinishTutorial { get; private set; }

        private readonly UIStateControl _uiStateControl;
        private readonly PlayerInventoryViewModel _playerInventoryViewModel;
        
        public IronMiningTutorial(UIStateControl uiStateControl,PlayerInventoryViewModel playerInventoryViewModel)
        {
            _uiStateControl = uiStateControl;
            _playerInventoryViewModel = playerInventoryViewModel;
        }


        public void StartTutorial() { }

        public void Update()
        {
            //すでに終了していたら処理をしない
            if (IsFinishTutorial)
            {
                return;
            }
            
            //鉄鉱石がメインインベントリに3つあるかをチェックする あったら完了にする
            var ironIngotCount = 0;
            foreach (var item in _playerInventoryViewModel.MainInventory)
            {
                if (item.ItemHash == IronOreItemId)
                {
                    ironIngotCount += item.Count;
                }
            }
            if (ironIngotCount >= 3)
            {
                IsFinishTutorial = true;
                return;
            }
            
            MouseCursorDescription.Instance.SetEnable(true);
            //採掘モードじゃなければ、採掘モードにする説明を出す
            if (_uiStateControl.CurrentState != UIStateEnum.DeleteBar)
            {
                MouseCursorDescription.Instance.SetDescription("<b>最初の一歩<\\b>\n[G]キー を押して採掘/破壊モードにする");
                return;
            }

            if (ironIngotCount == 0)
            {
                MouseCursorDescription.Instance.SetDescription("<b>最初の一歩<\\b>\n左クリック長押しで鉄鉱石を採掘する");
                return;
            }
            
            MouseCursorDescription.Instance.SetDescription("<b>最初の一歩<\\b>\nGood! 3つ鉄鉱石を採掘しよう");
        }

        public void EndTutorial()
        {
            MouseCursorDescription.Instance.SetEnable(false);
        }
    }
}