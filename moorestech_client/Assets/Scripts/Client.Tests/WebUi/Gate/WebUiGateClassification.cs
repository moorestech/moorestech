using System.Collections.Generic;

namespace Client.Tests.WebUi.Gate
{
    /// <summary>
    /// スクリーンスペースuGUIビューのWebゲート処遇分類。新規uGUI追加時は必ずここへ分類を追加する（未分類はテスト失敗）。
    /// 分類の正はdocs/webui/MIGRATION.md。Pendingは吸収先Phase完了時にGatedRoot/CoveredByRootへ更新する。
    /// Web-gate disposition classification for screen-space uGUI views; new uGUI files must be classified here (unclassified fails the test).
    /// Source of truth is docs/webui/MIGRATION.md; Pending entries flip to GatedRoot/CoveredByRoot when the absorbing phase completes.
    /// </summary>
    public static class WebUiGateClassification
    {
        public enum Category
        {
            // WebUiScreenGate.IsWebUiMode 参照を必須とするゲートルート
            // Gated root that must reference WebUiScreenGate.IsWebUiMode
            GatedRoot,

            // 親のゲートルートで表示抑止される配下ファイル
            // Child file suppressed via its parent gated root
            CoveredByRoot,

            // ゲート機構・状態機械そのもの
            // The gate mechanism / state machine itself
            Infra,

            // 移行Phase待ち（noteに吸収先Phase）
            // Awaiting migration phase (note holds the absorbing phase)
            Pending,

            // 移行対象外（ワールド空間・メインメニュー・デバッグ等。noteに根拠）
            // Out of migration scope (world-space, main menu, debug; note holds the reason)
            Excluded,
        }

        public readonly struct Rule
        {
            public readonly string PathPrefix;
            public readonly Category RuleCategory;
            public readonly string Note;

            public Rule(string pathPrefix, Category category, string note)
            {
                PathPrefix = pathPrefix;
                RuleCategory = category;
                Note = note;
            }
        }

        // 走査対象ルート（Assets/Scripts からの相対）。スクリーンスペースUIが存在し得る領域を列挙
        // Scan roots (relative to Assets/Scripts) covering every area where screen-space UI can live
        public static readonly string[] ScanRoots =
        {
            "Client.Game/InGame/UI",
            "Client.Game/InGame/Presenter/PauseMenu",
            "Client.Game/InGame/BackgroundSkit",
            "Client.Game/Skit",
            "Client.Game/InGame/Mining",
            "Client.Game/InGame/Tutorial",
            "Client.Skit",
            "Client.CutScene",
        };

        // 最長一致で適用する分類ルール。ファイル指定がディレクトリ指定より優先される
        // Longest-prefix-match rules; file entries take precedence over directory entries
        public static readonly IReadOnlyList<Rule> Rules = new List<Rule>
        {
            // --- ゲートルート（ゲート参照必須） / Gated roots (gate reference required)
            new Rule("Client.Game/InGame/UI/Inventory/HotBarView.cs", Category.GatedRoot, "常駐ホットバーHUD"),
            new Rule("Client.Game/InGame/UI/Inventory/Main/PlayerInventoryViewController.cs", Category.GatedRoot, "インベントリ"),
            new Rule("Client.Game/InGame/UI/Inventory/RecipeViewer/RecipeViewerView.cs", Category.GatedRoot, "レシピビューア/クラフト"),
            new Rule("Client.Game/InGame/UI/Inventory/Block/Research/ResearchTreeViewManager.cs", Category.GatedRoot, "研究ツリー"),
            new Rule("Client.Game/InGame/UI/BuildMenu/BuildMenuView.cs", Category.GatedRoot, "ビルドメニュー"),
            new Rule("Client.Game/InGame/UI/Blueprint/BlueprintNameInputView.cs", Category.GatedRoot, "ブループリント名入力"),
            new Rule("Client.Game/InGame/UI/Challenge/ChallengeListView.cs", Category.GatedRoot, "チャレンジリスト/ツリー (C1)"),
            new Rule("Client.Game/InGame/UI/Challenge/CurrentChallengeHudView.cs", Category.GatedRoot, "進行中チャレンジHUD (C1)"),
            new Rule("Client.Game/InGame/UI/Crosshair/CrosshairView.cs", Category.GatedRoot, "クロスヘア (C2)"),
            new Rule("Client.Game/InGame/UI/KeyControl/KeyControlDescription.cs", Category.GatedRoot, "キー操作ヒント (C2)"),
            new Rule("Client.Game/InGame/UI/Tooltip/MouseCursorTooltip.cs", Category.GatedRoot, "カーソル追従ツールチップ (C2)"),
            new Rule("Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs", Category.GatedRoot, "削除バーHUD (C2)"),
            new Rule("Client.Game/InGame/UI/UIState/State/PauseMenu/PauseMenuStateService.cs", Category.GatedRoot, "ポーズメニュー (C2)"),
            new Rule("Client.Game/InGame/Presenter/PauseMenu/NetworkDisconnectPresenter.cs", Category.GatedRoot, "切断表示 (C2)"),
            new Rule("Client.Game/InGame/UI/Inventory/Train/TrainInventoryView.cs", Category.GatedRoot, "列車インベントリ (C3)"),
            new Rule("Client.Game/InGame/BackgroundSkit/BackgroundSkitManager.cs", Category.GatedRoot, "背景スキット (C4/S1)"),
            new Rule("Client.Game/Skit/SkitManager.cs", Category.GatedRoot, "通常スキット UI Toolkit 抑止 (C4/S2)"),
            new Rule("Client.Game/InGame/Tutorial/UIHighlight/UIHighlightTutorialManager.cs", Category.GatedRoot, "DOM UIハイライト (C4/T3)"),
            new Rule("Client.Game/InGame/Tutorial/UIHighlight/ItemViewHighLightTutorialManager.cs", Category.GatedRoot, "DOM itemハイライト (C4/T3)"),
            new Rule("Client.Game/InGame/Tutorial/KeyControlTutorialManager.cs", Category.GatedRoot, "共通key hint統合 (C4/T4)"),

            // --- ルート配下 / Covered by roots
            new Rule("Client.Game/InGame/UI/Inventory", Category.CoveredByRoot, "移行済み画面の配下部品（Phase Dで全量最終監査）"),
            new Rule("Client.Game/InGame/UI/BuildMenu", Category.CoveredByRoot, "BuildMenuView配下"),
            new Rule("Client.Game/InGame/UI/Blueprint", Category.CoveredByRoot, "BuildMenu/名入力配下"),
            new Rule("Client.Game/InGame/UI/Modal", Category.CoveredByRoot, "モーダル基盤は移行済み（Phase Dで最終監査）"),

            // --- 基盤 / Infra
            new Rule("Client.Game/InGame/UI/UIState", Category.Infra, "状態機械・ゲート本体・トグル"),

            // --- Phase待ち / Pending migration
            new Rule("Client.Game/InGame/UI/Inventory/Train", Category.CoveredByRoot, "TrainInventoryView配下 (C3)"),
            new Rule("Client.Game/InGame/UI/Challenge", Category.CoveredByRoot, "ChallengeListView/CurrentChallengeHudView配下（C1移行済み）"),
            new Rule("Client.Game/InGame/UI/Crosshair", Category.CoveredByRoot, "CrosshairView配下 (C2)"),
            new Rule("Client.Game/InGame/UI/KeyControl", Category.CoveredByRoot, "KeyControlDescription配下 (C2)"),
            new Rule("Client.Game/InGame/UI/Tooltip", Category.CoveredByRoot, "MouseCursorTooltip配下 (C2)"),
            new Rule("Client.Game/InGame/UI/ProgressBar/ProgressBarView.cs", Category.GatedRoot, "スクリーン進捗バー（D監査で二重表示ゲート漏れを検出し修正。論理状態はProgressTopicのデータ源として維持）"),
            new Rule("Client.Game/InGame/Presenter/PauseMenu", Category.CoveredByRoot, "PauseMenuStateService/NetworkDisconnectPresenterで抑止 (C2)"),
            new Rule("Client.Game/InGame/BackgroundSkit", Category.CoveredByRoot, "BackgroundSkitManagerでWeb文字表示を抑止（音声はUnity維持） (C4/S1)"),
            new Rule("Client.Game/InGame/Mining", Category.Excluded, "ワールド空間表示のためUnity残置。画面固定HUDはWeb側ui.mining_hudで新設 (C2)"),
            new Rule("Client.Game/InGame/Tutorial/UIHighlight", Category.CoveredByRoot, "UIHighlight managerでWeb表示へ切替 (C4/T3)"),
            new Rule("Client.Game/InGame/Tutorial/MapObjectPin.cs", Category.Excluded, "ワールド座標ピンのためUnity残置"),
            new Rule("Client.Game/InGame/Tutorial/HudArrow", Category.Excluded, "Camera依存のワールド矢印のためUnity残置"),
            new Rule("Client.Game/InGame/Tutorial/BlockPlacePreviewTutorialManager.cs", Category.Excluded, "3D配置previewのためUnity残置"),
            new Rule("Client.Game/InGame/Tutorial/TutorialBlock", Category.Excluded, "3D配置preview配下"),
            new Rule("Client.Game/InGame/Tutorial", Category.Infra, "challenge lifecycle・presentation state・interface"),
            new Rule("Client.Skit", Category.CoveredByRoot, "SkitManagerがUI Toolkit rootをWebモード時に抑止 (C4/S2-S3)"),
            new Rule("Client.CutScene", Category.Pending, "C4: カットシーン退避（GameStateType Topic化）"),
        };
    }
}
