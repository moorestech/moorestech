using System;
using System.Collections.Generic;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.State.Toast;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens
{
    /// <summary>
    /// 画面の L1 の土台。言語を英語に固定して TearDown で保存値ごと戻し、出たトーストを集める
    /// Base for screen L1 tests: pins the language to English and restores it with the saved value in TearDown, and collects toasts
    /// </summary>
    public abstract class ScreenTestHost : UiTestHost
    {
        private const string SelectedClass = "mode-switch__option--selected";

        private bool _hadSavedLanguageCode;
        private string _savedLanguageCode;
        private IDisposable _toastSubscription;

        protected GameUiToastFeed Toasts { get; private set; }
        protected UiCommandResultToaster Toaster { get; private set; }
        protected List<GameUiToast> ShownToasts { get; } = new();

        [SetUp]
        public void SetUpScreenHost()
        {
            _hadSavedLanguageCode = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguageCode = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            Localize.Initialize();
            Localize.TrySetChosenLanguage("english");

            Toasts = new GameUiToastFeed();
            Toaster = new UiCommandResultToaster(Toasts);
            ShownToasts.Clear();
            _toastSubscription = Toasts.OnToast.Subscribe(ShownToasts.Add);
        }

        [TearDown]
        public void TearDownScreenHost()
        {
            // 載せた View を外して購読を切り、言語の保存値と有無を戻す
            // Detach the views to drop their subscriptions, then restore the saved language value and its presence
            rootVisualElement.Clear();
            _toastSubscription.Dispose();
            if (_hadSavedLanguageCode) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguageCode);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            Localize.Initialize();
        }

        protected T Mount<T>(T view) where T : VisualElement
        {
            rootVisualElement.Add(view);
            simulate.FrameUpdate();
            return view;
        }

        protected static T Find<T>(VisualElement root, string testid) where T : VisualElement
        {
            var found = root.Q<T>(testid);
            Assert.IsNotNull(found, testid);
            return found;
        }

        // 直前の差し替えを解決済みの表示へ反映してから見る
        // Flush the latest change into the resolved style before reading it
        protected bool IsShown(VisualElement element)
        {
            simulate.FrameUpdate();
            return element.panel != null && element.resolvedStyle.display == DisplayStyle.Flex;
        }

        protected static bool IsSelected(VisualElement root, string optionTestid)
        {
            return Find<Button>(root, optionTestid).ClassListContains(SelectedClass);
        }

        protected static int SelectedCount(VisualElement modeSwitch)
        {
            return modeSwitch.Query<Button>(className: SelectedClass).ToList().Count;
        }
    }
}
