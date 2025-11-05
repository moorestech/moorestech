using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.Control;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.Skit;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class GameScreenState : IUIState
    {
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        private readonly InGameCameraController _inGameCameraController;
        private readonly SkitManager _skitManager;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly SubInventoryState _subInventoryState;
        
        public GameScreenState(
            IPlacementPreviewBlockGameObjectController previewBlockController,
            SkitManager skitManager,
            InGameCameraController inGameCameraController,
            BlockGameObjectDataStore blockGameObjectDataStore,
            SubInventoryState subInventoryState)
        {
            _previewBlockController = previewBlockController;
            _skitManager = skitManager;
            _inGameCameraController = inGameCameraController;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _subInventoryState = subInventoryState;
        }
        
        public UIStateEnum GetNextUpdate()
        {
            if (InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.PlayerInventory;
            if (InputManager.UI.OpenMenu.GetKeyDown) return UIStateEnum.PauseMenu;
            
            // ブロックインベントリのクリック判定
            // Block inventory click detection
            if (BlockClickDetect.IsClickOpenableBlock())
            {
                if (BlockClickDetect.TryGetCursorOnBlockPosition(out var blockPos) &&
                    _blockGameObjectDataStore.TryGetBlockGameObject(blockPos, out var blockGameObject))
                {
                    var blockSource = new BlockInventorySource(blockPos, blockGameObject);
                    _subInventoryState.SetInventorySource(blockSource);
                    return UIStateEnum.SubInventory;
                }
            }
            
            // 列車インベントリのクリック判定（将来の実装用）
            // Train inventory click detection (for future implementation)
            if (BlockClickDetect.TryGetCursorOnComponent(out TrainEntityObject trainEntity))
            {
                // TODO: 列車がクリックされた時の処理
                // TODO: Handle train click
                // var trainSource = new TrainInventorySource(trainEntity.TrainId, trainEntity);
                // _subInventoryState.SetInventorySource(trainSource);
                // return UIStateEnum.SubInventory;
            }
            
            if (InputManager.UI.BlockDelete.GetKeyDown) return UIStateEnum.DeleteBar;
            if (_skitManager.IsPlayingSkit) return UIStateEnum.Story;
            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetKeyDown(KeyCode.B)) return UIStateEnum.PlaceBlock;
            if (UnityEngine.Input.GetKeyDown(KeyCode.T)) return UIStateEnum.ChallengeList;
            
            return UIStateEnum.Current;
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            InputManager.MouseCursorVisible(false);
            _inGameCameraController.SetControllable(true);
            
            KeyControlDescription.Instance.SetText("Tab: インベントリ\n1~9: アイテム持ち替え\nB: ブロック配置\nG:ブロック削除\nT: チャレンジ一覧\n");
        }
        
        public void OnExit()
        {
            _inGameCameraController.SetControllable(false);
        }
    }
}