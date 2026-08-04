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
        public bool IsAvailable => true;
        public bool IsPickUp => false;
        public List<ItemId> UsableToolItemIds { get; } = new();
        public SoundEffectType DestroySoundType => SoundEffectType.DestroyStone;
        public int AttackCallCount { get; private set; }

        public AttackTrackingMiningTarget(string name, Transform parent)
        {
            GameObject = new GameObject(name);
            GameObject.transform.SetParent(parent);
        }

        public bool TryResolveUsableTool(ItemId equippedItemId, out MiningToolCandidate tool)
        {
            tool = default;
            return false;
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
        }

        public void Destroy()
        {
            Object.DestroyImmediate(_soundEffectObject);
            Object.DestroyImmediate(_soundEffectClip);
            var instanceField = typeof(SoundEffectManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            instanceField.SetValue(null, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, null);
        }
    }
}
