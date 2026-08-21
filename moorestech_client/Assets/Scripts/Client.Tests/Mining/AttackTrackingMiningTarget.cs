using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Mining;
using Client.Game.InGame.SoundEffect;
using Core.Master;
using UnityEngine;

namespace Client.Tests.Mining
{
    internal sealed class AttackTrackingMiningTarget : IMiningTargetObject
    {
        public GameObject GameObject { get; }
        public SoundEffectType DestroySoundType => SoundEffectType.DestroyStone;
        public int AttackCallCount { get; private set; }
        private readonly List<ItemId> _recommendedToolItemIds = new();

        public AttackTrackingMiningTarget(string name, Transform parent)
        {
            GameObject = new GameObject(name);
            GameObject.transform.SetParent(parent);
        }

        public MiningStartOutcome TryBeginHandMining(ItemId equippedItemId, out MiningToolCandidate tool, out List<ItemId> recommendedToolItemIds)
        {
            // 打撃回数だけを見るfixtureなので常に採掘可能として応じる
            // This fixture only counts attacks, so it always reports itself as minable
            tool = new MiningToolCandidate(equippedItemId, 0.01f);
            recommendedToolItemIds = _recommendedToolItemIds;
            return MiningStartOutcome.Ready;
        }

        public void SetFocused(bool focused)
        {
        }

        public void SendAttack()
        {
            AttackCallCount++;
        }
    }

    internal sealed class MiningCompleteSoundEffectFixture
    {
        private readonly GameObject _soundEffectObject;
        private readonly AudioClip _soundEffectClip;

        public MiningCompleteSoundEffectFixture()
        {
            // 実SE管理を初期化して完了状態の副作用を通す
            // Initialize the real SE manager to exercise completion side effects
            _soundEffectObject = new GameObject("SoundEffectManager");
            _soundEffectObject.SetActive(false);
            var manager = _soundEffectObject.AddComponent<SoundEffectManager>();
            SetField(manager, "audioSource", _soundEffectObject.AddComponent<AudioSource>());
            _soundEffectClip = AudioClip.Create("MiningComplete", 1, 1, 44100, false);
            foreach (var fieldName in new[] { "destroyBlockSound", "destroyStoneSound", "destroyTreeSound", "destroyBushSound", "placeBlockSound" })
                SetField(manager, fieldName, _soundEffectClip);
            InvokePrivate(manager, "Awake");
            _soundEffectObject.SetActive(true);

            #region Internal

            static void SetField(object target, string fieldName, object value)
            {
                var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                field.SetValue(target, value);
            }

            static void InvokePrivate(object target, string methodName)
            {
                var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                method.Invoke(target, null);
            }

            #endregion
        }

        public void Destroy()
        {
            Object.DestroyImmediate(_soundEffectObject);
            Object.DestroyImmediate(_soundEffectClip);
            var instanceField = typeof(SoundEffectManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            instanceField.SetValue(null, null);
        }

    }
}
