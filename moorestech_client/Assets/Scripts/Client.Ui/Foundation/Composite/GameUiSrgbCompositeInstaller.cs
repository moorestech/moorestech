using Client.Ui.Foundation.Root;
using VContainer.Unity;

namespace Client.Ui.Foundation.Composite
{
    /// <summary>
    /// GameUi の系でだけパネルを sRGB 合成の RT へ描かせ、画面の大きさを追う（A-180・A-183）
    /// Only in the GameUi system, draws the panel into the sRGB composite RT and follows the screen size (A-180, A-183)
    /// </summary>
    /// <remarks>
    /// Web の系では作られないので RT も合成パスも無い。GameUi の系では DI より前に GameUiPreDiFoundation が作って Initialize し、DI へは ITickable として渡す（S5-gate D-2）。パネルを差し替えるので root.panel を控える入口より先に立つ
    /// Never built in the Web system, so neither the RT nor the composite passes exist; in the GameUi system GameUiPreDiFoundation builds and initializes it before DI and hands it to DI as an ITickable (S5-gate D-2); it swaps the panel, so it starts before entry points that keep root.panel
    /// </remarks>
    public sealed class GameUiSrgbCompositeInstaller : IInitializable, ITickable
    {
        private readonly GameUiRoot _gameUiRoot;
        private UiSrgbPanelTarget _panelTarget;

        public GameUiSrgbCompositeInstaller(GameUiRoot gameUiRoot)
        {
            _gameUiRoot = gameUiRoot;
        }

        public void Initialize()
        {
            _panelTarget = new UiSrgbPanelTarget(_gameUiRoot.GetPanelDocument());
        }

        // 画面の大きさの変化通知が無いので毎フレーム比べる（前例 ScreenFrameReader）
        // No screen-size change notification exists, so compare every frame (precedent ScreenFrameReader)
        public void Tick()
        {
            // silent-ok: 生成失敗は Initialize の例外として初期化の失敗（MainGameSceneLoadedHandler の LogError。RT を作れないときはアプリ終了、それ以外はメインメニュー）になり Tick は回らない / 開発者は Console の最初の例外で、プレイヤーはアプリの終了かメインメニューへ戻ることで気づく
            if (_panelTarget == null) return;
            _panelTarget.SyncToScreenSize();
            UiSrgbCompositeSource.ReportWhenNotComposited();
        }

    }
}
