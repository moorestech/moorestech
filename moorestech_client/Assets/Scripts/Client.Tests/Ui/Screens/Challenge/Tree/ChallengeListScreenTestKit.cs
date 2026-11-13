using System;
using System.Text.RegularExpressions;
using Client.Game.Localization;
using Client.Localization;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Challenge.CurrentHud;
using Client.Ui.Screens.Challenge.Tree;
using Client.Ui.State.Challenge;
using Client.Ui.State.Challenge.Commands;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Challenge.Tree.Selection;
using Client.Ui.State.Text;
using Core.Master;
using Game.Context;
using Mod.Loader;
using Mooresmaster.Localization.Generated;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧 L1 の共通材料
    /// Shared material for challenge list L1 tests
    /// </summary>
    internal static class ChallengeListScreenTestKit
    {
        public static readonly Guid Basics = Guid.Parse("03ca4ded-3b2b-4e7f-bb6e-430f060c4ed1");
        public static readonly Guid Logistics = Guid.Parse("35330f9d-f44f-493d-a6bc-07ae6413d7c4");
        public static readonly Guid FirstCraft = Guid.Parse("00000000-0000-0000-4567-000000000001");
        public static readonly Guid SecondStep = Guid.Parse("00000000-0000-0000-4567-000000000002");
        public static readonly Guid LockedStep = Guid.Parse("00000000-0000-0000-4567-000000000003");
        public static readonly Guid LogisticsDone = Guid.Parse("c2d84cfb-73cb-4ffa-99b5-9703c550f330");
        public static readonly Guid LogisticsCurrent = Guid.Parse("74fe5f1f-5671-4924-ad36-4447fd80a9c3");

        private static readonly Regex RichTextTag = new("<[^>]+>");

        // テスト mod を読み名前をマスタ原文で引く（前例 LocalizeContentTest）
        // Load the test mod to resolve master text (precedent LocalizeContentTest)
        public static void LoadMasterAndDictionary()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var masterContainer = ServerContext.GetService<MasterJsonFileContainer>();
            Localize.MergeGameDictionaries(ServerContext.GetService<ModsResource>(), masterContainer.SortedModIds, MasterSourceTextCollector.Collect());
        }

        // Web mock と同形の木。Basics に未解放を足す
        // Web mock-shaped tree plus one locked node in Basics
        public static ChallengeListViewModel Tree(ChallengeCategorySelection selection)
        {
            var basics = ChallengeCategoryViewModel.Create(Basics, new[]
            {
                Node(FirstCraft, Vector2.zero, ChallengeNodeState.Completed),
                Node(SecondStep, new Vector2(220f, 0f), ChallengeNodeState.Current, FirstCraft),
                Node(LockedStep, new Vector2(0f, -200f), ChallengeNodeState.Locked, SecondStep),
            });
            var logistics = ChallengeCategoryViewModel.Create(Logistics, new[]
            {
                Node(LogisticsDone, Vector2.zero, ChallengeNodeState.Completed),
                Node(LogisticsCurrent, new Vector2(1400f, 700f), ChallengeNodeState.Current, LogisticsDone),
            });
            return ChallengeListViewModel.Create(new[] { basics, logistics }, selection);
        }

        // 一覧を描いて OverlayPanel・Stage 層へ載せる（前例 ResearchTreePresenter）
        // Render the list onto the OverlayPanel/Stage layer (precedent ResearchTreePresenter)
        public static ChallengeListView MountList(UiLayerStack layers, IChallengeListCommands commands)
        {
            var view = new ChallengeListView(commands, new UiOsCursorRequestStack(new FakeUiOsCursorApplier()));
            view.Render(Tree(ChallengeCategorySelection.None));
            view.SetShown(true);
            layers.GetLayer(UiLayer.OverlayPanel, UiFamily.Stage).Add(view);
            return view;
        }

        // 進行中 HUD を Chrome・Viewport 層へ載せる
        // Mount the current HUD on the Chrome/Viewport layer
        public static CurrentChallengeHudView MountHud(UiLayerStack layers)
        {
            var hud = new CurrentChallengeHudView();
            layers.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(hud);
            hud.Render(CurrentChallengeHudViewModel.Create(new[] { UiText.FromContent(ContentLocalizationKeys.ChallengeTitle(SecondStep)) }, false));
            return hud;
        }

        public static VisualElement Card(VisualElement root, Guid challengeGuid)
        {
            return root.Q($"challenge-node-{challengeGuid}");
        }

        // LocalizedLabel は行高のタグを前置するので外す（前例 ChallengeHudTestText.Plain）
        // LocalizedLabel prefixes a line-height tag, so strip it (precedent ChallengeHudTestText.Plain)
        public static string PlainText(Label label)
        {
            return RichTextTag.Replace(label.text, string.Empty);
        }

        public static string ButtonText(Button button)
        {
            return PlainText(button.Q<Label>());
        }

        private static ChallengeNodeCardViewModel Node(Guid guid, Vector2 position, ChallengeNodeState state, params Guid[] previous)
        {
            return new ChallengeNodeCardViewModel(guid, position, Vector2.one, previous, state);
        }
    }
}
