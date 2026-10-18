using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.Preview;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State.PlaceBlock
{
    internal sealed class PlaceBlockPreviewDisplay
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly List<IDisposable> _subscriptions = new();

        public PlaceBlockPreviewDisplay(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        public void OnEnter()
        {
            // 当たり判定は全ブロック有効にし、描画はプレイヤー半径内だけにする
            // Enable colliders on every block, but render only within the player radius
            foreach (var block in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                block.SetPreviewOnlyObjectsDisplay(PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
            }
            _subscriptions.Add(_blockGameObjectDataStore.OnBlockPlaced.Subscribe(OnPlaceBlock));
        }

        public void OnExit()
        {
            // 退出後にアニメーション完了が表示を再開しないよう購読を外す
            // Unsubscribe so animation completion cannot re-enable display after leaving
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
            foreach (var block in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                block.SetPreviewOnlyObjectsDisplay(PreviewOnlyObjectsDisplay.Hidden);
            }
        }

        private void OnPlaceBlock(BlockGameObject block)
        {
            // アニメーション無しの設置は完了通知が来ないので即座に半径判定へ
            // A placement without animation never gets the finish notice, so follow the radius right away
            if (!block.IsPlayingPlaceAnimation)
            {
                block.SetPreviewOnlyObjectsDisplay(PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius);
                return;
            }

            // 設置中は当たり判定だけ残し、完了時に半径に従って描画する
            // Keep only colliders during placement and render according to the radius on completion
            block.SetPreviewOnlyObjectsDisplay(PreviewOnlyObjectsDisplay.CollidersOnly);
            _subscriptions.Add(block.OnFinishedPlaceAnimation.Subscribe(_ =>
                block.SetPreviewOnlyObjectsDisplay(PreviewOnlyObjectsDisplay.RenderedWithinPlayerRadius)));
        }
    }
}
