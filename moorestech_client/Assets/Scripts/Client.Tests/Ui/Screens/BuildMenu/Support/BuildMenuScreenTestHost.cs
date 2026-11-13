using System;
using System.Linq;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.Hotbar;
using Client.Ui.Components.Controls;
using Client.Ui.Content;
using Client.Ui.Screens.BuildMenu;
using Client.Ui.Screens.BuildMenu.CategoryScroll;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Session;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BuildMenu.Support
{
    /// <summary>
    /// ビルドメニュー画面の L1 の土台（View・セッション・トラッカーを組み、開閉と待ちを持つ）
    /// Build-menu screen L1 base: assembles the view, session and tracker, and owns open/close and settling
    /// </summary>
    public abstract class BuildMenuScreenTestHost : ScreenTestHost
    {
        // レイアウト・スペーサの反映・300ms の smooth scroll が収まるまで進める時間
        // Time to advance so layout, spacer application and the 300 ms smooth scroll all finish
        private const int SettleFrames = 12;
        private const int SettleFrameMilliseconds = 50;

        protected RecordingBuildMenuCommands Commands { get; private set; }
        protected RecordingHotbarCommands HotbarCommands { get; private set; }
        protected BuildMenuSessionStore Session { get; private set; }
        protected BuildMenuCategoryScrollTracker Tracker { get; private set; }
        protected BuildMenuView View { get; private set; }
        protected GameContentPresentation Content { get; private set; }

        protected GameScrollView Scroll => Find<GameScrollView>(View, "build-menu-scroll-area");
        protected VisualElement Detail => Find<VisualElement>(View, "build-menu-detail");

        [SetUp]
        public void SetUpBuildMenuScreen()
        {
            // 素材の名前と絵はテスト mod から引く（前例 BuildMenuSlotTooltipTest）
            // Material names and pictures come from the test mod (precedent BuildMenuSlotTooltipTest)
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Content = new GameContentPresentation(new FakeContentTextureSource(Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture));
            Commands = new RecordingBuildMenuCommands();
            HotbarCommands = new RecordingHotbarCommands();
            Session = new BuildMenuSessionStore();
            Tracker = new BuildMenuCategoryScrollTracker(Session);
            View = Mount(new BuildMenuView(Commands, HotbarCommands, Content, BuildMenuSamples.Icons(Content), Toaster, Session, Tracker));
        }

        // 本番の Presenter と同じ順（描いてから出す）で開く
        // Open in the production presenter's order: render, then show
        protected void OpenMenu(BuildMenuViewModel viewModel)
        {
            View.Render(viewModel);
            View.SetShown(true);
            Settle();
        }

        protected void CloseMenu()
        {
            View.SetShown(false);
            Settle();
        }

        protected void Settle()
        {
            for (var i = 0; i < SettleFrames; i++) simulate.FrameUpdateMs(SettleFrameMilliseconds);
        }

        protected void Search(string query)
        {
            Find<TextField>(View, "build-menu-search").value = query;
            Settle();
        }

        protected void Hover(VisualElement element)
        {
            simulate.MouseMove(new Vector2(1f, 1f), element.worldBound.center);
            Settle();
        }

        protected VisualElement Entry(string kind, Guid targetId) => Find<VisualElement>(View, $"build-menu-entry-{kind}-{targetId:D}");

        protected Button CategoryButton(Guid categoryGuid) => Find<Button>(View, $"build-menu-category-{categoryGuid:D}");

        protected Button[] CategoryButtons() => Find<VisualElement>(View, "build-menu-sidebar").Query<Button>().ToList().ToArray();

        // Web の heading.offsetTop（視口の内余白込み）と同じ基準の、中身の上端からの距離
        // Distance from the content top, on the same basis as the Web heading.offsetTop (viewport padding included)
        protected float HeadingTop(Guid categoryGuid)
        {
            Assert.IsTrue(Scroll.TryGetContentTop(Find<VisualElement>(View, $"build-menu-category-heading-{categoryGuid:D}"), out var top));
            return top;
        }

        protected static string TextOf(VisualElement root)
        {
            return string.Join("\n", root.Query<TextElement>().ToList().Select(text => text.text).Where(text => !string.IsNullOrEmpty(text)));
        }
    }
}
