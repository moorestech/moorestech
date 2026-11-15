using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.Preview;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
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
            // 設置モード中だけ、照準が占有セルを走査する
            // The aim traverses occupied cells only while in placement mode
            PlacementAimTargetCells.Enable(_blockGameObjectDataStore.OccupiedCells);

            // 入場時の既設ブロックも設置途中なら完了まで描画を待つ
            // Existing blocks entering mid-placement also wait for completion before rendering
            foreach (var block in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                OnPlaceBlock(block);
            }
            _subscriptions.Add(_blockGameObjectDataStore.OnBlockPlaced.Subscribe(OnPlaceBlock));

            #region Internal

            void OnPlaceBlock(BlockGameObject block)
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

            #endregion
        }

        public void OnExit()
        {
            // 設置モードを出たら照準は物理レイの結果だけに戻る
            // Leaving placement mode returns the aim to the physics ray result only
            PlacementAimTargetCells.Disable();

            // 退出後にアニメーション完了が表示を再開しないよう購読を外す
            // Unsubscribe so animation completion cannot re-enable display after leaving
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
            foreach (var block in _blockGameObjectDataStore.BlockGameObjectDictionary.Values)
            {
                block.SetPreviewOnlyObjectsDisplay(PreviewOnlyObjectsDisplay.Hidden);
            }
        }
    }
}
