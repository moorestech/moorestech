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
            new Rule("Client.Game/InGame/UI/Inventory/Main/PlayerInventoryViewController.cs", Category.GatedRoot, "インベントリ/ホットバー"),
            new Rule("Client.Game/InGame/UI/Inventory/RecipeViewer/RecipeViewerView.cs", Category.GatedRoot, "レシピビューア/クラフト"),
            new Rule("Client.Game/InGame/UI/Inventory/Block/Research/ResearchTreeViewManager.cs", Category.GatedRoot, "研究ツリー"),
            new Rule("Client.Game/InGame/UI/BuildMenu/BuildMenuView.cs", Category.GatedRoot, "ビルドメニュー"),
            new Rule("Client.Game/InGame/UI/Blueprint/BlueprintNameInputView.cs", Category.GatedRoot, "ブループリント名入力"),

            // --- ルート配下 / Covered by roots
            new Rule("Client.Game/InGame/UI/Inventory", Category.CoveredByRoot, "移行済み画面の配下部品（Phase Dで全量最終監査）"),
            new Rule("Client.Game/InGame/UI/BuildMenu", Category.CoveredByRoot, "BuildMenuView配下"),
            new Rule("Client.Game/InGame/UI/Blueprint", Category.CoveredByRoot, "BuildMenu/名入力配下"),
            new Rule("Client.Game/InGame/UI/Modal", Category.CoveredByRoot, "モーダル基盤は移行済み（Phase Dで最終監査）"),

            // --- 基盤 / Infra
            new Rule("Client.Game/InGame/UI/UIState", Category.Infra, "状態機械・ゲート本体・トグル"),

            // --- Phase待ち / Pending migration
            new Rule("Client.Game/InGame/UI/Inventory/Train", Category.Pending, "C3: 列車インベントリ"),
            new Rule("Client.Game/InGame/UI/Challenge", Category.Pending, "C1: チャレンジ（ChallengeListUI系2枚はC1で死コード削除）"),
            new Rule("Client.Game/InGame/UI/ChallengeList", Category.Pending, "C1: 空スタブ死コード（C1で削除）"),
            new Rule("Client.Game/InGame/UI/ContextMenu", Category.Pending, "C2: コンテキストメニュー"),
            new Rule("Client.Game/InGame/UI/Crosshair", Category.Pending, "C2: クロスヘア"),
            new Rule("Client.Game/InGame/UI/KeyControl", Category.Pending, "C2: キー操作ヒント"),
            new Rule("Client.Game/InGame/UI/Tooltip", Category.Pending, "C2: ツールチップ基盤"),
            new Rule("Client.Game/InGame/UI/ProgressBar", Category.Pending, "D: 処遇確認（ワールド進捗バーならExcludedへ）"),
            new Rule("Client.Game/InGame/Presenter/PauseMenu", Category.Pending, "C2: ポーズメニュー"),
            new Rule("Client.Game/InGame/BackgroundSkit", Category.Pending, "C4: バックグラウンドスキット"),
            new Rule("Client.Game/InGame/Mining", Category.Pending, "C2: 直接採掘HUD（ワールドピンは対象外）"),
            new Rule("Client.Game/InGame/Tutorial", Category.Pending, "C4: DOMハイライト移行（ワールド空間系はUnity残置）"),
            new Rule("Client.Skit", Category.Pending, "C4: スキット（UI Toolkit表示層）"),
            new Rule("Client.CutScene", Category.Pending, "C4: カットシーン退避（GameStateType Topic化）"),
        };
    }
}
