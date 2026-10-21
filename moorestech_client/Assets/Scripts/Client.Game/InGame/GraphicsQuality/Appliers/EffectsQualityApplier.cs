using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.View.Object.Core;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.GraphicsQuality.Appliers
{
    /// <summary>
    /// - 全ブロック・列車へ切替を付与
    /// - エフェクト設定変更を配信
    /// - Attach switches to all blocks and cars
    /// - Distribute effects-setting changes
    /// </summary>
    public sealed class EffectsQualityApplier : IStartable
    {
        private readonly BlockGameObjectDataStore _blockDataStore;
        private readonly TrainCarObjectDatastore _trainCarDatastore;
        private readonly HashSet<EffectsQualitySwitch> _switches = new();

        public EffectsQualityApplier(BlockGameObjectDataStore blockDataStore, TrainCarObjectDatastore trainCarDatastore)
        {
            _blockDataStore = blockDataStore;
            _trainCarDatastore = trainCarDatastore;
        }

        public void Start()
        {
            // 既存分へ付けてから生成通知を購読する
            // Attach to existing objects first, then subscribe to creation
            foreach (var block in _blockDataStore.BlockGameObjectDictionary.Values) AttachBlock(block);
            foreach (var trainCar in _trainCarDatastore.EnumerateTrainCars()) Attach(trainCar.gameObject);
            _blockDataStore.OnBlockPlaced.Subscribe(AttachBlock);
            _trainCarDatastore.OnTrainCarCreated.Subscribe(trainCar => Attach(trainCar.gameObject));
            GraphicsQualityPreference.OnSettingsChanged.Subscribe(settings => Apply(settings.EffectsEnabled));

            #region Internal

            void AttachBlock(BlockGameObject block)
            {
                var effects = Attach(block.gameObject);
                // 設置演出が子を再有効化した直後に現在の画質を再適用する
                // Reapply the current quality immediately after placement reactivates children
                block.OnFinishedPlaceAnimation.Subscribe(_ => effects.ReapplyAfterPlacement(GraphicsQualityPreference.Current.EffectsEnabled));
            }

            void Apply(bool effectsEnabled)
            {
                foreach (var effects in _switches) effects.SetEffectsEnabled(effectsEnabled);
            }

            #endregion
        }

        private EffectsQualitySwitch Attach(GameObject root)
        {
            var effects = root.AddComponent<EffectsQualitySwitch>();
            effects.Initialize(GraphicsQualityPreference.Current.EffectsEnabled);
            _switches.Add(effects);
            // 非activeのままの破棄も監視して保持を外す
            // Monitor destruction before activation as well to release retained switches
            Disposable.Create(() => _switches.Remove(effects)).AddTo(root);
            return effects;
        }

    }
}
