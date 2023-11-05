using MainGame.Basic;
using MainGame.UnityView.UI.Inventory.Control;
using MainGame.UnityView.UI.Tutorial;
using MainGame.UnityView.UI.UIState;
using SinglePlay;

namespace MainGame.Presenter.Tutorial.ExecutableTutorials
{
    /// <summary>
    ///     鉄の採掘をさせるチュートリアル
    /// </summary>
    public class _0_IronMiningTutorial : IExecutableTutorial
    {
        private const string IronOreItemName = "iron ore";
        private readonly int _ironItemId;
        private readonly PlayerInventoryViewModel _playerInventoryViewModel;

        private readonly UIStateControl _uiStateControl;

        public _0_IronMiningTutorial(UIStateControl uiStateControl, PlayerInventoryViewModel playerInventoryViewModel, SinglePlayInterface singlePlayInterface)
        {
            _uiStateControl = uiStateControl;
            _playerInventoryViewModel = playerInventoryViewModel;
            _ironItemId = singlePlayInterface.ItemConfig.GetItemId(AlphaMod.ModId, IronOreItemName);
        }

        public bool IsFinishTutorial { get; private set; }


        public void StartTutorial()
        {
        }

        public void Update()
        {
            //すでに終了していたら処理をしない
            if (IsFinishTutorial) return;

            //鉄鉱石がメインインベントリに3つあるかをチェックする あったら完了にする
            var ironIngotCount = 0;
            foreach (var item in _playerInventoryViewModel.MainInventory)
                if (item.Id == _ironItemId)
                    ironIngotCount += item.Count;
            if (ironIngotCount >= 3)
            {
                IsFinishTutorial = true;
                return;
            }

            MouseCursorDescription.Instance.SetEnable(true);
            //採掘モードじゃなければ、採掘モードにする説明を出す
            if (_uiStateControl.CurrentState != UIStateEnum.DeleteBar)
            {
                MouseCursorDescription.Instance.SetDescription("<size=27>最初の一歩</size>\n[G]キー を押して採掘/破壊モードにする");
                return;
            }

            if (ironIngotCount == 0)
            {
                MouseCursorDescription.Instance.SetDescription("<size=27>最初の一歩</size>\n左クリック長押しで鉄鉱石を採掘する");
                return;
            }

            MouseCursorDescription.Instance.SetDescription("<size=27>最初の一歩</size>\nGood! 3つ鉄鉱石を採掘しよう");
        }

        public void EndTutorial()
        {
            MouseCursorDescription.Instance.SetEnable(false);
        }
    }
}