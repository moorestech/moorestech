using System;
using Client.Localization;
using Client.Ui.Foundation.Root;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// 言語変更に合わせてルートのロケールのクラスを付け替え、フォントの先頭を切り替える
    /// Re-applies the root locale class on language change to switch the leading font
    /// </summary>
    public class GameUiLocaleClassBinder : IInitializable, IDisposable
    {
        private readonly GameUiRoot _gameUiRoot;
        private IDisposable _subscription;

        public GameUiLocaleClassBinder(GameUiRoot gameUiRoot)
        {
            _gameUiRoot = gameUiRoot;
        }

        public void Initialize()
        {
            Apply();
            _subscription = Localize.OnLanguageChanged.Subscribe(_ => Apply());
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private void Apply()
        {
            _gameUiRoot.SetRootClassEnabled(GameUiLocaleClass.CjkClassName, GameUiLocaleClass.IsCjk(Localize.GetCurrentLanguageCode()));
        }
    }
}
