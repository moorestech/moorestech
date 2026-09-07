using System.Collections.Generic;

namespace Client.Tests.WebUi.Gate
{
    /// <summary>
    /// スクリーンスペースuGUIビューのWebゲート処遇分類。新規uGUI追加時は必ずここへ分類を追加する（未分類はテスト失敗）。
    /// 移行済み画面uGUIはADR 0052で全削除済みで、本分類は新規追加を止める安全網として残る。
    /// Web-gate disposition classification for screen-space uGUI views; new uGUI files must be classified here (unclassified fails the test).
    /// The migrated screen uGUI is fully deleted per ADR 0052; this classification remains as a safety net against new additions.
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
            new Rule("Client.Game/Skit/SkitManager.cs", Category.GatedRoot, "通常スキット UI Toolkit 抑止"),

            // --- 基盤 / Infra
            new Rule("Client.Game/InGame/UI", Category.Infra, "状態機械・論理モデル・ゲート本体（uGUIビューは全削除済み: ADR 0052）"),
            new Rule("Client.Game/InGame/Presenter/PauseMenu", Category.Infra, "終了経路・切断状態・セーブ要求（uGUI非依存）"),
            new Rule("Client.Game/InGame/BackgroundSkit", Category.Infra, "音声再生専用オーケストレータ。文字表示はWeb UI側が担う"),
            new Rule("Client.Game/Skit/Localization", Category.Infra, "通常スキットの辞書読込・合成・解決基盤（画面表示なし）"),
            new Rule("Client.Game/InGame/Tutorial", Category.Infra, "challenge lifecycle・presentation state・interface"),
            new Rule("Client.Game/InGame/Tutorial/UIHighlight", Category.Infra, "DOMハイライト一本化済み"),

            // --- 移行対象外 / Excluded
            new Rule("Client.Game/InGame/Mining", Category.Excluded, "採掘FSM（進捗は ProgressBarState→ui.progress）"),
            new Rule("Client.Game/InGame/Tutorial/MapObjectPin.cs", Category.Excluded, "ワールド座標ピンのためUnity残置"),
            new Rule("Client.Game/InGame/Tutorial/VeinPin.cs", Category.Excluded, "鉱脈露頭を指すワールド座標ピンのためUnity残置"),
            new Rule("Client.Game/InGame/Tutorial/BlockPlacePreviewTutorialManager.cs", Category.Excluded, "3D配置previewのためUnity残置"),
            new Rule("Client.Game/InGame/Tutorial/BlockPlacePreviewTutorialView.cs", Category.Excluded, "3D配置previewの表示体"),
            new Rule("Client.Game/InGame/Tutorial/PlacementGuide", Category.Excluded, "3D配置previewの鎖・相対・鉱脈限定ガイド"),
            new Rule("Client.Game/Skit/SkitWorldObjectControlGroup.cs", Category.Excluded, "ワールド表示物の切替でスクリーンUIを持たない"),
            new Rule("Client.Game/Skit/SkitVisibilityLedger.cs", Category.Excluded, "スキットが消したワールド表示の復元台帳でスクリーンUIを持たない"),
            new Rule("Client.Game/Skit/SkitUiRestoreResult.cs", Category.Excluded, "会話UI復帰要求の帰結を表すenumでスクリーンUIを持たない"),
            new Rule("Client.Skit", Category.CoveredByRoot, "SkitManagerがUI Toolkit rootをWebモード時に抑止"),
            new Rule("Client.CutScene", Category.Excluded, "TimelinePlayerのみ（Canvasは削除済み: ADR 0052）"),
        };
    }
}
