using MainGame.Basic;
using MainGame.Presenter.Tutorial.Util;
using MainGame.UnityView.UI.Inventory.Control;
using MainGame.UnityView.UI.Tutorial;
using MainGame.UnityView.UI.UIState;
using SinglePlay;

namespace MainGame.Presenter.Tutorial.ExecutableTutorials
{
    public class _1_MinerCraftTutorial : IExecutableTutorial
    {
        public bool IsFinishTutorial { get; private set; }
        private readonly HighlightRecipeViewerItem _highlightRecipeViewerItem;
        private readonly GameUIHighlight _gameUIHighlight;
        private readonly UIStateControl _uiStateControl;
        private readonly InventoryItemCountChecker _inventoryChecker;

        private IronTutorialState _currentState = IronTutorialState.ChangeCraftMode;

        public _1_MinerCraftTutorial(HighlightRecipeViewerItem highlightRecipeViewerItem,GameUIHighlight gameUIHighlight,UIStateControl uiStateControl,SinglePlayInterface singlePlayInterface,PlayerInventoryViewModel playerInventoryViewModel)
        {
            _highlightRecipeViewerItem = highlightRecipeViewerItem;
            _gameUIHighlight = gameUIHighlight;
            _uiStateControl = uiStateControl;
            _inventoryChecker = new InventoryItemCountChecker(playerInventoryViewModel, singlePlayInterface.ItemConfig);
        }
        
        
        public void StartTutorial()
        {
        }

        public void Update()
        {
            MouseCursorDescription.Instance.SetEnable(true);
            switch (_currentState)
            {
                case IronTutorialState.ChangeCraftMode:
                    MouseCursorDescription.Instance.SetDescription("<size=27>最初の一歩</size>\n[Tab]を押してインベントリを表示する");
                    if (_uiStateControl.CurrentState == UIStateEnum.PlayerInventory )
                    {
                        _currentState = IronTutorialState.IronIngotRecipeViewer;
                    }
                    break;
                
                case IronTutorialState.IronIngotRecipeViewer:
                    _highlightRecipeViewerItem.SetHighLight(AlphaMod.ModId,"iron ingot",true);
                    MouseCursorDescription.Instance.SetDescription("<size=27>最初の一歩</size>\n右のレシピビューワーから、鉄インゴットを選択しよう");
                    if (_uiStateControl.CurrentState == UIStateEnum.RecipeViewer )
                    {
                        _highlightRecipeViewerItem.SetHighLight(AlphaMod.ModId,"iron ingot",false);
                        _currentState = IronTutorialState.PlaceIronIngot;
                    }
                    break;
                
                case IronTutorialState.PlaceIronIngot:
                    _gameUIHighlight.SetHighlight(HighlightType.CraftItemPutButton,true);
                    MouseCursorDescription.Instance.SetDescription("<size=27>最初の一歩</size>\nアイテム配置ボタンを押して、クラフトスロットに鉄インゴットのレシピを配置しよう");
                    if (_uiStateControl.CurrentState == UIStateEnum.PlayerInventory)
                    {
                        _gameUIHighlight.SetHighlight(HighlightType.CraftItemPutButton,false);
                        _currentState = IronTutorialState.CraftIronIngot;
                    }
                    break;
                
                case IronTutorialState.CraftIronIngot:
                    _gameUIHighlight.SetHighlight(HighlightType.CraftResultSlot,true);
                    MouseCursorDescription.Instance.SetDescription("<size=27>最初の一歩</size>\n鉄インゴットを3つクラフトしよう");
                    if (_inventoryChecker.ExistItemMainInventory(AlphaMod.ModId,"iron ingot",3))
                    {
                        _gameUIHighlight.SetHighlight(HighlightType.CraftResultSlot,false);
                        _currentState = IronTutorialState.MinerRecipeViewer;
                    }
                    break;
                
                
                
                //ここ、鉄のクラフトと採掘機のクラフトのコードがほとんど同じでDRY原則に反しているが、
                //共通化するコストの高さと、チュートリアルという一時的なコードであるため、今回は共通化しない
                case IronTutorialState.MinerRecipeViewer:
                    _highlightRecipeViewerItem.SetHighLight(AlphaMod.ModId,"miner",true);
                    MouseCursorDescription.Instance.SetDescription("<size=27>最初の一歩</size>\nいいね！次は採掘機を作成しよう！\nレシピビューワーから、採掘機を選択しよう");
                    if (_uiStateControl.CurrentState == UIStateEnum.RecipeViewer )
                    {
                        _highlightRecipeViewerItem.SetHighLight(AlphaMod.ModId,"miner",false);
                        _currentState = IronTutorialState.PlaceMiner;
                    }
                    break;
                
                case IronTutorialState.PlaceMiner:
                    _gameUIHighlight.SetHighlight(HighlightType.CraftItemPutButton,true);
                    MouseCursorDescription.Instance.SetDescription("<size=27>最初の一歩</size>\nアイテム配置ボタンを押して、クラフトスロットに採掘機のレシピを配置しよう");
                    if (_uiStateControl.CurrentState == UIStateEnum.PlayerInventory)
                    {
                        _gameUIHighlight.SetHighlight(HighlightType.CraftItemPutButton,false);
                        _currentState = IronTutorialState.CraftMiner;
                    }
                    break;
                
                case IronTutorialState.CraftMiner:
                    _gameUIHighlight.SetHighlight(HighlightType.CraftResultSlot,true);
                    MouseCursorDescription.Instance.SetDescription("<size=27>最初の一歩</size>\n採掘機をクラフトしよう！");
                    if (_inventoryChecker.ExistItemMainInventory(AlphaMod.ModId,"miner",1))
                    {
                        _gameUIHighlight.SetHighlight(HighlightType.CraftResultSlot,false);
                        _currentState = IronTutorialState.Finish;
                    }
                    break;
                    
                
            }
            
            
            IsFinishTutorial = _currentState == IronTutorialState.Finish;
        }

        public void EndTutorial()
        {
            MouseCursorDescription.Instance.SetEnable(false);
        }

        enum IronTutorialState 
        {
            ChangeCraftMode,
            IronIngotRecipeViewer,
            PlaceIronIngot,
            CraftIronIngot,
            
            MinerRecipeViewer,
            PlaceMiner,
            CraftMiner,
            
            Finish,
        }
    }
}