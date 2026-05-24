using Client.Game.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Control;
using Client.Game.InGame.Entity.Object;
using Client.Input;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    public class GameScreenSubInventoryInteractService
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        public GameScreenSubInventoryInteractService(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }
        
        /// <summary>
        /// ブロックや列車など、SubInventoryを開けるオブジェクトをクリックしたかどうかを判定します。
        /// Determines whether an object that can open a SubInventory, such as a block or train
        /// </summary>
        public bool TryGetSubInventoryInteractObject(out UITransitContext uiTransitContext)
        {
            uiTransitContext = null;
            
            // クリックしてなければ無視
            // Ignore if not clicked
            if (!InputManager.Playable.ScreenLeftClick.GetKeyDown) return false;
            if (EventSystem.current.IsPointerOverGameObject()) return false;
            
            
            if (BlockClickDetectUtil.TryGetCursorOnBlockPosition(out var blockPos) && // クリックしたブロックの位置を取得 Get position of clicked block
                _blockGameObjectDataStore.TryGetBlockGameObject(blockPos, out var blockGameObject) && // ブロックのGameObjectを取得 Get Block GameObject
                blockGameObject.BlockMasterElement.IsBlockOpenable()) // ブロックが開けるタイプか確認 Check if block is openable
            {
                var container = UITransitContextContainer.Create<ISubInventorySource>(new BlockSubInventorySource(blockGameObject));
                
                uiTransitContext = new UITransitContext(UIStateEnum.SubInventory, container);
                return true;
            }
            
            if (BlockClickDetectUtil.TryGetCursorOnComponent(out TrainCarEntityChildrenObject trainEntity))
            {
                var container = UITransitContextContainer.Create<ISubInventorySource>(new TrainSubInventorySource(trainEntity.TrainCarEntityObject));
                
                uiTransitContext = new UITransitContext(UIStateEnum.SubInventory, container);
                return true;
            }
            
            return false;
        }
    }
}