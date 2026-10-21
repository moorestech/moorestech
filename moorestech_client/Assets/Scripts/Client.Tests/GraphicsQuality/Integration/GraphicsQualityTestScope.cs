using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.GraphicsQuality;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.GraphicsQuality
{
    public abstract class GraphicsQualityTestScope
    {
        private string _saved;
        private bool _hadSaved;
        private bool _recovered;
        private static readonly FieldInfo RecoveryField = typeof(GraphicsQualityPreference).GetField("<RecoveredInvalidPreference>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        private object _observer;
        private readonly List<Object> _owned = new();
        private readonly Subject<GraphicsQualitySettings> _subject = (Subject<GraphicsQualitySettings>)GraphicsQualityPreference.OnSettingsChanged;
        private static readonly FieldInfo ObserverField = typeof(Subject<GraphicsQualitySettings>).GetField("outObserver", BindingFlags.Instance | BindingFlags.NonPublic);

        [SetUp]
        public void StashPreferenceAndObservers()
        {
            // ゲーム寿命の購読もテストの寿命へ隔離する
            // Isolate game-lifetime subscriptions to this test's lifetime
            _hadSaved = PlayerPrefs.HasKey(GraphicsQualityPreference.PreferenceKey);
            _saved = PlayerPrefs.GetString(GraphicsQualityPreference.PreferenceKey);
            _recovered = GraphicsQualityPreference.RecoveredInvalidPreference;
            RecoveryField.SetValue(null, false);
            _observer = ObserverField.GetValue(_subject);
            using var empty = new Subject<GraphicsQualitySettings>();
            ObserverField.SetValue(_subject, ObserverField.GetValue(empty));
            PlayerPrefs.DeleteKey(GraphicsQualityPreference.PreferenceKey);
        }

        [TearDown]
        public void RestorePreferenceAndObservers()
        {
            // 破棄済みfixtureへの通知を残さず、端末設定も復元する
            // Leave no notifications targeting destroyed fixtures and restore device preferences
            try
            {
                for (var index = _owned.Count - 1; 0 <= index; index--) Object.DestroyImmediate(_owned[index]);
            }
            finally
            {
                _owned.Clear();
                ObserverField.SetValue(_subject, _observer);
                RecoveryField.SetValue(null, _recovered);
                if (_hadSaved) PlayerPrefs.SetString(GraphicsQualityPreference.PreferenceKey, _saved);
                else PlayerPrefs.DeleteKey(GraphicsQualityPreference.PreferenceKey);
                PlayerPrefs.Save();
            }
        }

        protected T Own<T>(T value) where T : Object
        {
            _owned.Add(value);
            return value;
        }

        protected static void SetField(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        protected static object GetField(object target, string field)
        {
            return target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }
    }
}
