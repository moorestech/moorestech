using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.View.Object.Core;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.GraphicsQuality.Appliers
{
    /// <summary>
    /// 全ブロック・列車へエフェクト切替を付け、画質設定のエフェクト項目の変化を配る
    /// Attaches the effects switch to every block and train and distributes changes of the effects quality item
    /// </summary>
    public sealed class EffectsQualityApplier : IStartable
    {
        private readonly BlockGameObjectDataStore _blockDataStore;
        private readonly TrainCarObjectDatastore _trainCarDatastore;
        private readonly List<EffectsQualitySwitch> _switches = new();

        public EffectsQualityApplier(BlockGameObjectDataStore blockDataStore, TrainCarObjectDatastore trainCarDatastore)
        {
            _blockDataStore = blockDataStore;
            _trainCarDatastore = trainCarDatastore;
        }

        public void Start()
        {
            // 既存分へ付けてから生成通知を購読する
            // Attach to existing objects first, then subscribe to creation
            foreach (var block in _blockDataStore.BlockGameObjectDictionary.Values) Attach(block.gameObject);
            foreach (var trainCar in _trainCarDatastore.EnumerateTrainCars()) Attach(trainCar.gameObject);
            _blockDataStore.OnBlockPlaced.Subscribe(block => Attach(block.gameObject));
            _trainCarDatastore.OnTrainCarCreated.Subscribe(trainCar => Attach(trainCar.gameObject));
            GraphicsQualityPreference.OnSettingsChanged.Subscribe(settings => Apply(settings.EffectsEnabled));
        }

        private void Attach(GameObject root)
        {
            var effects = root.AddComponent<EffectsQualitySwitch>();
            effects.Initialize(GraphicsQualityPreference.Current.EffectsEnabled);
            _switches.Add(effects);
        }

        private void Apply(bool effectsEnabled)
        {
            // 撤去済み（破棄済み）の切替は配布前に外す
            // Drop switches whose objects were removed before distributing
            _switches.RemoveAll(effects => effects == null);
            foreach (var effects in _switches) effects.SetEffectsEnabled(effectsEnabled);
        }
    }
}
