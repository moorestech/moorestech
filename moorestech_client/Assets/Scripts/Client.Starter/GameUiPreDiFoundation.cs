using Client.Ui.Foundation.Composite;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Foundation.Text;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace Client.Starter
{
    /// <summary>
    /// GameUi の系で DI より前に 1 本だけ立てる土台（sRGB 合成・ロケールのクラス・倍率の見張り）。DI 前の画面は倍率の読み口を使い、DI へは作り済みのまま渡す
    /// The GameUi foundation built once before DI (sRGB composite, locale class, scale watch); pre-DI screens read its scale source and DI receives it as built
    /// </summary>
    /// <remarks>
    /// 出展モードか否かで DI の登録の形を分けないため、GameUi の系では常にここで立てる（A-183・A-75 の置き場を改めた。S5-gate D-2）。ゲーム寿命なので破棄の口を持たない（ゲームはメインメニューへ戻らない。AGENTS.md の既知の制約）
    /// Always built here in the GameUi system so DI registration never splits by exhibition mode (relocates A-183 and A-75; S5-gate D-2); it lives as long as the game, so it has no disposal (the game never returns to the main menu; AGENTS.md known constraint)
    /// </remarks>
    internal sealed class GameUiPreDiFoundation
    {
        // DI 前の画面と DI が共有する倍率の読み口
        // Scale source shared by pre-DI screens and DI
        public StageScaleSource StageScale { get; }

        private readonly GameUiRoot _gameUiRoot;
        private readonly GameUiSrgbCompositeInstaller _composite;
        private readonly GameUiLocaleClassBinder _localeClass;
        private readonly GameUiStageScaleWatch _stageScaleWatch;
        private IVisualElementScheduledItem _compositeTickBeforeDi;

        public GameUiPreDiFoundation(GameUiRoot gameUiRoot)
        {
            _gameUiRoot = gameUiRoot;
            _composite = new GameUiSrgbCompositeInstaller(gameUiRoot);
            _localeClass = new GameUiLocaleClassBinder(gameUiRoot);
            _stageScaleWatch = new GameUiStageScaleWatch(gameUiRoot);
            StageScale = new StageScaleSource(_stageScaleWatch.Scale);
        }

        public void Initialize()
        {
            // 合成はパネルを差し替えるので、root.panel を読む見張りより先に立てる（DI の入口だったときの並び :39・:46 と同じ順）
            // The composite swaps the panel, so it starts before the watch that reads root.panel (same order as the former DI entry points at :39 and :46)
            _composite.Initialize();
            _localeClass.Initialize();
            _stageScaleWatch.Initialize();

            // DI の ITickable が回り出すまでは、画面の大きさの追従をルートの予定で回す（言語ゲートの待ちの間に解像度が変わりうる）
            // Until DI's ITickable runs, follow the screen size on the root's schedule (the resolution may change while the language gate waits)
            _compositeTickBeforeDi = _gameUiRoot.GetRootElement().schedule.Execute(_composite.Tick).Every(0);
        }

        // DI へ作り済みのまま渡す。DI 前の毎フレームはここで止め、以後は DI の ITickable が回す（同じ物を二度 Initialize しない）
        // Hand over to DI as built; the pre-DI per-frame tick stops here and DI's ITickable runs it from now on (never Initialize the same object twice)
        public void HandOverTo(ContainerBuilder builder)
        {
            _compositeTickBeforeDi.Pause();
            builder.RegisterInstance(_composite).As<ITickable>();
            builder.RegisterInstance(StageScale);
        }
    }
}
