# UITK S3-hotbar-modal（段階 3 画面: ホットバー 9 枠・モーダル層と BP 名の入力）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Web 版の `features/hotbar`（9 枠・番号タブ・選択・未解決の減光・選択を受けない画面の減光・割当/入替/解除の落とし先・ホイール素通しの印）と `features/modal`（Mantine Modal の題・本文・BP 名の入力・確定ボタン・幕のクリックで取消）を、C3-a（`SlotFrame`・`PlacementTargetFace`）・C3-b（`UiDragDrop`・`UiModalHost`・`DialogFrame`）・C2-b（`FormButton`・`GameTextField`）の部品で Web と同じ見た目の UITK 画面にし、全体スイッチ UITK でモーダルの描き手を UITK 1 本にする。Model は Web の topic の源（`ClientHotbarDatastore`・`PlacementTargetResolver`・`PlaceSystemStateController`・`ModalRequestService`）から直に組み、L1・並べ比べ（720p・2560×1080、モーダルは 1080p も実画面 px で [A-41]）・録画 `uitk/uitk-hotbar-and-bp-modal` まで済ませて `feature/uitk` へ取り込む。

**Architecture:** 画面レーン（単位エージェント・Editor 要）。ホットバーの Model（`Client.Ui.State/Hotbar`）は源を購読して不変の `HotbarViewModel` を `IReadOnlyReactiveProperty` で配り、View（`Client.Ui/Screens/Hotbar`）は `Render(HotbarViewModel)` で部品へ流す。Presenter が起動時に 1 回だけ View を `UiLayer.Chrome`・`UiFamily.Viewport`（Web の `.viewportOverlay` z31。前例 S2-mode-tooltip D-1）へ載せ、`_model.Current.Subscribe(view.Render)` で繋ぐ。ドラッグは C3-b の `UiDragDrop`（源が解く・ゴースト無し [A-78]）に、枠の印 `HotbarSlotDropMark`・列の隙間の印 `HotbarRowGapDropMark` と、L0-s3 の `HotbarDropResolver` を呼んで `IHotbarCommands` へ流す解き方 を画面内（`Screens/Hotbar/Parts/`）で与える。枠の印は S3-buildmenu が State に作る `IHotbarDropReceiver`（`void Receive(HotbarDragSource)`）だけを実装して `UiDragDrop.MarkTarget<IHotbarDropReceiver>` で付き、操作（`IHotbarCommands`）を持って自分で解く。源はどちらも `IUiDragPayload<IHotbarDropReceiver>` で、Foundation は解放点から同じ型の最寄りの印だけを渡し、別の型の印は飛ばす（[A-132]）。ビルドメニューの源（`BuildMenuEntryDragPayload`）は最初から `OnDropped(IHotbarDropReceiver receiver)` で `receiver.Receive(Source)` を呼ぶので、このレーンはビルドメニューのファイルを書き換えない。配置対象の絵の出どころ（`PlacementTargetIcon`・`PlacementTargetIconResolver`・`PlacementTargetIcons.TryToUiIcon`）も S3-buildmenu が最終の置き場に作り、ホットバーは使うだけ（面へ当てる分岐は枠が持つ）。選択を受ける画面の規則は `HotbarSelectRule` の 1 か所で、Model（減光）と `HotbarCommands`（受理）が同じものを読む。ホットバーの Model は源の変化ごとに同期で組み直す（[A-57]。前例 ModeHudModel・BuildMenuModel）。操作は `UiCommandResult` を返し、拒否は `UiCommandResultToaster` へ渡す（[A-8]）。モーダルは状態の正が既に `ModalRequestService` にあるので Model を作らず、`ModalScreenPresenter` が `OnPendingChanged` を購読して `ModalView`（`DialogFrame`＋`LocalizedLabel`＋`GameTextField(MantineInput)`＋`FormButton`）を C3-b の `UiModalHost` に `Show`/`Hide` する。確定・ESC（1 回）・幕のクリックはどれも View が `IModalCommands.Respond`（[A-8]）を呼ぶ。見た目の値は Web と同名のトークン・つまみだけを使い、Web が直書きする色は `DerivedTokens.uss` の末尾の名札付きブロック「S3-hotbar-modal」に前計算して足す（[A-52]）。ホットバーは世界の上に直に置くので `--slot-frame-face` を画面ルートで世界の地向けの α に上書きし、列と未解決枠の減光（Web の opacity 0.55・0.4）はランタイムの PanelSettings（Linear 合成）で明るい地の画素を Web の sRGB 合成と照合して決める（bd `moorestech-albof.34`）。

**Tech Stack:** Unity 6000.3.8f1 UI Toolkit（ランタイムパネル・USS・`CustomStyleResolvedEvent`・`PointerDownEvent` 系・`panel.Pick`）、UniRx、UniTask、VContainer、NUnit＋`com.unity.ui.test-framework`（`UITestFixture`・`PanelSimulator`）、uloop CLI、プレイテスト DSL（`run-scenario.sh`）、node（Web の参照キャプチャ・test-name-parity・`convert-web-tokens.mjs`）、gh CLI、bd（Beads）

- **パスの略記**: `S/` = `moorestech_client/Assets/Scripts/`、`W/` = `moorestech_web/webui/`、`LOGS` = `../moorestech_logs`（repo の兄弟）、`SKILL` = `.agents/skills/unity-playmode-recorded-playtest`、`THEME` = `S/Client.Ui/Foundation/Theme`。Files 節と各コマンドはリポジトリ相対の完全なパスで書く
- **正本**: 裁定 `.decisions/2026-11-06-*`（とくに「段階2以降の画面もWeb版と同じ見た目にする」「長命ブランチの中は全体スイッチ1つ」「UITK切替までCEF由来の不具合は直さない」）、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1.1・§1.2 DragDrop・§2.3 SlotFrame/PlacementTargetFace・§2.5 ドラッグ源・§3.2・§3.3・§4・§5.1、[A-28]・[A-40]・[A-41]・[A-42]・[A-47]・[A-52]・[A-56]・[A-58]・[A-78]）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 の S3-hotbar-modal 行・§4.3・§5・§6.2・E-1・E-13）。食い違ったら 裁定 → 統合計画 → 設計書 → 実行計画 → この計画 の順で上が勝つ。この計画が設計書・完了条件と違う所は判断記録に書き、設計書に足す行の案は指揮が設計書 §7.1 へ写す（A-105〜A-109）
- **レーンの起動は指揮が行う**（実行計画 §4.1。実装は moores-subagent-driven-development でこのホストの Claude Code の subagent が担う）

## Requirements

設計の出所: 実行計画 §1.4 の S3-hotbar-modal 行（完了条件の原文:「ホットバー 9 枠（`SlotFrame` のベベル上書き・`--bevel-inset: inset` [A-42]・`PlacementTargetFace`・割当/入替/解除の落とし先・`wheel-passthrough`・床トークン）、モーダル層と BP 名（`DialogFrame`・`FormButton`・`GameTextField`、描画者を UITK に一本化。題・本文・ボタン文言は `UiText.FromVerbatim` [A-40]。モーダルは実画面 px で 720p・1080p の並べ比べ [A-41]）。写す: hotbar（P6・C7）、modal（P4、L8 は L0-s3 済み）、`placementIconCentering.spec.ts`(2)。写す（S2-hud から）: `commonHud/alwaysOnHudWheel.test.ts`「クリックを持つスロット列だけが入力を取り戻し…」（bd `moorestech-albof.30`）」）、§6.2（画面レーンの計画に必ず入れるもの）、設計書 [A-28]（`ModalButtonTone` は View が `Blue→Filled`・`Red→Danger` の 1 か所の switch で写す）・[A-42]・[A-58]、bd `moorestech-albof.34`（C3-a から回送: ホットバー画面の USS で `--slot-frame-face` を世界の地用に上書き・減光の opacity も同じ地で補正）。

| ID | 要件 | 受け入れ基準 |
|---|---|---|
| R-1 | ホットバーの Model: 割当（`ClientHotbarDatastore.Assignments`）を枠ごとに `HotbarEmptySlot`（未割当）・`HotbarUnresolvedSlot`（割当はあるが `PlacementTargetResolver.TryResolve` が偽）・`HotbarAssignedSlot`（解決済みの対象の ID・表示名・`PlacementTargetIcon`）の判別 union にし、選択枠（`PlaceSystemStateController.CurrentOrigin.TryGetHotbarSlot`）を `HotbarSelection`（`None`／`Of(i)`）、選択の受理（`HotbarSelectRule.Accepts(UIStateControl.CurrentState)`＝`GameScreen`・`PlaceBlock` だけ）、表示（`UiScreenTraitsTable.Get(UiRoutingState.Current.Value.Screen).AlwaysOnHud` ∧ ¬`BlockingSkitPresence.IsBlocking`）を `HotbarViewModel` にまとめる。割当・BP ライブラリ・サムネイル・設置対象・解放（`UnlockedEventPacket`）・UI 状態・画面・スキット・言語の変化で組み直す | `HotbarViewModelFactoryTest`（L0）が緑。録画（R-12）の主張 |
| R-2 | ホットバーの見た目: 帯 `.hotbar-hud`（実画面の下端から `--hotbar-bottom`、中央寄せ、拾わない）の中に列 `hotbar-grid`（拾う・枠間 `--hotbar-slot-gap`・`--hotbar-horizontal-offset` だけ右へ）、9 つの枠 `hotbar-slot-{i}`（`SlotFrame`。`--slot-size: var(--hotbar-slot-size)`、Web `.cell` の局所上書き変数を同名で上書き、`--bevel-inset: inset` [A-42]）と番号タブ（左 17・上へ `--hotbar-number-tab-overhang`・15×15・枠 1px・暗い縁取りの 13px の数字）。選択枠は `slot-frame--selected`、割当済みは `--filled`、絵は `PlacementTargetFace` | `HotbarHudViewTest` が緑（Web `hotbar.spec.ts` 1 件目）。並べ比べ（R-10） |
| R-3 | 未解決の割当枠は白面（filled）のまま中身を出さず、枠ごと `--hotbar-unresolved-opacity` で減光する。選択を受けない画面では列ごと `--hotbar-select-disabled-opacity` で減光し、タップの選択は `HotbarCommands.Select` が良性の拒否 `HotbarSelectNotAccepted`（wire `invalid_state`）を返し、源が `UiCommandResultToaster` へ渡す（良性なのでログだけ。クリックごとに警告ログが 1 行出る）。Web の画面は受けないときタップを送らず、橋の `HotbarSelectActionHandler` が同じ規則で `invalid_state` を返す。UITK は規則を 1 か所（A-106）にするため操作側で断る。選ばれない結果は Web と同じ。D&D はどちらでも成立する | `HotbarHudViewTest`・`HotbarHudDropTest` が緑（Web `hotbar.spec.ts` 2・4 件目） |
| R-4 | ドラッグ: 枠を源（`UiDragDrop.MarkSource`）にし、タップは `IHotbarCommands.Select(i)`、枠へ落とすと入替（自分自身は無操作）、列の隙間は無操作、それ以外（他の画面の落とし先・何も無い所）は解除。空き枠は掴む物が無いのでドラッグ後は何もせずタップにも落とさない。右ボタンは無視（捕捉しない）。ビルドメニューの源（S3-buildmenu の `BuildMenuEntryDragPayload`）は State の `IHotbarDropReceiver` で枠の印へ落とし、空き枠なら割当になる（このレーンはビルドメニューを書き換えない） | `HotbarSlotDragGestureTest`（Web `useHotbarDragSource.test.ts` 7 件）・`HotbarHudDropTest`（Web `hotbar.spec.ts` 3・4・6 件目）が緑 |
| R-5 | 入力の素通し: 帯は拾わず、列だけが拾い、列にホイール素通しの印（`WheelPassthroughMark`）がある（帯には無い） | `HotbarHudPickingTest` が緑（Web `alwaysOnHudWheel.test.ts` の「%s は入力を奪わない」ホットバー帯の行と「クリックを持つスロット列だけが入力を取り戻し…」。bd `moorestech-albof.30`） |
| R-6 | 世界の上の色（bd `moorestech-albof.34`）: 画面ルートで `--slot-frame-face: var(--hotbar-slot-frame-face)`（α 0.7226）に上書きし、空き枠の面・番号タブの面・未解決と受理外の減光を、ランタイムの PanelSettings（Linear 合成）の明るい地 (240,203,166) と暗い地 (40,42,48) で Web の sRGB 合成と照合する。明るい地で RGB 7 段以内（C3-a の基準）にし、暗い地で外れる分は `LX-76` に記帳する | Task 10 の照合表（EDC の出力）が `done.md` にあり、明るい地の全行が 7 段以内 |
| R-7 | アイコンが枠の中央に描かれる | `HotbarIconCenteringTest` が緑（Web `placementIconCentering.spec.ts`「ホットバーのアイコンは枠の中央に描かれる」） |
| R-8 | モーダル: 全体スイッチ UITK では `ModalRequestService.Pending` を UITK だけが描く。`DialogFrame("modal")` に題（`UiText.FromVerbatim(Title)`）、本文（`modal-message`・`text--sm`・`text--dimmed`・下余白 lg）、入力付きなら `GameTextField("modal-input", false, MantineInput)`（下余白 lg・表示時にフォーカス）、`FormButton("modal-button", FromVerbatim(ButtonText), Blue→Filled／Red→Danger)`（入力付きで空白だけなら押せない）。確定は入力付きなら前後の空白を除いた文を、取消は文なしを `IModalCommands.Respond` へ返す。幕のクリックと ESC 1 回（Web の Mantine と同じ。入力欄の変換中を除く。ESC はワールドへ渡さない。フォーカスの無い ESC は View でなく既存のゲーム入力の経路が BP 名の入力ごと取り消す〈D-11〉）は取消。入力欄の外を押しただけでは閉じない。要求 ID が変わったら入力欄を空にする | `ModalViewTest`・`ModalScreenPresenterTest`・`ModalCommandsTest` が緑（Web `modal.spec.ts` 4 件と ESC・欄外の押下の 2 件）。フォーカスの無い ESC は録画（R-12 の流れ 7b） |
| R-9 | モーダルは実画面 px で Web と同じ（[A-41]）: 720p・1080p・2560×1080 で幅 440 実画面 px・画面の中央 | `ModalScreenPresenterTest` の倍率 1.5 の主張が緑。並べ比べ（R-10）の 3 解像度 |
| R-10 | 並べ比べ: 参照 `3-hotbar-equipment-empty`（1280×720・2560×1080）と `3-modal`（1280×720・1920×1080・2560×1080）を UITK と並べ、差の表を `done.md` に書く | 2 状態の組（ホットバー 2 解像度・モーダル 3 解像度）が報告ページにあり、差の表の全行に「同じ／直した／LX-n／捨てる」のどれかがある |
| R-11 | 登録: `MainGameUiRegistration` の画面登録区画に 2 行（`HotbarGameUiRegistration.Register(builder);`・`ModalGameUiRegistration.Register(builder);`）。`GameUiTheme.tss` の `Screens` 区画に `@import` 2 行。BP 名のブリッジは確定の検証を `ModalLogic.HasConfirmableText` に委ねる | 全体スイッチ UITK で起動して `uloop get-logs --log-type Error` が 0 件。L2（`UiThemeImportBundleTest`・`UiStyleConventionTest`・`UiLayerDependencyDirectionTest`・`LookExceptionLedgerTest`・`TutorialAnchorCoverageTest`）が緑 |
| R-12 | 録画 `uitk/uitk-hotbar-and-bp-modal`（全体スイッチ UITK）: 枠のクリックで建築モードへ入る・枠の入替・枠外へのドラッグで解除・BP コピーの名前入力をモーダルで打って確定し BP ができる・ESC 1 回でモーダルが閉じ、ポーズも建築モードの解除も起きない（入力欄にフォーカスがあるとき・無いときの両方）。既存の `building/blueprint-copy-paste-via-ui` も全体スイッチ UITK で通す | 2 本の `result.json` が `Success: true`、録画 mp4 があり、`done.md` に UITK で回した証拠（Note の `系=GameUi`）がある。録画に ESC 1 回でモーダルが閉じポーズが出ない段がある |
| R-13 | 写した Web テストは `<web-test>` だけで示し、処遇の表（`docs/uitk/disposition.md`）には行を足さない。一部だけ写した 2 件の残りの範囲と持ち主は `done.md` の「## 部分移植の残り」に書く。移植で処遇の表の「写す予定」（albof.30 の行）が要らなくなるので、その行を移植と同じコミットで消す | `node tools/uitk/test-name-parity.mjs --bundle 3 --emit-missing` で、このレーンの持ち分（下の「写すテスト」の表）の未対応が 0。`--bundle 2` と `--bundle 3` の終了コードが 2 でない（書式・整合の誤りなし） |
| R-14 | 終わりは moores-code-review の全ブランチレビュー → pr-create（base `feature/uitk`・報告ページ）→ `origin/feature/uitk` の取り込み・compile・テスト → G1 緑 → `gh pr merge --merge` → bd（レーン・`albof.30`・`albof.34`）を閉じる → `moores-wt rm` | `git log origin/feature/uitk` にこのレーンのマージコミット。`done.md` の 1 行目が「状態: 完了」 |

**やらないこと（スコープ境界）**

- 部品（`SlotFrame`・`PlacementTargetFace`・`OutlinedCountLabel`・`DialogFrame`・`FormButton`・`GameTextField`）・既存の Foundation（`UiDragDrop`・`UiModalHost`・`UiPressGesture`・`StageScaleSource`・`GameUiInputRules`。新規の `Foundation/Input/WheelPassthroughMark.cs` だけ足す。D-21）・トークン USS（`Tokens.uss`・`SlotTokens.uss`・`MantineTokens.uss`）・`UiLayer`・`UiScreenTraits`・`AnchorIds` は変えない。要るものは `done.md` の「integrator への依頼」に書く（`DerivedTokens.uss` は末尾の名札付きブロックを足す [A-52]）。例外は 1 つ: 部品 `OutlinedCountLabel` に縁の色のつまみ `--outlined-count-label-stroke` を足す（`OutlinedCountLabel.uss` の縁の層の 2 行・`.cs` のコメント・`DerivedTokens.uss` の `--outlined-count-label-color` の次に既定 1 行。設計書 A-108・計画 D-7・Task 6 Step 3）
- ビルドメニューの画面と、その源（エントリのドラッグ・タップ）、配置対象の絵の出どころ（`PlacementTargetIcon` 系・`PlacementTargetIconResolver`・`PlacementTargetIcons`）、State の落とし先の口 `IHotbarDropReceiver` は作らない（S3-buildmenu が最終の置き場に作る。W3 で先に入る）。S3-buildmenu のファイルは書き換えない。ビルドメニューのパネルからホットバーへ通しで確かめる横断の L1 は S3-ladder。このレーンは本物のエントリの源（`BuildMenuEntryDragPayload`）を単独で置いて確かめる（使うだけ）
- ホイールで装備を回す処理は作らない（S4-inventory。このレーンは列に素通しの印と、その判定 `WheelPassthroughMark.IsPassthrough` を作るまで）
- Web 側（`moorestech_web/`）と Web の橋（`Client.WebUiHost/**`）は一切変えない。Web の系のモーダル（`ModalTopic`・`ModalActions`）はそのまま残る
- L3（EditModeInPlayingTest）は持たない（段の締め S3-ladder）
- `app/iconTextOutlineDesign.test.ts` は記帳しない（[A-54] で integrator が配る。配られたら「integrator への依頼」の返りとして写す）

## Global Constraints

- **作業場所**: `moores-wt new uitk-lane/s3-hotbar-modal --from origin/feature/uitk`（Editor 要）。最初に `pwd` で worktree にいることを確かめる。Unity は自分の worktree の Editor だけを使い、起動・停止の前に `moores-wt status` で所有を確かめる
- **依存**: C3-a（#1612・d4347f75ef。マージ済み）・C3-b・L0-s3（マージ済み）・S2-hud（`BlockingSkitPresence`）・S3-buildmenu（`PlacementTargetIcon` 系・`PlacementTargetIconResolver`・`PlacementTargetIcons`・`IHotbarDropReceiver`・`BuildMenuEntryDragPayload`・`IBuildMenuEntryHandler`。W3 の順で先に入る）。Task 1 Step 1 で全部の実物を確かめ、無ければ止めて指揮へ報告する
- **使う部品と公開 API**。C3-a・C2-b・L0-s3・S2-hud（#1613・8369fc1f39）・既存の State は `origin/feature/uitk` の実物から写した。C3-b・S3-buildmenu はそれぞれの計画の Produces から写したので「マージ後に実物と照合」（Task 1 Step 1 で grep し、名前が違えば以後を実物の名前で読み替え、`done.md` の「未確認」に「API 名の読み替え: <旧> → <実物>」と書く）

```csharp
// ===== C3-a（実物。S/Client.Ui/Components/Slots/・Foundation/Icons/・Content/）=====
namespace Client.Ui.Components.Slots
{
    public sealed class SlotFrame : VisualElement
    {
        public IObservable<SlotGesture> OnSlotGestured { get; }        // このレーンは使わない（タップとドラッグは UiDragDrop が分ける）
        public override VisualElement contentContainer { get; }       // .slot-frame__content（PickingMode.Ignore）
        public SlotFrame(string testid);                               // name = testid。ルートは PickingMode.Position（既定）
        public void SetState(SlotFrameState state);                    // .slot-frame--selected・--filled・--catalog・--insufficient・--ghost
    }
    public readonly struct SlotFrameState { public SlotFrameState(bool selected, bool filled, bool catalog, bool insufficient, bool ghost); }
    public sealed class PlacementTargetFace : VisualElement           // PickingMode.Ignore
    {
        public PlacementTargetFace();
        public void SetIconTarget(UiIcon icon, UiText displayName);   // icon.Texture が null なら名前で描く
        public void SetNameTarget(UiText displayName);
    }
    // 子の GameIcon に .placement-target-face__icon（padding は --icon-pad）。名前は .placement-target-face__label（10px）
    public sealed class OutlinedCountLabel
    {
        public OutlinedCountLabel(VisualElement parent, string placementClass, params string[] layerClasses); // 縁の層 .outlined-count-label--stroke と字の層 --glyph を parent の子に足す
        public void SetText(UiText text);
    }
    // 縁の色は .outlined-count-label--stroke の color・-unity-text-outline-color: var(--icon-text-stroke-light)（土台。Task 6 Step 3 がつまみ --outlined-count-label-stroke に替える）。字の色はつまみ --outlined-count-label-color、大きさは --outlined-count-label-size
}
namespace Client.Ui.Foundation.Icons { public sealed class UiIcon { public Texture2D Texture { get; } public string IdText { get; } public UiText Label { get; } public UiIcon(Texture2D texture, string idText, UiText label); } }
namespace Client.Ui.Content { public sealed class GameContentPresentation { public UiIcon Item(ItemId id); public UiIcon Block(BlockId id); public UiIcon Fluid(FluidId id); public UiText ItemName(ItemId id); } }
// 登録は ContentGameUiRegistration（MainGameUiRegistration の画面登録区画）。テスト用の偽は S/Client.Tests/Ui/Content/FakeContentTextureSource.cs
// SlotFrame のつまみの既定は DerivedTokens.uss の「C3-a」ブロック: --slot-frame-face rgba(50,52,67,0.42)（パネルの地向け・LX-23）・--bevel-inset outset・--sel-c1..3・--selected-face-ring-inset 0・--selected-face-bottom-trim 0・--icon-pad 5px・--count-bottom -3px。SlotTokens.uss: --face-inset 0・--bevel-1 0.782875px・--bevel-2/3 1.565749px・--bevel-c1 rgb(56,58,72)・--bevel-c2/c3 rgb(104,106,120)

// ===== C3-b（計画 2026-11-07-uitk-c3-b.md の Produces。マージ後に実物と照合）=====
namespace Client.Ui.Foundation.DragDrop
{
    public interface IUiDragPayload<in TTarget> where TTarget : class { void OnTapped(); void OnDropped(TTarget target); void OnDroppedOutside(); }
    public static class UiDragDrop
    {
        public static void MarkSource<TTarget>(VisualElement source, IUiDragPayload<TTarget> payload) where TTarget : class;   // 同じ型での 2 度目は payload を差し替え、別の型での 2 度目は LogError で無視。左押下だけ・実画面 5px を超えたらドラッグ・右は捕捉しない（UiPressGesture）
        public static void MarkTarget<TTarget>(VisualElement target, TTarget handler) where TTarget : class;            // 印は型ごとの表に入る
        public static void UnmarkTarget<TTarget>(VisualElement target) where TTarget : class;                          // クラス ui-drop-target は全部の型の印が外れたときだけ消える
        internal static bool TryFindTarget<TTarget>(VisualElement from, out TTarget target) where TTarget : class;     // from から祖先へ同じ型の最寄りの印。別の型の印は飛ばす
    } // 解放点は panel.Pick → TryFindTarget<源の型>。印が無ければ OnDroppedOutside。印のクラス ui-drag-source・ui-drop-target（設計書 A-132）
}
namespace Client.Ui.Foundation.Stage.ScaleReading { public sealed class StageScaleSource { public StageScaleSource(IReadOnlyReactiveProperty<float> scale); public IReadOnlyReactiveProperty<float> Scale { get; } } }
namespace Client.Ui.Foundation.Stage.ScaleReading { public static class StageScaleMeasure { public static bool TryRead(IPanel panel, out float scale); } } // 倍率の式の 1 本（設計書 A-75）。並べ比べの EDC だけが使う
namespace Client.Ui.Foundation.Modal
{
    public sealed class UiModalHost
    {
        public UiModalHost(StageScaleSource stageScale);
        public void Mount(VisualElement layer);
        public void Show(VisualElement content);          // 前の中身を差し替える。中身は UiScreenPixelScope の中で中央（実画面 px）
        public void Hide();
        public IObservable<Unit> OnBackdropClicked { get; } // 知らせるだけで閉じない
    } // 要素 modal-scope・modal-backdrop・modal-center。表示中は .ui-modal-host--shown。GameUiModalHostInstaller が Modal/Viewport へ Mount（FoundationGameUiRegistration が登録）
}
namespace Client.Ui.Components.Panels
{
    public sealed class DialogFrame : VisualElement
    {
        public DialogFrame(string testid);                // name = testid。幅 --modal-size（440px）
        public void SetTitle(UiText title);               // 題の Label の name は "<testid>-title"（title--order-4）
        public void AddAction(FormButton button);         // 幅いっぱい
        public override VisualElement contentContainer { get; }
    }
}

// ===== C2-b（実物）=====
namespace Client.Ui.Components.Controls
{
    public enum FormButtonVariant { Filled, Danger, Subtle }
    public sealed class FormButton : Button { public FormButton(string testid, UiText label, FormButtonVariant variant); public IObservable<Unit> OnClicked { get; } public void SetLabel(UiText label); public void SetVariant(FormButtonVariant variant); }
    // 押せない状態は SetEnabled(false)（.form-button.unity-button:disabled）
    public enum GameTextFieldLook { GaugeInset, MantineInput }
    public sealed class GameTextField : VisualElement
    {
        public UnityEngine.UIElements.TextField Field { get; }  // name = testid（DSL の SetText が引く）
        public IObservable<string> OnValueChanged { get; }
        public GameTextField(string testid, bool multiline, GameTextFieldLook look);
        public void SetValueWithoutNotify(string value);
    }
}
namespace Client.Ui.Foundation.Text
{
    public static class UiTypographyClass { public const string TextSm = "text--sm"; public const string TextDimmed = "text--dimmed"; public const string TitleOrder4 = "title--order-4"; }
    public class LocalizedLabel : Label { public LocalizedLabel(); public void SetText(UiText text); }
}
namespace Client.Ui.State.Text { public sealed class UiText { public UiText(LocalizationKey key); public static UiText FromVerbatim(string text); public static UiText FromContent(ContentLocalizationKey key); public string Resolve(); } }
namespace Client.Ui.Foundation.Anchor { public static class TutorialAnchor { public static void Mark(VisualElement element, params string[] ids); } }
// AnchorIds.HotbarHud = "hotbar.hud"（Client.Game.InGame.Tutorial.UIHighlight）
namespace Client.Ui.Foundation.Theme { public static class UssCustomStyleReader { public static bool TryReadPixels(ICustomStyle style, string propertyName, out float pixels); } } // 読めないときは LogError を出して false

// ===== L0-s3（実物。S/Client.Ui.State/Hotbar/Logic・Modal）=====
namespace Client.Ui.State.Hotbar.Logic
{
    public abstract class HotbarDragSource { }   // BuildMenuEntryDragSource(Guid targetId).TargetId・HotbarSlotDragSource(int slotIndex).SlotIndex
    public abstract class HotbarDropTarget { }   // HotbarSlotDropTarget(int slotIndex).SlotIndex・OutsideHotbarDropTarget.Instance
    public abstract class HotbarDropAction { }   // HotbarAssignAction(SlotIndex, TargetId)・HotbarSwapAction(FromSlotIndex, ToSlotIndex)・HotbarClearAction(SlotIndex)・HotbarNoDropAction.Instance
    public static class HotbarDropResolver { public static HotbarDropAction Resolve(HotbarDragSource source, HotbarDropTarget target); }
}
namespace Client.Ui.State.Modal
{
    public class ModalRequest { public string Id; public string Title; public string Message; public string ButtonText; public ModalVariant Variant; public bool RequiresInput; }
    public enum ModalVariant { Confirm, Error }
    public enum ModalResult { Confirm, Cancel }
    public class ModalRequestService : IInitializable
    {
        public ModalRequest Pending { get; private set; }
        public IObservable<Unit> OnPendingChanged { get; }      // Subject（購読時に今の値を流さない）
        public (string requestId, UniTask<(ModalResult result, string text)> response) RequestInputModal(string title, string message, string buttonText);
        public UniTask<ModalResult> RequestModal(string title, string message, string buttonText, ModalVariant variant);
        public void CancelPendingRequest();
        public bool Respond(string id, ModalResult result, string text); // id 不一致・保留無しは false
    } // MainGameUiStateRegistration が両系で登録済み（BlueprintNameInputModalBridge も）
}
namespace Client.Ui.State.Modal.Logic
{
    public static class ModalLogic { public static bool TryGetConfirmableText(ModalRequest request, string enteredText, out string confirmedText); public static bool HasConfirmableText(string enteredText); public static ModalButtonTone ConfirmButtonTone(ModalVariant variant); }（既存の `CanConfirm` は本番の呼び手がテストだけなので Task 4 で消す）
    public enum ModalButtonTone { Blue, Red }
    public sealed class ModalResponse { public string RequestId { get; } public ModalResult Result { get; } public string Text { get; } public static ModalResponse Confirm(ModalRequest request, string enteredText); public static ModalResponse Cancel(ModalRequest request); }
}

// ===== 段階 1 のコマンドの結果（実物。S/Client.Ui.State/Commands・Toast）=====
namespace Client.Ui.State.Commands
{
    public abstract class UiCommandResult { }   // UiCommandAccepted.Instance・UiCommandRejected(UiCommandRejectReason reason, string detail)（.Reason・.Detail・.ToWireCode()。Detail が空でなければ Detail が wire）
    public enum UiCommandRejectReason { EmptyDescription, UnsubmittableReportKind, CaptureRefused, BundleWriteFailed, UnknownLanguage, LanguageAlreadySelected, /* 共有区画: 末尾に足す */ }
    public static class UiCommandRejectReasonTraits { public static bool IsBenign(UiCommandRejectReason r); public static LocalizationKey ToLocalizationKey(UiCommandRejectReason r); public static string ToWireCode(UiCommandRejectReason r); } // 3 つの switch とも全値を要し、末尾が共有区画
}
namespace Client.Ui.State.Toast { public sealed class UiCommandResultToaster { public void Report(string commandName, UiCommandResult result); } } // 拒否は LogWarning「[UiCommandResultToaster] <名> failed: <wire>」、良性でなければトースト。MainGameUiRegistration が登録・テストは ScreenTestHost.Toaster
// 入力: Client.Input.UiInputExclusivity.ConsumeEscapeThisFrame()・IsEscapeConsumedThisFrame・SetGameUiTextInputFocused(bool)・ClearEscapeLatchForTest()。GameUiInputRules.Attach(root)（Foundation/Input。入力欄の ESC を TrickleDown で消費してフォーカスだけ外す）

// ===== S2-hud（実物 origin/feature/uitk 8369fc1f39 の Client.Ui.State/SkitPresence/BlockingSkitPresence.cs から写した）=====
namespace Client.Ui.State.SkitPresence { public sealed class BlockingSkitPresence { public IReadOnlyReactiveProperty<bool> IsBlocking => _isBlocking; public BlockingSkitPresence(SkitPresentationStateStore store); } } // MainGameUiRegistration の本体が登録済み（`builder.Register<BlockingSkitPresence>(Lifetime.Singleton)`。[A-56]）。テストでは `new BlockingSkitPresence(new SkitPresentationStateStore())` と `store.BeginBlocking(new SkitActionContext())`（前例 `Client.Tests/Ui/State/SkitPresence/BlockingSkitPresenceTest.cs`）

// ===== S3-buildmenu が作る（W3 で先に入る。指揮の決定どおりの形。Task 1 Step 1 で実物と照合）=====
namespace Client.Ui.State.PlacementTarget
{
    public abstract class PlacementTargetIcon { }                                   // ID だけを持つ判別 union。BP 以外は値の等価つき（Texture は持たない）
    public sealed class PlacementTargetBlockIcon : PlacementTargetIcon { public PlacementTargetBlockIcon(BlockId blockId); public BlockId BlockId { get; } }
    public sealed class PlacementTargetTrainCarIcon : PlacementTargetIcon { public PlacementTargetTrainCarIcon(Guid trainCarGuid); }
    public sealed class PlacementTargetConnectToolIcon : PlacementTargetIcon { public PlacementTargetConnectToolIcon(Guid connectToolGuid); }
    public sealed class PlacementTargetBlueprintIcon : PlacementTargetIcon { public PlacementTargetBlueprintIcon(Guid blueprintGuid); } // これだけ値の等価を持たない（参照の等価）。撮り直しのたびに組み直した VM が配られる
    public sealed class PlacementTargetNameOnlyIcon : PlacementTargetIcon { public static readonly PlacementTargetNameOnlyIcon Instance; }
    public static class PlacementTargetIconResolver { public static PlacementTargetIcon Resolve(IPlacementTarget target, IBlueprintThumbnailLookup thumbnails); }
} // S/Client.Ui.State/PlacementTarget/PlacementTargetIcon.cs・PlacementTargetIconResolver.cs
namespace Client.Ui.Content
{
    public sealed class PlacementTargetIcons                                         // DI のクラス。ContentGameUiRegistration が登録。ctor は 3 引数 (GameContentPresentation, IGameContentTextureSource, IBlueprintThumbnailLookup)。Task 1 Step 1 で実物と照合
    {
        public bool TryToUiIcon(PlacementTargetIcon icon, out UiIcon uiIcon);        // 値だけ。NameOnly とテクスチャの欠けで偽（面は名前で描く）。面へ当てる分岐（真なら SetIconTarget、偽なら SetNameTarget）は使う画面が持つ
    }
}
namespace Client.Ui.State.Hotbar.Logic { public interface IHotbarDropReceiver { void Receive(HotbarDragSource source); } } // S/Client.Ui.State/Hotbar/Logic/IHotbarDropReceiver.cs。印は MarkTarget<IHotbarDropReceiver> で付ける。断る理由のログは受け手（このレーンの印）が出す
namespace Client.Ui.Screens.BuildMenu.Parts
{
    public interface IBuildMenuEntryHandler { void OnEntrySelected(Guid targetId); void OnBlueprintDeleteRequested(Guid targetId); void OnEntryHovered(Guid targetId); }
    public sealed class BuildMenuEntryDragPayload : IUiDragPayload<IHotbarDropReceiver>
    {
        public BuildMenuEntryDragPayload(Guid targetId, IBuildMenuEntryHandler handler);
        public BuildMenuEntryDragSource Source { get; }
        // OnTapped → handler.OnEntrySelected。OnDropped(IHotbarDropReceiver receiver) は receiver.Receive(Source) だけ（別の型の印は Foundation が飛ばし OnDroppedOutside）。OnDroppedOutside は silent-ok の無操作
    }
}
// テストの補助: S/Client.Tests/Ui/Screens/BuildMenu/Support/RecordingBuildMenuEntryHandler.cs（internal sealed。Selected・DeleteRequested・Hovered の List<Guid>。namespace Client.Tests.Ui.Screens.BuildMenu.Support）
// IPlacementTarget { Guid Id; PlacementTargetKind Kind; string DisplayName; … }（Client.Game.InGame.BlockSystem.PlaceSystem.Targets。DisplayName は今の言語の名。S2-mode-tooltip D-8）
// 言語の変化: Client.Localization.Localize.OnLanguageChanged（BuildMenuModel が購読する前例）
```

- **Model の源（Client.Game・Client.Network の実物。Web の topic `local_player.hotbar` を流している `S/Client.WebUiHost/Game/Topics/Hotbar/HotbarTopic.cs` と同じ購読元）**

```csharp
// namespace Client.Game.InGame.Hotbar
public class ClientHotbarDatastore
{
    public IReadOnlyList<Guid> Assignments { get; }       // 長さ HotbarAssignmentDatastore.SlotCount（9）。Guid.Empty が未割当
    public IObservable<Unit> OnAssignmentsChanged { get; }
    public void RequestAssign(int slot, Guid targetId);
    public void RequestClear(int slot);
    public void RequestSwap(int slotA, int slotB);
    public void EnqueueSelectRequest(int slot);            // HotbarTapInputService（Client.Game）が UI 状態の中で消費する
}
// namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
public class PlacementTargetResolver { public bool TryResolve(Guid id, out IPlacementTarget target); }   // 未解放・削除済み BP は false
// namespace Client.Game.InGame.BlockSystem.PlaceSystem
public class PlaceSystemStateController { public PlacementOrigin CurrentOrigin { get; } public IObservable<IPlacementTarget> OnTargetChanged { get; } }
public readonly struct PlacementOrigin { public static readonly PlacementOrigin NonHotbar; public static PlacementOrigin FromHotbarSlot(int slot); public bool TryGetHotbarSlot(out int slot); }
// namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
public class ClientBlueprintLibrary { public IObservable<Unit> OnChanged { get; } }
// namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail
public interface IBlueprintThumbnailLookup { IObservable<Guid> OnThumbnailChanged { get; } bool Contains(Guid blueprintGuid); bool TryGet(Guid blueprintGuid, out Texture2D thumbnail); }
// namespace Client.Game.InGame.UI.UIState
public class UIStateControl : MonoBehaviour { public IObservable<UIStateEnum> OnStateChanged { get; } public UIStateEnum CurrentState { get; } }
// namespace Client.Ui.State.Routing
public sealed class UiRoutingState { public IReadOnlyReactiveProperty<UiRoutingSnapshot> Current { get; } }  // UiRoutingSnapshot.Screen（UiScreen）
public static class UiScreenTraitsTable { public static UiScreenTraits Get(UiScreen screen); }             // .AlwaysOnHud
// namespace Client.Network.API
public interface IVanillaApiEvent { IDisposable SubscribeEventResponse(string tag, Action<byte[]> responseAction); } // MainGameModelRegistration が RegisterInstance 済み。前例 ChallengeProgressState
// namespace Server.Event.EventReceive: UnlockedEventPacket.EventTag（解放で未解決枠が解決できるようになる）
// namespace Client.Game.InGame.UI.Blueprint
public class BlueprintNameInputState { public IObservable<bool> OnOpenChanged { get; } public void Open(); public void Confirm(string name); public void Cancel(); }
// DI: ClientBlueprintLibrary・IBlueprintThumbnailLookup・PlacementTargetResolver は MainGameInteractionRegistration、ClientHotbarDatastore は MainGameModelRegistration、UiRoutingState は MainGameUiStateRegistration が両系で登録済み。UIStateControl はシーンの component
```

- **層の向き**（設計書 §1.1・`UiLayerDependencyRules`）: `Client.Ui.State/**` は Client.Game・Client.Network・Server.Event を参照してよい（asmdef 既存）。`Client.Ui/Screens/**` は State・Components・Foundation・Content を参照してよい。Screens は `Client.Game` の型を受けない（ViewModel が運ぶのは対象の ID・表示名・`PlacementTargetIcon` で、絵への変換は Content の `PlacementTargetIcons`）。Content は Screens を参照しない（`UiLayerDependencyRules` の Content 行）
- **AGENTS.md の規約**: 1 ファイル 200 行未満・1 ディレクトリ 10 ファイルまで（.meta は数えない）・`partial`/`Func<>` 禁止・イベントは UniRx（Foundation の仕組みの通知は受け手 interface [A-78]）・try-catch 禁止・デフォルト引数禁止・単純な getter/setter プロパティ禁止（`{ get; private set; }` と式本体の読み取り専用は可）・主要処理に「// 日本語 → // English」の 2 行コメント（各 1 行）・メソッド内のローカル関数は `#region Internal`・初期化は `Initialize`・fail-closed の経路はログ（意図した無音は `silent-ok: <理由> / <誰がどこで気づくか>`）・`.meta` は手で作らない・Prefab/シーンを手で書き換えない・`Update()` で毎フレームの同値判定をしない
- **USS の規約**（ref-uitk-design・設計書 §3.2）: 画面 USS はルートのクラス（`.hotbar-hud`・`.modal-view`）で始まり、画面のクラスは `hotbar-hud__*`・`modal-view__*`。部品のクラスを書かない（部品の見た目はつまみで変える。`OutlinedCountLabel` の置き場のクラスは画面のクラス）。色は `var()` だけ、`z-index`・構造擬似クラス（`:first-child` 等）・`gap`・合成の太字を使わない。USS の先頭に `/* web: <Web の CSS> */` を日英で書く。C# が値を要るときは画面のクラスで `--<名>: var(--token)` と受け直して `UssCustomStyleReader` で読む（`:root` の変数は customStyle へ継承されない。台帳「## 実測の結論」customStyle）
- **calc() の写し**（[A-47]）: Web の `top: calc(var(--hotbar-number-tab-overhang) * -1)` と `transform: translateX(var(--hotbar-horizontal-offset))` は画面内の `HotbarCalcKnobs` が customStyle から読んで inline で当てる。換算値を USS に直書きしない
- **字間・色の罠（持ち越し）**: UITK の字間は字の大きさ/100 × 値（px のまま書くと 100/字の大きさ 倍に効く）。このレーンは字間を書かない（番号タブ・モーダルは Web も `normal`）。部品ギャラリーは sRGB 合成なので、色はランタイムの PanelSettings（Linear 合成）で比べる。暗い半透明の α は I-fix1（e26010aa0c）が指数 1.85 で選び直した（LX-43）が、明るい色の式（α^2.2）は実測されていない。このレーンの明るい半透明（番号タブの枠）は並べ比べで確かめ、外れたら `LX-77` に記帳する
- **テストの置き場と名前**: `S/Client.Tests/Ui/Screens/{Hotbar,Modal}/`・`S/Client.Tests/Ui/State/{Hotbar,Modal}/`、namespace はディレクトリに合わせる。テスト名は日本語の挙動文。Web から写したものは `[Test]` の直前に `/// <web-test file="<W/ からの相対パス>">it/test の第 1 引数</web-test>`（describe は含めない。`it.each` は第 1 引数の書式文字列そのまま。実行計画 E-13）
- **コマンド**: コンパイル `uloop compile --project-path ./moorestech_client`（期待: ErrorCount 0）。テスト `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。このレーンのテストフィルタ（以下 `$FILTER`）は `^Client\.Tests\.Ui\.(Screens\.(Hotbar|Modal)|State\.(Hotbar|Modal|PauseMenuCommandsTest)|Conventions|Theme|Layers|Anchor|DragDrop|Modal|Content)\.`（`PauseMenuCommandsTest` は namespace `Client.Tests.Ui.State` 直下なので名指しで入れる。S3-buildmenu と共有のテスト）。ドメインリロード中のエラーは 45 秒待って再試行
- **共有ファイルの所有**（実行計画 E-1）: このレーンが書く共有ファイルは、`MainGameUiRegistration.cs` の画面登録区画の 2 行、`GameUiTheme.tss` の `Screens` 区画の `@import` 2 行、`THEME/DerivedTokens.uss` の末尾の名札付きブロック「S3-hotbar-modal」（[A-52]）と、その半透明 3 行を前計算の表へ載せる `tools/uitk/webTokens/handWrittenDerivedRows.mjs` の `rowSources` の末尾 3 件・再生成した `docs/uitk/translucent-tokens.md`（前例 S2-hud [A-55]）、`docs/uitk/TODO.md` の「## 実測の結論」「## 見た目の例外」（`LX-76`〜`LX-80`）の末尾、`docs/uitk/disposition.md` の「## 処遇」の albof.30 の行の削除（行は足さない）、`S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` の `AnchorIds.HotbarHud` の 1 行の削除（在るときだけ）、`S/Client.Ui.State/Commands/UiCommandRejectReason.cs` の共有区画（enum と 3 つの switch の末尾に `HotbarSelectNotAccepted`・`ModalResponseStale` を 1 行ずつ。前例 S2-skit [A-84]）と良性の一覧を固定する `S/Client.Tests/Ui/State/PauseMenuCommandsTest.cs` の「良性の拒否理由は…」1 件、`Localization/localization.csv` の `ui.commandRejected.*` の群の末尾 2 行（`ui.commandRejected.hotbarSelectNotAccepted`・`ui.commandRejected.modalResponseStale`。良性で表示されないが `ToLocalizationKey` が全値を要る）と再生成した `W/src/shared/i18n/generated/localizationKeys.ts`（`npm run gen:i18n`。台帳「## 追従記録」へ 1 行。E-1）。S3-buildmenu のファイルは書き換えない（使うだけ）。画面の文言の `localization.csv` は足さない（ホットバーは数字だけ、モーダルの文言は要求側が解いた文。Task 1 Step 2 で確かめる）。シナリオ `SKILL/scenarios/uitk/uitk-hotbar-and-bp-modal.cs` はこのレーンが持つ
- **画面専用の部品の置き場**（設計書 §5.1-2）: このレーンで作る汎用になりそうなもの（`HotbarCalcKnobs`・`HotbarNumberTab`）は 1 機能目なので `S/Client.Ui/Screens/Hotbar/Parts/` に置く。`WheelPassthroughMark` は Web で既に 2 機能（ホットバーの列・装備 HUD。`W/src/shared/uiState/activeLayer.ts:22`）が使うので §5.1-1 により最初から `S/Client.Ui/Foundation/Input/WheelPassthroughMark.cs` に置く（新規ファイルで他レーンと重ならない。D-21）。落とし先の印（`HotbarSlotDropMark`・`HotbarRowGapDropMark`。State の `IHotbarDropReceiver` だけを実装し `MarkTarget<IHotbarDropReceiver>` で付く）はホットバーの語彙なので昇格させず、同じ `Parts/` に置く（D-5）。配置対象の絵の出どころと State の口 `IHotbarDropReceiver` は S3-buildmenu が作る（D-6）
- **新しい共有部品を見つけたときの手順**: 作業中に「別の画面でも要る部品」を見つけたら作らない。`LOGS/harness/uitk/S3-hotbar-modal/done.md` の「## integrator への依頼」に「部品名／Web の正本のパス／使う画面／このレーンで作らない理由」を 1 行で書き、`bd note <bd> "<同じ 1 行>"` にも残して先へ進む。既知の依頼（Task 1 Step 3 で雛形に書く）: (1) 無し（`OutlinedCountLabel` の縁の色のつまみは、既存部品へのつまみ 1 つの追加でこのレーンが足す。Task 6 Step 3・D-7）
- **参照キャプチャ**: `LOGS/uitk/reference-captures/web-869b2d4264/`（1280×720）・`web-0e81bbe3c3-2560x1080/`（2560×1080）の `3-hotbar-equipment-empty.png`・`3-modal.png`。1920×1080 の `3-modal` は無いので Task 11 Step 1 で撮る
- **並べ比べの画像の原本**は worktree の gitignore 下 `moorestech_client/.uloop/outputs/compare/S3-hotbar-modal/` に置き、`moorestech_logs` へ入れない。`moorestech_logs` へ書く前に `LOGS/README.md` を読む
- **報告**: `LOGS/harness/uitk/S3-hotbar-modal/done.md`（1 行目「状態: 完了」。途中で止まるときは「状態: 停止（理由）」）。節は「証拠」「差の表」「要裁定」「integrator への依頼」「未確認」

## 設計検査記録

- 配置検査: 実施済み / 違反4件・修正4件 / 間引き撤回・Select と Modal を UiCommandResult のコマンドへ・WheelPassthroughMark を Foundation/Input へ
- Phase 2.6: 実施済み / 強4・弱3・第3バケツ3 / 名前だけの絵を判別子で・payload に VM・表示名空の扱い・間引き撤回（A-57）、弱は重複の導出を 1 本に
- 再検査 3（独立評価 3〜4 巡目の後の変更範囲・fresh-context）: 実施済み / 違反1・強0・弱1・第3バケツ0 / 全部反映（テストだけが呼ぶ `ModalLogic.CanConfirm` を消し既存テストを書き換え・空白の判定を private の 1 か所に。部分移植の残りを done.md に置く形は新規パターンとして done.md の要裁定でなく判断記録 D-41 に理由付きで残す）
- 再検査 2（独立評価 2 巡目の後の変更範囲・fresh-context）: 実施済み / 違反0・強0・弱1・第3バケツ1 / 弱（`ModalLogic` の判定の重複）は採って 1 本に（D-41）。第3バケツ（`BlueprintNameInputState.Confirm` の空白の規則）は既存の「本PR外のリファクタ提案」と D-12 のまま
- 再検査（独立評価 1 巡目の後・fresh-context）: 実施済み / 違反3・強1・弱3・不一致1 / 全部反映（ESC を Web と同じ 1 回へ・幕の取消を View へ・`OutlinedCountLabel` の縁の色のつまみ・ホイール素通しの判定 `IsPassthrough`・確定の文を `TryGetConfirmableText` の 1 本に・`IsBenign` の一覧を全 true の腕に）

## 死活表（Phase 2.5）

全体スイッチ UITK（`UiInputExclusivity.ActiveUiSystem == GameUi`）で起動したとき、Web の系で動いていた振る舞いが生きるか。`MainGameStarter` は両系で共通の登録（`MainGameModelRegistration`・`MainGameInteractionRegistration`・`MainGameUiStateRegistration`）の後に `MainGameUiRegistration.Register(builder, gameUiRoot, UiInputExclusivity.ActiveUiSystem)` を呼び、Web の橋（`HotbarTopic`・`Hotbar*ActionHandler`・`ModalTopic`・`ModalRespondActionHandler`）は Web の系でだけ登録される。

| 振る舞い | GameUi で生きるか | 根拠 |
|---|---|---|
| ホットバーの表示（割当・選択・未解決・受理外の減光） | 生きる | 源（`ClientHotbarDatastore`・`PlacementTargetResolver`・`PlaceSystemStateController` ほか）は両系で登録済み。`HotbarModel`→`HotbarHudView` が描く（Task 2・3・6・7） |
| 数字キーで枠を選ぶ | 生きる（このレーンの外） | `HotbarKeyInput`・`HotbarTapInputService` は Client.Game で、UI の系を読まない |
| 数字キーの長押しで今の設置対象を枠へ割り当てる | 生きる（このレーンの外） | `PlaceBlockState` が `HotbarTapInputService.ApplyLongPressAssign` を呼び `RequestAssign` を積む。UI の系を読まず、割当の変化は `OnAssignmentsChanged` で Model が描き直す |
| 枠のクリックで選ぶ | 生きる | Web の `hotbar.select` の代わりに `HotbarCommands.Select` が同じ規則（`HotbarSelectRule`）で `EnqueueSelectRequest` を積み、`HotbarTapInputService` が消費する。受けない状態は良性の拒否を源がトースト口へ渡す（Task 4・6・7） |
| 割当・入替・解除の D&D | 生きる | 源 → 解放点の印（`IHotbarDropReceiver.Receive`）→ `HotbarDropResolver` → `HotbarCommands.Apply` → `RequestAssign/Swap/Clear`（Task 4・6・7）。ビルドメニューの源は S3-buildmenu が最初から同じ口で落とすので、このレーンのマージで割当が生きる。パネル越しの横断は S3-ladder |
| 枠の上でのクリックがワールドの設置にならない | 生きる | 列と枠は `PickingMode.Position` で、`GameUiPointerProbe`→`UiPointerHitTest.IsPointerOverAnyUi` が真になり `PlaceBlockState` が設置を止める（Task 7 の Pick の L1、Task 12 の録画） |
| 帯の何も無い所の入力の素通し | 生きる | 帯は `PickingMode.Ignore`（Task 7 の L1） |
| 枠の上のホイールの素通し | 生きる（判定まで。使う側は S4-inventory） | 列に印、`WheelPassthroughMark.IsPassthrough` が枠の上で真・帯で偽（Task 7 の L1）。今のゲームで枠の上のホイールを読むのは装備の切り替えだけで、それは S4-inventory が `IsPassthrough` を読んで繋ぐ（Web `EquipmentPanel/index.tsx:60` と同じ形）。D-21 |
| 列の上のホイールがゲーム側へ届く（装備の持ち替え） | S4-inventory のマージまで死ぬ | 列に印 `wheel-passthrough` は付けるが、`IsPointerOverAnyUi` の真で抑えられるホイールを印で素通す読み手は S4-inventory（[A-58]）。その間の装備の持ち替えは §4.4 の裁定「UITK の間、未実装の画面は使えない」で受け入れる。notes で指揮へ返す |
| 錨 `hotbar.hud` のチュートリアル強調 | S2-tutorial の描き手が入っていれば生きる | `TutorialAnchor.Mark(row, AnchorIds.HotbarHud)` を付ける。`TutorialAnchorCoveragePending` に行があれば同じ PR で消す（Task 7 Step 6） |
| BP 名の入力・確定・取消 | 生きる | `BlueprintNameInputModalBridge`（両系で登録）が `ModalRequestService` に要求を出し、UITK の `ModalScreenPresenter` だけが描き、`ModalView`・幕が `IModalCommands.Respond` を返す（Task 4・8・9） |
| 名前入力中にゲームのキーが動かない | 生きる | 入力欄のフォーカスで `GameUiInputRules` が `UiInputExclusivity.SetGameUiTextInputFocused(true)` を立て、ゲームのキー入力が止まる（C2-b の仕組み。`GameTextField` を使うので画面側の追加は無い）。フォーカスが入ることは Task 8 の L1、名前を打つ間に設置・移動が起きないことは Task 12 の録画で見る |
| 名前入力中のカーソル解放 | 生きる（このレーンの外） | `BlueprintNameInputCursorApplier`（Client.Game。`BlueprintNameInputState.OnOpenChanged` を購読）が両系で動き、UI の系を読まない |
| 幕の上のクリックがワールドの設置にならない | 生きる | `UiModalHost` の幕は `PickingMode.Position` で `IsPointerOverAnyUi` が真になり、`PlaceBlockState` が設置を止める（Task 12 の幕で取消の段で BP もブロックも増えない） |
| 右短押しで名前入力を閉じる | 生きる | `BlueprintCopySystem`（Client.Game）が `BlueprintNameInputState.Cancel` を呼び、ブリッジが `CancelPendingRequest` → `OnPendingChanged` → `Hide`（Task 12 で既存シナリオ `building/blueprint-copy-paste-via-ui` を通す） |
| 入力欄の ESC | 生きる（Web と同じ 1 回で閉じる） | 既存の規則（`GameUiInputRules`・C2-b の albof.12 要件）は入力欄の ESC を消費してフォーカスを外す。`ModalView` はその外れ（`FocusOutEvent`）を、このフレームの ESC が消費済み（`UiInputExclusivity.IsEscapeConsumedThisFrame`）のときだけ取消として読む。規則は書き換えず、Web の Mantine と同じ 1 回で BP の命名を取り消す。変換中の ESC は規則がフォーカスを外さないので閉じない（albof.12）。ESC はワールドへ漏れない（消費済みの印を `InputSuppressionFilter` が読み、ポーズ・建築モードの `CloseUI` は立たない）。フォーカスの無い ESC は既存のゲーム入力の経路（`PlaceBlockState` の `CloseUI` → BP 名の入力の取消 → ブリッジの `CancelPendingRequest`）が 1 回で閉じる（Task 8 Step 0）。Task 8 の L1 と Task 12 の録画で見る。D-11 |
| ゲーム終了での保留の取消 | 生きる | `ModalRequestService.Initialize` が `GameShutdownEvent` を購読（両系） |

---

## File Structure

| パス | 責務 | 新規／変更 |
|---|---|---|
| `S/Client.Ui.State/Hotbar/HotbarSlotViewModel.cs` | 判別 union（抽象基底＋sealed の `HotbarEmptySlot`・`HotbarUnresolvedSlot`・`HotbarAssignedSlot` を 1 ファイルに置く。前例 `HotbarDropAction.cs`） | 新規 |
| `S/Client.Ui.State/Hotbar/HotbarSelection.cs` | 選択枠の有無を型で持つ値（`None`・`Of`・`TryGetSlot`） | 新規 |
| `S/Client.Ui.State/Hotbar/HotbarViewModel.cs` | 不変の ViewModel（枠の列・選択・受理・表示）。値の等価 | 新規 |
| `S/Client.Ui.State/Hotbar/HotbarViewModelFactory.cs` | 枠・由来・UI 状態・画面・スキットから ViewModel を組む純関数 | 新規 |
| `S/Client.Ui.State/Hotbar/HotbarSelectRule.cs` | 選択を受ける UI 状態の唯一の規則（`GameScreen`・`PlaceBlock`） | 新規 |
| `S/Client.Ui.State/Hotbar/HotbarModel.cs` | 源を購読し、変化ごとに同期で組み直して `Current` で配る（`IInitializable`。[A-57]） | 新規 |
| `S/Client.Ui.State/Hotbar/IHotbarCommands.cs` | 画面から呼ぶ操作（`UiCommandResult Select`・`void Apply`。[A-8]） | 新規 |
| `S/Client.Ui.State/Hotbar/HotbarCommands.cs` | 本番の実装（規則で受理を決め、受けない状態は良性の拒否 `HotbarSelectNotAccepted`。`ClientHotbarDatastore` へ積む） | 新規 |
| `S/Client.Ui.State/Modal/Logic/ModalLogic.cs` | 可否と確定の文を 1 回で導く `TryGetConfirmableText` と、BP 名の書き戻しが読む `HasConfirmableText` を足し、空白の除去を private の 1 か所に置く。既存の `CanConfirm`（本番の呼び手 0・テストだけ）は消す | 変更 |
| `S/Client.Tests/Ui/State/Modal/ModalLogicTest.cs` | `CanConfirm` を読む既存の 2 件（Web `modalLogic.test.ts` の写し。名前とタグは変えない）を `TryGetConfirmableText` の真偽へ書き換える | 変更 |
| `S/Client.Ui.State/Modal/IModalCommands.cs` | モーダルの応答の操作（`UiCommandResult Respond(ModalResponse)`。[A-8]） | 新規 |
| `S/Client.Ui.State/Modal/ModalCommands.cs` | 本番の実装（`ModalRequestService.Respond`。古い応答は良性の拒否 `ModalResponseStale`） | 新規 |
| `S/Client.Ui.State/Commands/UiCommandRejectReason.cs` | enum と 3 つの switch の末尾に 2 値（共有区画） | 変更（共有区画） |
| `Localization/localization.csv`・`W/src/shared/i18n/generated/localizationKeys.ts`・`moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` | 拒否理由の文言 2 行と再生成（`gen:i18n`・SchemaWatcher の印） | 変更（共有区画・生成物） |
| `S/Client.Ui.State/Blueprint/BlueprintNameInputModalBridge.cs` | 確定の検証を `ModalLogic.HasConfirmableText` に委ねる | 変更 |
| `S/Client.Ui/Screens/Hotbar/HotbarHudPresenter.cs` | View を Chrome/Viewport へ 1 回載せて繋ぐ | 新規 |
| `S/Client.Ui/Screens/Hotbar/HotbarHudView.cs` | 帯・列・9 つの枠。`Render(HotbarViewModel)` | 新規 |
| `S/Client.Ui/Screens/Hotbar/HotbarHud.uss` | 画面の USS（帯・列・枠のつまみの上書き・番号タブ・減光） | 新規 |
| `S/Client.Ui/Screens/Hotbar/Parts/HotbarCell.cs` | 1 枠（番号タブ＋`SlotFrame`＋`PlacementTargetFace`）。落とし先の印と源の印 | 新規 |
| `S/Client.Ui/Screens/Hotbar/Parts/HotbarNumberTab.cs` | 番号タブ（暗い縁の 13px の数字） | 新規 |
| `S/Client.Ui/Screens/Hotbar/Parts/HotbarCalcKnobs.cs` | calc() を通る 2 つのつまみ（タブのはみ出し・列の横ずれ）を customStyle から読んで当てる（[A-47]） | 新規 |
| `S/Client.Ui/Screens/Hotbar/Parts/HotbarSlotDropMark.cs` | 枠の落とし先の印（State の `IHotbarDropReceiver`。`MarkTarget<IHotbarDropReceiver>` で付く。枠番号と操作を持ち、受けた源を解いて `Apply`） | 新規 |
| `S/Client.Ui/Screens/Hotbar/Parts/HotbarRowGapDropMark.cs` | 列の隙間の印（`IHotbarDropReceiver`。`MarkTarget<IHotbarDropReceiver>` で付く。受けて無操作） | 新規 |
| `S/Client.Ui/Screens/Hotbar/Parts/HotbarSlotDragPayload.cs` | 枠の源（タップ → 選択・ドロップ → 解決） | 新規 |
| `S/Client.Ui/Components/Slots/OutlinedCountLabel/OutlinedCountLabel.uss`・`.cs` | 縁の色のつまみ `--outlined-count-label-stroke` を読む（.cs はコメントだけ。D-7） | 変更（C3-a の部品。C4-a が先に足していれば触らない。Task 6 Step 3 の分岐） |
| `S/Client.Tests/Ui/Components/Slots/CountLabel/OutlinedCountLabelStrokeKnobTest.cs`・`SlotKnobOverrides.uss` | 縁の色のつまみの既定と上書きの L1 | 新規・変更 |
| `S/Client.Ui/Foundation/Input/WheelPassthroughMark.cs` | ホイール素通しの印と読み手の判定 `IsPassthrough`（Web `data-wheel-passthrough`・`isWheelPassthrough`。Web で 2 機能が使うので §5.1-1 で最初から Foundation） | 新規 |
| `S/Client.Ui/Screens/Modal/ModalScreenPresenter.cs` | 保留の要求を View にして宿主へ `Show`/`Hide`（幕の押下は View へ渡す） | 新規 |
| `S/Client.Ui/Screens/Modal/ModalView.cs` | `DialogFrame` の中身（本文・入力・ボタン）。確定・ESC 1 回・幕の押下（取消）を `IModalCommands.Respond` へ | 新規 |
| `S/Client.Ui/Screens/Modal/ModalButtonVariants.cs` | `ModalButtonTone` → `FormButtonVariant` の唯一の switch（[A-28]） | 新規 |
| `S/Client.Ui/Screens/Modal/Modal.uss` | 画面の USS（本文と入力の下余白） | 新規 |
| `S/Client.Starter/Registration/GameUi/HotbarGameUiRegistration.cs` | Model・操作・Presenter の登録 | 新規 |
| `S/Client.Starter/Registration/GameUi/ModalGameUiRegistration.cs` | Presenter の登録 | 新規 |
| `S/Client.Starter/Registration/MainGameUiRegistration.cs` | 画面登録区画に 2 行 | 変更（共有区画） |
| `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` | `Screens` 区画に `@import` 2 行 | 変更（共有区画） |
| `THEME/DerivedTokens.uss` | 末尾に名札付きブロック「S3-hotbar-modal」（色 6・opacity 2）と、`--outlined-count-label-color` の次に部品のつまみの既定 1 行 | 変更（共有区画 [A-52]） |
| `tools/uitk/webTokens/handWrittenDerivedRows.mjs`・`docs/uitk/translucent-tokens.md` | 半透明 3 行を前計算の表へ（再生成） | 変更（共有区画） |
| `S/Client.Tests/Ui/State/Hotbar/HotbarViewModelFactoryTest.cs` | 選択・受理・表示・未解決の L0 | 新規 |
| `S/Client.Tests/Ui/State/Modal/ModalConfirmableTextTest.cs` | `TryGetConfirmableText`・`HasConfirmableText` の L0 | 新規 |
| `S/Client.Tests/Ui/State/Modal/ModalCommandsTest.cs` | 保留への応答は受理、古い応答は良性の拒否（wire `no_pending_modal`） | 新規 |
| `S/Client.Tests/Ui/State/PauseMenuCommandsTest.cs` | 良性の拒否理由の一覧に 2 値を足す | 変更 |
| `S/Client.Tests/Ui/Screens/Hotbar/HotbarViewModelSamples.cs` | Web の fixture（枠 0 選択・絵あり、枠 4 未解決）の ViewModel | 新規 |
| `S/Client.Tests/Ui/Screens/Hotbar/RecordingHotbarCommands.cs` | 操作を文字列で記録する偽（Web の `payloadsOf` に当たる） | 新規 |
| `S/Client.Tests/Ui/Screens/Hotbar/HotbarTestPresentation.cs` | テスト用の `PlacementTargetIcons`・撮り直せる偽のサムネイル `HotbarTestThumbnails`・正方の絵のある割当 | 新規 |
| `S/Client.Tests/Ui/Screens/Hotbar/HotbarHudViewTest.cs` | 9 枠・選択・寸法・番号タブ・クリックの選択・スキットで退く（P6 1・2・5） | 新規 |
| `S/Client.Tests/Ui/Screens/Hotbar/HotbarHudDropTest.cs` | 割当（本物のエントリの源を単独で置く）・未解決の減光と解除・枠外で解除・隙間は無操作（P6 3・4・6） | 新規 |
| `S/Client.Tests/Ui/Screens/Hotbar/HotbarSlotDragGestureTest.cs` | タップとドラッグの分け方（C7 7 件） | 新規 |
| `S/Client.Tests/Ui/Screens/Hotbar/HotbarHudPickingTest.cs` | 帯は拾わず列だけが拾い印を持つ（alwaysOnHudWheel 2 件） | 新規 |
| `S/Client.Tests/Ui/Screens/Hotbar/HotbarIconCenteringTest.cs` | 絵が枠の中央（placementIconCentering 1 件）・同じ BP の撮り直しで絵が替わる | 新規 |
| `S/Client.Tests/Ui/Screens/Modal/ModalRequestSamples.cs` | Web の fixture の要求（確認・BP 名・エラー） | 新規 |
| `S/Client.Tests/Ui/Screens/Modal/RecordingModalCommands.cs` | 応答を記録する偽 | 新規 |
| `S/Client.Tests/Ui/Screens/Modal/ModalViewTest.cs` | 描画・確定の可否と空白の除去・色・ID が変わると空にする（P4 1・4） | 新規 |
| `S/Client.Tests/Ui/Screens/Modal/ModalScreenPresenterTest.cs` | OK で確定して消える・幕で取消・入力欄の ESC 1 回で取消（ワールドへ渡さない）・欄の外の押下では閉じない・入力欄にフォーカスがあるまま外から取り消されてもゲームのキー入力を止めたままにしない・実画面 px（P4 2・3、[A-41]）の 7 件 | 新規 |
| `SKILL/scenarios/uitk/uitk-hotbar-and-bp-modal.cs` | ホットバーの操作と BP 名のモーダルを UITK で通す録画 | 新規 |
| `docs/uitk/disposition.md` | 「## 処遇」の albof.30 の「写す予定」の行（`alwaysOnHudWheel.test.ts`「クリックを持つスロット列だけが…」）を消す（Task 7 で `<web-test>` を付けるのと同じコミット）。行は足さない | 変更（共有区画） |
| `S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs` | 在れば `AnchorIds.HotbarHud` の 1 行を消す | 変更（共有区画・在れば） |
| `docs/uitk/TODO.md` | 「## 実測の結論」末尾（世界の地の減光の照合）、要れば「## 見た目の例外」末尾（`LX-76`〜`LX-80`） | 変更（共有区画） |

ディレクトリのファイル数（.cs と .uss）: `State/Hotbar/` 8（既存の `Logic/` は別）・`State/Modal/` 既存 2＋2・`Foundation/Input/` 既存 4＋1・`Screens/Hotbar/` 3・`Screens/Hotbar/Parts/` 6・`Screens/Modal/` 4・`Tests/.../Screens/Hotbar/` 8・`Tests/.../Screens/Modal/` 4・`Tests/.../State/Hotbar/` 1・`Tests/.../State/Modal/` 2（L0-s3 の既存を含めて 10 以下を Task 1 Step 1 で数える）。`Registration/GameUi/` は 2 増える（Task 1 Step 1 で 10 以下を確かめる。超えるなら止めて指揮へ）。

**MainGameUiRegistration の画面登録区画に足す行**: 2 行（ホットバーとモーダルは別の画面なので 1 画面 1 行。`HotbarGameUiRegistration.Register(builder);`・`ModalGameUiRegistration.Register(builder);`）。

**つまみの上書き（Web `W/src/features/hotbar/HotbarPanel/style.module.css` の `.cell` の局所上書き変数と同名。値は Web と同じ。色は前計算トークン）**

| つまみ | Web の値 | UITK の値（`.hotbar-hud__cell`） |
|---|---|---|
| `--slot-size` | `var(--hotbar-slot-size)` | `var(--hotbar-slot-size)`（51.68px） |
| `--selected-face-bottom-trim` | `0.78px` | `0.78px` |
| `--selected-face-ring-inset` | `1.565749px` | `1.565749px` |
| `--icon-pad` | `5px` | `5px` |
| `--count-bottom` | `-1px` | `-1px` |
| `--bevel-inset` | `inset` | `inset`（[A-42]） |
| `--face-inset` | `0px` | `0px` |
| `--bevel-1` / `--bevel-2` / `--bevel-3` | `0.782875px` / `1.565749px` / `1.565749px` | 同じ |
| `--bevel-c1` | `rgb(125 127 133)` | `var(--hotbar-bevel-c1)` |
| `--bevel-c2` / `--bevel-c3` | `rgb(116 118 125)` | `var(--hotbar-bevel-c2)` |
| `--sel-c1` / `--sel-c2` / `--sel-c3` | `var(--select-cyan)` | `var(--select-cyan)` |
| `--slot-frame-face`（画面ルート `.hotbar-hud`） | （Web は `SlotFrame` の直書き `rgb(50 52 67 / 50%)`。地は世界） | `var(--hotbar-slot-frame-face)`（α 0.7226。bd `albof.34`） |

**写すテストの件数（Web の実物を grep して数え直した）**

| Web のファイル（`W/` から） | 件数 | 写す | 写さない・一部（処遇） | 写し先 |
|---|---|---|---|---|
| `e2e/tests/hotbar/hotbar.spec.ts`（P） | 6 | 6 | 一部 2 件（処遇の表には書かず `<web-test>` だけ付ける。残りの範囲は `done.md` の「## 部分移植の残り」）: 3 件目はビルドメニューのパネルを通さず本物のエントリの源を単独で置く（パネル越しは S3-ladder。D-16）、5 件目は会話窓の残りを S2-skit へ（D-15） | `HotbarHudViewTest`・`HotbarHudDropTest` |
| `src/features/hotbar/useHotbarDragSource.test.ts`（C） | 7 | 7 | 0 | `HotbarSlotDragGestureTest` |
| `src/features/hotbar/hotbarDnd.test.ts`（L） | — | — | L0-s3 で写し済み | — |
| `e2e/tests/modal.spec.ts`（P） | 4 | 4 | 0 | `ModalViewTest`・`ModalScreenPresenterTest` |
| `src/features/modal/modalLogic.test.ts`（L） | — | — | L0-s3 で写し済み | — |
| `e2e/tests/regression/placementIconCentering.spec.ts` | 2 | 1（ホットバー） | 1（ビルドメニューの件は S3-buildmenu が写す。D-14） | `HotbarIconCenteringTest` |
| `src/features/commonHud/alwaysOnHudWheel.test.ts` | 1 行（`it.each` のホットバー帯）＋1 件 | 1 行＋1 件 | 0（他の行は S2-hud・S2-mode-tooltip・S2-tutorial、装備 HUD は S4-inventory。[A-58]） | `HotbarHudPickingTest` |

合計: `<web-test>` の付く NUnit は 20 件（P6 6、C7 7、P4 4、placementIconCentering 1、alwaysOnHudWheel 2）。タグの無い NUnit（Web に無い主張）は `HotbarHudViewTest` 2・`HotbarHudDropTest` 1・`HotbarIconCenteringTest` 1（撮り直し）・L0 の `HotbarViewModelFactoryTest` 4・`ModalConfirmableTextTest` 2・`ModalCommandsTest` 2 と、モーダルの Task 8 の分（ESC の 1 件を含む）。

---
## Tasks

### Task 1: 前提の確認と記録の準備

**Files:**
- Create: `LOGS/harness/uitk/S3-hotbar-modal/done.md`（雛形。moorestech_logs 側）

**Interfaces:**
- Consumes: `origin/feature/uitk`（C3-a・C3-b・L0-s3・S2-hud・S3-buildmenu マージ済み）、Global Constraints の「使う部品と公開 API」「Model の源」
- Produces: bd のレーン ID（以下 `<bd>`）、`done.md` の雛形、API 名の読み替え表（違ったときだけ）

- [ ] **Step 1: 作業場所と部品の実物を確かめる**

Run:
```bash
pwd
git log -1 --format='%H %s' origin/feature/uitk
S=moorestech_client/Assets/Scripts
# C3-a（実物から写した名前の再確認）
grep -n "public SlotFrame(\|public void SetState" $S/Client.Ui/Components/Slots/SlotFrame/SlotFrame.cs
grep -n "public void SetIconTarget\|public void SetNameTarget" $S/Client.Ui/Components/Slots/PlacementTargetFace/PlacementTargetFace.cs
grep -n "public OutlinedCountLabel(\|icon-text-stroke-light" $S/Client.Ui/Components/Slots/OutlinedCountLabel/OutlinedCountLabel.cs $S/Client.Ui/Components/Slots/OutlinedCountLabel/OutlinedCountLabel.uss
# C3-b（マージ後に実物と照合）
grep -rn "public static void MarkSource<TTarget>\|public static void MarkTarget<TTarget>\|interface IUiDragPayload<in TTarget>" $S/Client.Ui/Foundation/DragDrop/
grep -rn "public UiModalHost(\|public void Show(\|public void Hide(\|OnBackdropClicked" $S/Client.Ui/Foundation/Modal/UiModalHost.cs
grep -n "public StageScaleSource(\|public static bool TryRead(IPanel" -r $S/Client.Ui/Foundation/Stage/
grep -n "public DialogFrame(\|public void SetTitle\|public void AddAction" $S/Client.Ui/Components/Panels/DialogFrame/DialogFrame.cs
# S2-hud（マージ済み。念のため）・S3-buildmenu（マージ後に実物と照合）
grep -rn "class BlockingSkitPresence\|IsBlocking" $S/Client.Ui.State/SkitPresence/
grep -n "class PlacementTarget\(Block\|TrainCar\|ConnectTool\|Blueprint\|NameOnly\)\?Icon\b\|public override bool Equals" $S/Client.Ui.State/PlacementTarget/PlacementTargetIcon.cs
grep -n "public static PlacementTargetIcon Resolve(" $S/Client.Ui.State/PlacementTarget/PlacementTargetIconResolver.cs
grep -n "public PlacementTargetIcons(\|public bool TryToUiIcon(" $S/Client.Ui/Content/PlacementTargetIcons.cs
grep -n "PlacementTargetIcons" $S/Client.Starter/Registration/GameUi/ContentGameUiRegistration.cs
# 段階 1 のコマンドの結果と入力
grep -n "LanguageAlreadySelected\|public static bool IsBenign\|public static string ToWireCode\|public static LocalizationKey ToLocalizationKey" $S/Client.Ui.State/Commands/UiCommandRejectReason.cs
grep -n "public void Report(\|Toaster" $S/Client.Ui.State/Toast/UiCommandResultToaster.cs $S/Client.Tests/Ui/Screens/ScreenTestHost.cs
grep -n "ConsumeEscapeThisFrame\|IsEscapeConsumedThisFrame\|ClearEscapeLatchForTest" $S/Client.Input/UiInputExclusivity.cs
grep -n "SubscribeEventResponse(UnlockedEventPacket.EventTag" -r $S/Client.Game/InGame/UnlockState/
grep -n "interface IHotbarDropReceiver\|void Receive(" $S/Client.Ui.State/Hotbar/Logic/IHotbarDropReceiver.cs
grep -n "public void OnDropped\|IHotbarDropReceiver\|public BuildMenuEntryDragPayload(\|interface IBuildMenuEntryHandler" -r $S/Client.Ui/Screens/BuildMenu/Parts/
grep -n "class RecordingBuildMenuEntryHandler\|Selected\|^namespace" $S/Client.Tests/Ui/Screens/BuildMenu/Support/RecordingBuildMenuEntryHandler.cs
ls $S/Client.Ui/Content/*.cs | wc -l
# トークンとアンカー
grep -n "hotbar-slot-size\|hotbar-slot-gap\|hotbar-bottom\|hotbar-number-tab-overhang\|hotbar-horizontal-offset\|select-cyan\|icon-text-stroke-dark" $S/Client.Ui/Foundation/Theme/Tokens.uss
grep -n "mantine-spacing-lg\|modal-size\|modal-backdrop-face" $S/Client.Ui/Foundation/Theme/MantineTokens.uss $S/Client.Ui/Foundation/Theme/DerivedTokens.uss
grep -n "HotbarHud" $S/Client.Game/InGame/Tutorial/UIHighlight/AnchorIds.cs
grep -n "section: Screens" "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
# ディレクトリのファイル数（10 以下）
ls $S/Client.Starter/Registration/GameUi/*.cs | wc -l
ls $S/Client.Tests/Ui/State/Hotbar/*.cs $S/Client.Tests/Ui/State/Modal/*.cs | wc -l
ls $S/Client.Ui/Foundation/Input/*.cs $S/Client.Ui.State/Modal/*.cs
```
Expected: worktree の絶対パス。C3-a の 3 行は Global Constraints のとおり。C3-b の `MarkSource<TTarget>(VisualElement source, IUiDragPayload<TTarget> payload)`・`MarkTarget<TTarget>(VisualElement target, TTarget handler)`・`interface IUiDragPayload<in TTarget>`・`UiModalHost(StageScaleSource stageScale)`・`DialogFrame(string testid)`、S2-hud の `BlockingSkitPresence`・`IsBlocking`、S3-buildmenu の `PlacementTargetIcon` の 6 型（ID だけ）と値の等価・`PlacementTargetIconResolver.Resolve`・DI のクラス `PlacementTargetIcons` の 3 引数の ctor（`HotbarTestPresentation.Create` を実物の引数の順で組む）と `bool TryToUiIcon(PlacementTargetIcon, out UiIcon)`・`ContentGameUiRegistration` の登録・`IHotbarDropReceiver.Receive`・`BuildMenuEntryDragPayload : IUiDragPayload<IHotbarDropReceiver>` と `OnDropped(IHotbarDropReceiver receiver)`・`BuildMenuEntryDragPayload(Guid, IBuildMenuEntryHandler)`・`RecordingBuildMenuEntryHandler`（`Selected`）が出る。トークン 7 つ・`--mantine-spacing-lg: 20px`・`--modal-size`・`--modal-backdrop-face`・`HotbarHud = "hotbar.hud"`・`/* == section: Screens == */` が出る。`Registration/GameUi/*.cs` が 8 以下（2 足して 10 以下）、State のテストが 7 以下。`UiCommandRejectReason` の 3 つの switch、`ScreenTestHost.Toaster`、ESC の印の 3 つ、`ClientGameUnlockStateDatastore` の ctor の購読（D-18 の順序の前提）が出る。`Foundation/Input/` の .cs が 9 以下、`State/Modal/` の .cs が 8 以下。
- どれかのファイルが無ければ、その依存（C3-b・S2-hud・S3-buildmenu）が未取り込みなので止め、`done.md` を「状態: 停止（<依存> 未取り込み）」にして指揮へ報告する
- 名前が違えば以後の計画を実物の名前で読み替え、`done.md` の「未確認」に「API 名の読み替え: <旧> → <実物>」と書く。S3-buildmenu の型の名前・引数が違えば、Task 2・3・6・7 の該当箇所を実物に読み替える（S3-buildmenu のファイルは書き換えない。足りなければ止めて指揮へ）
- `Registration/GameUi/*.cs` が 9 以上なら（このレーンの 2 本で 11 以上）止めて指揮へ。`done.md` の「integrator への依頼」に「`Registration/GameUi/` の分け方」を書く（サブディレクトリの切り方は共有区画の外で integrator の持ち物。S3-buildmenu と同じ扱い）。再編が `origin/feature/uitk` に入り、取り込んで 10 本以下の置き場へ足せるまで、このレーンは完了にせずマージもしない（AGENTS.md の 1 ディレクトリ 10 ファイル）。再編後は File Structure と Task 9 の登録のパスを新しいサブディレクトリへ読み替え、読み替えたことを `done.md` に書く

- [ ] **Step 2: 使う文言が要らないことを確かめる**

Run:
```bash
grep -n "^ui.blueprint\.\(webModalTitle\|webModalMessage\|webModalConfirm\)," Localization/localization.csv
grep -rn "WebModalTitle" moorestech_client/Assets/Scripts/Client.Ui.State/Blueprint/BlueprintNameInputModalBridge.cs
```
Expected: CSV に 3 行（english `Blueprint Name` 系）。ブリッジが要求の時点の言語で解いた文を `ModalRequest` に入れているので、UITK の View は `UiText.FromVerbatim` で出すだけ（[A-40]）。ホットバーの番号は訳さない数字（`FromVerbatim`）。`localization.csv` は書かない

- [ ] **Step 3: bd を積み、done.md の雛形を置く**

Run:
```bash
bd create "UITK S3-hotbar-modal: ホットバー 9 枠とモーダル層・BP 名の入力を UITK へ" --parent moorestech-albof -t task -p 2
bd update <bd> --claim
bd update moorestech-albof.30 --claim
bd update moorestech-albof.34 --claim
mkdir -p ../moorestech_logs/harness/uitk/S3-hotbar-modal
```
`done.md` に次を書く（1 行目は終わるまで「状態: 作業中」）:
```markdown
状態: 作業中

# S3-hotbar-modal 作業報告

## 証拠
## 差の表
## 要裁定
## integrator への依頼
- 縁の色のつまみ --outlined-count-label-stroke: <このレーンが足した | C4-a が先に足していたので使うだけ>（設計書 A-108・計画 D-7・Task 6 Step 3 の分岐）
## 未確認
```
Expected: bd の ID が出る。以後 `<bd>` をその ID で読む。`bd note <bd>` に上の 2 行と「integrator への依頼」の 1 行を同じ文で残す

---

### Task 2: 選択の規則と ViewModel（`Client.Ui.State/Hotbar`）

**Files:**
- Create: `S/Client.Ui.State/Hotbar/HotbarSlotViewModel.cs`・`HotbarSelection.cs`・`HotbarViewModel.cs`・`HotbarViewModelFactory.cs`・`HotbarSelectRule.cs`
- Test: `S/Client.Tests/Ui/State/Hotbar/HotbarViewModelFactoryTest.cs`

**Interfaces:**
- Consumes: S3-buildmenu が作る `PlacementTargetIcon` 系（W3 で先に入る。Task 1 Step 1 で実物照合）、`PlacementOrigin`・`UIStateEnum`・`UiScreen`・`UiScreenTraitsTable`
- Produces:
  ```csharp
  namespace Client.Ui.State.Hotbar
  {
      public abstract class HotbarSlotViewModel { public abstract bool IsAssigned { get; } }
      public sealed class HotbarEmptySlot : HotbarSlotViewModel { public static readonly HotbarEmptySlot Instance; }
      public sealed class HotbarUnresolvedSlot : HotbarSlotViewModel { public HotbarUnresolvedSlot(Guid assignedId); public Guid AssignedId { get; } }
      public sealed class HotbarAssignedSlot : HotbarSlotViewModel { public HotbarAssignedSlot(Guid targetId, string displayLabel, PlacementTargetIcon icon); public Guid TargetId { get; } public string DisplayLabel { get; } public PlacementTargetIcon Icon { get; } }
      public readonly struct HotbarSelection { public static readonly HotbarSelection None; public static HotbarSelection Of(int slotIndex); public static HotbarSelection FromOrigin(PlacementOrigin origin); public bool TryGetSlot(out int slotIndex); public bool IsSlot(int slotIndex); }
      public sealed class HotbarViewModel { public HotbarViewModel(IReadOnlyList<HotbarSlotViewModel> slots, HotbarSelection selection, bool selectAccepted, bool visible); public IReadOnlyList<HotbarSlotViewModel> Slots { get; } public HotbarSelection Selection { get; } public bool SelectAccepted { get; } public bool Visible { get; } }
      public static class HotbarViewModelFactory { public static HotbarViewModel Create(IReadOnlyList<HotbarSlotViewModel> slots, PlacementOrigin origin, UIStateEnum uiState, UiScreen screen, bool blockingSkit); }
      public static class HotbarSelectRule { public static bool Accepts(UIStateEnum uiState); }
  }
  ```

- [ ] **Step 1: 失敗するテストを書く**

`S/Client.Tests/Ui/State/Hotbar/HotbarViewModelFactoryTest.cs`:
```csharp
using System;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Hotbar;
using Client.Ui.State.PlacementTarget;
using Client.Ui.State.Routing;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.Hotbar
{
    /// <summary>
    /// ホットバーの ViewModel の組み立て（選択・受理・表示・未解決。L0）
    /// Building the hotbar view model (selection, acceptance, visibility, unresolved; L0)
    /// </summary>
    public class HotbarViewModelFactoryTest
    {
        private static readonly HotbarSlotViewModel[] NineEmpty = Enumerable.Repeat<HotbarSlotViewModel>(HotbarEmptySlot.Instance, 9).ToArray();

        [Test]
        public void 由来がホットバーの枠ならその枠を選択しそれ以外は選択なし()
        {
            var fromSlot = HotbarViewModelFactory.Create(NineEmpty, PlacementOrigin.FromHotbarSlot(2), UIStateEnum.GameScreen, UiScreen.None, false);
            Assert.IsTrue(fromSlot.Selection.IsSlot(2));
            Assert.IsFalse(fromSlot.Selection.IsSlot(0));

            var fromMenu = HotbarViewModelFactory.Create(NineEmpty, PlacementOrigin.NonHotbar, UIStateEnum.PlaceBlock, UiScreen.None, false);
            Assert.IsFalse(fromMenu.Selection.TryGetSlot(out _));
        }

        [Test]
        public void 選択を受けるのはゲーム画面と建築モードだけ()
        {
            // Web uiStateAcceptsHotbarSelect と HotbarSelectActionHandler の 2 状態
            // The two states of Web uiStateAcceptsHotbarSelect and HotbarSelectActionHandler
            foreach (UIStateEnum state in Enum.GetValues(typeof(UIStateEnum)))
            {
                var expected = state == UIStateEnum.GameScreen || state == UIStateEnum.PlaceBlock;
                Assert.AreEqual(expected, HotbarSelectRule.Accepts(state), state.ToString());
                Assert.AreEqual(expected, HotbarViewModelFactory.Create(NineEmpty, PlacementOrigin.NonHotbar, state, UiScreen.None, false).SelectAccepted, state.ToString());
            }
        }

        [Test]
        public void 常駐HUDを出す画面で見えて研究画面とblockingスキットで退く()
        {
            Assert.IsTrue(Create(UiScreen.None, false).Visible);
            Assert.IsTrue(Create(UiScreen.PlayerInventory, false).Visible);
            Assert.IsTrue(Create(UiScreen.BuildMenu, false).Visible);
            Assert.IsFalse(Create(UiScreen.ResearchTree, false).Visible);
            Assert.IsFalse(Create(UiScreen.None, true).Visible);

            #region Internal

            HotbarViewModel Create(UiScreen screen, bool blockingSkit)
            {
                return HotbarViewModelFactory.Create(NineEmpty, PlacementOrigin.NonHotbar, UIStateEnum.GameScreen, screen, blockingSkit);
            }

            #endregion
        }

        [Test]
        public void 同じ中身なら等しく絵の出どころか表示名が変われば等しくない()
        {
            // BP のサムネイルの到着と改名は ViewModel の値の違いとして届き、同じ値なら描き直さない
            // Blueprint thumbnail arrival and renames arrive as view-model value changes; equal values skip the redraw
            var targetId = Guid.NewGuid();
            var nameOnly = Assigned(new HotbarAssignedSlot(targetId, "参照拠点", PlacementTargetNameOnlyIcon.Instance));
            var sameNameOnly = Assigned(new HotbarAssignedSlot(targetId, "参照拠点", PlacementTargetNameOnlyIcon.Instance));
            var captured = Assigned(new HotbarAssignedSlot(targetId, "参照拠点", new PlacementTargetBlueprintIcon(targetId)));
            var renamed = Assigned(new HotbarAssignedSlot(targetId, "第二拠点", PlacementTargetNameOnlyIcon.Instance));
            Assert.AreEqual(nameOnly, sameNameOnly);
            Assert.AreNotEqual(nameOnly, captured);
            // BP の絵は参照の等価なので、同じ BP でも組み直せば等しくない（撮り直しを描き直す前提。計画 D-19）
            // A blueprint icon has reference equality, so the same blueprint rebuilt is unequal (the premise for redrawing a recapture; plan D-19)
            Assert.AreNotEqual(captured, Assigned(new HotbarAssignedSlot(targetId, "参照拠点", new PlacementTargetBlueprintIcon(targetId))));
            Assert.AreNotEqual(nameOnly, renamed);
            Assert.IsTrue(nameOnly.Slots[4].IsAssigned);
            Assert.IsFalse(nameOnly.Slots[0].IsAssigned);

            // 未解決の枠も割当あり（外せる）として扱う
            // An unresolved slot also counts as assigned (clearable)
            Assert.IsTrue(new HotbarUnresolvedSlot(targetId).IsAssigned);

            #region Internal

            HotbarViewModel Assigned(HotbarSlotViewModel slot4)
            {
                var slots = NineEmpty.ToArray();
                slots[4] = slot4;
                return HotbarViewModelFactory.Create(slots, PlacementOrigin.FromHotbarSlot(0), UIStateEnum.GameScreen, UiScreen.None, false);
            }

            #endregion
        }
    }
}
```

Run: `uloop compile --project-path ./moorestech_client`
Expected: コンパイルエラー（`Client.Ui.State.Hotbar` の型が無い）

- [ ] **Step 2: 型を書く**

`S/Client.Ui.State/Hotbar/HotbarSlotViewModel.cs`:
```csharp
using System;
using Client.Ui.State.PlacementTarget;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// ホットバー 1 枠の中身。未割当・割当はあるが解けない・解けた対象の 3 つ（Web の null・unresolved・解決済み）
    /// One hotbar slot's content: unassigned, assigned but unresolvable, or resolved (Web null, unresolved, resolved)
    /// </summary>
    public abstract class HotbarSlotViewModel
    {
        private protected HotbarSlotViewModel()
        {
        }

        // 割当があるか（未解決も割当あり。外せる・白面で出す）
        // Whether an assignment exists (unresolved counts: it can be cleared and shows the white face)
        public abstract bool IsAssigned { get; }
    }

    public sealed class HotbarEmptySlot : HotbarSlotViewModel
    {
        public static readonly HotbarEmptySlot Instance = new();

        private HotbarEmptySlot()
        {
        }

        public override bool IsAssigned => false;
    }

    public sealed class HotbarUnresolvedSlot : HotbarSlotViewModel
    {
        public Guid AssignedId { get; }

        public HotbarUnresolvedSlot(Guid assignedId)
        {
            AssignedId = assignedId;
        }

        public override bool IsAssigned => true;
        public override bool Equals(object obj) => obj is HotbarUnresolvedSlot other && other.AssignedId == AssignedId;
        public override int GetHashCode() => AssignedId.GetHashCode();
    }

    // 解けた対象。Screens が Client.Game の型を受けないよう、描くのに要る ID・表示名・絵の出どころだけを運ぶ
    // A resolved target; carries only the id, display name and picture source needed to draw, so Screens never take Client.Game types
    public sealed class HotbarAssignedSlot : HotbarSlotViewModel
    {
        public Guid TargetId { get; }
        public string DisplayLabel { get; }
        public PlacementTargetIcon Icon { get; }

        public HotbarAssignedSlot(Guid targetId, string displayLabel, PlacementTargetIcon icon)
        {
            TargetId = targetId;
            DisplayLabel = displayLabel;
            Icon = icon;
        }

        public override bool IsAssigned => true;
        public override bool Equals(object obj) => obj is HotbarAssignedSlot other && other.TargetId == TargetId && other.DisplayLabel == DisplayLabel && other.Icon.Equals(Icon);
        public override int GetHashCode() => TargetId.GetHashCode();
    }
}
```

`S/Client.Ui.State/Hotbar/HotbarSelection.cs`:
```csharp
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// 光らせる枠の有無と番号。Web の selectedSlot -1 を型で表す
    /// Whether a slot is highlighted and which; the Web selectedSlot -1 expressed as a type
    /// </summary>
    public readonly struct HotbarSelection : IEquatable<HotbarSelection>
    {
        public static readonly HotbarSelection None = new(false, 0);

        private readonly bool _hasSlot;
        private readonly int _slotIndex;

        private HotbarSelection(bool hasSlot, int slotIndex)
        {
            _hasSlot = hasSlot;
            _slotIndex = slotIndex;
        }

        public static HotbarSelection Of(int slotIndex)
        {
            return new HotbarSelection(true, slotIndex);
        }

        // 光らせる枠は設置対象の持ち主が持つ由来から導く（表示と実対象が構造的に一致する。Web HotbarTopic と同じ）
        // The highlighted slot derives from the origin the placement owner holds, so display and target agree (same as Web HotbarTopic)
        public static HotbarSelection FromOrigin(PlacementOrigin origin)
        {
            return origin.TryGetHotbarSlot(out var slot) ? Of(slot) : None;
        }

        public bool TryGetSlot(out int slotIndex)
        {
            slotIndex = _slotIndex;
            return _hasSlot;
        }

        public bool IsSlot(int slotIndex)
        {
            return _hasSlot && _slotIndex == slotIndex;
        }

        public bool Equals(HotbarSelection other) => _hasSlot == other._hasSlot && _slotIndex == other._slotIndex;
        public override bool Equals(object obj) => obj is HotbarSelection other && Equals(other);
        public override int GetHashCode() => _hasSlot ? _slotIndex + 1 : 0;
    }
}
```

`S/Client.Ui.State/Hotbar/HotbarViewModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// ホットバー全体の表示状態。値が同じなら等しく、ReactiveProperty が描き直しを省く
    /// The whole hotbar display state; equal values compare equal so ReactiveProperty skips redraws
    /// </summary>
    public sealed class HotbarViewModel : IEquatable<HotbarViewModel>
    {
        public IReadOnlyList<HotbarSlotViewModel> Slots { get; }
        public HotbarSelection Selection { get; }
        public bool SelectAccepted { get; }
        public bool Visible { get; }

        public HotbarViewModel(IReadOnlyList<HotbarSlotViewModel> slots, HotbarSelection selection, bool selectAccepted, bool visible)
        {
            Slots = slots;
            Selection = selection;
            SelectAccepted = selectAccepted;
            Visible = visible;
        }

        public bool Equals(HotbarViewModel other)
        {
            if (other == null) return false;
            return Selection.Equals(other.Selection) && SelectAccepted == other.SelectAccepted && Visible == other.Visible && Slots.SequenceEqual(other.Slots);
        }

        public override bool Equals(object obj) => obj is HotbarViewModel other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Selection, SelectAccepted, Visible, Slots.Count);
    }
}
```

`S/Client.Ui.State/Hotbar/HotbarSelectRule.cs`:
```csharp
using Client.Game.InGame.UI.UIState;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// 枠の選択を受ける UI 状態の唯一の規則（Web uiStateAcceptsHotbarSelect の写し）
    /// The single rule for which UI states accept a slot selection (port of Web uiStateAcceptsHotbarSelect)
    /// </summary>
    public static class HotbarSelectRule
    {
        // 選択を消費する者がいる 2 状態だけ。他で積むと画面を閉じた瞬間に暴発する
        // Only the two states with a consumer; queuing elsewhere fires the moment that screen closes
        public static bool Accepts(UIStateEnum uiState)
        {
            return uiState == UIStateEnum.GameScreen || uiState == UIStateEnum.PlaceBlock;
        }
    }
}
```

`S/Client.Ui.State/Hotbar/HotbarViewModelFactory.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Routing;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// 枠・由来・UI 状態・画面・スキットから ViewModel を組む純関数
    /// Pure function building the view model from slots, origin, UI state, screen and skit
    /// </summary>
    public static class HotbarViewModelFactory
    {
        public static HotbarViewModel Create(IReadOnlyList<HotbarSlotViewModel> slots, PlacementOrigin origin, UIStateEnum uiState, UiScreen screen, bool blockingSkit)
        {
            // 常駐 HUD を出す画面で、blocking スキットの間だけ退く（Web App.tsx の screenShowsAlwaysOnHud と HotbarPanel の useBlockingSkitActive）
            // Shows on screens that show always-on HUDs and retreats only during a blocking skit (Web App.tsx screenShowsAlwaysOnHud and HotbarPanel useBlockingSkitActive)
            var visible = UiScreenTraitsTable.Get(screen).AlwaysOnHud && !blockingSkit;
            return new HotbarViewModel(slots, HotbarSelection.FromOrigin(origin), HotbarSelectRule.Accepts(uiState), visible);
        }
    }
}
```

- [ ] **Step 3: 流して緑にする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.Hotbar\."`
Expected: `ErrorCount: 0`、`HotbarViewModelFactoryTest` 4 件と既存 `HotbarDropResolverTest` が PASS

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Hotbar moorestech_client/Assets/Scripts/Client.Tests/Ui/State/Hotbar
git commit -m "feat(uitk): ホットバーの ViewModel と選択の規則を足す（S3-hotbar-modal）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 3: ホットバーの Model（源の購読）

**Files:**
- Create: `S/Client.Ui.State/Hotbar/HotbarModel.cs`

**Interfaces:**
- Consumes: Task 2 の型、Global Constraints の「Model の源」、`BlockingSkitPresence`（S2-hud）
- Produces: `namespace Client.Ui.State.Hotbar { public sealed class HotbarModel : IInitializable { public IReadOnlyReactiveProperty<HotbarViewModel> Current { get; } } }`（ctor は下のコード。Presenter が購読する）

- [ ] **Step 1: Model を書く**

`S/Client.Ui.State/Hotbar/HotbarModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Network.API;
using Client.Ui.State.PlacementTarget;
using Client.Ui.State.Routing;
using Client.Ui.State.SkitPresence;
using Server.Event.EventReceive;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// ホットバーの源を購読し、変化ごとに同期で組み直して配る（Web HotbarTopic と同じ源。設計書 A-57・前例 ModeHudModel）
    /// Subscribes to the hotbar sources and rebuilds synchronously on each change (same sources as Web HotbarTopic; design A-57, precedent ModeHudModel)
    /// </summary>
    public sealed class HotbarModel : IInitializable
    {
        private readonly ClientHotbarDatastore _datastore;
        private readonly PlacementTargetResolver _resolver;
        private readonly ClientBlueprintLibrary _blueprintLibrary;
        private readonly IBlueprintThumbnailLookup _thumbnails;
        private readonly PlaceSystemStateController _placeSystem;
        private readonly UIStateControl _uiStateControl;
        private readonly UiRoutingState _routing;
        private readonly BlockingSkitPresence _blockingSkit;
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly ReactiveProperty<HotbarViewModel> _current;

        public IReadOnlyReactiveProperty<HotbarViewModel> Current => _current;

        public HotbarModel(ClientHotbarDatastore datastore, PlacementTargetResolver resolver, ClientBlueprintLibrary blueprintLibrary, IBlueprintThumbnailLookup thumbnails, PlaceSystemStateController placeSystem, UIStateControl uiStateControl, UiRoutingState routing, BlockingSkitPresence blockingSkit, IVanillaApiEvent vanillaApiEvent)
        {
            _datastore = datastore;
            _resolver = resolver;
            _blueprintLibrary = blueprintLibrary;
            _thumbnails = thumbnails;
            _placeSystem = placeSystem;
            _uiStateControl = uiStateControl;
            _routing = routing;
            _blockingSkit = blockingSkit;
            _vanillaApiEvent = vanillaApiEvent;
            _current = new ReactiveProperty<HotbarViewModel>(Build());
        }

        public void Initialize()
        {
            // 割当・設置対象（由来の枠）・UI 状態・画面・スキットの変化で組み直す
            // Rebuild on assignment, placement target (origin slot), UI state, screen and skit changes
            _datastore.OnAssignmentsChanged.Subscribe(_ => Rebuild());
            _placeSystem.OnTargetChanged.Subscribe(_ => Rebuild());
            _uiStateControl.OnStateChanged.Subscribe(_ => Rebuild());
            _routing.Current.Subscribe(_ => Rebuild());
            _blockingSkit.IsBlocking.Subscribe(_ => Rebuild());

            // 絵と名前の出どころ（BP の改名・サムネイルの到着・言語）の変化も組み直す。値が変われば ViewModel の等価が外れて描き直る
            // Picture and name source changes (blueprint rename, thumbnail arrival, language) also rebuild; a changed value breaks view-model equality and redraws
            _blueprintLibrary.OnChanged.Subscribe(_ => Rebuild());
            _thumbnails.OnThumbnailChanged.Subscribe(_ => Rebuild());
            Localize.OnLanguageChanged.Subscribe(_ => Rebuild());

            // 研究の解放で未解決の枠が解けるようになる。解放状態の持ち主（ClientGameUnlockStateData）は ctor で同じタグを先に購読しているので、ここで読む時点で更新済み（計画 D-18）
            // An unlock makes unresolved slots resolvable; the unlock-state owner subscribes to the same tag in its ctor first, so it is already updated when read here (plan D-18)
            _vanillaApiEvent.SubscribeEventResponse(UnlockedEventPacket.EventTag, _ => Rebuild());
            Rebuild();
        }

        // 値が同じなら ReactiveProperty が流さない。BP の絵だけは参照の等価なので、BP の割当があれば組み直すたびに流れる（撮り直しの描き直しに要る）
        // ReactiveProperty skips equal values; a blueprint icon has reference equality, so a slot with a blueprint pushes on every rebuild (needed to redraw a recapture)
        private void Rebuild()
        {
            _current.Value = Build();
        }

        private HotbarViewModel Build()
        {
            var slots = new List<HotbarSlotViewModel>(_datastore.Assignments.Count);
            foreach (var assignedId in _datastore.Assignments) slots.Add(ResolveSlot(assignedId));
            return HotbarViewModelFactory.Create(slots, _placeSystem.CurrentOrigin, _uiStateControl.CurrentState, _routing.Current.Value.Screen, _blockingSkit.IsBlocking.Value);

            #region Internal

            // 未割当だけが空き枠。割当があって解けない枠は外せる未解決として残す（Web HotbarTopic.ResolveSlotDto）
            // Only unassigned slots are empty; an assigned but unresolvable slot stays as a clearable unresolved one (Web HotbarTopic.ResolveSlotDto)
            HotbarSlotViewModel ResolveSlot(Guid assignedId)
            {
                if (assignedId == Guid.Empty) return HotbarEmptySlot.Instance;
                if (!_resolver.TryResolve(assignedId, out var target)) return new HotbarUnresolvedSlot(assignedId);

                // 表示名は今の言語の名（S2-mode-tooltip D-8）、絵の出どころはビルドメニューと同じ解き方
                // The display name is in the current language (S2-mode-tooltip D-8); the picture source resolves as in the build menu
                return new HotbarAssignedSlot(target.Id, target.DisplayName, PlacementTargetIconResolver.Resolve(target, _thumbnails));
            }

            #endregion
        }
    }
}
```
（ゲームは起動後メインメニューへ戻らないので購読の Dispose は持たない〈AGENTS.md の既知の制約〉。Model の配線は Task 12 の録画で見て、解放パケットの直後に枠が解決済みになること〈D-18 の順序〉は録画の 1 段で同期に確かめる〈D-17〉）

- [ ] **Step 2: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ErrorCount: 0`。ctor が `_current` を `Build()` で作るので、`Initialize` の最初の `Rebuild()` は BP の割当が無ければ同値で流れない（BP があれば 1 回多く描くだけ）。`Server.Event.EventReceive` が解けなければ `grep -rn "class UnlockedEventPacket" moorestech_server/Assets/Scripts` で namespace と asmdef を確かめ、`Client.Ui.State.asmdef` の参照（`Server.Event` は既存）と照らす。asmdef に足す要があれば足して `done.md` の「未確認」に 1 行書く

- [ ] **Step 3: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State/Hotbar
git commit -m "feat(uitk): ホットバーの Model を源の購読で組む（S3-hotbar-modal）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 4: 操作（`IHotbarCommands`・`IModalCommands`）と拒否理由、モーダルの確定の規則

**Files:**
- Create: `S/Client.Ui.State/Hotbar/IHotbarCommands.cs`・`HotbarCommands.cs`、`S/Client.Ui.State/Modal/IModalCommands.cs`・`ModalCommands.cs`
- Modify: `S/Client.Ui.State/Modal/Logic/ModalLogic.cs`・`S/Client.Ui.State/Blueprint/BlueprintNameInputModalBridge.cs`・`S/Client.Ui.State/Commands/UiCommandRejectReason.cs`（共有区画）・`Localization/localization.csv`（末尾 2 行）・`W/src/shared/i18n/generated/localizationKeys.ts`（再生成）・`moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`（印。生成物）・`S/Client.Tests/Ui/State/PauseMenuCommandsTest.cs`
- Test: `S/Client.Tests/Ui/State/Modal/ModalConfirmableTextTest.cs`・`ModalCommandsTest.cs`、変更 `S/Client.Tests/Ui/State/Modal/ModalLogicTest.cs`（`CanConfirm` の 2 件の本体）

**Interfaces:**
- Consumes: `HotbarSelectRule`、L0-s3 の `HotbarDropAction` 群・`ModalResponse`、`ClientHotbarDatastore`、`UIStateControl`、`ModalRequestService`、段階 1 の `UiCommandResult` 系
- Produces:
  ```csharp
  namespace Client.Ui.State.Hotbar
  {
      public interface IHotbarCommands { UiCommandResult Select(int slotIndex); void Apply(HotbarDropAction action); }
      public sealed class HotbarCommands : IHotbarCommands { public const string SelectCommandName = "hotbar.select"; public HotbarCommands(ClientHotbarDatastore datastore, UIStateControl uiStateControl); }
  }
  namespace Client.Ui.State.Modal
  {
      public interface IModalCommands { UiCommandResult Respond(ModalResponse response); }
      public sealed class ModalCommands : IModalCommands { public const string RespondCommandName = "ui.modal.respond"; public ModalCommands(ModalRequestService service); }
  }
  namespace Client.Ui.State.Modal.Logic { public static class ModalLogic { public static bool TryGetConfirmableText(ModalRequest request, string enteredText, out string confirmedText); public static bool HasConfirmableText(string enteredText); } } // 可否と確定の文を 1 回で導く（入力なしは常に真で文は null）。null は ""。空白だけは偽
  // UiCommandRejectReason に HotbarSelectNotAccepted（良性・wire invalid_state）・ModalResponseStale（良性・wire no_pending_modal）
  ```

- [ ] **Step 1: 失敗するテストを書く**

`S/Client.Tests/Ui/State/Modal/ModalConfirmableTextTest.cs`:
```csharp
using Client.Ui.State.Modal;
using Client.Ui.State.Modal.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Modal
{
    /// <summary>
    /// 確定できる文の規則と空白の除去を、モーダルの可否・確定の文・BP 名の書き戻しが共有する（L0）
    /// The confirmable-text rule and trimming shared by the modal's eligibility, the confirmed text and the blueprint-name write-back (L0)
    /// </summary>
    public class ModalConfirmableTextTest
    {
        [Test]
        public void 空白だけとnullは確定できず前後に空白のある名前は除いた形で確定できる()
        {
            Assert.IsFalse(ModalLogic.HasConfirmableText(null));
            Assert.IsFalse(ModalLogic.HasConfirmableText(""));
            Assert.IsFalse(ModalLogic.HasConfirmableText(" \t　"));
            Assert.IsTrue(ModalLogic.HasConfirmableText("  参照拠点  "));
        }

        [Test]
        public void 入力付きモーダルは可否と除いた文を1回で導き入力なしは常に確定できる()
        {
            var input = new ModalRequest { Id = "1", RequiresInput = true };
            var plain = new ModalRequest { Id = "2", RequiresInput = false };
            Assert.IsFalse(ModalLogic.TryGetConfirmableText(input, "   ", out _));
            Assert.IsFalse(ModalLogic.TryGetConfirmableText(input, null, out _));
            Assert.IsTrue(ModalLogic.TryGetConfirmableText(input, "  参照拠点  ", out var trimmed));
            Assert.AreEqual("参照拠点", trimmed);
            Assert.IsTrue(ModalLogic.TryGetConfirmableText(plain, "", out var none));
            Assert.IsNull(none);
        }
    }
}
```

`S/Client.Tests/Ui/State/Modal/ModalCommandsTest.cs`:
```csharp
using Client.Ui.State.Commands;
using Client.Ui.State.Modal;
using Client.Ui.State.Modal.Logic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Modal
{
    /// <summary>
    /// モーダルの応答の操作。保留への応答は受理、古い応答は良性の拒否（Web ui.modal.respond の no_pending_modal と同じ）
    /// The modal reply command: a reply to the pending request is accepted, a stale one is a benign rejection (same as Web no_pending_modal)
    /// </summary>
    public class ModalCommandsTest
    {
        [Test]
        public void 保留中の要求への確定は受理され要求が閉じる()
        {
            var service = new ModalRequestService();
            var result = service.RequestModal("確認", "本文", "OK", ModalVariant.Confirm);
            var commands = new ModalCommands(service);

            Assert.AreSame(UiCommandAccepted.Instance, commands.Respond(ModalResponse.Confirm(service.Pending, "")));
            Assert.AreEqual(UniTaskStatus.Succeeded, result.Status);
            Assert.IsNull(service.Pending);
        }

        [Test]
        public void 閉じた後の古い応答は良性の拒否になる()
        {
            var service = new ModalRequestService();
            service.RequestModal("確認", "本文", "OK", ModalVariant.Confirm).Forget();
            var request = service.Pending;
            var commands = new ModalCommands(service);
            commands.Respond(ModalResponse.Cancel(request));

            var rejected = (UiCommandRejected)commands.Respond(ModalResponse.Cancel(request));
            Assert.AreEqual(UiCommandRejectReason.ModalResponseStale, rejected.Reason);
            Assert.AreEqual("no_pending_modal", rejected.ToWireCode());
            Assert.IsTrue(UiCommandRejectReasonTraits.IsBenign(rejected.Reason));
        }
    }
}
```

`S/Client.Tests/Ui/State/PauseMenuCommandsTest.cs` の良性の一覧を固定するテスト（土台では「良性の拒否理由は言語の選択済みだけ」。他レーンが先に名前を変えていれば `grep -n "IsBenign" $S/Client.Tests/Ui/State/PauseMenuCommandsTest.cs` で実物を探す）を次に置き換える。名前は良性の値が増えても変えずに済む形にし、`benign` の配列は、その時点の `grep -n "=> true" $S/Client.Ui.State/Commands/UiCommandRejectReason.cs` の値の全部にこのレーンの 2 値を足して書く:
```csharp
        [Test]
        public void 良性の拒否理由は宣言した一覧と一致する()
        {
            // 状態を変えない再送・受理外・古い応答は利用者に知らせない（ログだけ）
            // Resends, non-accepting taps and stale replies change no state, so players are not told (log only)
            var benign = new[] { UiCommandRejectReason.LanguageAlreadySelected, UiCommandRejectReason.HotbarSelectNotAccepted, UiCommandRejectReason.ModalResponseStale };
            foreach (UiCommandRejectReason reason in System.Enum.GetValues(typeof(UiCommandRejectReason)))
            {
                Assert.AreEqual(System.Array.IndexOf(benign, reason) >= 0, UiCommandRejectReasonTraits.IsBenign(reason), reason.ToString());
            }
        }
```
（`benign` は、その時点の `UiCommandRejectReasonTraits.IsBenign` の `=> true` の腕の全部と一致させる。土台の取り込みのたびに `grep -n "=> true" $S/Client.Ui.State/Commands/UiCommandRejectReason.cs` を流し、別レーン〈S2-skit 等〉が足した良性の値も `benign` に採る。良性でない値は足さない）

Run: `uloop compile --project-path ./moorestech_client`
Expected: コンパイルエラー（`TryGetConfirmableText`・`ModalCommands`・2 つの拒否理由が無い）

- [ ] **Step 2: 拒否理由を共有区画に足す**

`S/Client.Ui.State/Commands/UiCommandRejectReason.cs` の 4 か所の末尾（enum の最後の値の次、各 switch の `_ =>` の直前）に 1 行ずつ足す:
```csharp
        HotbarSelectNotAccepted,
        ModalResponseStale,
```
```csharp
                UiCommandRejectReason.HotbarSelectNotAccepted => true,
                UiCommandRejectReason.ModalResponseStale => true,
```
```csharp
                UiCommandRejectReason.HotbarSelectNotAccepted => LocalizationKeys.Ui.CommandRejected.HotbarSelectNotAccepted,
                UiCommandRejectReason.ModalResponseStale => LocalizationKeys.Ui.CommandRejected.ModalResponseStale,
```
```csharp
                UiCommandRejectReason.HotbarSelectNotAccepted => "invalid_state",
                UiCommandRejectReason.ModalResponseStale => "no_pending_modal",
```
`IsBenign` の上のコメント「選択済み再選択のみ良性」を「状態を変えない再選択・受理外のタップ・古い応答は良性」（英語行も同じ意味）に直す。wire の 2 つは Web の `HotbarSelectActionHandler`・`ModalRespondActionHandler` の失敗コードと同じ。

`Localization/localization.csv` の `ui.commandRejected.*` の群の末尾（その時点の群の最後の行の次。S3-buildmenu など他のレーンが先に足していればその後ろ）に（良性なので表示されないが、`ToLocalizationKey` の switch が全値を要る）:
```csv
ui.commandRejected.hotbarSelectNotAccepted,The hotbar cannot be selected on this screen,The hotbar cannot be selected on this screen,この画面ではホットバーを選べません,Die Schnellleiste kann auf diesem Bildschirm nicht ausgewählt werden,이 화면에서는 단축바를 선택할 수 없습니다
ui.commandRejected.modalResponseStale,This dialog is already closed,This dialog is already closed,このダイアログはすでに閉じています,Dieser Dialog ist bereits geschlossen,이 대화 상자는 이미 닫혔습니다
```
Run:
```bash
pnpm --dir moorestech_web/webui run gen:i18n
uloop compile --project-path ./moorestech_client
git diff --stat -- moorestech_web/webui/src/shared/i18n/generated/ moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs
```
Expected: `localizationKeys.ts` に 2 キーが増え（`contentKeys.ts` は変わらない）、`_CompileRequester.cs` の印（Editor の SchemaWatcher が CSV の変化で書き換える）が変わる。`_CompileRequester.cs` が変わっていなければ `uloop compile` をもう 1 回流してから見直す（コンパイルエラー自体は Step 1 の赤のままでよい）。台帳 `docs/uitk/TODO.md` の「## 追従記録」の末尾に、W0-merge の `3e946e4bd3` と同じ書き方で `写した`（写し先＝同じコミットの C# と CSV）を 1 行書く（コミットのハッシュは Step 6 の後に埋める）

- [ ] **Step 3: 規則を 1 か所にし、ブリッジを委ねる**

`S/Client.Ui.State/Modal/Logic/ModalLogic.cs` の `CanConfirm` を消し（本番の呼び手は 0。`git grep -n "ModalLogic.CanConfirm" origin/feature/uitk` でテスト以外に無いことを確かめる。あれば `TryGetConfirmableText(request, text, out _)` へ書き換える）、次の 3 つを置く:
```csharp
        // 可否と確定の文を 1 回で導く。入力付きは空白だけを弾き前後の空白を除いた文、入力なしは文を運ばない（Web text.trim()・uGUI と同じ検証）
        // Derive eligibility and the confirmed text in one pass; input modals reject whitespace-only and carry the trimmed text, others carry none (Web text.trim(), same as uGUI)
        public static bool TryGetConfirmableText(ModalRequest request, string enteredText, out string confirmedText)
        {
            confirmedText = null;
            if (!request.RequiresInput) return true;
            var trimmed = Trim(enteredText);
            if (!IsConfirmable(trimmed)) return false;
            confirmedText = trimmed;
            return true;
        }

        // 空白だけでない文か。BP 名の書き戻しが同じ規則を読む
        // Whether the text is more than whitespace; the blueprint-name write-back reads the same rule
        public static bool HasConfirmableText(string enteredText)
        {
            return IsConfirmable(Trim(enteredText));
        }

        private static bool IsConfirmable(string trimmed)
        {
            return 0 < trimmed.Length;
        }

        private static string Trim(string enteredText)
        {
            return enteredText == null ? "" : enteredText.Trim();
        }
```

`S/Client.Tests/Ui/State/Modal/ModalLogicTest.cs` の `CanConfirm` を読む 2 件を、名前と `<web-test>` を変えずに本体だけ書き換える（`Request(id, requiresInput)` は同じファイルの既存の補助）:
```csharp
        /// <web-test file="src/features/modal/modalLogic.test.ts">非inputモーダルは常に確定可</web-test>
        [Test]
        public void 入力なしのモーダルは常に確定できる()
        {
            Assert.IsTrue(ModalLogic.TryGetConfirmableText(Request("m1", false), "", out _));
        }

        /// <web-test file="src/features/modal/modalLogic.test.ts">inputモーダルは空白のみを確定不可にする</web-test>
        [Test]
        public void 入力付きモーダルは空白だけの文では確定できない()
        {
            Assert.IsFalse(ModalLogic.TryGetConfirmableText(Request("m1", true), "   ", out _));
            Assert.IsTrue(ModalLogic.TryGetConfirmableText(Request("m1", true), " 家 ", out _));
        }
```
（`git grep -n "CanConfirm" -- moorestech_client` が 0 件になることを Step 5 の前に見る）

`S/Client.Ui.State/Blueprint/BlueprintNameInputModalBridge.cs` の確定の分岐を次に置き換え、`using Client.Ui.State.Modal.Logic;` を足す:
```csharp
                // 確定は空白のみを弾いて状態へ書き戻す（描き手の確定無効化と同じ規則 ModalLogic.HasConfirmableText の二重防御）
                // Confirm rejects whitespace-only before writing back (double guard with the renderer's disabled confirm, same rule ModalLogic.HasConfirmableText)
                if (result == ModalResult.Confirm && ModalLogic.HasConfirmableText(text))
                {
                    _state.Confirm(text);
                    return;
                }
```
（ブリッジ自身は `IsNullOrWhiteSpace` で弾くだけで、空白の除去は `BlueprintNameInputState.Confirm` の `Trim` にある。状態側の除去は Web の描き手から来た文にも掛かる整形なので残し、本PR外のリファクタ提案に書く）

- [ ] **Step 4: 操作を書く**

`S/Client.Ui.State/Hotbar/IHotbarCommands.cs`:
```csharp
using Client.Ui.State.Commands;
using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// ホットバーの画面から呼ぶ操作（Web の hotbar.select・assign・swap・clear）
    /// Operations the hotbar screen calls (Web hotbar.select, assign, swap, clear)
    /// </summary>
    public interface IHotbarCommands
    {
        UiCommandResult Select(int slotIndex);
        void Apply(HotbarDropAction action);
    }
}
```

`S/Client.Ui.State/Hotbar/HotbarCommands.cs`:
```csharp
using System;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Commands;
using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// ホットバーの操作の本番の実装。受理は HotbarSelectRule が決め、割当の変更は ClientHotbarDatastore へ積む
    /// Production hotbar operations; HotbarSelectRule decides acceptance and assignment changes go to ClientHotbarDatastore
    /// </summary>
    public sealed class HotbarCommands : IHotbarCommands
    {
        public const string SelectCommandName = "hotbar.select";

        private readonly ClientHotbarDatastore _datastore;
        private readonly UIStateControl _uiStateControl;

        public HotbarCommands(ClientHotbarDatastore datastore, UIStateControl uiStateControl)
        {
            _datastore = datastore;
            _uiStateControl = uiStateControl;
        }

        public UiCommandResult Select(int slotIndex)
        {
            // 受けない状態のタップは積まずに良性の拒否を返す（Web の invalid_state と同じ。画面は列の減光で示し、ログはトースト口が出す）
            // A tap in a non-accepting state is not queued and returns a benign rejection (Web invalid_state; the row dims and the toaster logs it)
            if (!HotbarSelectRule.Accepts(_uiStateControl.CurrentState)) return new UiCommandRejected(UiCommandRejectReason.HotbarSelectNotAccepted, "");
            _datastore.EnqueueSelectRequest(slotIndex);
            return UiCommandAccepted.Instance;
        }

        public void Apply(HotbarDropAction action)
        {
            switch (action)
            {
                case HotbarAssignAction assign:
                    _datastore.RequestAssign(assign.SlotIndex, assign.TargetId);
                    return;
                case HotbarSwapAction swap:
                    _datastore.RequestSwap(swap.FromSlotIndex, swap.ToSlotIndex);
                    return;
                case HotbarClearAction clear:
                    _datastore.RequestClear(clear.SlotIndex);
                    return;
                case HotbarNoDropAction:
                    // silent-ok: 自分自身への落とし・ビルドメニューの枠外への落としは Web と同じく何も変えない / L0-s3 の HotbarDropResolverTest が組を固定する
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "未知のホットバーの落としの結果");
            }
        }
    }
}
```

`S/Client.Ui.State/Modal/IModalCommands.cs`:
```csharp
using Client.Ui.State.Commands;
using Client.Ui.State.Modal.Logic;

namespace Client.Ui.State.Modal
{
    /// <summary>
    /// モーダルの応答の操作（Web の ui.modal.respond。設計書 A-8）
    /// The modal reply command (Web ui.modal.respond; design A-8)
    /// </summary>
    public interface IModalCommands
    {
        UiCommandResult Respond(ModalResponse response);
    }
}
```

`S/Client.Ui.State/Modal/ModalCommands.cs`:
```csharp
using Client.Ui.State.Commands;
using Client.Ui.State.Modal.Logic;

namespace Client.Ui.State.Modal
{
    /// <summary>
    /// モーダルの応答の本番の実装。状態の正 ModalRequestService へ渡す
    /// Production modal reply; hands the reply to ModalRequestService, the source of truth
    /// </summary>
    public sealed class ModalCommands : IModalCommands
    {
        public const string RespondCommandName = "ui.modal.respond";

        private readonly ModalRequestService _service;

        public ModalCommands(ModalRequestService service)
        {
            _service = service;
        }

        // ID 不一致・保留無しは古い応答。状態を変えないので良性の拒否（ログはトースト口）
        // An id mismatch or no pending request is a stale reply; it changes nothing, so it is a benign rejection (the toaster logs it)
        public UiCommandResult Respond(ModalResponse response)
        {
            if (_service.Respond(response.RequestId, response.Result, response.Text)) return UiCommandAccepted.Instance;
            return new UiCommandRejected(UiCommandRejectReason.ModalResponseStale, "");
        }
    }
}
```

- [ ] **Step 5: 流して緑にする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.State\.(Hotbar|Modal|PauseMenuCommandsTest|UiCommandResultToasterTest)"`
Expected: `ErrorCount: 0`、`ModalConfirmableTextTest` 2・`ModalCommandsTest` 2・`PauseMenuCommandsTest`（良性の一覧を含む）と既存の `ModalLogicTest`・`HotbarDropResolverTest`・`UiCommandResultToasterTest` が PASS。`localization.csv` のキー数を数えるテスト（在れば `Client.Tests.Localization`）も同じ回で回す

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Ui.State moorestech_client/Assets/Scripts/Client.Tests/Ui/State Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs
git commit -m "feat(uitk): ホットバーとモーダルの操作を UiCommandResult で返し、確定の規則を 1 本にする（S3-hotbar-modal）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```
台帳の追従記録の 1 行にこのコミットのハッシュを書き、`git add docs/uitk/TODO.md && git commit -m "docs(uitk): 拒否理由の文言の再生成を追従記録へ（S3-hotbar-modal）"`（末尾に Co-Authored-By の行）

---

### Task 5: ホットバーの前計算色と減光のトークン（`DerivedTokens.uss` の名札付きブロック）

**Files:**
- Modify: `THEME/DerivedTokens.uss`（`:root` の閉じ括弧の直前に名札付きブロック）
- Modify: `tools/uitk/webTokens/handWrittenDerivedRows.mjs`（`rowSources` の末尾 3 件）・`docs/uitk/translucent-tokens.md`（再生成）

**Interfaces:**
- Consumes: Web `W/src/features/hotbar/HotbarPanel/style.module.css`・`W/src/shared/ui/SlotFrame/style.module.css`、[A-52] の式（暗い色 `1-(1-α)^1.85`・明るい色 `α^2.2`）
- Produces: トークン `--hotbar-slot-frame-face`・`--hotbar-bevel-c1`・`--hotbar-bevel-c2`・`--hotbar-number-tab-face`・`--hotbar-number-tab-border`・`--hotbar-number-tab-text`・`--hotbar-select-disabled-opacity`・`--hotbar-unresolved-opacity`

- [ ] **Step 1: ブロックを足す**

`THEME/DerivedTokens.uss` の最後の `}` の直前に（前のブロックとの間に空行 1 つ）:
```css
    /* S3-hotbar-modal: 世界の上に直に置くホットバーの前計算色と減光。Web の HotbarPanel/style.module.css・SlotFrame の直書きを前計算した値（設計書 A-52） */
    /* S3-hotbar-modal: precomputed colors and dimming for the hotbar placed directly over the world; precomputes literals in the Web HotbarPanel/style.module.css and SlotFrame (design A-52) */
    --hotbar-slot-frame-face: rgba(50, 52, 67, 0.7226); /* web: SlotFrame .slot の rgb(50 52 67 / 50%)。地は世界なので暗い色の式（bd moorestech-albof.34。Task 10 で明るい地の画素を照合） */
    --hotbar-bevel-c1: rgb(125, 127, 133); /* web: .cell の --bevel-c1: rgb(125 127 133) */
    --hotbar-bevel-c2: rgb(116, 118, 125); /* web: .cell の --bevel-c2・--bevel-c3: rgb(116 118 125) */
    --hotbar-number-tab-face: rgba(50, 52, 67, 0.8785); /* web: .num の background rgb(50 52 67 / 68%)。暗い色の式 */
    --hotbar-number-tab-border: rgba(156, 158, 166, 0.2684); /* web: .num の border rgb(156 158 166 / 55%)。生成器の判定で明るい色の式 α^2.2（未実測。Task 11 で確かめ外れたら LX-77） */
    --hotbar-number-tab-text: rgb(226, 229, 238); /* web: .num の color #e2e5ee */
    --hotbar-select-disabled-opacity: 0.55; /* web: .hotbarFrame[data-select-disabled] の opacity 0.55。Task 10 の照合で決め直す */
    --hotbar-unresolved-opacity: 0.4; /* web: .cell[data-unresolved] の opacity 0.4。Task 10 の照合で決め直す */
```

- [ ] **Step 2: 前計算の表へ載せて再生成する**

`tools/uitk/webTokens/handWrittenDerivedRows.mjs` の `rowSources` の末尾（その時点の最後の行の次。S3-buildmenu など他のレーンが先に足していればその後ろ）に:
```js
  { name: "--hotbar-slot-frame-face", web: "rgb(50 52 67 / 50%)", formula: [50, 52, 67, 0.5], method: `${translucencyMethod}（ホットバーの空き枠の面。世界の地の上。S3-hotbar-modal・bd albof.34）` },
  { name: "--hotbar-number-tab-face", web: "rgb(50 52 67 / 68%)", formula: [50, 52, 67, 0.68], method: `${translucencyMethod}（ホットバーの番号タブの面。S3-hotbar-modal）` },
  { name: "--hotbar-number-tab-border", web: "rgb(156 158 166 / 55%)", formula: [156, 158, 166, 0.55], method: `${translucencyMethod}（ホットバーの番号タブの枠。S3-hotbar-modal）` },
```
Run:
```bash
node tools/uitk/convert-web-tokens.mjs
git diff --stat
grep -n "hotbar-" docs/uitk/translucent-tokens.md
```
Expected: 差分は `docs/uitk/translucent-tokens.md` に 3 行足されたことだけ（他の生成物〈`Tokens.uss`・`SlotTokens.uss`・`MantineTokens.uss`〉に差が出たら、生成器の入力が土台とずれているので `git checkout -- <そのファイル>` で戻して `done.md` の「integrator への依頼」に書く）。3 行の UITK の値が Step 1 の `0.7226`・`0.8785`・`0.2684` と一致する。違えば表（生成器の判定）を正として Step 1 の値を直す。Task 10 の照合で `--hotbar-slot-frame-face` を決め直したら、この行を `formula` から `uss: "<値>"` と決め方の文へ書き換えて再生成する（前例 `--slot-frame-face`）

- [ ] **Step 3: L2 を通してコミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Theme|Conventions)\."`
Expected: PASS（トークンの書式・`look-exception` の照合・tss の束ね）
```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens.uss tools/uitk/webTokens/handWrittenDerivedRows.mjs docs/uitk/translucent-tokens.md
git commit -m "feat(uitk): ホットバーの前計算色と減光のトークンを足す（S3-hotbar-modal）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---
### Task 6: ホットバーの画面内の部品（`Screens/Hotbar/Parts/`）

**Files:**
- Create: `S/Client.Ui/Screens/Hotbar/Parts/HotbarSlotDropMark.cs`・`HotbarRowGapDropMark.cs`・`HotbarSlotDragPayload.cs`・`HotbarCalcKnobs.cs`・`HotbarNumberTab.cs`・`HotbarCell.cs`、`S/Client.Ui/Foundation/Input/WheelPassthroughMark.cs`
- Modify: `S/Client.Ui/Components/Slots/OutlinedCountLabel/OutlinedCountLabel.uss`・`OutlinedCountLabel.cs`（コメントだけ）、`THEME/DerivedTokens.uss`（`--outlined-count-label-color` の次に 1 行）、`S/Client.Tests/Ui/Components/Slots/SlotKnobOverrides.uss`（末尾に 1 ブロック）
- Test: `S/Client.Tests/Ui/Components/Slots/CountLabel/OutlinedCountLabelStrokeKnobTest.cs`

**Interfaces:**
- Consumes: C3-b の `IUiDragPayload<in TTarget>`・`UiDragDrop`（`MarkSource<TTarget>`・`MarkTarget<TTarget>`）、C3-a の `SlotFrame`・`SlotFrameState`・`PlacementTargetFace`・`OutlinedCountLabel`、S3-buildmenu の `PlacementTargetIcons`（DI のクラス）・`IHotbarDropReceiver`、Task 2・4 の型、`UiCommandResultToaster`、`UssCustomStyleReader`
- Produces:
  ```csharp
  namespace Client.Ui.Screens.Hotbar.Parts
  {
      public sealed class HotbarSlotDropMark : IHotbarDropReceiver { public HotbarSlotDropMark(int slotIndex, IHotbarCommands commands); } // MarkTarget<IHotbarDropReceiver> で付く。Receive で解いて Apply
      public sealed class HotbarRowGapDropMark : IHotbarDropReceiver { public static readonly HotbarRowGapDropMark Instance; }   // MarkTarget<IHotbarDropReceiver> で付く。受けて無操作
      public sealed class HotbarSlotDragPayload : IUiDragPayload<IHotbarDropReceiver> { public HotbarSlotDragPayload(int slotIndex, HotbarSlotViewModel slot, IHotbarCommands commands, UiCommandResultToaster toaster); }
      public sealed class HotbarCalcKnobs { public const string RowOffsetProperty = "--hotbar-hud-row-offset"; public const string TabOverhangProperty = "--hotbar-hud-number-tab-overhang"; public HotbarCalcKnobs(VisualElement row); public void ApplyToRebuiltTabs(); } // スタイル解決の通知で読み、解決後に作り直したタブへは ApplyToRebuiltTabs で当てる
      public sealed class HotbarNumberTab : VisualElement { public const string ClassName = "hotbar-hud__number-tab"; public HotbarNumberTab(int slotIndex); }
      public sealed class HotbarCell : VisualElement { public HotbarCell(int slotIndex, IHotbarCommands commands, PlacementTargetIcons icons, UiCommandResultToaster toaster); public void Render(HotbarSlotViewModel slot, bool selected); }
  }
  namespace Client.Ui.Foundation.Input { public static class WheelPassthroughMark { public const string ClassName = "wheel-passthrough"; public static void Mark(VisualElement element); public static bool IsPassthrough(VisualElement target); } }
  ```

- [ ] **Step 1: 落とし先の印を書く**

`S/Client.Ui/Screens/Hotbar/Parts/HotbarSlotDropMark.cs`:
```csharp
using Client.Ui.State.Hotbar;
using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.Screens.Hotbar.Parts
{
    /// <summary>
    /// 枠の落とし先の印（Web の data-hotbar-slot-index）。受けた源を L0-s3 の規則で解いて操作へ流す
    /// Drop-target mark for a slot (the Web data-hotbar-slot-index); resolves the received source by the L0-s3 rule and sends the operation
    /// </summary>
    public sealed class HotbarSlotDropMark : IHotbarDropReceiver
    {
        private readonly IHotbarCommands _commands;

        private readonly int _slotIndex;

        public HotbarSlotDropMark(int slotIndex, IHotbarCommands commands)
        {
            _slotIndex = slotIndex;
            _commands = commands;
        }

        // 枠の落としは L0-s3 の規則が全組を解く（断る組は HotbarNoDropAction で、HotbarCommands が silent-ok で受ける）
        // Slot drops resolve every pair by the L0-s3 rule (refused pairs become HotbarNoDropAction, taken as silent-ok by HotbarCommands)
        public void Receive(HotbarDragSource source)
        {
            _commands.Apply(HotbarDropResolver.Resolve(source, new HotbarSlotDropTarget(_slotIndex)));
        }
    }
}
```

`S/Client.Ui/Screens/Hotbar/Parts/HotbarRowGapDropMark.cs`:
```csharp
using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.Screens.Hotbar.Parts
{
    /// <summary>
    /// 列の枠の間の隙間の印（Web の data-hotbar-row。ここで離すと受けて無操作）
    /// Mark for the gap between slots in the row (the Web data-hotbar-row; releasing here is received and does nothing)
    /// </summary>
    public sealed class HotbarRowGapDropMark : IHotbarDropReceiver
    {
        public static readonly HotbarRowGapDropMark Instance = new();

        private HotbarRowGapDropMark()
        {
        }

        // 受けて何もしない（まだ HUD の中なので源は枠外の解除をしない）
        // Receive and do nothing (still inside the HUD, so the source must not clear as outside)
        public void Receive(HotbarDragSource source)
        {
            // silent-ok: 枠の間の隙間で離すのは Web と同じく解除でなく無操作 / HotbarHudDropTest「枠の間の隙間で離すと何も送らない」が固定する
        }
    }
}
```

- [ ] **Step 2: 枠の源とホイール素通しの印を書く**

`S/Client.Ui/Screens/Hotbar/Parts/HotbarSlotDragPayload.cs`:
```csharp
using Client.Ui.Foundation.DragDrop;
using Client.Ui.State.Hotbar;
using Client.Ui.State.Hotbar.Logic;
using Client.Ui.State.Toast;

namespace Client.Ui.Screens.Hotbar.Parts
{
    /// <summary>
    /// 枠の源。タップは選択、ドロップは割当の入替・解除（Web HotbarCell の useHotbarDragSource）
    /// A slot as a source: a tap selects, a drop swaps or clears the assignment (Web HotbarCell useHotbarDragSource)
    /// </summary>
    public sealed class HotbarSlotDragPayload : IUiDragPayload<IHotbarDropReceiver>
    {
        private readonly int _slotIndex;
        private readonly HotbarSlotViewModel _slot;
        private readonly IHotbarCommands _commands;
        private readonly UiCommandResultToaster _toaster;

        public HotbarSlotDragPayload(int slotIndex, HotbarSlotViewModel slot, IHotbarCommands commands, UiCommandResultToaster toaster)
        {
            _slotIndex = slotIndex;
            _slot = slot;
            _commands = commands;
            _toaster = toaster;
        }

        // 受けるかは操作側（HotbarSelectRule）が決め、拒否はトースト口へ（良性なのでログだけ）。D&D は受けない画面でも成立する
        // The operation side (HotbarSelectRule) decides; a rejection goes to the toaster (benign, so log only). Drag-and-drop works even on non-accepting screens
        public void OnTapped()
        {
            _toaster.Report(HotbarCommands.SelectCommandName, _commands.Select(_slotIndex));
        }

        public void OnDropped(IHotbarDropReceiver receiver)
        {
            // silent-ok: 空き枠は掴む物が無い（Web の source === null） / HotbarSlotDragGestureTest「空き枠を引きずって…」が固定する
            if (!TryGetDragSource(out var source)) return;
            // ホットバーの印（枠・列の隙間）だけがここへ来て、入替か無操作かは受け手が解く。他の画面の印は Foundation が飛ばし OnDroppedOutside になる
            // Only hotbar marks (slot, row gap) arrive here and the receiver resolves swap or no-op; other screens' marks are skipped by Foundation into OnDroppedOutside
            receiver.Receive(source);
        }

        public void OnDroppedOutside()
        {
            // silent-ok: 空き枠は掴む物が無い（Web の source === null） / HotbarSlotDragGestureTest「空き枠を引きずって…」が固定する
            if (!TryGetDragSource(out var source)) return;
            ClearAsDroppedOutside(source);
        }

        // 掴める物があるかの判定はここ 1 か所。空き枠は掴む物が無い
        // The only check for something to carry; an empty slot carries nothing
        private bool TryGetDragSource(out HotbarSlotDragSource source)
        {
            source = new HotbarSlotDragSource(_slotIndex);
            // silent-ok: 空き枠はドラッグに確定した後も何もせずタップにも落とさない（Web useHotbarDragSource の source === null） / HotbarSlotDragGestureTest「空枠を閾値超えで…」が固定する
            return _slot.IsAssigned;
        }

        // 枠外（何も無い所・他の画面の印）は L0-s3 の規則で解除になる
        // Outside (empty space or another screen's mark) resolves to a clear by the L0-s3 rule
        private void ClearAsDroppedOutside(HotbarSlotDragSource source)
        {
            _commands.Apply(HotbarDropResolver.Resolve(source, OutsideHotbarDropTarget.Instance));
        }
    }
}
```

`S/Client.Ui/Foundation/Input/WheelPassthroughMark.cs`:
```csharp
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// 実 UI だがホイールだけはゲームへ素通す印と、その判定（Web の data-wheel-passthrough・isWheelPassthrough）。判定を使うのは装備のホイール（S4-inventory）
    /// Mark for real UI that still lets the wheel through to the game, and its check (the Web data-wheel-passthrough and isWheelPassthrough); the equipment wheel (S4-inventory) uses the check
    /// </summary>
    /// <remarks>
    /// Web で既に 2 機能（ホットバーの列・装備 HUD）が使うので Foundation に置く（設計書 §5.1-1。計画 D-21）
    /// Already used by two features on the Web (hotbar row, equipment HUD), so it lives in Foundation (design §5.1-1; plan D-21)
    /// </remarks>
    public static class WheelPassthroughMark
    {
        public const string ClassName = "wheel-passthrough";

        public static void Mark(VisualElement element)
        {
            element.AddToClassList(ClassName);
        }

        // 印の付いた祖先を持つか（Web の closest("[data-wheel-passthrough]")）
        // Whether a marked ancestor exists (the Web closest("[data-wheel-passthrough]"))
        public static bool IsPassthrough(VisualElement target)
        {
            for (var element = target; element != null; element = element.parent)
            {
                if (element.ClassListContains(ClassName)) return true;
            }
            return false;
        }
    }
}
```

- [ ] **Step 3: 部品 `OutlinedCountLabel` に縁の色のつまみを足す**

**先に分岐を見る**: 同じつまみは C4-a（Web の `FluidSlot` の暗い縁）も要り、先に入ったほうが足す（設計書 A-108・実行計画の C4-a 行）。まず `git grep -n "outlined-count-label-stroke" origin/feature/uitk -- moorestech_client/Assets/Scripts/Client.Ui` を流す。出たら（C4-a が先に足した）`OutlinedCountLabel.uss`・`.cs`・`DerivedTokens.uss` は書き換えず、下の USS・コメント・既定の行を飛ばし、`SlotKnobOverrides.uss` の `.slot-knob-dark-outline` と L1 `OutlinedCountLabelStrokeKnobTest` だけを足す（C4-a が同じ L1 を足していれば L1 も足さない。`git grep -n "OutlinedCountLabelStrokeKnobTest\|slot-knob-dark-outline" origin/feature/uitk` で見る）。つまみの名前・既定が下と違えば止めて指揮へ返す。出なければ以下のとおり足す

Web の `.iconTextOutlineDark`（`W/src/app/tokens.css:489`）は縁の色だけが `.iconTextOutlineLight` と違う。部品の縁の色は `--icon-text-stroke-light` の直書きなので、画面から暗い縁にする口が無い。部品につまみ `--outlined-count-label-stroke`（既定 `--icon-text-stroke-light`）を足し、番号タブはそのつまみだけを上書きする（設計書 §3.2「部品の見た目はつまみで変える」。共有トークン `--icon-text-stroke-light` を部分木で受け直すと、タブの中に後から置く他の部品の白い縁まで黒くなる）。C3-a はマージ済み。W3 で並走する C4-a（Web の `FluidSlot` も `.iconTextOutlineDark`。`W/src/shared/ui/FluidSlot/index.tsx:24,47`）も同じつまみを使い、先に入ったほうが足す（実行計画の C4-a 行に同じ形を書いた。S3-buildmenu の計画は `OutlinedCountLabel` を使わない）。

`S/Client.Ui/Components/Slots/OutlinedCountLabel/OutlinedCountLabel.uss` の `.outlined-count-label--stroke` を次に置き換える:
```css
.outlined-count-label--stroke {
    color: var(--outlined-count-label-stroke);
    -unity-text-outline-width: var(--icon-text-stroke-width);
    -unity-text-outline-color: var(--outlined-count-label-stroke);
}
```
同じファイルの冒頭の 2 行コメントの「大きさはつまみ --outlined-count-label-size で渡す」の後ろに日本語「、縁の色はつまみ --outlined-count-label-stroke（暗い縁 = Web .iconTextOutlineDark は置く側が --icon-text-stroke-dark を渡す）」、英語「; the outline color comes through the --outlined-count-label-stroke knob (the placing side passes --icon-text-stroke-dark for the Web .iconTextOutlineDark)」を足す。`OutlinedCountLabel.cs` の `<remarks>` の「字の色はつまみ --outlined-count-label-color」の文にも同じ趣旨で「縁の色はつまみ --outlined-count-label-stroke」を日英 1 句ずつ足す（コードは変えない）。

`THEME/DerivedTokens.uss` の `--outlined-count-label-color` の行の次に:
```css
    --outlined-count-label-stroke: var(--icon-text-stroke-light); /* web: .iconTextOutlineLight の縁の色。暗い縁（.iconTextOutlineDark）は置く側が --icon-text-stroke-dark で上書きする */
```

`S/Client.Tests/Ui/Components/Slots/SlotKnobOverrides.uss` の末尾に:
```css
/* W/app/tokens.css の .iconTextOutlineDark（ホットバーの番号タブ） */
/* .iconTextOutlineDark in W/app/tokens.css (the hotbar number tab) */
.slot-knob-dark-outline {
    --outlined-count-label-stroke: var(--icon-text-stroke-dark);
}
```

`S/Client.Tests/Ui/Components/Slots/CountLabel/OutlinedCountLabelStrokeKnobTest.cs`:
```csharp
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Slots.CountLabel
{
    /// <summary>
    /// OutlinedCountLabel の縁の色のつまみを既定と祖先の上書きで確かめる（L1）
    /// Verifies the OutlinedCountLabel outline-color knob with its default and an ancestor override (L1)
    /// </summary>
    public class OutlinedCountLabelStrokeKnobTest : UiTestHost
    {
        private const string KnobPath = "Assets/Scripts/Client.Tests/Ui/Components/Slots/SlotKnobOverrides.uss";

        [TearDown]
        public void NoUnexpectedLogs()
        {
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 縁の色は既定で白い縁のトークンになる()
        {
            var stroke = MountStroke(null);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--icon-text-stroke-light"), stroke.resolvedStyle.color, "stroke");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--icon-text-stroke-light"), stroke.resolvedStyle.unityTextOutlineColor, "outline");
        }

        [Test]
        public void 祖先で縁の色のつまみを上書きすると暗い縁になり字の色は変わらない()
        {
            var stroke = MountStroke("slot-knob-dark-outline");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--icon-text-stroke-dark"), stroke.resolvedStyle.color, "stroke");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--icon-text-stroke-dark"), stroke.resolvedStyle.unityTextOutlineColor, "outline");
            var glyph = stroke.parent.Q<Label>(className: "outlined-count-label--glyph");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--count-text"), glyph.resolvedStyle.color, "glyph");
        }

        private Label MountStroke(string knobClass)
        {
            // 置く側の要素に祖先のつまみを当て、2 層の数字を足す
            // Apply the ancestor knob to the placing element and add the two-layer number
            var screen = new VisualElement();
            if (knobClass != null)
            {
                var knobs = AssetDatabase.LoadAssetAtPath<StyleSheet>(KnobPath);
                Assert.IsNotNull(knobs, KnobPath);
                screen.styleSheets.Add(knobs);
                screen.AddToClassList(knobClass);
            }
            var label = new OutlinedCountLabel(screen, "slot-content__count");
            label.SetText(UiText.FromVerbatim("1"));
            rootVisualElement.Add(screen);
            simulate.FrameUpdate();
            return screen.Q<Label>(className: "outlined-count-label--stroke");
        }
    }
}
```
（置き場は `Slots/CountLabel/`: `Slots/` 直下は既に `.cs` が 10 本あり、11 本目を置くと 1 ディレクトリ 10 ファイルの規約を破る。`using Client.Tests.Ui.Components;`〈`ThemeTokenFile`〉が要るなら足す。`UiTestHost` が GameUiTheme を当てる前例は `SlotContentTest`。置き場のクラスは既存の `slot-content__count` を借りる。`UiText` の名前空間は `OutlinedCountLabel.cs` の `using Client.Ui.State.Text` に合わせる）

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Components\.Slots\."`
Expected: `ErrorCount: 0`、`OutlinedCountLabelStrokeKnobTest` 2 件と C3-a のスロット族の L1（`SlotContentTest`・`ItemSlotShortageTest` 等。既定の縁は白のまま）が PASS。赤のときの手順: 既定の件が赤 → `DerivedTokens.uss` の行が `:root` の中に入っているか。C3-a の既存の件が赤 → 置き換えた 2 行以外を触っていないか `git diff` で見る

- [ ] **Step 4: calc() のつまみと番号タブを書く**

`S/Client.Ui/Screens/Hotbar/Parts/HotbarCalcKnobs.cs`:
```csharp
using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Hotbar.Parts
{
    /// <summary>
    /// Web で calc()・transform を通る 2 つのつまみを、列のスタイル解決の通知で読んで当てる（設計書 A-47）
    /// Reads the two knobs that go through calc() and transform on the Web when the row's style resolves, and applies them (design A-47)
    /// </summary>
    /// <remarks>
    /// customStyle は CustomStyleResolvedEvent の後でしか読めない（UssCustomStyleReader の前提。未解決で読むと LogError）。読めた値を持ち、解決の前に作った子は解決の通知で当てる
    /// customStyle is readable only after CustomStyleResolvedEvent (the UssCustomStyleReader premise; reading early logs an error); keep the read values and apply them to children built before resolution when the notification arrives
    /// </remarks>
    public sealed class HotbarCalcKnobs
    {
        public const string RowOffsetProperty = "--hotbar-hud-row-offset";
        public const string TabOverhangProperty = "--hotbar-hud-number-tab-overhang";

        private readonly VisualElement _row;
        private bool _overhangResolved;
        private float _overhang;

        public HotbarCalcKnobs(VisualElement row)
        {
            _row = row;
            row.RegisterCallback<CustomStyleResolvedEvent>(_ => ReadAndApply());
        }

        // 子（番号タブ）を作り直した後に呼ぶ。解決の前なら、解決の通知の ReadAndApply が当てる
        // Call after rebuilding the children (number tabs); before resolution, ReadAndApply on the notification applies it
        public void ApplyToRebuiltTabs()
        {
            // silent-ok: スタイル解決の前に作ったタブは解決の通知で当たる / HotbarHudViewTest（SetUp が解決の前に Render する。未解決で読めば LogError で Unity のテストが落ちる）のタブの -16 の主張が見る
            if (!_overhangResolved) return;
            ApplyOverhang();
        }

        // 列の横ずれ（Web .hotbarFrame の translateX）と、タブの上へのはみ出し（Web .num の top: calc(overhang * -1)）
        // The row's horizontal shift (Web .hotbarFrame translateX) and the tab's upward overhang (Web .num top: calc(overhang * -1))
        private void ReadAndApply()
        {
            // 読めないつまみは UssCustomStyleReader が LogError を出す
            // An unreadable knob is logged as an error by UssCustomStyleReader
            if (UssCustomStyleReader.TryReadPixels(_row.customStyle, RowOffsetProperty, out var offset))
            {
                _row.style.translate = new Translate(new Length(offset), new Length(0f));
            }
            _overhangResolved = UssCustomStyleReader.TryReadPixels(_row.customStyle, TabOverhangProperty, out _overhang);
            if (_overhangResolved) ApplyOverhang();
        }

        private void ApplyOverhang()
        {
            _row.Query(className: HotbarNumberTab.ClassName).ForEach(tab => tab.style.top = -_overhang);
        }
    }
}
```
（つまみが読めないときの `_overhangResolved = false` は `TryReadPixels` が LogError を出し済みなので無音ではない）

`S/Client.Ui/Screens/Hotbar/Parts/HotbarNumberTab.cs`:
```csharp
using Client.Ui.Components.Slots;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Hotbar.Parts
{
    /// <summary>
    /// 枠の左上に重なる番号タブ（Web .num。暗い縁取りの 13px の数字）
    /// The number tab overlapping the slot's top-left (Web .num; a 13px digit with a dark outline)
    /// </summary>
    /// <remarks>
    /// 暗い縁はタブで部品のつまみ --outlined-count-label-stroke を上書きして出す（HotbarHud.uss。計画 D-7）。タブ自身は拾う（Web の pointer-events 既定。タブの上で離しても枠の落とし先になる）
    /// The dark outline comes from overriding the part knob --outlined-count-label-stroke on the tab (HotbarHud.uss; plan D-7). The tab itself is pickable (Web default pointer-events; releasing on it still hits the slot)
    /// </remarks>
    public sealed class HotbarNumberTab : VisualElement
    {
        public const string ClassName = "hotbar-hud__number-tab";

        public HotbarNumberTab(int slotIndex)
        {
            AddToClassList(ClassName);
            var number = new OutlinedCountLabel(this, "hotbar-hud__number");
            number.SetText(UiText.FromVerbatim((slotIndex + 1).ToString()));
        }
    }
}
```

- [ ] **Step 5: 1 枠を書く**

`S/Client.Ui/Screens/Hotbar/Parts/HotbarCell.cs`:
```csharp
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.DragDrop;
using Client.Ui.State.Hotbar;
using Client.Ui.State.Hotbar.Logic;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Hotbar.Parts
{
    /// <summary>
    /// ホットバーの 1 枠（番号タブ＋SlotFrame＋PlacementTargetFace）。Web HotbarCell
    /// One hotbar slot (number tab + SlotFrame + PlacementTargetFace); the Web HotbarCell
    /// </summary>
    public sealed class HotbarCell : VisualElement
    {
        private const string UnresolvedClass = "hotbar-hud__cell--unresolved";

        private readonly int _slotIndex;
        private readonly IHotbarCommands _commands;
        private readonly PlacementTargetIcons _icons;
        private readonly UiCommandResultToaster _toaster;
        private readonly SlotFrame _frame;
        private readonly PlacementTargetFace _face;

        public HotbarCell(int slotIndex, IHotbarCommands commands, PlacementTargetIcons icons, UiCommandResultToaster toaster)
        {
            _slotIndex = slotIndex;
            _commands = commands;
            _icons = icons;
            _toaster = toaster;
            pickingMode = PickingMode.Ignore;
            AddToClassList("hotbar-hud__cell");
            EnableInClassList("hotbar-hud__cell--first", slotIndex == 0);

            // 枠 → タブの順に足し、タブを枠の上に描く（Web .num の z-index 2）
            // Add the frame then the tab so the tab draws above the frame (Web .num z-index 2)
            _frame = new SlotFrame($"hotbar-slot-{slotIndex}");
            _face = new PlacementTargetFace();
            Add(_frame);
            Add(new HotbarNumberTab(slotIndex));

            // 落とし先の印は枠の箱に付ける（Web の data-hotbar-slot-index は .cell。枠もタブもここへ辿る）
            // The drop mark goes on the cell box (the Web data-hotbar-slot-index is on .cell; frame and tab both walk up to it)
            UiDragDrop.MarkTarget<IHotbarDropReceiver>(this, new HotbarSlotDropMark(slotIndex, commands));
        }

        public void Render(HotbarSlotViewModel slot, bool selected)
        {
            // 割当があれば白面（未解決も外せる割当として白面）、未解決は枠ごと減光
            // An assignment shows the white face (unresolved too, as a clearable assignment); unresolved dims the whole cell
            _frame.SetState(new SlotFrameState(selected, slot.IsAssigned, false, false, false));
            EnableInClassList(UnresolvedClass, slot is HotbarUnresolvedSlot);
            RenderFace();

            // 源の値を差し替える（2 度目の MarkSource は値だけを替える）
            // Replace the source value (a second MarkSource only swaps the payload)
            UiDragDrop.MarkSource(_frame, new HotbarSlotDragPayload(_slotIndex, slot, _commands, _toaster));

            #region Internal

            // 解けた対象だけが絵（無ければ名前）を持ち、未解決と空きは面だけ（Web slotBody）。表示名が空でも Web と同じくそのまま出す（計画 D-38）
            // Only a resolved target has a picture (or its name); unresolved and empty show just the face (Web slotBody). An empty name is shown as is, like the Web (plan D-38)
            void RenderFace()
            {
                if (slot is not HotbarAssignedSlot assigned)
                {
                    _face.RemoveFromHierarchy();
                    return;
                }

                // 絵になるかは Content が判別子で決め、面へ当てる分岐はこの枠が持つ（名前だけ・絵の欠けは名前。Web PlacementTargetFace の !iconUrl）
                // Content decides from the discriminator whether there is a picture; this cell owns the face branch (name-only or missing picture shows the name; Web !iconUrl)
                var name = UiText.FromVerbatim(assigned.DisplayLabel);
                if (_icons.TryToUiIcon(assigned.Icon, out var icon)) _face.SetIconTarget(icon, name);
                else _face.SetNameTarget(name);
                if (_face.parent == null) _frame.Add(_face);
            }

            #endregion
        }
    }
}
```

- [ ] **Step 6: コンパイルしてコミット**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ErrorCount: 0`（テストは Task 7 で View と一緒に書く）
```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Hotbar moorestech_client/Assets/Scripts/Client.Ui/Foundation/Input/WheelPassthroughMark.cs moorestech_client/Assets/Scripts/Client.Ui/Components/Slots/OutlinedCountLabel moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens.uss moorestech_client/Assets/Scripts/Client.Tests/Ui/Components/Slots/SlotKnobOverrides.uss moorestech_client/Assets/Scripts/Client.Tests/Ui/Components/Slots/CountLabel
git commit -m "feat(uitk): ホットバーの枠・番号タブ・落とし先の印を足す（S3-hotbar-modal）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 7: ホットバーの View・USS・Presenter と L1

**Files:**
- Create: `S/Client.Ui/Screens/Hotbar/HotbarHudView.cs`・`HotbarHud.uss`・`HotbarHudPresenter.cs`
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画の末尾に 1 行）
- Test: `S/Client.Tests/Ui/Screens/Hotbar/HotbarViewModelSamples.cs`・`RecordingHotbarCommands.cs`・`HotbarTestPresentation.cs`・`HotbarHudViewTest.cs`・`HotbarHudDropTest.cs`・`HotbarSlotDragGestureTest.cs`・`HotbarHudPickingTest.cs`・`HotbarIconCenteringTest.cs`
- Modify（在れば）: `S/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs`
- Modify: `docs/uitk/disposition.md`（albof.30 の「写す予定」の行を消す。Step 6）

**Interfaces:**
- Consumes: Task 2・4・5・6 の型とトークン、S3-buildmenu の `BuildMenuEntryDragPayload`・テストの `RecordingBuildMenuEntryHandler`、`GameUiRoot.GetLayer(UiLayer.Chrome, UiFamily.Viewport)`、`TutorialAnchor.Mark`、`ScreenTestHost`（`Mount`・`Find`・`IsShown`・`simulate`・`Toaster`）、`ThemeTokenFile`、`FakeContentTextureSource`
- Produces:
  ```csharp
  namespace Client.Ui.Screens.Hotbar
  {
      public sealed class HotbarHudView : VisualElement { public HotbarHudView(IHotbarCommands commands, PlacementTargetIcons icons, UiCommandResultToaster toaster); public void Render(HotbarViewModel viewModel); }
      public sealed class HotbarHudPresenter : IInitializable { public HotbarHudPresenter(GameUiRoot gameUiRoot, HotbarModel model, IHotbarCommands commands, PlacementTargetIcons icons, UiCommandResultToaster toaster); }
  }
  ```
  要素名: 帯 `hotbar-hud`・列 `hotbar-grid`・枠 `hotbar-slot-{i}`（Web の testid と同名）。USS のクラス: `hotbar-hud`・`__row`・`__row--select-disabled`・`__cell`・`__cell--first`・`__cell--unresolved`・`__number-tab`・`__number`

- [ ] **Step 1: テストの補助を書く**

`S/Client.Tests/Ui/Screens/Hotbar/RecordingHotbarCommands.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Hotbar;
using Client.Ui.State.Hotbar.Logic;

namespace Client.Tests.Ui.Screens.Hotbar
{
    /// <summary>
    /// 画面が呼んだ操作を文字列で記録する（Web e2e の payloadsOf にあたる）
    /// Records the operations the screen called as strings (the Web e2e payloadsOf)
    /// </summary>
    internal sealed class RecordingHotbarCommands : IHotbarCommands
    {
        public readonly List<string> Calls = new();

        public UiCommandResult Select(int slotIndex)
        {
            Calls.Add($"select:{slotIndex}");
            return UiCommandAccepted.Instance;
        }

        public void Apply(HotbarDropAction action)
        {
            Calls.Add(action switch
            {
                HotbarAssignAction assign => $"assign:{assign.SlotIndex}:{assign.TargetId:D}",
                HotbarSwapAction swap => $"swap:{swap.FromSlotIndex}:{swap.ToSlotIndex}",
                HotbarClearAction clear => $"clear:{clear.SlotIndex}",
                HotbarNoDropAction => "none",
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
            });
        }
    }
}
```

`S/Client.Tests/Ui/Screens/Hotbar/HotbarTestPresentation.cs`:
```csharp
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Tests.Ui.Content;
using Client.Ui.Content;
using Client.Ui.State.Hotbar;
using Client.Ui.State.PlacementTarget;
using UniRx;
using UnityEngine;

namespace Client.Tests.Ui.Screens.Hotbar
{
    /// <summary>
    /// テスト用の絵の解き手と、絵のある割当（撮影済みの BP。マスタに依らない）
    /// Icon resolver for tests and an assignment with a picture (a captured blueprint; independent of master data)
    /// </summary>
    internal static class HotbarTestPresentation
    {
        public static readonly Guid PicturedBlueprintId = new("5f0b9a8e-1c2d-4e3f-8a9b-0c1d2e3f4a5b");

        public static PlacementTargetIcons Create()
        {
            return Create(new HotbarTestThumbnails());
        }

        // ctor の引数の順は S3-buildmenu の実物に合わせる（Task 1 Step 1）。BP のサムネイルだけを偽で持つ
        // Match the ctor argument order to the S3-buildmenu original (Task 1 Step 1); only blueprint thumbnails are faked
        public static PlacementTargetIcons Create(HotbarTestThumbnails thumbnails)
        {
            var textures = new FakeContentTextureSource(null, null, null);
            return new PlacementTargetIcons(new GameContentPresentation(textures), textures, thumbnails);
        }

        // Web の fixture の枠 0（正方の赤い SVG の絵）にあたる割当。呼ぶたびに新しい絵の値（参照の等価）
        // The assignment standing in for the Web fixture's slot 0 (a red square SVG); a new icon value (reference equality) per call
        public static HotbarAssignedSlot PicturedBlueprint()
        {
            return new HotbarAssignedSlot(PicturedBlueprintId, "参照拠点", new PlacementTargetBlueprintIcon(PicturedBlueprintId));
        }
    }

    /// <summary>
    /// PicturedBlueprintId にだけ 256 角の単色を返すサムネイル。撮り直し（Recapture）で絵を替えられる
    /// Thumbnails returning a 256 square only for PicturedBlueprintId; Recapture swaps the picture
    /// </summary>
    internal sealed class HotbarTestThumbnails : IBlueprintThumbnailLookup
    {
        private readonly Subject<Guid> _onThumbnailChanged = new();

        public Texture2D Current { get; private set; } = CreateSquare(new Color32(204, 51, 51, 255));

        public IObservable<Guid> OnThumbnailChanged => _onThumbnailChanged;

        public bool Contains(Guid blueprintGuid)
        {
            return blueprintGuid == HotbarTestPresentation.PicturedBlueprintId;
        }

        public bool TryGet(Guid blueprintGuid, out Texture2D thumbnail)
        {
            thumbnail = Contains(blueprintGuid) ? Current : null;
            return thumbnail != null;
        }

        public void Recapture(Color32 color)
        {
            Current = CreateSquare(color);
            _onThumbnailChanged.OnNext(HotbarTestPresentation.PicturedBlueprintId);
        }

        // 正方なので枠の中央の主張（R-7）がそのまま効く
        // Square, so the centring claim (R-7) applies directly
        private static Texture2D CreateSquare(Color32 color)
        {
            var texture = new Texture2D(256, 256);
            var pixels = new Color32[256 * 256];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
```

`S/Client.Tests/Ui/Screens/Hotbar/HotbarViewModelSamples.cs`:
```csharp
using System;
using System.Linq;
using Client.Ui.State.Hotbar;

namespace Client.Tests.Ui.Screens.Hotbar
{
    /// <summary>
    /// Web の mock-host の fixture（枠 0 が選択で絵あり、枠 4 が未解決、他は空き）の ViewModel
    /// View models for the Web mock-host fixture (slot 0 selected with a picture, slot 4 unresolved, the rest empty)
    /// </summary>
    internal static class HotbarViewModelSamples
    {
        public static readonly Guid UnresolvedId = new("0d3c2b1a-9f8e-4d7c-b6a5-948372615041");

        public static HotbarViewModel WebFixture(bool selectAccepted, bool visible)
        {
            var slots = Enumerable.Repeat<HotbarSlotViewModel>(HotbarEmptySlot.Instance, 9).ToArray();
            slots[0] = HotbarTestPresentation.PicturedBlueprint();
            slots[4] = new HotbarUnresolvedSlot(UnresolvedId);
            return new HotbarViewModel(slots, HotbarSelection.Of(0), selectAccepted, visible);
        }

        public static HotbarViewModel WebFixture()
        {
            return WebFixture(true, true);
        }
    }
}
```

- [ ] **Step 2: 失敗するテストを書く（見た目・クリック・スキット）**

`S/Client.Tests/Ui/Screens/Hotbar/HotbarHudViewTest.cs`:
```csharp
using System.Globalization;
using Client.Tests.Ui.Components;
using Client.Ui.Components.Slots;
using Client.Ui.Screens.Hotbar;
using Client.Ui.Screens.Hotbar.Parts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Hotbar
{
    /// <summary>
    /// ホットバーの 9 枠・寸法・番号タブ・選択・減光・スキットで退くこと（L1）
    /// Hotbar nine slots, sizes, number tabs, selection, dimming and retreat during skits (L1)
    /// </summary>
    public class HotbarHudViewTest : ScreenTestHost
    {
        private RecordingHotbarCommands _commands;
        private HotbarHudView _view;

        [SetUp]
        public void MountHotbar()
        {
            _commands = new RecordingHotbarCommands();
            _view = Mount(new HotbarHudView(_commands, HotbarTestPresentation.Create(), Toaster));
            _view.Render(HotbarViewModelSamples.WebFixture());
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        /// <web-test file="e2e/tests/hotbar/hotbar.spec.ts">ホットバー9枠が常時表示され、fixtureのselectedSlotがハイライトされる</web-test>
        [Test]
        public void ホットバーは9枠を出し由来の枠だけを選択で光らせる()
        {
            Assert.IsTrue(IsShown(_view));
            Assert.IsNotNull(Find<VisualElement>(_view, "hotbar-grid"));
            for (var i = 0; i < 9; i++) Find<SlotFrame>(_view, $"hotbar-slot-{i}");
            Assert.IsTrue(Find<SlotFrame>(_view, "hotbar-slot-0").ClassListContains("slot-frame--selected"));
            Assert.IsFalse(Find<SlotFrame>(_view, "hotbar-slot-1").ClassListContains("slot-frame--selected"));
        }

        [Test]
        public void 枠は下端から2pxの中央に並び寸法と枠間と番号タブがWebの値になる()
        {
            // 51.68px の枠が 5px 間隔で並び、列は中央から 0.78px 右・下端から 2px
            // 51.68px slots 5px apart; the row sits 0.78px right of centre and 2px above the bottom
            var slot0 = Find<SlotFrame>(_view, "hotbar-slot-0");
            var slot1 = Find<SlotFrame>(_view, "hotbar-slot-1");
            var row = Find<VisualElement>(_view, "hotbar-grid");
            Assert.AreEqual(51.68f, slot0.worldBound.width, 0.5f);
            Assert.AreEqual(5f, slot1.worldBound.xMin - slot0.worldBound.xMax, 0.5f);
            Assert.AreEqual(720f - 2f, slot0.worldBound.yMax, 0.5f);
            Assert.AreEqual(640f + 0.782875f, row.worldBound.center.x, 0.5f);

            // 番号タブ: 枠の左から 17・上へ 16 はみ出し・高さ 15・暗い縁の数字
            // Number tab: 17 from the slot's left, 16 above, 15 tall, a dark-outlined digit
            var tab = slot0.parent.Q(className: HotbarNumberTab.ClassName);
            Assert.AreEqual(17f, tab.worldBound.xMin - slot0.worldBound.xMin, 0.5f);
            Assert.AreEqual(-16f, tab.worldBound.yMin - slot0.worldBound.yMin, 0.5f);
            Assert.AreEqual(15f, tab.worldBound.height, 0.5f);
            Assert.GreaterOrEqual(tab.worldBound.width, 15f - 0.5f);
            StringAssert.EndsWith("1", tab.Q<Label>(className: "outlined-count-label--glyph").text);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--hotbar-number-tab-face"), tab.resolvedStyle.backgroundColor, "tab face");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--icon-text-stroke-dark"), tab.Q<Label>(className: "outlined-count-label--stroke").resolvedStyle.color, "tab stroke");

            // 空き枠の面は世界の地向けの値（bd albof.34）、ベベルはホットバーの色
            // The empty face uses the over-the-world value (bd albof.34); the bevel uses the hotbar colors
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--hotbar-slot-frame-face"), slot1.resolvedStyle.backgroundColor, "empty face");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--hotbar-bevel-c1"), slot1.Q(className: "slot-frame__band--1").resolvedStyle.borderTopColor, "bevel c1");
        }

        /// <web-test file="e2e/tests/hotbar/hotbar.spec.ts">空き枠のクリックはhotbar.select{index}を送る</web-test>
        [Test]
        public void 空き枠のクリックはその枠の選択を送る()
        {
            var center = Find<SlotFrame>(_view, "hotbar-slot-3").worldBound.center;
            simulate.MouseDown(center, MouseButton.LeftMouse);
            simulate.MouseUp(center, MouseButton.LeftMouse);
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "select:3" }, _commands.Calls);
        }

        [Test]
        public void 選択を受けない画面では列が減光し受理は操作側が決める()
        {
            // Web .hotbarFrame[data-select-disabled] の opacity。タップは操作へ渡し、HotbarCommands が規則で捨てて理由を残す
            // Web .hotbarFrame[data-select-disabled] opacity; the tap reaches the operation and HotbarCommands drops it by the rule with a reason
            _view.Render(HotbarViewModelSamples.WebFixture(false, true));
            simulate.FrameUpdate();
            var row = Find<VisualElement>(_view, "hotbar-grid");
            Assert.IsTrue(row.ClassListContains("hotbar-hud__row--select-disabled"));
            Assert.AreEqual(float.Parse(ThemeTokenFile.Resolve("--hotbar-select-disabled-opacity"), CultureInfo.InvariantCulture), row.resolvedStyle.opacity, 0.001f);
        }

        /// <web-test file="e2e/tests/hotbar/hotbar.spec.ts">blockingスキット中はホットバーHUDだけが退き、会話窓は残る</web-test>
        [Test]
        public void blockingスキットの間は帯ごと退き終われば戻る()
        {
            // 会話窓が残ることは S2-skit が見る（計画 D-15）
            // That the dialogue window stays is checked by S2-skit (plan D-15)
            _view.Render(HotbarViewModelSamples.WebFixture(true, false));
            Assert.IsFalse(IsShown(_view));
            _view.Render(HotbarViewModelSamples.WebFixture());
            Assert.IsTrue(IsShown(_view));
        }
    }
}
```

- [ ] **Step 3: 失敗するテストを書く（落とし先・身振り・入力・中央）**

`S/Client.Tests/Ui/Screens/Hotbar/HotbarHudDropTest.cs`:
```csharp
using System;
using System.Globalization;
using Client.Tests.Ui.Components;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.DragDrop;
using Client.Ui.Screens.BuildMenu.Parts;
using Client.Ui.Screens.Hotbar;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Hotbar
{
    /// <summary>
    /// 落とし先の解き方（割当・解除・隙間）と未解決の枠（L1。Web hotbar.spec.ts の D&D）
    /// How drops resolve (assign, clear, gap) and the unresolved slot (L1; the Web hotbar.spec.ts drag-and-drop)
    /// </summary>
    public class HotbarHudDropTest : ScreenTestHost
    {
        private static readonly Guid EntryId = new("7a1e2d3c-4b5a-4968-8776-655443322110");

        private RecordingHotbarCommands _commands;
        private HotbarHudView _view;

        [SetUp]
        public void MountHotbar()
        {
            _commands = new RecordingHotbarCommands();
            _view = Mount(new HotbarHudView(_commands, HotbarTestPresentation.Create(), Toaster));
            _view.Render(HotbarViewModelSamples.WebFixture());
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        /// <web-test file="e2e/tests/hotbar/hotbar.spec.ts">ビルドメニューエントリを空き枠へドラッグするとhotbar.assign{slot,id}を送る</web-test>
        [Test]
        public void ビルドメニューの源を空き枠へ落とすと割当を送りタップは出さない()
        {
            // 画面の左上に本物のエントリの源を単独で置く（パネル越しの結線は S3-ladder。計画 D-16）
            // Put the real entry source alone at the top-left (wiring through the panel is S3-ladder; plan D-16)
            var entry = new VisualElement { name = "build-menu-entry-alone" };
            entry.style.position = Position.Absolute;
            entry.style.left = 10;
            entry.style.top = 10;
            entry.style.width = 60;
            entry.style.height = 60;
            rootVisualElement.Add(entry);
            var entryHandler = new RecordingBuildMenuEntryHandler();
            UiDragDrop.MarkSource(entry, new BuildMenuEntryDragPayload(EntryId, entryHandler));
            simulate.FrameUpdate();

            Drag(entry.worldBound.center, Slot(1).worldBound.center);
            CollectionAssert.AreEqual(new[] { $"assign:1:{EntryId:D}" }, _commands.Calls);
            CollectionAssert.IsEmpty(entryHandler.Selected);
        }

        /// <web-test file="e2e/tests/hotbar/hotbar.spec.ts">未解決の割当枠は使用不可表示になり、枠外ドラッグで外せる</web-test>
        [Test]
        public void 未解決の割当枠は白面のまま減光され枠外へ落とすと解除を送る()
        {
            var slot4 = Slot(4);
            Assert.IsTrue(slot4.parent.ClassListContains("hotbar-hud__cell--unresolved"));
            Assert.IsTrue(slot4.ClassListContains("slot-frame--filled"));
            Assert.IsNull(slot4.Q<PlacementTargetFace>());
            Assert.AreEqual(float.Parse(ThemeTokenFile.Resolve("--hotbar-unresolved-opacity"), CultureInfo.InvariantCulture), slot4.parent.resolvedStyle.opacity, 0.001f);

            Drag(slot4.worldBound.center, new Vector2(slot4.worldBound.center.x, 5f));
            CollectionAssert.AreEqual(new[] { "clear:4" }, _commands.Calls);
        }

        /// <web-test file="e2e/tests/hotbar/hotbar.spec.ts">枠外へドラッグするとhotbar.clear{slot}を送る</web-test>
        [Test]
        public void 割当済みの枠を画面の上端へ落とすと解除を送る()
        {
            var slot0 = Slot(0);
            Drag(slot0.worldBound.center, new Vector2(slot0.worldBound.center.x, 5f));
            CollectionAssert.AreEqual(new[] { "clear:0" }, _commands.Calls);
        }

        [Test]
        public void 枠の間の隙間で離すと何も送らない()
        {
            var gap = new Vector2(Slot(1).worldBound.xMax + 2.5f, Slot(1).worldBound.center.y);
            Drag(Slot(0).worldBound.center, gap);
            CollectionAssert.IsEmpty(_commands.Calls);
        }

        private SlotFrame Slot(int index)
        {
            return Find<SlotFrame>(_view, $"hotbar-slot-{index}");
        }

        private void Drag(Vector2 from, Vector2 to)
        {
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(Vector2.Lerp(from, to, 0.5f));
            simulate.MouseMove(to);
            simulate.MouseUp(to, MouseButton.LeftMouse);
            simulate.FrameUpdate();
        }
    }
}
```

`S/Client.Tests/Ui/Screens/Hotbar/HotbarSlotDragGestureTest.cs`:
```csharp
using System;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.DragDrop;
using Client.Ui.Screens.BuildMenu.Parts;
using Client.Ui.Screens.Hotbar;
using Client.Ui.Screens.Hotbar.Parts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Hotbar
{
    /// <summary>
    /// 枠とビルドメニューの源の押下をタップとドラッグに分ける（L1。Web useHotbarDragSource.test.ts）
    /// Splitting presses on slots and the build-menu source into taps and drags (L1; Web useHotbarDragSource.test.ts)
    /// </summary>
    public class HotbarSlotDragGestureTest : ScreenTestHost
    {
        private static readonly Guid EntryId = new("7a1e2d3c-4b5a-4968-8776-655443322110");

        private RecordingHotbarCommands _commands;
        private HotbarHudView _view;
        private VisualElement _entry;
        private RecordingBuildMenuEntryHandler _entryHandler;

        [SetUp]
        public void MountHotbarAndEntry()
        {
            _commands = new RecordingHotbarCommands();
            _view = Mount(new HotbarHudView(_commands, HotbarTestPresentation.Create(), Toaster));
            _view.Render(HotbarViewModelSamples.WebFixture());

            // 左上に本物のビルドメニューのエントリの源を単独で置く
            // The real build-menu entry source alone at the top-left
            _entry = new VisualElement { name = "build-menu-entry-alone" };
            _entry.style.position = Position.Absolute;
            _entry.style.left = 10;
            _entry.style.top = 10;
            _entry.style.width = 60;
            _entry.style.height = 60;
            rootVisualElement.Add(_entry);
            _entryHandler = new RecordingBuildMenuEntryHandler();
            UiDragDrop.MarkSource(_entry, new BuildMenuEntryDragPayload(EntryId, _entryHandler));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        /// <web-test file="src/features/hotbar/useHotbarDragSource.test.ts">閾値未満で離すとタップとしてonTapが1回呼ばれる(ビルドメニュー/ホットバー枠どちらのクリック選択も壊れない)</web-test>
        [Test]
        public void 閾値未満で離すと枠もエントリも1回のタップになる()
        {
            var slot2 = Slot(2).worldBound.center;
            Press(slot2, slot2 + new Vector2(3f, 0f));
            Press(_entry.worldBound.center, _entry.worldBound.center + new Vector2(3f, 0f));
            CollectionAssert.AreEqual(new[] { "select:2" }, _commands.Calls);
            CollectionAssert.AreEqual(new[] { EntryId }, _entryHandler.Selected);
        }

        /// <web-test file="src/features/hotbar/useHotbarDragSource.test.ts">閾値を超えるとドラッグへ確定しonTapは発火しない</web-test>
        [Test]
        public void 閾値を超えるとドラッグになり自分の枠で離しても選択しない()
        {
            var slot0 = Slot(0).worldBound.center;
            Press(slot0, slot0 + new Vector2(20f, 0f));
            CollectionAssert.AreEqual(new[] { "none" }, _commands.Calls);
        }

        /// <web-test file="src/features/hotbar/useHotbarDragSource.test.ts">枠外(elementFromPointが枠を返さない)へドラッグして離すとhotbar.clearをdispatchする</web-test>
        [Test]
        public void 枠の無い所へ落とすと解除する()
        {
            Press(Slot(0).worldBound.center, new Vector2(640f, 100f));
            CollectionAssert.AreEqual(new[] { "clear:0" }, _commands.Calls);
        }

        /// <web-test file="src/features/hotbar/useHotbarDragSource.test.ts">data-hotbar-slot-index付きの枠へドラッグして離すとhotbar.swapをdispatchする</web-test>
        [Test]
        public void 別の枠や番号タブの上へ落とすと入れ替える()
        {
            Press(Slot(0).worldBound.center, Slot(2).worldBound.center);
            var tab = Slot(6).parent.Q(className: HotbarNumberTab.ClassName);
            Press(Slot(0).worldBound.center, tab.worldBound.center);
            CollectionAssert.AreEqual(new[] { "swap:0:2", "swap:0:6" }, _commands.Calls);
        }

        /// <web-test file="src/features/hotbar/useHotbarDragSource.test.ts">ビルドメニューエントリを枠へドラッグして離すとhotbar.assignをdispatchする</web-test>
        [Test]
        public void エントリを枠へ落とすと割当てる()
        {
            Press(_entry.worldBound.center, Slot(5).worldBound.center);
            CollectionAssert.AreEqual(new[] { $"assign:5:{EntryId:D}" }, _commands.Calls);
            CollectionAssert.IsEmpty(_entryHandler.Selected);
        }

        /// <web-test file="src/features/hotbar/useHotbarDragSource.test.ts">空枠を閾値超えで引きずって離してもタップにならない（建築モードを抜けさせない）</web-test>
        [Test]
        public void 空き枠を引きずって離しても選択も解除もしない()
        {
            Press(Slot(1).worldBound.center, new Vector2(640f, 100f));
            CollectionAssert.IsEmpty(_commands.Calls);
        }

        /// <web-test file="src/features/hotbar/useHotbarDragSource.test.ts">右ボタン押下は無視し、preventDefaultもpointer捕捉も行わない（右クリック削除の温存）</web-test>
        [Test]
        public void 右ボタンの押下は捕捉せず何も送らない()
        {
            var slot0 = Slot(0);
            simulate.MouseDown(slot0.worldBound.center, MouseButton.RightMouse);
            Assert.IsFalse(slot0.HasPointerCapture(PointerId.mousePointerId));
            simulate.MouseUp(slot0.worldBound.center, MouseButton.RightMouse);
            simulate.FrameUpdate();
            CollectionAssert.IsEmpty(_commands.Calls);
        }

        private SlotFrame Slot(int index)
        {
            return Find<SlotFrame>(_view, $"hotbar-slot-{index}");
        }

        private void Press(Vector2 from, Vector2 to)
        {
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(Vector2.Lerp(from, to, 0.5f));
            simulate.MouseMove(to);
            simulate.MouseUp(to, MouseButton.LeftMouse);
            simulate.FrameUpdate();
        }
    }
}
```

`S/Client.Tests/Ui/Screens/Hotbar/HotbarHudPickingTest.cs`:
```csharp
using Client.Ui.Foundation.Input;
using Client.Ui.Screens.Hotbar;
using Client.Ui.Screens.Hotbar.Parts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Hotbar
{
    /// <summary>
    /// 帯は入力を奪わず、列だけが拾ってホイール素通しの印を持つ（L1。Web alwaysOnHudWheel.test.ts・bd albof.30）
    /// The band never takes input; only the row picks and carries the wheel pass-through mark (L1; Web alwaysOnHudWheel.test.ts, bd albof.30)
    /// </summary>
    public class HotbarHudPickingTest : ScreenTestHost
    {
        private HotbarHudView _view;

        [SetUp]
        public void MountHotbar()
        {
            _view = Mount(new HotbarHudView(new RecordingHotbarCommands(), HotbarTestPresentation.Create(), Toaster));
            _view.Render(HotbarViewModelSamples.WebFixture());
            simulate.FrameUpdate();
        }

        /// <web-test file="src/features/commonHud/alwaysOnHudWheel.test.ts">%s は入力を奪わない</web-test>
        [Test]
        public void ホットバー帯は入力を奪わない()
        {
            // 帯は画面幅いっぱいだが、列の外の何も無い所は画面の下へ素通す
            // The band spans the screen width, yet empty space outside the row passes through
            Assert.AreEqual(PickingMode.Ignore, _view.pickingMode);
            var row = Find<VisualElement>(_view, "hotbar-grid");
            var picked = _view.panel.Pick(new Vector2(row.worldBound.xMin - 40f, row.worldBound.center.y));
            Assert.IsFalse(picked == _view || (picked != null && _view.Contains(picked)), picked == null ? "null" : picked.name);
        }

        /// <web-test file="src/features/commonHud/alwaysOnHudWheel.test.ts">クリックを持つスロット列だけが入力を取り戻し、ホイールは素通しの印を持つ</web-test>
        [Test]
        public void クリックを持つスロット列だけが入力を取り戻しホイール素通しの印を持つ()
        {
            var row = Find<VisualElement>(_view, "hotbar-grid");
            Assert.AreEqual(PickingMode.Position, row.pickingMode);
            Assert.IsTrue(row.ClassListContains(WheelPassthroughMark.ClassName));
            Assert.IsFalse(_view.ClassListContains(WheelPassthroughMark.ClassName));

            // 読み手の判定: 枠の上（列の子孫）は素通し、帯そのものは素通しでない（Web isWheelPassthrough の closest）
            // The reader's check: over a slot (a row descendant) passes through, the band itself does not (the Web isWheelPassthrough closest)
            Assert.IsTrue(WheelPassthroughMark.IsPassthrough(Find<VisualElement>(_view, "hotbar-slot-1")));
            Assert.IsFalse(WheelPassthroughMark.IsPassthrough(_view));
            Assert.IsFalse(WheelPassthroughMark.IsPassthrough(null));

            // 枠の隙間も列が拾う（Web の .hotbarFrame は pointer-events: auto）
            // The row also picks the gaps between slots (the Web .hotbarFrame is pointer-events: auto)
            var gap = new Vector2(Find<VisualElement>(_view, "hotbar-slot-1").worldBound.xMax + 2.5f, row.worldBound.center.y);
            Assert.AreEqual(row, _view.panel.Pick(gap));
        }
    }
}
```

`S/Client.Tests/Ui/Screens/Hotbar/HotbarIconCenteringTest.cs`:
```csharp
using Client.Ui.Components.Slots;
using Client.Ui.Screens.Hotbar;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Hotbar
{
    /// <summary>
    /// 配置対象の絵が枠の中央に描かれる（L1。Web placementIconCentering.spec.ts のホットバー）
    /// The placement picture is drawn centred in the slot (L1; the hotbar case of Web placementIconCentering.spec.ts)
    /// </summary>
    public class HotbarIconCenteringTest : ScreenTestHost
    {
        /// <web-test file="e2e/tests/regression/placementIconCentering.spec.ts">ホットバーのアイコンは枠の中央に描かれる</web-test>
        [Test]
        public void ホットバーのアイコンは枠の中央に描かれる()
        {
            var view = Mount(new HotbarHudView(new RecordingHotbarCommands(), HotbarTestPresentation.Create(), Toaster));
            view.Render(HotbarViewModelSamples.WebFixture());
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            // 描く面は絵の要素の内容箱（padding を除いた箱）。正方の絵はそこへ収まる
            // The drawn area is the icon element's content box (padding removed); a square picture fills it
            var slot = Find<SlotFrame>(view, "hotbar-slot-0");
            var icon = slot.Q(className: "placement-target-face__icon");
            Assert.IsNotNull(icon);
            var drawn = icon.contentRect;
            var drawnWorld = icon.LocalToWorld(drawn);
            var left = drawnWorld.xMin - slot.worldBound.xMin;
            var right = slot.worldBound.xMax - drawnWorld.xMax;
            var top = drawnWorld.yMin - slot.worldBound.yMin;
            var bottom = slot.worldBound.yMax - drawnWorld.yMax;
            Assert.Greater(left, 0f);
            Assert.Less(Mathf.Abs(left - right), 0.5f);
            Assert.Less(Mathf.Abs(top - bottom), 0.5f);
            Assert.Less(Mathf.Abs(left - top), 0.5f);
        }

        [Test]
        public void 同じBPの撮り直しで枠の絵が替わる()
        {
            // BP の絵は参照の等価なので、Model が組み直した VM は撮り直しのたびに配られる。ここでは配られた VM で絵が替わることを見る（計画 D-19）
            // A blueprint icon has reference equality, so the model's rebuilt VM is pushed on each recapture; here we check the pushed VM swaps the picture (plan D-19)
            var thumbnails = new HotbarTestThumbnails();
            var view = Mount(new HotbarHudView(new RecordingHotbarCommands(), HotbarTestPresentation.Create(thumbnails), Toaster));
            view.Render(HotbarViewModelSamples.WebFixture());
            simulate.FrameUpdate();
            var first = thumbnails.Current;
            Assert.AreSame(first, IconTexture(view));

            thumbnails.Recapture(new Color32(51, 153, 204, 255));
            view.Render(HotbarViewModelSamples.WebFixture());
            simulate.FrameUpdate();
            Assert.AreNotSame(first, IconTexture(view));
            Assert.AreSame(thumbnails.Current, IconTexture(view));
        }

        private Texture2D IconTexture(HotbarHudView view)
        {
            return Find<SlotFrame>(view, "hotbar-slot-0").Q(className: "placement-target-face__icon").resolvedStyle.backgroundImage.texture;
        }
    }
}
```
（`WebFixture()` は呼ぶたびに `PicturedBlueprint()` で新しい絵の値を作る。絵の要素が `backgroundImage` でなく子の `Image` で描くなら、C3-a の `GameIcon` の実物の読み方に合わせる）

Run: `uloop compile --project-path ./moorestech_client`
Expected: コンパイルエラー（`HotbarHudView` が無い）

- [ ] **Step 4: View・USS・Presenter を書く**

`S/Client.Ui/Screens/Hotbar/HotbarHudView.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Content;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.DragDrop;
using Client.Ui.Foundation.Input;
using Client.Ui.Screens.Hotbar.Parts;
using Client.Ui.State.Hotbar;
using Client.Ui.State.Hotbar.Logic;
using Client.Ui.State.Toast;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Hotbar
{
    /// <summary>
    /// ホットバーの帯・列・9 枠（Web features/hotbar/HotbarPanel）。Model を知らず Render で描く
    /// The hotbar band, row and nine slots (Web features/hotbar/HotbarPanel); knows no model and draws via Render
    /// </summary>
    public sealed class HotbarHudView : VisualElement
    {
        private const string SelectDisabledClass = "hotbar-hud__row--select-disabled";

        private readonly IHotbarCommands _commands;
        private readonly PlacementTargetIcons _icons;
        private readonly UiCommandResultToaster _toaster;
        private readonly VisualElement _row;
        private readonly HotbarCalcKnobs _calcKnobs;
        private readonly List<HotbarCell> _cells = new();

        public HotbarHudView(IHotbarCommands commands, PlacementTargetIcons icons, UiCommandResultToaster toaster)
        {
            _commands = commands;
            _icons = icons;
            _toaster = toaster;

            // 帯は画面幅の空箱なので拾わず、実 UI の列だけが拾う（Web .hotbarArea の pointer-events: none）
            // The band is an empty full-width box and does not pick; only the real-UI row does (Web .hotbarArea pointer-events: none)
            name = "hotbar-hud";
            pickingMode = PickingMode.Ignore;
            AddToClassList("hotbar-hud");

            // 列: ホイール素通し・隙間の落とし先・チュートリアルの錨・calc() のつまみ
            // Row: wheel pass-through, gap drop target, tutorial anchor and calc() knobs
            _row = new VisualElement { name = "hotbar-grid" };
            _row.AddToClassList("hotbar-hud__row");
            WheelPassthroughMark.Mark(_row);
            UiDragDrop.MarkTarget<IHotbarDropReceiver>(_row, HotbarRowGapDropMark.Instance);
            TutorialAnchor.Mark(_row, AnchorIds.HotbarHud);
            _calcKnobs = new HotbarCalcKnobs(_row);
            Add(_row);
        }

        public void Render(HotbarViewModel viewModel)
        {
            // 退く間は帯ごと隠す（Web は null を返す。計画 D-10）
            // Hide the whole band while retreating (the Web returns null; plan D-10)
            style.display = viewModel.Visible ? DisplayStyle.Flex : DisplayStyle.None;
            _row.EnableInClassList(SelectDisabledClass, !viewModel.SelectAccepted);

            if (_cells.Count != viewModel.Slots.Count) RebuildCells(viewModel.Slots.Count);
            for (var i = 0; i < _cells.Count; i++) _cells[i].Render(viewModel.Slots[i], viewModel.Selection.IsSlot(i));
        }

        // 枠の数は割当の長さに合わせる（通常は起動時に 1 回）。作り直したらタブのはみ出しを当て直す
        // Match the cell count to the assignment length (normally once at boot); re-apply the tab overhang after rebuilding
        private void RebuildCells(int count)
        {
            _row.Clear();
            _cells.Clear();
            for (var i = 0; i < count; i++)
            {
                var cell = new HotbarCell(i, _commands, _icons, _toaster);
                _cells.Add(cell);
                _row.Add(cell);
            }
            _calcKnobs.ApplyToRebuiltTabs();
        }
    }
}
```

`S/Client.Ui/Screens/Hotbar/HotbarHud.uss`:
```css
/* web: moorestech_web/webui/src/features/hotbar/HotbarPanel/style.module.css の .hotbarArea・.hotbarFrame・.cell・.num・data-unresolved・data-select-disabled。calc() と transform の 2 つ（.num の top・.hotbarFrame の translateX）は HotbarCalcKnobs が当てる（設計書 A-47）。gap は UITK に無いので 2 枠目以降の左余白で写す */
/* web: .hotbarArea, .hotbarFrame, .cell, .num, data-unresolved and data-select-disabled in moorestech_web/webui/src/features/hotbar/HotbarPanel/style.module.css; the calc() and transform pair (.num top, .hotbarFrame translateX) is applied by HotbarCalcKnobs (design A-47); UITK has no gap, so cells after the first take a left margin */
.hotbar-hud {
    position: absolute;
    left: 0;
    right: 0;
    bottom: var(--hotbar-bottom);
    flex-direction: row;
    justify-content: center;
    align-items: flex-end;
    /* 世界の上に直に置くので、空き枠の面を世界の地向けの値に上書きする（bd moorestech-albof.34・台帳 LX-23） */
    /* Placed directly over the world, so the empty face is overridden with the over-the-world value (bd moorestech-albof.34, ledger LX-23) */
    --slot-frame-face: var(--hotbar-slot-frame-face);
}

.hotbar-hud__row {
    flex-direction: row;
    --hotbar-hud-row-offset: var(--hotbar-horizontal-offset);
    --hotbar-hud-number-tab-overhang: var(--hotbar-number-tab-overhang);
}

.hotbar-hud__row--select-disabled {
    opacity: var(--hotbar-select-disabled-opacity);
}

/* Web .cell の局所上書き変数を同名で上書きする（設計書 §3.3-3） */
/* Override the Web .cell local variables under the same names (design §3.3-3) */
.hotbar-hud__cell {
    margin-left: var(--hotbar-slot-gap);
    --slot-size: var(--hotbar-slot-size);
    --selected-face-bottom-trim: 0.78px;
    --selected-face-ring-inset: 1.565749px;
    --icon-pad: 5px;
    --count-bottom: -1px;
    --bevel-inset: inset;
    --face-inset: 0px;
    --bevel-1: 0.782875px;
    --bevel-2: 1.565749px;
    --bevel-3: 1.565749px;
    --bevel-c1: var(--hotbar-bevel-c1);
    --bevel-c2: var(--hotbar-bevel-c2);
    --bevel-c3: var(--hotbar-bevel-c2);
    --sel-c1: var(--select-cyan);
    --sel-c2: var(--select-cyan);
    --sel-c3: var(--select-cyan);
}

.hotbar-hud__cell--first {
    margin-left: 0;
}

.hotbar-hud__cell--unresolved {
    opacity: var(--hotbar-unresolved-opacity);
}

/* 番号タブ: 角張った 15px 角・細い明るい枠・半透明の面。縁取りは暗い色（Web .iconTextOutlineDark。部品の縁の色のつまみで渡す。計画 D-7） */
/* Number tab: an angular 15px square with a thin light border and translucent face; the outline is dark (Web .iconTextOutlineDark; passed through the part's outline-color knob; plan D-7) */
.hotbar-hud__number-tab {
    position: absolute;
    left: 17px;
    min-width: 15px;
    height: 15px;
    padding-left: 1px;
    padding-right: 1px;
    border-width: 1px;
    border-radius: 0;
    border-color: var(--hotbar-number-tab-border);
    background-color: var(--hotbar-number-tab-face);
    --outlined-count-label-stroke: var(--icon-text-stroke-dark);
    --outlined-count-label-size: 13px;
    --outlined-count-label-color: var(--hotbar-number-tab-text);
}

/* 字の 2 層をタブの内側いっぱいに重ね、中央に置く（Web の text-align: center・line-height 13px）。中央寄せは行の箱を中央に置くので基線は Web の line-height:1 と同じ 0.112em に来る。部品の translate 18.1% の下げは要らない（Task 11 Step 3 の数字の縦位置で確かめる） */
/* Stack both glyph layers across the tab's inside and centre them (Web text-align: center, line-height 13px); centring the line box puts the baseline at 0.112em like the Web line-height:1, so the component's 18.1% translate is not needed (checked by the digit position in Task 11 Step 3) */
.hotbar-hud__number-tab .hotbar-hud__number {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    translate: 0 0;
    -unity-text-align: middle-center;
}
```

`S/Client.Ui/Screens/Hotbar/HotbarHudPresenter.cs`:
```csharp
using Client.Ui.Content;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.Hotbar;
using Client.Ui.State.Toast;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Hotbar
{
    /// <summary>
    /// ホットバーを起動時に 1 回 Chrome 層（Viewport の族。Web の .viewportOverlay）へ載せる
    /// Mounts the hotbar once into the Chrome layer (Viewport family; the Web .viewportOverlay)
    /// </summary>
    public sealed class HotbarHudPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly HotbarModel _model;
        private readonly IHotbarCommands _commands;
        private readonly PlacementTargetIcons _icons;
        private readonly UiCommandResultToaster _toaster;

        public HotbarHudPresenter(GameUiRoot gameUiRoot, HotbarModel model, IHotbarCommands commands, PlacementTargetIcons icons, UiCommandResultToaster toaster)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
            _commands = commands;
            _icons = icons;
            _toaster = toaster;
        }

        public void Initialize()
        {
            // 載せるのは 1 回だけ。購読は Presenter が持ち、今の値で 1 回描く（前例 ModeHudPresenter）
            // Mount once; the presenter owns the subscription, which draws once with the current value (precedent ModeHudPresenter)
            var view = new HotbarHudView(_commands, _icons, _toaster);
            _gameUiRoot.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(view);
            _model.Current.Subscribe(view.Render);
        }
    }
}
```

`moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss` の `/* == section: Screens == */` の区画の末尾に:
```css
@import url("project://database/Assets/Scripts/Client.Ui/Screens/Hotbar/HotbarHud.uss");
```

- [ ] **Step 5: 流して緑にする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Screens\.(Hotbar|BuildMenu)|Conventions|Theme|Layers)\."`
Expected: `ErrorCount: 0`、`HotbarHudViewTest` 5・`HotbarHudDropTest` 4・`HotbarSlotDragGestureTest` 7・`HotbarHudPickingTest` 2・`HotbarIconCenteringTest` 2 が PASS、L2 も PASS。赤のときの手順:
- 番号タブの `-16` が外れる → `HotbarCalcKnobs` の `CustomStyleResolvedEvent` が列に届いていない。`Debug.Log` で読めた値を出して原因を確かめる（テストの期待は変えない）
- `slot1.resolvedStyle.backgroundColor` が `--hotbar-slot-frame-face` でない → `.hotbar-hud` の上書きが `SlotFrame.uss` の `var()` へ継承していない。`--slot-frame-face` を `.hotbar-hud__cell` へ移して試し、移した理由を `done.md` の「未確認」に書く
- 番号タブの縁が黒くならない → タブの `--outlined-count-label-stroke` が効いていない。Task 6 Step 3 の部品の USS の置き換え（`color` と `-unity-text-outline-color` の両方がつまみを読む）と、`HotbarHud.uss` のタブの行を見る
- 絵の中央が 0.5px を超えてずれる → `--icon-pad` と `--bevel-inset: inset` の組み合わせを確かめ、Web の正方 SVG と同じ条件（256 角）であることを確かめてから USS を直す
- 右ボタンで捕捉される → C3-b の `UiPressGesture` の誤り。止めて `done.md` の「integrator への依頼」に書く（Foundation はこのレーンの持ち物でない）

- [ ] **Step 6: 錨の未移植の表を整え、コミット**

Run:
```bash
F=moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor/TutorialAnchorCoveragePending.cs
test -f $F && grep -n "AnchorIds.HotbarHud" $F || echo "no-pending-row"
```
行が出たらその 1 行を消し、`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.Anchor\."` を回して PASS を見る（印済みの錨が未移植の表に残ると `TutorialAnchorCoverageTest` が赤になる）。`no-pending-row` ならそのまま

`HotbarHudPickingTest` が `alwaysOnHudWheel.test.ts`「クリックを持つスロット列だけが入力を取り戻し、ホイールは素通しの印を持つ」に `<web-test>` を付けたので、処遇の表に残る同じテストの「写す予定」の行（albof.30）を同じコミットで消す。タグと処遇の行が並ぶと突き合わせが整合の誤りで落ちる（`compareBundles.mjs` の「`<web-test>` で写したテストに処遇の行もある」）:
```bash
D=docs/uitk/disposition.md
grep -n "alwaysOnHudWheel.test.ts\` | クリックを持つスロット列だけが入力を取り戻し、ホイールは素通しの印を持つ | 写す予定" $D
python3 - "$D" <<'PY'
import sys
path = sys.argv[1]
lines = open(path, encoding="utf-8").read().split("\n")
key = "| `src/features/commonHud/alwaysOnHudWheel.test.ts` | クリックを持つスロット列だけが入力を取り戻し、ホイールは素通しの印を持つ | 写す予定 |"
kept = [line for line in lines if key not in line]
if len(lines) - len(kept) != 1:
    raise SystemExit(f"expected exactly one albof.30 row, removed {len(lines) - len(kept)}")
open(path, "w", encoding="utf-8").write("\n".join(kept))
PY
node tools/uitk/test-name-parity.mjs --bundle 2; echo "exit=$?"
```
Expected: `grep` が 1 行（消す前）、python が例外なく終わり、`exit=` が 0 か 1（2 は書式・整合の誤り。出たら出力の行番号の行を直す）。出力に `disposition` の行番号を含む誤りが無い。albof.31（装備 HUD）の行は S4-inventory の持ち物なので残す
```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Hotbar moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Hotbar moorestech_client/Assets/Scripts/Client.Tests/Ui/Anchor "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss" docs/uitk/disposition.md
git commit -m "feat(uitk): ホットバーの View・USS・Presenter と L1 を足す（S3-hotbar-modal）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---
### Task 8: モーダルの View・Presenter と L1

**Files:**
- Create: `S/Client.Ui/Screens/Modal/ModalView.cs`・`ModalButtonVariants.cs`・`Modal.uss`・`ModalScreenPresenter.cs`
- Modify: `moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss`（`Screens` 区画の末尾に 1 行）
- Test: `S/Client.Tests/Ui/Screens/Modal/ModalRequestSamples.cs`・`RecordingModalCommands.cs`・`ModalViewTest.cs`・`ModalScreenPresenterTest.cs`

**Interfaces:**
- Consumes: C3-b の `DialogFrame`・`UiModalHost`・`StageScaleSource`、C2-b の `FormButton`・`GameTextField`、`LocalizedLabel`・`UiTypographyClass`、L0-s3 の `ModalRequest`・`ModalRequestService`・`ModalLogic`・`ModalResponse`・`ModalButtonTone`、Task 4 の `IModalCommands`・`ModalCommands`、`UiCommandResultToaster`、`GameUiInputRules`・`UiInputExclusivity`
- Produces:
  ```csharp
  namespace Client.Ui.Screens.Modal
  {
      public sealed class ModalView : VisualElement { public ModalView(ModalRequest request, IModalCommands commands, UiCommandResultToaster toaster, IObservable<Unit> backdropClicked); } // 要求 1 件につき 1 つ（Web の key={id}）。確定・ESC（1 回）・幕の押下を自分で Respond へ（A-8）
      public static class ModalButtonVariants { public static FormButtonVariant From(ModalButtonTone tone); }                 // Blue→Filled・Red→Danger（[A-28]）
      public sealed class ModalScreenPresenter : IInitializable { public ModalScreenPresenter(UiModalHost host, ModalRequestService service, IModalCommands commands, UiCommandResultToaster toaster); }
  }
  ```
  要素名（Web の testid と同名）: 枠 `modal`・題 `modal-title`（DialogFrame が付ける）・本文 `modal-message`・入力 `modal-input`（`GameTextField.Field`）・ボタン `modal-button`・幕 `modal-backdrop`（`UiModalHost`）。USS のクラス: `modal-view`・`__message`・`__input`

- [ ] **Step 0: フォーカスの無い ESC を既存のゲーム入力の経路が取り消すことを確かめる**

ESC は Web の Mantine と同じ 1 回で取り消す（D-11）。経路は 2 つで、View が聞くのは 1 つ目だけ:
1. 入力欄にフォーカスがある ESC: `GameUiInputRules` が ESC を消費して（`UiInputExclusivity.ConsumeEscapeThisFrame`）フォーカスを外す。View は入力欄の `FocusOutEvent` を、このフレームの ESC が消費済みのときだけ取消として読む。消費の印は `InputSuppressionFilter` が読むので、同じフレームに `PlaceBlockState` の `InputManager.UI.CloseUI` は立たない（建築モードを抜けない・ポーズが開かない）。変換中の ESC は規則がフォーカスを外さないので閉じない（albof.12）
2. フォーカスの無い ESC（欄の外を押した後）: UITK のキーイベントは GameUi が入力欄以外にフォーカスを持たせないので届かない（設計書 A-83 の理由と同じ）。代わりに既存のゲーム入力の経路が 1 回で取り消す: `PlaceBlockState` が `InputManager.UI.CloseUI.GetKeyDown` を読む → `PlaceSystemStateController.TryCancelInProgressOperation` → `BlueprintCopySystem` の `AwaitingName` の腕が `BlueprintNameInputState.Close` → `BlueprintNameInputModalBridge` が `ModalRequestService.CancelPendingRequest` → 保留が消えて Presenter が `Hide`。View は根でキーを聞かない（聞くと入力の第 2 経路になる。A-83）

Run:
```bash
S=moorestech_client/Assets/Scripts
grep -n "CloseUI.GetKeyDown\|TryCancelInProgressOperation" $S/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs
grep -n "AwaitingName" -A3 $S/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy/BlueprintCopySystem.cs
grep -n "CancelPendingRequest" $S/Client.Ui.State/Blueprint/BlueprintNameInputModalBridge.cs
grep -rn "\.RequestModal(\|\.RequestInputModal(" $S | grep -v "/Client.Tests/"
```
Expected: 1〜3 行目が上の経路どおりに出る。4 行目は `BlueprintNameInputModalBridge.cs` の `RequestInputModal` 1 行だけ（本番でモーダルを出すのは建築モード中の BP 名だけ）。4 行目に別の呼び手（建築モードの外で出す確認モーダル等）が増えていたら、その画面のフォーカスの無い ESC を受ける経路が無いので、`done.md` の「要裁定」に「<呼び手> のモーダルのフォーカスの無い ESC の経路」と書いて先へ進む（View に根の聞き取りを足さない）。1 回で閉じ、ポーズが開かず、建築モードを抜けないことは Task 12 の録画の流れ 7b で実入力で見る

- [ ] **Step 1: 要求の見本を書く**

`S/Client.Tests/Ui/Screens/Modal/ModalRequestSamples.cs`:
```csharp
using Client.Ui.State.Modal;

namespace Client.Tests.Ui.Screens.Modal
{
    /// <summary>
    /// Web の mock-host の fixture（modalSample・blueprintNameModal）と同じ要求
    /// Requests matching the Web mock-host fixtures (modalSample, blueprintNameModal)
    /// </summary>
    internal static class ModalRequestSamples
    {
        public static ModalRequest Confirm()
        {
            return new ModalRequest { Id = "m1", Title = "確認", Message = "これは確認ダイアログです", ButtonText = "OK", Variant = ModalVariant.Confirm, RequiresInput = false };
        }

        public static ModalRequest BlueprintName()
        {
            return new ModalRequest { Id = "blueprint-name", Title = "ブループリント名", Message = "ブループリントの名前を入力してください", ButtonText = "保存", Variant = ModalVariant.Confirm, RequiresInput = true };
        }

        public static ModalRequest Error()
        {
            return new ModalRequest { Id = "e1", Title = "エラー", Message = "保存できませんでした", ButtonText = "閉じる", Variant = ModalVariant.Error, RequiresInput = false };
        }
    }
}
```

`S/Client.Tests/Ui/Screens/Modal/RecordingModalCommands.cs`:
```csharp
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Modal;
using Client.Ui.State.Modal.Logic;

namespace Client.Tests.Ui.Screens.Modal
{
    /// <summary>
    /// View が返した応答を記録する偽（受理を返す）
    /// Records the replies the view returned (always accepts)
    /// </summary>
    internal sealed class RecordingModalCommands : IModalCommands
    {
        public readonly List<ModalResponse> Responses = new();

        public UiCommandResult Respond(ModalResponse response)
        {
            Responses.Add(response);
            return UiCommandAccepted.Instance;
        }
    }
}
```

- [ ] **Step 2: 失敗するテストを書く（View）**

`S/Client.Tests/Ui/Screens/Modal/ModalViewTest.cs`:
```csharp
using System;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.Modal;
using Client.Ui.State.Modal;
using Client.Ui.State.Modal.Logic;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Modal
{
    /// <summary>
    /// モーダルの中身（題・本文・入力・ボタン）と確定の可否・空白の除去・色（L1。Web modal.spec.ts）
    /// Modal contents (title, message, input, button), confirm eligibility, trimming and colour (L1; Web modal.spec.ts)
    /// </summary>
    public class ModalViewTest : ScreenTestHost
    {
        /// <web-test file="e2e/tests/modal.spec.ts">接続後にモーダルが描画される</web-test>
        [Test]
        public void 確認の要求は題と本文とボタンを描き入力欄を持たない()
        {
            var view = Mount(new ModalView(ModalRequestSamples.Confirm(), new RecordingModalCommands(), Toaster, Observable.Never<Unit>()));
            simulate.FrameUpdate();
            Assert.IsTrue(IsShown(Find<VisualElement>(view, "modal")));
            StringAssert.EndsWith("確認", Find<Label>(view, "modal-title").text);
            StringAssert.EndsWith("これは確認ダイアログです", Find<Label>(view, "modal-message").text);
            var button = Find<FormButton>(view, "modal-button");
            StringAssert.EndsWith("OK", button.Q<Label>(className: FormButton.LabelUssClassName).text);
            Assert.IsTrue(button.enabledSelf);
            Assert.IsNull(view.Q<TextField>("modal-input"));
        }

        /// <web-test file="e2e/tests/modal.spec.ts">BP名入力fixtureは入力後の確定値を送る</web-test>
        [Test]
        public void BP名の入力は空のうち押せず前後の空白を除いた名前で確定する()
        {
            var commands = new RecordingModalCommands();
            var view = Mount(new ModalView(ModalRequestSamples.BlueprintName(), commands, Toaster, Observable.Never<Unit>()));
            var responses = commands.Responses;
            simulate.FrameUpdate();

            var input = Find<TextField>(view, "modal-input");
            var button = Find<FormButton>(view, "modal-button");
            Assert.IsTrue(IsShown(input));
            Assert.IsFalse(button.enabledSelf);

            input.value = "  参照拠点  ";
            simulate.FrameUpdate();
            Assert.IsTrue(button.enabledSelf);
            simulate.Click(button.worldBound.center);

            Assert.AreEqual(1, responses.Count);
            Assert.AreEqual("blueprint-name", responses[0].RequestId);
            Assert.AreEqual(ModalResult.Confirm, responses[0].Result);
            Assert.AreEqual("参照拠点", responses[0].Text);
        }

        [Test]
        public void 空白だけの入力では押せず押しても確定を流さない()
        {
            var commands = new RecordingModalCommands();
            var view = Mount(new ModalView(ModalRequestSamples.BlueprintName(), commands, Toaster, Observable.Never<Unit>()));
            var responses = commands.Responses;
            simulate.FrameUpdate();

            Find<TextField>(view, "modal-input").value = " \t　";
            simulate.FrameUpdate();
            var button = Find<FormButton>(view, "modal-button");
            Assert.IsFalse(button.enabledSelf);
            simulate.Click(button.worldBound.center);
            CollectionAssert.IsEmpty(responses);
        }

        [Test]
        public void 入力付きの要求は表示の後に入力欄へフォーカスが入る()
        {
            // Web の TextInput autoFocus。パネルに載った次のフレームで当てる（計画 D-13）
            // Web TextInput autoFocus; applied on the frame after attaching to the panel (plan D-13)
            var view = Mount(new ModalView(ModalRequestSamples.BlueprintName(), new RecordingModalCommands(), Toaster, Observable.Never<Unit>()));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var input = Find<TextField>(view, "modal-input");
            var focused = view.focusController.focusedElement as VisualElement;
            Assert.IsTrue(focused == input || (focused != null && input.Contains(focused)), focused == null ? "null" : focused.name);
        }

        [Test]
        public void エラーは危険色で確認は塗りのボタンになる()
        {
            // 色の写しは ModalButtonVariants の 1 か所（設計書 A-28）
            // The colour mapping lives only in ModalButtonVariants (design A-28)
            Assert.AreEqual(FormButtonVariant.Filled, ModalButtonVariants.From(ModalButtonTone.Blue));
            Assert.AreEqual(FormButtonVariant.Danger, ModalButtonVariants.From(ModalButtonTone.Red));
            var error = Mount(new ModalView(ModalRequestSamples.Error(), new RecordingModalCommands(), Toaster, Observable.Never<Unit>()));
            simulate.FrameUpdate();
            Assert.IsTrue(Find<FormButton>(error, "modal-button").ClassListContains(FormButton.DangerUssClassName));
        }
    }
}
```

- [ ] **Step 3: 失敗するテストを書く（Presenter と宿主）**

`S/Client.Tests/Ui/Screens/Modal/ModalScreenPresenterTest.cs`:
```csharp
using Client.Input;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Modal;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Screens.Modal;
using Client.Ui.State.Modal;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Modal
{
    /// <summary>
    /// 保留の要求を宿主へ出し、確定・幕の取消を Respond へ返して消す。倍率 1.5 でも実画面 px（L1。Web modal.spec.ts・設計書 A-41）
    /// Shows the pending request in the host, returns confirm and backdrop cancel to Respond and hides; real-screen px at scale 1.5 (L1; Web modal.spec.ts, design A-41)
    /// </summary>
    public class ModalScreenPresenterTest : ScreenTestHost
    {
        private const float StageScale = 1.5f;
        private const string ShownClass = "ui-modal-host--shown";

        private VisualElement _layer;
        private ModalRequestService _service;

        [SetUp]
        public void MountPresenter()
        {
            _layer = new VisualElement { name = "modal-test-layer" };
            _layer.style.position = Position.Absolute;
            _layer.style.left = 0;
            _layer.style.top = 0;
            _layer.style.right = 0;
            _layer.style.bottom = 0;
            rootVisualElement.Add(_layer);

            // 入力の規則は本番と同じく層の根に付ける（前例 GameTextFieldTest）
            // Attach the input rules at the layer root as in production (precedent GameTextFieldTest)
            GameUiInputRules.Attach(_layer);
            var host = new UiModalHost(new StageScaleSource(new ReactiveProperty<float>(StageScale)));
            host.Mount(_layer);
            _service = new ModalRequestService();
            new ModalScreenPresenter(host, _service, new ModalCommands(_service), Toaster).Initialize();
            simulate.FrameUpdate();
        }

        [TearDown]
        public void ResetInputLatches()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.ClearEscapeLatchForTest();
        }

        /// <web-test file="e2e/tests/modal.spec.ts">OK クリックで confirm を送りモーダルが消える</web-test>
        [Test]
        public void OKを押すと確定を返してモーダルが消える()
        {
            var result = _service.RequestModal("確認", "これは確認ダイアログです", "OK", ModalVariant.Confirm);
            simulate.FrameUpdate();
            Assert.IsTrue(Shown());

            simulate.Click(Find<FormButton>(_layer, "modal-button").worldBound.center);
            simulate.FrameUpdate();
            Assert.AreEqual(UniTaskStatus.Succeeded, result.Status);
            Assert.AreEqual(ModalResult.Confirm, result.GetAwaiter().GetResult());
            Assert.IsNull(_service.Pending);
            Assert.IsFalse(Shown());
        }

        /// <web-test file="e2e/tests/modal.spec.ts">背景クリックで cancel を送る</web-test>
        [Test]
        public void 幕の左上を押すと取消を返す()
        {
            var (_, response) = _service.RequestInputModal("ブループリント名", "ブループリントの名前を入力してください", "保存");
            simulate.FrameUpdate();
            simulate.Click(new Vector2(_layer.worldBound.xMin + 5f, _layer.worldBound.yMin + 5f));
            simulate.FrameUpdate();
            Assert.AreEqual(UniTaskStatus.Succeeded, response.Status);
            var (result, text) = response.GetAwaiter().GetResult();
            Assert.AreEqual(ModalResult.Cancel, result);
            Assert.IsNull(text);
            Assert.IsFalse(Shown());
        }

        [Test]
        public void 倍率が掛かっても枠は実画面440pxで中央に出る()
        {
            // 設計書 A-41: モーダルは Portal で倍率を掛けない。テストのパネルは倍率 1 なので舞台の倍率を掛けて実画面 px を見る
            // Design A-41: the modal is a Portal without scaling; the test panel has scale 1, so multiply by the stage scale for real-screen px
            _service.RequestModal("確認", "これは確認ダイアログです", "OK", ModalVariant.Confirm).Forget();
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var frame = Find<VisualElement>(_layer, "modal");
            Assert.AreEqual(440f, frame.worldBound.width * StageScale, 0.5f);
            Assert.AreEqual(_layer.worldBound.center.x, frame.worldBound.center.x, 0.5f);
            Assert.AreEqual(_layer.worldBound.center.y, frame.worldBound.center.y, 0.5f);
        }

        [Test]
        public void 新しい要求は前の入力を持ち越さない()
        {
            // Web は要求 ID を key にして入力をリセットする。UITK は要求ごとに View を作り直す（計画 D-13）
            // The Web resets input by keying on the request id; UITK rebuilds the view per request (plan D-13)
            _service.RequestInputModal("ブループリント名", "ブループリントの名前を入力してください", "保存");
            simulate.FrameUpdate();
            Find<TextField>(_layer, "modal-input").value = "前の名前";
            _service.RequestInputModal("ブループリント名", "ブループリントの名前を入力してください", "保存");
            simulate.FrameUpdate();
            Assert.AreEqual("", Find<TextField>(_layer, "modal-input").value);
            Assert.IsFalse(Find<FormButton>(_layer, "modal-button").enabledSelf);
        }

        [Test]
        public void 入力欄のESCは1回で取り消しワールド側へESCを渡さない()
        {
            // Web の Mantine と同じく ESC 1 回で閉じる。GameUiInputRules がフォーカスを外して消費し、その外れで View が取り消す（計画 D-11）
            // One ESC closes, as Mantine on the Web; GameUiInputRules drops focus and consumes it, and the view cancels on that blur (plan D-11)
            var (_, response) = _service.RequestInputModal("ブループリント名", "ブループリントの名前を入力してください", "保存");
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Find<TextField>(_layer, "modal-input").value = "途中の名前";

            simulate.KeyDown(KeyCode.Escape);
            simulate.FrameUpdate();
            Assert.AreEqual(UniTaskStatus.Succeeded, response.Status);
            Assert.AreEqual(ModalResult.Cancel, response.GetAwaiter().GetResult().result);
            Assert.IsFalse(Shown());
            Assert.IsTrue(UiInputExclusivity.IsEscapeConsumedThisFrame);
        }

        [Test]
        public void 入力欄の外を押してフォーカスが外れても閉じない()
        {
            var (_, response) = _service.RequestInputModal("ブループリント名", "ブループリントの名前を入力してください", "保存");
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            simulate.Click(Find<Label>(_layer, "modal-message").worldBound.center);
            simulate.FrameUpdate();
            Assert.AreEqual(UniTaskStatus.Pending, response.Status);
            Assert.IsTrue(Shown());
        }

        [Test]
        public void 入力欄にフォーカスがあるまま外から取り消されてもゲームのキー入力を止めたままにしない()
        {
            // 右短押し・要求の差し替えはブリッジの CancelPendingRequest で閉じ、入力欄のフォーカスが外れる前に View が外れる
            // Right short-press and request replacement close through the bridge's CancelPendingRequest, detaching the view before the field loses focus
            _service.RequestInputModal("ブループリント名", "ブループリントの名前を入力してください", "保存");
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);

            _service.CancelPendingRequest();
            simulate.FrameUpdate();
            Assert.IsFalse(Shown());
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        private bool Shown()
        {
            // C3-b の UiModalHostTest.ScopeShown と同じ式
            // Same expression as C3-b UiModalHostTest.ScopeShown
            return _layer.Q("modal-scope").ClassListContains(ShownClass);
        }
    }
}
```
（`ui-modal-host--shown` が `modal-scope` に付くことは C3-b の計画の `UiModalHostTest` から写した。実物で付く先が違えば `UiModalHostTest` の式に揃える。「外から取り消されても…」が赤なら、`ModalView` の外れで `FocusOutEvent` が出ていない〈`GameUiInputRules` が戻すのは FocusOut だけ〉。`IsTextInputFocused` の値と、外れの前後で `focusController.focusedElement` を `TestContext.WriteLine` で出して確かめてから、`ModalView` に `RegisterCallback<DetachFromPanelEvent>` を足し、パネルから外れる前に入力欄を `Blur()` する形で直す〈Blur は ESC の印が無いので取消を二重に出さない〉）

Run: `uloop compile --project-path ./moorestech_client`
Expected: コンパイルエラー（`ModalView` 等が無い）

- [ ] **Step 4: View・色の写し・USS・Presenter を書く**

`S/Client.Ui/Screens/Modal/ModalButtonVariants.cs`:
```csharp
using System;
using Client.Ui.Components.Controls;
using Client.Ui.State.Modal.Logic;

namespace Client.Ui.Screens.Modal
{
    /// <summary>
    /// 確定ボタンの色の向き → ボタンの見た目の唯一の写し（設計書 A-28。Web は blue・red）
    /// The single mapping from confirm-button tone to button look (design A-28; the Web uses blue and red)
    /// </summary>
    public static class ModalButtonVariants
    {
        public static FormButtonVariant From(ModalButtonTone tone)
        {
            return tone switch
            {
                ModalButtonTone.Blue => FormButtonVariant.Filled,
                ModalButtonTone.Red => FormButtonVariant.Danger,
                _ => throw new ArgumentOutOfRangeException(nameof(tone), tone, null),
            };
        }
    }
}
```

`S/Client.Ui/Screens/Modal/ModalView.cs`:
```csharp
using System;
using Client.Input;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Modal;
using Client.Ui.State.Modal.Logic;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Modal
{
    /// <summary>
    /// モーダル 1 件の中身（Web ModalBody）。要求ごとに作り直し、入力の持ち越しを構造で無くす（Web の key={id}）。確定・ESC・幕の押下をすべて自分で IModalCommands へ返す（設計書 A-8）
    /// One modal's contents (Web ModalBody); rebuilt per request so input never carries over (Web key={id}). It returns confirm, ESC and backdrop presses to IModalCommands itself (design A-8)
    /// </summary>
    public sealed class ModalView : VisualElement
    {
        private readonly ModalRequest _request;
        private readonly IModalCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private readonly IObservable<Unit> _backdropClicked;
        private readonly FormButton _button;
        private bool _confirmable;
        private string _confirmedText;
        private IDisposable _backdropSubscription;

        public ModalView(ModalRequest request, IModalCommands commands, UiCommandResultToaster toaster, IObservable<Unit> backdropClicked)
        {
            _request = request;
            _commands = commands;
            _toaster = toaster;
            _backdropClicked = backdropClicked;
            AddToClassList("modal-view");

            // 題・本文（Web Modal.Title fz=h4・Text size=sm c=dimmed mb=lg）。文は要求側が解いた文なので FromVerbatim（A-40）
            // Title and message (Web Modal.Title fz=h4, Text size=sm c=dimmed mb=lg); requester-resolved text, so FromVerbatim (A-40)
            var frame = new DialogFrame("modal");
            frame.SetTitle(UiText.FromVerbatim(request.Title));
            Add(frame);
            var message = new LocalizedLabel { name = "modal-message" };
            message.AddToClassList(UiTypographyClass.TextSm);
            message.AddToClassList(UiTypographyClass.TextDimmed);
            message.AddToClassList("modal-view__message");
            message.SetText(UiText.FromVerbatim(request.Message));
            frame.Add(message);

            // 入力付きだけ入力欄を置き、確定ボタンは幅いっぱい（Web Button fullWidth）
            // Only input requests get the field; the confirm button spans the width (Web Button fullWidth)
            if (request.RequiresInput) frame.Add(CreateInput());
            _button = new FormButton("modal-button", UiText.FromVerbatim(request.ButtonText), ModalButtonVariants.From(ModalLogic.ConfirmButtonTone(request.Variant)));
            _button.OnClicked.Subscribe(_ => Confirm());
            frame.AddAction(_button);
            SetEnteredText("");

            // 幕の押下は載っている間だけ聞く。フォーカスの無い ESC は聞かない（既存のゲーム入力の経路が取り消す。計画 D-11・設計書 A-83）
            // Listen for backdrop presses only while attached; focus-less ESC is not heard here (the existing game input path cancels; plan D-11, design A-83)
            RegisterCallback<AttachToPanelEvent>(OnAttached);
            RegisterCallback<DetachFromPanelEvent>(OnDetached);
        }

        private GameTextField CreateInput()
        {
            var input = new GameTextField("modal-input", false, GameTextFieldLook.MantineInput);
            input.AddToClassList("modal-view__input");
            input.OnValueChanged.Subscribe(SetEnteredText);

            // 入力欄の ESC は GameUiInputRules が消費してフォーカスを外す。その外れを見て 1 回で取り消す（Web の Mantine と同じ 1 回。計画 D-11）
            // GameUiInputRules consumes ESC in the field and drops focus; seeing that blur, cancel in one press (one press like Mantine on the Web; plan D-11)
            input.Field.RegisterCallback<FocusOutEvent>(_ => CancelIfEscapeBlurred());

            // 表示時に入力欄へフォーカス（Web autoFocus）。載った直後はスタイルが未解決なので次のフレームで当てる
            // Focus the field when shown (Web autoFocus); styles are unresolved right after attaching, so apply next frame
            input.RegisterCallback<AttachToPanelEvent>(_ => input.schedule.Execute(() => input.Field.Focus()));
            return input;
        }

        private void OnAttached(AttachToPanelEvent evt)
        {
            _backdropSubscription = _backdropClicked.Subscribe(_ => Respond(ModalResponse.Cancel(_request)));
        }

        private void OnDetached(DetachFromPanelEvent evt)
        {
            _backdropSubscription?.Dispose();
            _backdropSubscription = null;
        }

        // 可否と確定の文は文の変化ごとに 1 回だけ導き、ボタンと確定の両方がそれを読む（ModalLogic.TryGetConfirmableText）
        // Derive eligibility and the confirmed text once per text change; both the button and Confirm read it (ModalLogic.TryGetConfirmableText)
        private void SetEnteredText(string text)
        {
            _confirmable = ModalLogic.TryGetConfirmableText(_request, text, out _confirmedText);
            _button.SetEnabled(_confirmable);
        }

        private void Confirm()
        {
            if (!_confirmable)
            {
                Debug.LogWarning($"[Modal] confirm ignored for request {_request.Id}: entered text is whitespace only");
                return;
            }
            Respond(ModalResponse.Confirm(_request, _confirmedText));
        }

        private void CancelIfEscapeBlurred()
        {
            // silent-ok: ESC 以外のフォーカスの外れ（クリック・閉じたときの外れ）は取り消さない / ModalScreenPresenterTest「入力欄のESCは1回で取り消す」と「欄の外を押しても閉じない」が見る
            if (!UiInputExclusivity.IsEscapeConsumedThisFrame) return;
            Respond(ModalResponse.Cancel(_request));
        }

        private void Respond(ModalResponse response)
        {
            _toaster.Report(ModalCommands.RespondCommandName, _commands.Respond(response));
        }
    }
}
```

`S/Client.Ui/Screens/Modal/Modal.uss`:
```css
/* web: moorestech_web/webui/src/features/modal/ModalHost.tsx の Text mb="lg"・TextInput mb="lg"。枠・題・本文の余白・幕は DialogFrame と UiModalHost（C3-b）が持つ */
/* web: Text mb="lg" and TextInput mb="lg" in moorestech_web/webui/src/features/modal/ModalHost.tsx; frame, title, body padding and backdrop belong to DialogFrame and UiModalHost (C3-b) */
.modal-view {
    flex-direction: column;
}

.modal-view__message {
    margin-bottom: var(--mantine-spacing-lg);
}

.modal-view__input {
    margin-bottom: var(--mantine-spacing-lg);
}
```

`S/Client.Ui/Screens/Modal/ModalScreenPresenter.cs`:
```csharp
using Client.Ui.Foundation.Modal;
using Client.Ui.State.Modal;
using Client.Ui.State.Toast;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Modal
{
    /// <summary>
    /// 保留の要求を宿主へ出し入れするだけ（Web ModalHost）。応答は View が IModalCommands へ返す（設計書 §4.2・A-8）
    /// Only shows and hides the pending request in the host (Web ModalHost); the view returns replies to IModalCommands (design §4.2, A-8)
    /// </summary>
    public sealed class ModalScreenPresenter : IInitializable
    {
        private readonly UiModalHost _host;
        private readonly ModalRequestService _service;
        private readonly IModalCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private string _shownRequestId;

        public ModalScreenPresenter(UiModalHost host, ModalRequestService service, IModalCommands commands, UiCommandResultToaster toaster)
        {
            _host = host;
            _service = service;
            _commands = commands;
            _toaster = toaster;
        }

        public void Initialize()
        {
            // 状態の正は ModalRequestService。変化の通知で出し入れする
            // ModalRequestService owns the state; show or hide on its change notice
            _service.OnPendingChanged.Subscribe(_ => Sync());
            Sync();
        }

        private void Sync()
        {
            var pending = _service.Pending;
            if (pending == null)
            {
                _shownRequestId = null;
                _host.Hide();
                return;
            }

            // silent-ok: 同じ要求の通知の重なりは描き直さない（入力中の文を守る） / 新しい要求は ID が変わるので ModalScreenPresenterTest「新しい要求は…」が見る
            if (pending.Id == _shownRequestId) return;
            _shownRequestId = pending.Id;
            _host.Show(new ModalView(pending, _commands, _toaster, _host.OnBackdropClicked));
        }
    }
}
```
（Presenter はゲームの寿命で 1 つなので購読の Dispose は持たない。View の幕の購読は載っている間だけで、外れたときに Dispose する）

`GameUiTheme.tss` の `Screens` 区画の末尾（Task 7 の行の次）に:
```css
@import url("project://database/Assets/Scripts/Client.Ui/Screens/Modal/Modal.uss");
```

- [ ] **Step 5: 流して緑にしてコミット**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Screens\.Modal|State\.Modal|Modal|Conventions|Theme)\."`
Expected: `ErrorCount: 0`、`ModalViewTest` 5・`ModalScreenPresenterTest` 7 と C3-b の `UiModalHostTest`・L2 が PASS。赤のときの手順:
- 入力欄の ESC で閉じない → `GameUiInputRules` が `_layer` に付いているか、`FocusOutEvent` が `input.Field`（内側の入力要素ではなく `TextField`）で受けられているかを `Debug.Log` で出す。フォーカスが内側の要素に入るなら `TextField` 側で `FocusOutEvent` を TrickleDown で受ける
- 欄の外を押しただけで閉じる → `CancelIfEscapeBlurred` の ESC の印の判定が抜けている
- フォーカスが入らない → `focusController.focusedElement` を `Debug.Log` で出し、`TextField` の内側の要素なら主張は既に `Contains` で受けている。null なら `schedule.Execute` を `ExecuteLater(1)` に替えて確かめ、それでも入らなければ `done.md` の「未確認」に書いて主張を外さずに止める（Web の autoFocus は R-8 の一部）
- 幕の押下で取消が返らない → `OnBackdropClicked` の購読より前に `Show` していないかを見る。C3-b の宿主の誤りなら `done.md` の「integrator への依頼」に書く
- `modal-title` が見つからない → C3-b の `DialogFrame` の題の名前を実物で読み、Task 1 Step 1 の読み替えに足す
```bash
git add moorestech_client/Assets/Scripts/Client.Ui/Screens/Modal moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/Modal "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss"
git commit -m "feat(uitk): モーダルの View・Presenter と L1 を足し、描き手を UITK に一本化する（S3-hotbar-modal）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 9: 登録と全体スイッチ UITK での起動

**Files:**
- Create: `S/Client.Starter/Registration/GameUi/HotbarGameUiRegistration.cs`・`ModalGameUiRegistration.cs`
- Modify: `S/Client.Starter/Registration/MainGameUiRegistration.cs`（画面登録区画に 2 行）

**Interfaces:**
- Consumes: Task 3・4・7・8 の型、C3-b の `UiModalHost`（`FoundationGameUiRegistration` が登録）、S3-buildmenu の `PlacementTargetIcons`（`ContentGameUiRegistration` が登録）、段階 1 の `UiCommandResultToaster`（`MainGameUiRegistration` が登録）、S2-hud の `BlockingSkitPresence`
- Produces: 全体スイッチ UITK の起動でホットバーとモーダルが UITK で出る

- [ ] **Step 1: 登録を書く**

`S/Client.Starter/Registration/GameUi/HotbarGameUiRegistration.cs`:
```csharp
using Client.Ui.Screens.Hotbar;
using Client.Ui.State.Hotbar;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // ホットバー（GameUi）の登録。操作は画面と同じ系でだけ要るのでここで登録する（計画 D-22）
    // Registers the GameUi hotbar; the commands are only needed alongside the screen, so they register here (plan D-22)
    internal static class HotbarGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<HotbarModel>().AsSelf();
            builder.Register<HotbarCommands>(Lifetime.Singleton).As<IHotbarCommands>();
            builder.RegisterEntryPoint<HotbarHudPresenter>();
        }
    }
}
```

`S/Client.Starter/Registration/GameUi/ModalGameUiRegistration.cs`:
```csharp
using Client.Ui.Screens.Modal;
using Client.Ui.State.Modal;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // モーダル（GameUi）の登録。操作は画面と同じ系でだけ要る。状態の ModalRequestService と BP 名のブリッジは両系で MainGameUiStateRegistration が持つ
    // Registers the GameUi modal; ModalRequestService and the blueprint-name bridge are registered for both systems by MainGameUiStateRegistration
    internal static class ModalGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.Register<ModalCommands>(Lifetime.Singleton).As<IModalCommands>();
            builder.RegisterEntryPoint<ModalScreenPresenter>();
        }
    }
}
```

`MainGameUiRegistration.cs` の画面登録区画の末尾（その時点の最後の `…GameUiRegistration.Register(builder);` の次）に:
```csharp
            HotbarGameUiRegistration.Register(builder);
            ModalGameUiRegistration.Register(builder);
```
（`FoundationGameUiRegistration`〈C3-b。`UiModalHost` と `GameUiModalHostInstaller`〉が画面登録区画より前に呼ばれていることを読んで確かめる。後なら `ModalScreenPresenter.Initialize` の `Hide` が宿主の `Mount` より先に走るので、`Hide` が未 Mount で安全か C3-b の実物を読み、危なければ止めて指揮へ）

- [ ] **Step 2: コンパイルと L2**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"`
Expected: `ErrorCount: 0`、全件 PASS（`UiLayerDependencyDirectionTest`・`UiThemeImportBundleTest`・`TutorialAnchorCoverageTest` を含む）

- [ ] **Step 3: 全体スイッチ UITK で起動してエラー 0 と木を見る**

Run:
```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); return Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Current.ToString();'
uloop clear-console --project-path ./moorestech_client
uloop control-play-mode --project-path ./moorestech_client --action Play
```
45 秒待ってから（ドメインリロード中のエラーは 45 秒待って再試行）:
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
var root = Client.Ui.Foundation.Root.GameUiRoot.Instance.GetRootElement();
var hud = root.Q("hotbar-hud");
var slots = root.Query<Client.Ui.Components.Slots.SlotFrame>().Where(f => f.name.StartsWith("hotbar-slot-")).ToList().Count;
return $"hud={hud != null} display={hud?.resolvedStyle.display} slots={slots} grid={root.Q("hotbar-grid")?.worldBound}";'
uloop get-logs --project-path ./moorestech_client --log-type Error
```
Expected: 2 行目の EDC が `GameUi`。`hud=True display=Flex slots=9`、`grid` の矩形が画面下端の中央。`get-logs` が 0 件（開幕スキットの間に起動したら `display=None` でよい。スキットを飛ばしてから見直す）。エラーがあれば原因を読んで直し、`done.md` の「証拠」に EDC の出力を貼る

- [ ] **Step 4: コミット**

```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
git add moorestech_client/Assets/Scripts/Client.Starter/Registration
git commit -m "feat(uitk): ホットバーとモーダルを全体スイッチ UITK の画面登録区画へ足す（S3-hotbar-modal）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 10: 世界の地の上の色と減光の照合（bd `moorestech-albof.34`）

**Files:**
- Modify（照合で外れたときだけ）: `THEME/DerivedTokens.uss` の「S3-hotbar-modal」ブロックの値、`tools/uitk/webTokens/handWrittenDerivedRows.mjs` の該当行と再生成した `docs/uitk/translucent-tokens.md`
- Modify: `docs/uitk/TODO.md`（「## 実測の結論」の末尾に 1 行、暗い地で外れたら「## 見た目の例外」の末尾に `LX-76`）

**Interfaces:**
- Consumes: Task 5 のトークン、Task 7 の `HotbarHudView`、`GameUiPanelSettings.asset`（ランタイムの PanelSettings。Linear 合成）、DI の `IHotbarCommands`・`PlacementTargetIcons`・`UiCommandResultToaster`
- Produces: 照合表（`done.md`「## 証拠」）と、明るい地で 7 段以内に揃ったトークン

照合の考え方（D-9）: Web の Chromium は半透明と opacity を sRGB の値のまま混ぜる（`出力 = 地 × (1-α) + 色 × α`）。UITK のランタイムは Linear で混ぜるので、同じ α では明るい地の上で淡く出る。画素を実際のランタイムの PanelSettings で描かせ、Web の式の値と比べる。

- [ ] **Step 0: 参照の列の周りの地の色を読む**

参照 `3-hotbar-equipment-empty.png` の地は世界の絵で、列の周りは濃い橙（目視。上部の空に近い (240,203,166) と違う可能性がある）。照合の地に、参照で列のすぐ外の色を 3 つ目として足す:
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
var bytes = System.IO.File.ReadAllBytes("<LOGS の絶対パス>/uitk/reference-captures/web-869b2d4264/3-hotbar-equipment-empty.png");
var png = new UnityEngine.Texture2D(2, 2); png.LoadImage(bytes);
// 列の左端の 40px 左・枠の高さの中ほど（参照の 1280×720 で列は x 388〜893・枠は y 666〜718。左下原点へ直す）
// 40px left of the row and mid-height of the slots (the row spans x 388-893 and the slots y 666-718 in the 1280x720 reference; convert to bottom-left origin)
var c = (UnityEngine.Color32)png.GetPixel(348, 720 - 692);
return $"{c.r},{c.g},{c.b}";'
```
（`<LOGS の絶対パス>` は `realpath <LOGS>` の結果。Editor の作業ディレクトリは `moorestech_client` なので相対パスにしない）
Expected: 3 つの数。読んだ点が列の枠やタブに掛かっていないことを、参照を `Read` して座標で確かめる（列の左端は参照の画素で読み直し、違えば x を直す）。以下の Step 1〜4 の `bright`・`dark` に `ref`（この色）を足して 3 地で回し、Step 4 の「明るい地で 7 段以内」は `ref` でも満たす値を採る（R-6 と設計書 A-108 の基準は明るい地で 7 段以内。`ref` は参照の実際の地で、`bright` と同じ基準を両方で満たす値を採る。両立しなければ基準を緩めず、完了にもマージにもせず、両方の差と候補の値を `done.md` の「要裁定」に書いて止める）

- [ ] **Step 1: Web の期待値を出す**

`W/src/shared/ui/SlotFrame/style.module.css` を Read し、割当済み（`data-filled`）の面の色を読む（以下 `FILLED`）。Run（`FILLED` を読んだ値に替える）:
```bash
node -e '
const mix=(g,c,a)=>g.map((v,i)=>Math.round(v*(1-a)+c[i]*a));
const FACE=[50,52,67], TAB=[50,52,67], FILLED=[/* SlotFrame の data-filled の面 */];
for (const [label,g] of [["bright",[240,203,166]],["dark",[40,42,48]],["ref",[/* Step 0 で読んだ 3 つの数 */]]]) {
  const empty=mix(g,FACE,0.5);
  const filled=mix(g,FILLED,1), unresolved=mix(g,FILLED,0.4);
  console.log(label,"empty",empty,"tab",mix(g,TAB,0.68),"unresolved",unresolved,"disabledEmpty",mix(g,empty,0.55),"disabledFilled",mix(g,filled,0.55),"disabledUnresolved",mix(g,unresolved,0.55));
}'
```
Expected: 地ごとに 6 値。`done.md` の「## 証拠」に貼る（`disabled*` は列の opacity 0.55 が枠の面ごと地へ混ざる値。未解決の枠は自分の 0.4 と列の 0.55 が重なる〈Web `SlotFrame/style.module.css` の白面と `HotbarPanel/style.module.css` の列の opacity〉）

- [ ] **Step 2: ランタイムの PanelSettings で描く口を作る（PlayMode・GameUi）**

Task 9 Step 3 の手順で全体スイッチ UITK の PlayMode に入り、開幕スキットを飛ばしてから:
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
var source = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>("Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset");
var resolver = Client.Game.InGame.Context.ClientDIContext.DIContainer.DIContainerResolver;
var commands = VContainer.IObjectResolverExtensions.Resolve<Client.Ui.State.Hotbar.IHotbarCommands>(resolver);
var icons = VContainer.IObjectResolverExtensions.Resolve<Client.Ui.Content.PlacementTargetIcons>(resolver);
var toaster = VContainer.IObjectResolverExtensions.Resolve<Client.Ui.State.Toast.UiCommandResultToaster>(resolver);
foreach (var (label, ground) in new[] { ("bright", new UnityEngine.Color32(240, 203, 166, 255)), ("dark", new UnityEngine.Color32(40, 42, 48, 255)), ("ref", new UnityEngine.Color32(/* Step 0 の r */ 0, /* g */ 0, /* b */ 0, 255)) })
{
    var settings = UnityEngine.Object.Instantiate(source);
    settings.targetTexture = new UnityEngine.RenderTexture(1280, 720, 0, UnityEngine.RenderTextureFormat.ARGB32, UnityEngine.RenderTextureReadWrite.sRGB) { name = $"s3hm-rt-{label}" };
    var go = new UnityEngine.GameObject($"s3hm-albof34-{label}");
    var document = go.AddComponent<UnityEngine.UIElements.UIDocument>();
    document.panelSettings = settings;
    var root = document.rootVisualElement;
    root.style.backgroundColor = (UnityEngine.Color)ground;
    var view = new Client.Ui.Screens.Hotbar.HotbarHudView(commands, icons, toaster) { name = "s3hm-hotbar" };
    root.Add(view);
    var slots = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Repeat<Client.Ui.State.Hotbar.HotbarSlotViewModel>(Client.Ui.State.Hotbar.HotbarEmptySlot.Instance, 9));
    slots[4] = new Client.Ui.State.Hotbar.HotbarUnresolvedSlot(System.Guid.NewGuid());
    view.Render(new Client.Ui.State.Hotbar.HotbarViewModel(slots, Client.Ui.State.Hotbar.HotbarSelection.None, true, true));
}
return "ok";'
```
Expected: `ok`（`IObjectResolverExtensions` の呼び方・`ClientDIContext` の名前が違えば、`scenarios/uitk/uitk-mode-hud-and-tooltip.cs` の `ClientDIContext.DIContainer.DIContainerResolver.Resolve<…>()` に揃える。この口が作れなければ止めて `done.md` の「integrator への依頼」に書き、指揮へ報告する）

- [ ] **Step 3: 画素を読む（受理あり → 受理なし）**

`uloop focus-window --project-path ./moorestech_client` の後に:
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
var lines = new System.Collections.Generic.List<string>();
foreach (var label in new[] { "bright", "dark", "ref" })
{
    var document = UnityEngine.GameObject.Find($"s3hm-albof34-{label}").GetComponent<UnityEngine.UIElements.UIDocument>();
    var root = document.rootVisualElement;
    var rt = document.panelSettings.targetTexture;
    UnityEngine.RenderTexture.active = rt;
    var texture = new UnityEngine.Texture2D(rt.width, rt.height, UnityEngine.TextureFormat.RGBA32, false);
    texture.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0);
    texture.Apply();
    UnityEngine.RenderTexture.active = null;
    // パネル座標（左上原点）→ テクスチャ（左下原点）
    // Panel coordinates (top-left origin) to texture (bottom-left origin)
    UnityEngine.Color32 At(UnityEngine.Vector2 p) => texture.GetPixel((int)p.x, rt.height - 1 - (int)p.y);
    var empty = root.Q("hotbar-slot-1").worldBound.center;
    var unresolved = root.Q("hotbar-slot-4").worldBound.center;
    var tab = root.Q("hotbar-slot-1").parent.Q(className: "hotbar-hud__number-tab").worldBound;
    lines.Add($"{label} empty={At(empty)} unresolved={At(unresolved)} tab={At(new UnityEngine.Vector2(tab.xMin + 2.5f, tab.yMax - 2.5f))}");
}
return string.Join("\n", lines);'
```
続けて 2 つの `s3hm-hotbar` を受理なしで描き直す（ctor は Task 2 の `HotbarViewModel(IReadOnlyList<HotbarSlotViewModel> slots, HotbarSelection selection, bool selectAccepted, bool visible)`）:
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
foreach (var label in new[] { "bright", "dark", "ref" })
{
    var root = UnityEngine.GameObject.Find($"s3hm-albof34-{label}").GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement;
    var view = UnityEngine.UIElements.UQueryExtensions.Q<Client.Ui.Screens.Hotbar.HotbarHudView>(root, "s3hm-hotbar");
    var slots = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Repeat<Client.Ui.State.Hotbar.HotbarSlotViewModel>(Client.Ui.State.Hotbar.HotbarEmptySlot.Instance, 9));
    slots[4] = new Client.Ui.State.Hotbar.HotbarUnresolvedSlot(System.Guid.NewGuid());
    // 割当済みの白面も減光の下で読む（枠 7。名前だけの絵で面を出す）
    // Also read an assigned white face under the dim (slot 7; a name-only icon shows the face)
    slots[7] = new Client.Ui.State.Hotbar.HotbarAssignedSlot(System.Guid.NewGuid(), "albof34", Client.Ui.State.PlacementTarget.PlacementTargetNameOnlyIcon.Instance);
    view.Render(new Client.Ui.State.Hotbar.HotbarViewModel(slots, Client.Ui.State.Hotbar.HotbarSelection.None, false, true));
}
return "ok";'
```
1 フレーム置いてから（`uloop control-play-mode --project-path ./moorestech_client --action Status` を 1 回挟む）同じ読み取りの EDC を流し、`empty` を `disabledEmpty`、`unresolved` を `disabledUnresolved` として読み、`hotbar-slot-7` の面（`worldBound` の左上から内側へ 6px。名前の文字・タブ・ベベルに掛からない点）を `disabledFilled` として読む（読み取りの EDC の `lines.Add` に `filled={At(new UnityEngine.Vector2(root.Q("hotbar-slot-7").worldBound.xMin + 6f, root.Q("hotbar-slot-7").worldBound.yMin + 6f))}` を足す）。
Expected: 3 地 × 6 値（受理ありの `empty`・`tab`・`unresolved` と、受理なしの `disabledEmpty`・`disabledUnresolved`・`disabledFilled`。Step 1 の `filled` は期待の計算だけに使い、受理ありの 1 回目では枠 7 が空きなので読まない）。どれも明るい地で 7 段以内が合格（Step 4。空き枠だけで合わせた補正が白面・未解決の重なりで外れたら、その行も Step 4 で直す）。Step 1 の期待と並べた表（列: 地・要素・Web 期待・UITK 実測・差の最大 RGB 段）を `done.md` の「## 証拠」に書く。読む点がタブの数字や枠のベベルに掛かっていないことを、同じ RT を `EncodeToPNG` して `moorestech_client/.uloop/outputs/compare/S3-hotbar-modal/albof34-<地>.png` に書き Read で確かめる

- [ ] **Step 4: 明るい地で 7 段を超える値を直す**

明るい地で 7 段を超えた行だけ直す（C3-a の基準）:
- `empty`・`tab` の面: α を上げ下げして合わせる。`α_uitk` を 0.01 刻みで変えて Step 2〜3 を回し、明るい地で 7 段以内かつ暗い地の差が最小の値を採る。採った値を `DerivedTokens.uss` に書き、`handWrittenDerivedRows.mjs` の該当行を `formula` から `uss: "<値>"` と決め方の文（「明るい地 (240,203,166) のランタイム画素で Web の sRGB 合成に 7 段以内で合わせた。bd albof.34」）へ書き換えて `node tools/uitk/convert-web-tokens.mjs` で再生成する（前例 `--slot-frame-face`）
- 決める順: 面の α を `empty`・`tab` の行で先に決め、次に `--hotbar-unresolved-opacity` を `unresolved` の行で、最後に `--hotbar-select-disabled-opacity` を受理外の 3 行（`disabledEmpty`・`disabledFilled`・`disabledUnresolved`）の最大差が最小になる値で決める（後の値は前の値を前提にするので順を変えない）。どの行かが明るい地（と `ref`）で 7 段を超えるなら基準を緩めず、完了にせず、差と候補の値を `done.md` の「要裁定」に書いて止める
- `unresolved`・`disabled*`（opacity）: `--hotbar-unresolved-opacity`・`--hotbar-select-disabled-opacity` を 0.01 刻みで変えて同じく合わせる。opacity は前計算の表の対象ではないので、`DerivedTokens.uss` の行のコメントに「明るい地のランタイム画素で Web の opacity <元の値> に合わせた（bd albof.34・LX-23 の「## 実測の結論」）」と書く
- 直したら `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Ui\.(Screens\.Hotbar|Theme|Conventions)\."`（`HotbarHudViewTest`・`HotbarHudDropTest` はトークンを読むので期待を書き換えない）

- [ ] **Step 5: 台帳に書き、口を片付けてコミット**

`docs/uitk/TODO.md` の「## 実測の結論」の末尾に 1 行: 「世界の上に直に置く面と opacity（S3-hotbar-modal・bd albof.34）: 明るい地 (240,203,166)・暗い地 (40,42,48) のランタイム画素を Web の sRGB 合成と照合。採った値 `--hotbar-slot-frame-face` α <値>・`--hotbar-number-tab-face` α <値>・`--hotbar-unresolved-opacity` <値>・`--hotbar-select-disabled-opacity` <値>。明るい地の最大差 <n> 段・暗い地の最大差 <n> 段」。暗い地で 7 段を超えた行があれば「## 見た目の例外」の末尾に `LX-76`（理由: Linear 合成では明るい地と暗い地の両方を 1 つの α で Web に揃えられない。明るい地を優先）を足し、該当トークンの行に `/* look-exception: LX-76 */` を付ける。撮り終えたら `s3hm-albof34-*` を `Object.Destroy` し RT を `Release` する
```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"
git add moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens.uss tools/uitk/webTokens/handWrittenDerivedRows.mjs docs/uitk/translucent-tokens.md docs/uitk/TODO.md
git commit -m "fix(uitk): ホットバーの世界の地の上の面と減光をランタイムの画素で Web に合わせる（S3-hotbar-modal・albof.34）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```
Expected: `LookExceptionLedgerTest` を含めて PASS。`bd note moorestech-albof.34 "<台帳に書いた 1 行>"`

---

### Task 11: Web との並べ比べ（ホットバー 720p・2560×1080、モーダル 720p・1080p・2560×1080）

**Files:**
- なし（撮影の原本は `moorestech_client/.uloop/outputs/compare/S3-hotbar-modal/`。差の表は `done.md`）。直しが出たら `HotbarHud.uss`・`Modal.uss` と、要れば台帳「## 見た目の例外」の末尾（`LX-77`〜`LX-80`）

**Interfaces:**
- Consumes: 参照 `LOGS/uitk/reference-captures/web-869b2d4264/`・`web-0e81bbe3c3-2560x1080/` の `3-hotbar-equipment-empty.png`・`3-modal.png`（`manifest.json` で状態を確かめる）
- Produces: ホットバー 2 解像度・モーダル 3 解像度の組、差の表、モーダルの実画面 px の実測

- [ ] **Step 1: 1920×1080 のモーダルの Web 参照を撮る**

先に `ls ../moorestech_logs/uitk/reference-captures/ | grep 1920x1080` で他レーンが撮ったものを探し、`3-modal.png` があればそれを使う。無ければ master の worktree で撮る（`feature/uitk` の Web には手を入れない）:
```bash
moores-wt new uitk-ref-1080-s3hm --from origin/master --no-editor
cd <作られた worktree の絶対パス>/moorestech_web/webui && pnpm install
sed -i '' 's/referenceViewport = { width: 1280, height: 720 }/referenceViewport = { width: 1920, height: 1080 }/' e2e/uitk-reference/shotRunner.ts
SHA=$(git rev-parse --short=10 HEAD)
UITK_REFERENCE_OUT=<moorestech_logs の絶対パス>/uitk/reference-captures/web-$SHA-1920x1080 \
UITK_REFERENCE_PORT=<空きポート> node --import tsx e2e/uitk-reference/capture.ts
git checkout -- e2e/uitk-reference/shotRunner.ts
cd - && moores-wt rm uitk-ref-1080-s3hm --force --prune-branch
```
Expected: `web-<sha>-1920x1080/3-modal.png` と `manifest.json`。worktree が消える。`done.md` の「integrator への依頼」に「1920×1080 の参照 `web-<sha>-1920x1080` を撮った。台帳『## 参照キャプチャ』への記載をお願いしたい」と書く

- [ ] **Step 2: 撮影の口を用意する（PlayMode・GameUi・言語・複製した PanelSettings の RenderTexture）**

S2-mode-tooltip の Task 10 Step 2 と同じ手順で、`GameUiPanelSettings.asset` の複製に 1280×720・1920×1080・2560×1080 の sRGB ARGB32 RenderTexture を付けた UIDocument（名前 `s3hm-compare-<w>`）を作り、言語を参照（`manifest.json` と png を Read して確かめる）に揃え、ロケールのクラスを当てる。ルートの背景は参照の画面から読んだ単色（ホットバーは枠の周りの地。モーダルは幕の無い `2-game-hud.png` の (640,150) の色〈rgb(243,191,136)〉の全面にし、その上に `UiModalHost` の幕を描く。`3-modal.png` は幕が掛かった後の色なので背景に使うと二重に暗くなる。前例 C3-b 計画 Task の並べ比べの (640,150)。D-31）にし、差の表に「背景は世界でなく単色」と書く。撮るたびに `uloop focus-window` の後の EDC で `RenderTexture.active` → `ReadPixels` → `EncodeToPNG` で `moorestech_client/.uloop/outputs/compare/S3-hotbar-modal/uitk-<撮影 id>-<w>x<h>.png` へ書く

- [ ] **Step 3: ホットバーを撮る（1280×720・2560×1080）**

参照 `3-hotbar-equipment-empty.png` を Read し、枠 0 が選択で絵あり・枠 4 が未解決・他は空きであることを確かめる。EDC で `s3hm-compare-1280`・`s3hm-compare-2560` のルートに Task 10 Step 2 と同じく `HotbarHudView` を載せ、枠 0 に `new HotbarAssignedSlot(<木のチェストの Guid>, "木のチェスト", new PlacementTargetBlockIcon(<木のチェストの BlockId>))`（PlayMode なのでマスタと本物の絵がある）・枠 4 に `HotbarUnresolvedSlot`・選択 `HotbarSelection.Of(0)`・受理あり・表示ありの ViewModel で `Render` して撮る（絵の中身の違いは差の表で「捨てる（参照の絵は fixture の SVG）」）。見る点: 下端から 2px（2560×1080 は 1.5 倍で 3px）・枠 51.68（77.52）・枠間 5（7.5）・列の 0.78 の右ずれ・番号タブ（左 17・上へ 16・15 角・暗い縁・面の色・枠の明るい半透明〈`--hotbar-number-tab-border`。外れたら `LX-77`〉・数字の縦位置〈字形の上端・下端の行を参照と UITK の画素で探し、差 1px 以内。外れたらタブの数字の `translate` を Web の `line-height: 13px` の基線から導き直す〈部品の `translate: 0 18.1%` は line-height 1 の基線合わせ〉）・選択の水色の輪と下の欠け（`--selected-face-*`）・ベベルの内向き（`--bevel-inset: inset`）・未解決の白面の減光・空き枠の面（Task 10 で合わせた値）

- [ ] **Step 4: モーダルを撮り、実画面 px を測る（1280×720・1920×1080・2560×1080）**

参照 `3-modal.png` を Read し、BP 名の要求（題「ブループリント名」・入力欄にフォーカスの青い輪・保存ボタンが押せない）であることを確かめる。EDC で各ルートに全画面の層を作り、`new UiModalHost(new StageScaleSource(new ReactiveProperty<float>(<StageScaleMeasure で読んだ倍率>)))` を `Mount` して `Show(new ModalView(<BP 名の要求>, <記録するだけの IModalCommands>, <UiCommandResultToaster>, Observable.Never<Unit>()))` で撮る（倍率は本番と同じ式で取る: 1 回目の EDC でそのルートのパネルを `StageScaleMeasure.TryRead(root.panel, out var scale)` で読み、偽なら止めて `done.md` の「未確認」に書く。`scaledPixelsPerPoint` を直に読まない＝設計書 A-75。C3-b が変種 B を選んでいても本番と同じ倍率になる）。幅は書き出した PNG の画素で測る: 2 回目の EDC で RT を `EncodeToPNG` した画像の、枠の高さの中ほどの 1 行を左から右へ走査し、幕の色から枠の面の色へ変わる x と戻る x を出す（`worldBound` × 倍率の換算は使わない。換算が正しくても画像の幅を保証しないため）。参照の画素で同じ走査をした枠の左右端（720p は x 420〜860）と並べる。期待: 3 解像度とも幅 440 実画面 px・画面の中央・題の行の高さ 60・本文の余白 20・入力とボタンの高さが Web と同じ実画面 px（倍率で大きくならない）。外れたら `Modal.uss` か、部品の側なら `done.md` の「integrator への依頼」

- [ ] **Step 5: 差の表を書き、口を片付ける**

参照と UITK を `Read` で並べて見て、`done.md` の「## 差の表」に組ごとに（位置・寸法／色／装飾の有無／文字の段組）の表を書く。各行は「同じ／直した／LX-n／捨てる」のどれか。一目で分かる差は USS で直して撮り直す。同じにするのが大きくて差が一目で分からないものだけ、台帳「## 見た目の例外」の末尾へ `LX-77`〜`LX-80` で記帳し、該当行に `/* look-exception: LX-n */` を付ける（範囲を超えるなら止めて指揮へ番号を求める）。撮り終えたら `s3hm-compare-*` を `Object.Destroy` し、RT を `Release` し、言語を元へ戻す

- [ ] **Step 6: 直しがあればコミット**

```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"
git add moorestech_client/Assets/Scripts/Client.Ui/Screens docs/uitk/TODO.md
git commit -m "fix(uitk): ホットバーとモーダルを Web との並べ比べで揃える（S3-hotbar-modal）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```
（直しが無ければコミットしない）

---

### Task 12: unityプレイ録画テスト（`uitk/uitk-hotbar-and-bp-modal`・`building/blueprint-copy-paste-via-ui`）

**Files:**
- Create: `SKILL/scenarios/uitk/uitk-hotbar-and-bp-modal.cs`

**Interfaces:**
- Consumes: プレイテスト DSL（`p.SetupDebugEnvironment`・`p.SkipOpeningSkitIfPlaying`・`p.ExitToGameScreen`・`p.Hotbar.AssignHotbar`・`p.Hotbar.UnlockBlueprint`・`p.ClickUi`・`p.SetText`・`p.UntilUiElement`・`p.WaitUiState`・`p.WaitForBlueprint`・`p.Screenshot`・`PlaytestUiElementOps.FindUi`・`SemanticInput.MouseMoveTo`・`MouseButtonDown`・`MouseGlideTo`・`MouseButtonUp`）、`ClientHotbarDatastore`、`HotbarModel`、`IGameUnlockStateData`、`PlaytestBlockOps.UnlockBlockServerSide`、`UnlockedEventPacket.EventTag`、`BlueprintCopyPasteScenarioChecks`（既存シナリオと同じ）、`UiInputExclusivity.ActiveUiSystem`
- Produces: 録画 2 本（`result.json` が `Success: true`）

- [ ] **Step 1: 録画を書く**

`uitk-hotbar-and-bp-modal.cs`（前例 `SKILL/scenarios/uitk/uitk-mode-hud-and-tooltip.cs` の冒頭の入力準備と系の主張、`building/blueprint-copy-paste-via-ui.cs` の BP コピーの選び方と始点・終点）。流れ:
1. 冒頭で `p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}")` と `p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）")`。入力の準備は前例と同じ
2. `p.SetupDebugEnvironment(new PlaytestEnvironmentConfig())` → `p.SkipOpeningSkitIfPlaying()` → `p.ExitToGameScreen()` → `p.Hotbar.AssignHotbar(0, "木のチェスト")`・`AssignHotbar(1, "石窯")`。`FindUi("hotbar-grid")` が見つかり、`FindUi("hotbar-slot-0")` の状態名に `filled` がある（状態名は USS クラスから作られる。名前の実物は `UiElementSnapshot.StateFlags` を Note に出して合わせる）。`p.Screenshot("01-hotbar")`
3. 枠のクリックで建築モード: `p.ClickUi("hotbar-slot-0")` → `p.WaitUiState(UIStateEnum.PlaceBlock, 10f)`、`FindUi("hotbar-slot-0")` の状態名に `selected`。`p.Screenshot("02-slot-click-build")` → `p.PressKey(Key.Escape)` → `GameScreen`
4. 入替: 枠 0 の中心から枠 2 の中心へ `MouseMoveTo`・`MouseButtonDown(0)`・`MouseGlideTo(…, 0.4f)`・`MouseButtonUp(0)`（座標は `FindUi(...).ScreenRect.center`。`ScreenRect` は左下原点の画面 px で、`SemanticInput` と同じ）→ `ClientHotbarDatastore.Assignments[2]` が木のチェストの ID、`[0]` が `Guid.Empty`（`p.Until` で 5 秒）。`p.Screenshot("03-swap")`
5. 枠外で解除: 枠 2 の中心から画面の上 1/3 へ同じドラッグ → `Assignments[2] == Guid.Empty`。`p.Screenshot("04-clear")`
6. BP 名のモーダル: `p.Hotbar.UnlockBlueprint()`、前例と同じく木のチェストを置き、`checks.SelectEntry(ToolCategory, CopyToolEntry)` で BP コピーを選び、始点・終点をクリック → `p.UntilUiElement("modal-input", 10f)` → `FindUi("modal-button")` が押せない（`Clickable` でなく disabled の状態名で見る）→ `p.SetText("modal-input", "  uitk-bp  ")` → `p.ClickUi("modal-button")` → `p.WaitForBlueprint("uitk-bp", true, 15f, "BP『uitk-bp』が前後の空白を除いた名前で登録される")` → `FindUi("modal")` が見つからない。`p.Screenshot("05-bp-modal")`（入力の直後にも 1 枚）
7. 幕で取消: もう一度始点・終点 → モーダル → 幕の左上（`FindUi("modal-backdrop").ScreenRect` の左上から内側へ 5px）をクリック → `FindUi("modal")` が消え、`UIStateEnum.PlaceBlock` のまま、BP もブロックも増えない（幕の上のクリックが設置にならない）。名前を打つ間（流れ 6）にプレイヤーの位置が動かないことも `p.Note` に出して見る
7b. ESC 1 回で取消（D-11・Task 8 Step 0）: もう一度始点・終点 → `p.UntilUiElement("modal-input", 10f)` → `p.SetText("modal-input", "途中")` → `p.PressKey(Key.Escape)` を 1 回 → 次のフレームで `FindUi("modal")` が消え、`UIStateEnum.PlaceBlock` のまま（建築モードを抜けない）、ポーズが出ない（`p.Assert(uiStateControl.CurrentState != UIStateEnum.PauseMenu, …)`）、BP が増えない。`p.Screenshot("07b-esc-in-field")`。続けてもう一度始点・終点 → モーダル → 本文 `modal-message` をクリックして入力欄のフォーカスを外し（閉じないことを Assert）→ `p.PressKey(Key.Escape)` を 1 回 → モーダルが消え、`PlaceBlock` のまま・ポーズが出ない（既存のゲーム入力の経路での取消）。`p.Screenshot("07b-esc-unfocused")`
8. 解放の直後の解決（D-18）: 流れ 2 の `PlaytestEnvironmentConfig()` は `FreeBlockPlacement = true` で、解決器（`PlacementTargetResolver.UnlockedEntries`）が未解放のブロックも解決するので、まず無料設置を切る: `p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { FreeBlockPlacement = false, CreateFlatGround = false, SpawnPosition = <今のプレイヤーの位置> })`（足場は流れ 2 で作り済み。前例 `building/placement-reason-tooltip.cs` の `FreeBlockPlacement = false`）。`IGameUnlockStateData.BlockUnlockStateInfos` から未解放のブロックを 1 つ選ぶ（無ければ止めて `done.md` の「未確認」に書く）。`ClientHotbarDatastore.RequestAssign(8, その BlockGuid)` → `p.Until` で `Assignments[8]` が一致 → `HotbarModel.Current.Value.Slots[8] is HotbarUnresolvedSlot` を Assert（偽なら、無料設置の値 `DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)` と枠 8 の型を `p.Note` に出して止める。流れ 8 の主張を外して通さない）。次に `ClientContext.VanillaApi.Event.SubscribeEventResponse(UnlockedEventPacket.EventTag, _ => resolvedAtPacket = model.Current.Value.Slots[8] is HotbarAssignedSlot)` を購読し（`HotbarModel` より後の購読なので同じ配送の中で組み直しの後に走る）、`PlaytestBlockOps.UnlockBlockServerSide(ct, 名前)` → `p.Assert(resolvedAtPacket, "解放パケットの配送の中で枠 8 が解決済みになる（間引き無しの同期の組み直し）")`。偽なら間引きを足さず、購読の順とパケットの時点の値を `done.md` に実測付きで申告して止める
ファイルは 200 行未満、`#region Internal` にローカル関数（ドラッグ・枠の中心）をまとめる。200 行を超えるなら、流れ 8（解放の直後の解決）を `SKILL/scenarios/uitk/uitk-hotbar-unlock-resolve.cs` へ分ける（流れ 2 の準備〈`SetupDebugEnvironment`・スキット飛ばし・`ExitToGameScreen`〉を写し、無料設置は最初から `FreeBlockPlacement = false`）。分けたら Step 2 で 3 本を回し、R-12 の完了条件と報告ページの録画は 3 本の `result.json`（どれも `Success: true`）にする

- [ ] **Step 2: 全体スイッチが GameUi であることを確かめてから 2 本を回す**

Run:
```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); return Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Current.ToString();'
SKILL=.agents/skills/unity-playmode-recorded-playtest
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/uitk/uitk-hotbar-and-bp-modal.cs"
```
2 行目の `GameUi` を流し直してから `"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/building/blueprint-copy-paste-via-ui.cs"`
Expected: 2 本とも `result.json` が `Success: true`、Note に `系=GameUi`、録画 mp4 がある。既存シナリオの「Web キャンセル（`nameState.Cancel`）」「右短押し」で UITK のモーダルが閉じる（死活表の右短押しの行）。初回に "already playing" で落ちたら PlayMode を止めて同じコマンドをもう 1 回だけ流す。失敗したら `result.json` と `uloop get-logs --log-type Error` を読み、原因を `done.md` に書いて直す（直したらテストを回し、このステップの 2 行目から流し直す）。既存シナリオが Web 前提の操作で落ちるなら、シナリオを直さずに止めて `done.md` の「要裁定」に書く（既存シナリオの持ち主は S3-ladder の横断）

- [ ] **Step 3: 証拠を書いてコミット**

`done.md` の「## 証拠」に 2 本の `result.json` のパス・`Success`・録画 mp4 のパス・Note の `系=GameUi` の行を書く。録画とスクショの原本は worktree の gitignore 下に置いたまま。テストで `Saves/` に作ったワールドは `list-work-saves.sh` で探して消す
```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/uitk-hotbar-and-bp-modal.cs
git commit -m "test(uitk): ホットバーの操作と BP 名のモーダルを UITK の録画で通す（S3-hotbar-modal）

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```

---

### Task 13: 部分移植の範囲の記録と名前の突き合わせ

**Files:**
- Modify: なし（記録は `../moorestech_logs/harness/uitk/S3-hotbar-modal/done.md`。処遇の表には行を足さない）

**Interfaces:**
- Consumes: Task 2〜8 の `<web-test>`、`tools/uitk/test-name-parity.mjs`
- Produces: 束 3 のこのレーンの持ち分（File Structure の「写すテスト」の表）の未対応 0、束 2・3 の書式・整合の誤り 0

- [ ] **Step 1: 一部だけ写した 2 件の残りを done.md に書く**

処遇の表は「目視・録画・捨てる・写す予定」の 4 語しか受けず（`tools/uitk/testNameParity/disposition.mjs`）、`<web-test>` を付けたテストに処遇の行があると整合の誤りになる（`compareBundles.mjs`）。一部だけ写した 2 件は `<web-test>` だけで写したことを示し、残りの範囲と持ち主は処遇の表の外、`done.md` の「## 部分移植の残り」に書く:
```markdown
## 部分移植の残り
- `e2e/tests/hotbar/hotbar.spec.ts`「ビルドメニューエントリを空き枠へドラッグするとhotbar.assign{slot,id}を送る」: 本物のエントリの源（`BuildMenuEntryDragPayload`）を単独で置いて写した（`HotbarHudDropTest`）。残り＝ビルドメニューのパネル越しの通しは S3-ladder の横断（D-16）
- `e2e/tests/hotbar/hotbar.spec.ts`「blockingスキット中はホットバーHUDだけが退き、会話窓は残る」: ホットバーが退く側を写した（`HotbarHudViewTest`）。残り＝会話窓が残る側は S2-skit の持ち物（D-15）
```
（同じ 2 行を `done.md` の「integrator への依頼」に「S3-ladder の横断で 1 件目の残りを通す」の 1 行として足す。`placementIconCentering.spec.ts` のビルドメニューの件は S3-buildmenu が写す）

- [ ] **Step 2: 名前を突き合わせる**

Run:
```bash
node tools/uitk/test-name-parity.mjs --bundle 3 --emit-missing | grep -E "hotbar|modal|placementIconCentering|alwaysOnHudWheel" || true
node tools/uitk/test-name-parity.mjs --bundle 3; echo "exit=$?"
node tools/uitk/test-name-parity.mjs --bundle 2; echo "exit=$?"
```
Expected: 1 行目にこのレーンの持ち分（`hotbar.spec.ts` 6・`useHotbarDragSource.test.ts` 7・`modal.spec.ts` 4・`placementIconCentering.spec.ts` のホットバーの件・`alwaysOnHudWheel.test.ts` のホットバー帯の行と「クリックを持つスロット列だけが…」）の行が出ない。`alwaysOnHudWheel.test.ts` の他の行（S2-hud・S2-mode-tooltip・S2-tutorial・S4-inventory）は束が違えば出ない、同じ束なら出てよい。2・3 行目の終了コードは 0 か 1（他レーンの未対応・写す予定が残る。束の差分 0 は ladder が締める）。どちらかが 2（書式・整合の誤り）なら完了にしない: `<web-test>` の綴りか、タグと処遇の行の重複（Task 7 Step 6 で albof.30 の行を消したか）を直して流し直す。`alwaysOnHudWheel.test.ts` は束 2 なので `--bundle 2 --emit-missing` でも同じ grep を流し、ホットバーの 2 行が出ないことを見る

- [ ] **Step 3: done.md をコミットする**

`../moorestech_logs` の `README.md` の手順で `harness/uitk/S3-hotbar-modal/done.md` をコミット・push する（このレーンの repo には変更が無い。`<web-test>` の綴りを直したときだけ、そのテストのファイルを repo でコミットする）

---

### Task 14: moores-code-review で全ブランチレビュー（必須）

**Files:**
- Modify: レビューの自動適用が触ったファイルだけ

**Interfaces:**
- Consumes: Task 1〜13 のコミット一式（`origin/feature/uitk..HEAD`）
- Produces: レビュー記録 `../moorestech_logs/harness/` 配下（`$RUNDIR`）

- [ ] **Step 1: 必ず moores-code-review スキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）**

対象は `origin/feature/uitk...HEAD`。レビューが裁定・設計書と食い違う直し（Web と違う見た目へ寄せる・画面 USS に部品のクラスを書く・色を C# に書く・Foundation や部品を書き換える・ESC で入力欄の規則〈`GameUiInputRules`〉を書き換える〈D-11〉・ホットバーの語彙を Foundation へ上げる〈D-5〉・S3-buildmenu のファイルを書き換える〈D-6〉）を提案したら採らず、`done.md` の「要裁定」に書く

- [ ] **Step 2: 自動適用があれば検査と録画を回し直してコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"`
Expected: `ErrorCount: 0`、全件 PASS。自動適用が `HotbarModel`・`HotbarCommands`・`HotbarHudView`・`HotbarCell`・Parts の落とし先・`ModalView`・`ModalScreenPresenter`・登録ファイル・シナリオに触れたら、反映後のバイナリで Task 12 Step 2 の 2 本を再走する（ユニットテストの通過は代替にならない）。トークンに触れたら Task 10 Step 2〜3 を回し直す。続けて:
```bash
git add -A moorestech_client/Assets/Scripts "moorestech_client/Assets/UI Toolkit/GameUi/GameUiTheme.tss" docs/uitk tools/uitk .agents/skills/unity-playmode-recorded-playtest/scenarios
git commit -m "fix(uitk): S3-hotbar-modal の moores-code-review 反映

<実行ホストの指示する帰属行（Co-Authored-By 等。モデル名は実行したモデル）>"
```
（自動適用が無ければコミットしない）

---

### Task 15: feature/uitk への PR・取り込み・片付け（必須）

**Files:**
- Modify: なし（Task 14 後の HEAD をそのまま出す）

**Interfaces:**
- Consumes: Task 14 後の HEAD
- Produces: `origin/feature/uitk` のマージコミット。以後 S3-ladder（ビルドメニュー → ホットバーの横断）・S4-inventory（装備のホイールが `wheel-passthrough` を読む）が使える

- [ ] **Step 1: pr-create スキルで `base=feature/uitk` の PR を作る**

push して PR を作る（`gh pr create --base feature/uitk`）。PR 本文の先頭行は報告ページ `# https://review.moores.tech/pr/<番号>/report/` だけ。報告ページは `~/hermes-agent/data/services/pr-review/publish/README.md` の手順で、manifest は次の形（`file` は絶対パス。並べ比べは Web を先に同じ `group` へ置く）:
```json
{
  "head": "<HEAD 10 桁>",
  "summary": [
    "ホットバー 9 枠（SlotFrame のベベル上書き・--bevel-inset: inset・番号タブ・選択・未解決と受理外の減光）を UITK に移し、世界の地の上の面と減光をランタイムの画素で Web に合わせた（albof.34）",
    "割当・入替・解除の D&D を UiDragDrop の上に写し、枠の印が S3-buildmenu の IHotbarDropReceiver を実装してビルドメニューからの割当も受けるようにした",
    "モーダル（BP 名の入力）を DialogFrame・GameTextField・FormButton で UITK に移し、全体スイッチ UITK の描き手を UITK 1 本にした。実画面 px で 720p・1080p・2560×1080 を確かめた"
  ],
  "media": [
    {"file": "<uitk-hotbar-and-bp-modal の recording.mp4>", "caption": "uitk/uitk-hotbar-and-bp-modal（UITK 側）", "group": "unityプレイ録画テスト"},
    {"file": "<blueprint-copy-paste-via-ui の recording.mp4>", "caption": "building/blueprint-copy-paste-via-ui（UITK 側）", "group": "unityプレイ録画テスト"},
    {"file": "<web-869b2d4264/3-hotbar-equipment-empty.png>", "caption": "Web 3-hotbar 1280×720", "group": "3-hotbar"},
    {"file": "<compare/S3-hotbar-modal/uitk-3-hotbar-equipment-empty-1280x720.png>", "caption": "UITK 3-hotbar 1280×720", "group": "3-hotbar"},
    {"file": "<web-0e81bbe3c3-2560x1080/3-hotbar-equipment-empty.png>", "caption": "Web 3-hotbar 2560×1080", "group": "3-hotbar"},
    {"file": "<compare/S3-hotbar-modal/uitk-3-hotbar-equipment-empty-2560x1080.png>", "caption": "UITK 3-hotbar 2560×1080", "group": "3-hotbar"},
    {"file": "<web-869b2d4264/3-modal.png>", "caption": "Web 3-modal 1280×720", "group": "3-modal"},
    {"file": "<compare/S3-hotbar-modal/uitk-3-modal-1280x720.png>", "caption": "UITK 3-modal 1280×720", "group": "3-modal"},
    {"file": "<web-…-1920x1080/3-modal.png>", "caption": "Web 3-modal 1920×1080", "group": "3-modal"},
    {"file": "<compare/S3-hotbar-modal/uitk-3-modal-1920x1080.png>", "caption": "UITK 3-modal 1920×1080", "group": "3-modal"},
    {"file": "<web-0e81bbe3c3-2560x1080/3-modal.png>", "caption": "Web 3-modal 2560×1080", "group": "3-modal"},
    {"file": "<compare/S3-hotbar-modal/uitk-3-modal-2560x1080.png>", "caption": "UITK 3-modal 2560×1080", "group": "3-modal"},
    {"file": "<compare/S3-hotbar-modal/albof34-bright.png>", "caption": "明るい地の照合（albof.34）", "group": "世界の地の上の色"}
  ],
  "verification": [
    "Unityコンパイル: エラー0",
    "L0/L1: State.Hotbar・State.PlacementTarget・State.Modal・Screens.Hotbar・Screens.Modal の件数と PASS、L2（Conventions・Theme・Layers・Anchor）PASS",
    "test-name-parity 束 3: このレーンの持ち分の未対応 0・束 2/3 とも書式・整合の誤りなし（一部だけ写した 2 件の残りは done.md）",
    "albof.34: 明るい地の最大差 <n> 段（7 段以内）・暗い地 <n> 段",
    "unityプレイ録画テスト: uitk-hotbar-and-bp-modal PASS・blueprint-copy-paste-via-ui PASS（全体スイッチ GameUi）"
  ],
  "notes": ["差の表と照合表は moorestech_logs/harness/uitk/S3-hotbar-modal/done.md"]
}
```
`python3 ~/hermes-agent/data/services/pr-review/publish/publish_pr_report.py <PR番号> <manifest.json>` が失敗したら、報告 URL 無しで完了にしない

- [ ] **Step 2: feature/uitk の進みを取り込み、G1 緑を見る**

Run:
```bash
git fetch origin
git merge --no-ff --no-edit origin/feature/uitk
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "$FILTER"
git push
gh pr checks <PR番号>
```
Expected: 進んでいなければ `Already up to date.`。共有区画（`GameUiTheme.tss` の `Screens` 区画・`MainGameUiRegistration.cs` の画面登録区画・`DerivedTokens.uss` の名札付きブロックと `--outlined-count-label-color` の次の 1 行（`--outlined-count-label-stroke`。他レーンが同じ並びに行を足していれば両方採る）・`handWrittenDerivedRows.mjs` の末尾・`docs/uitk/TODO.md` の節の末尾・`docs/uitk/disposition.md` の処遇の表〈他レーンが足した行は採り、albof.30 の行は消えたままにする〉・`PauseMenuCommandsTest.cs` の `benign` の配列〈両方の値の和。片側だけを採ると良性の値が落ちる〉）の衝突は両方の行を採る。ただし縁の色のつまみは例外: 取り込みの後に `git grep -n "outlined-count-label-stroke\|OutlinedCountLabelStrokeKnobTest" origin/feature/uitk` を流し、C4-a が先に足していたら、このレーンが Task 6 Step 3 で足した 5 か所（`OutlinedCountLabel.uss` の 2 行とコメント・`.cs` のコメント・`DerivedTokens.uss` の既定 1 行・`SlotKnobOverrides.uss` の `.slot-knob-dark-outline`・`OutlinedCountLabelStrokeKnobTest.cs`）を捨てて相手の形を採る（両方採ると既定が 2 行・テストのクラスが二重になる）。名前や既定が違えば止めて指揮へ返す（`translucent-tokens.md` は採った後に `node tools/uitk/convert-web-tokens.mjs` で作り直す）。採ったら compile とテストを回し、衝突が `UiLayer`・`GameUiRoot`・Chrome/Modal 層の載せ方・`UiDragDrop`・入力の裁きに触れていれば Task 12 の 2 本を再走する。`ErrorCount: 0`・PASS。`gh pr checks` で `UITK Web follow gate (G1)` と `UITK Web follow gate self-test` が `pass`（終わっていなければ前景で `gh pr checks <PR番号> --watch`）、G1 の出力で未記帳 0。CI の `BugReportSubmitKindTest.空白本文でも種別を先に解く` の失敗は既存（moorestech-albof.22）で、マージの条件は G1 緑

取り込みの後、1 つを見る（当たれば同じ PR へコミットして push し、`$FILTER` を回し直す）:
- 錨の未移植の表: Task 7 Step 6 の `test -f … && grep …` を流し直し、`AnchorIds.HotbarHud` の行があれば消して `TutorialAnchorCoverageTest` を回す（S2-tutorial がこのレーンより後に入った場合）

- [ ] **Step 3: マージコミットで取り込む**

Run: `gh pr merge <PR番号> --merge`
Expected: マージされ、`git fetch origin && git log -1 --format='%H %P' origin/feature/uitk` の親が 2 つ（Squash しない）

- [ ] **Step 4: bd を閉じ、報告を締め、worktree を畳む**

Run:
```bash
bd close <bd> --reason "PR #<番号> を feature/uitk へマージ（ホットバー 9 枠・モーダル層と BP 名の入力）"
bd close moorestech-albof.30 --reason "PR #<番号>: ホットバーの列だけが入力を取り戻し wheel-passthrough の印を持つ（HotbarHudPickingTest）"
bd close moorestech-albof.34 --reason "PR #<番号>: 世界の地の上の空き枠の面・番号タブ・減光を明るい地のランタイム画素で Web に合わせた（台帳の実測の結論）"
git status --short
git log origin/uitk-lane/s3-hotbar-modal..HEAD
```
Expected: 3 つとも closed、`git status --short` と `git log` が空。`done.md` の 1 行目を「状態: 完了」にし、「## 証拠」に PR 番号・マージコミット・G1 のジョブ URL・各テストと録画の結果・報告ページの URL を足す。続けて、テストで `Saves/` に作ったワールドが残っていないことを確かめてから:
```bash
cd <メインのクローンの絶対パス>
moores-wt rm uitk-lane/s3-hotbar-modal --prune-branch
moores-wt status
```
Expected: worktree と Editor が畳まれ、`moores-wt status` にこのレーンの行が無い（未コミット・未 push で拒否されたら、`git status` と `git log @{u}..` を読んで残りを片付けてからもう一度。`--force` は使わない）

---

## Self-Review

1. **Requirements coverage**: R-1 → Task 2・3（`HotbarViewModelFactoryTest`）、R-2 → Task 5・6・7（`HotbarHudViewTest`）、R-3 → Task 4・7（`HotbarHudViewTest`・`HotbarHudDropTest`）、R-4 → Task 6・7（`HotbarSlotDragGestureTest`・`HotbarHudDropTest`、本物のビルドメニューの源を使う）、R-5 → Task 6・7（`HotbarHudPickingTest`）、R-6 → Task 5・10、R-7 → Task 7（`HotbarIconCenteringTest`）、R-8 → Task 4・8、R-9 → Task 8（倍率 1.5）・Task 11 Step 4、R-10 → Task 11、R-11 → Task 4・7・8・9、R-12 → Task 12、R-13 → Task 13、R-14 → Task 14・15。完了条件の各句: ホットバー 9 枠（Task 7）・`SlotFrame` のベベル上書きと `--bevel-inset: inset`（Task 7 の USS・File Structure のつまみの表）・`PlacementTargetFace`（Task 6）・割当/入替/解除の落とし先（Task 6・7）・`wheel-passthrough`（Task 6・7）・床トークン（`--hotbar-bottom`・`--hotbar-slot-*`・`--hotbar-number-tab-overhang`・`--hotbar-horizontal-offset` を USS と `HotbarCalcKnobs` で使う。Task 1 Step 1 で実在を見る）、モーダル層と BP 名（Task 8・9・12）・描き手の一本化（Task 9。Web の系の登録は MainGameUiRegistration の分岐が持つ）・`UiText.FromVerbatim` [A-40]（Task 8）・実画面 px で 720p・1080p [A-41]（Task 8・11）、写す P6・C7・P4・`placementIconCentering` のホットバーの 1 件（2 件のうち 1 件。もう 1 件は S3-buildmenu）・`alwaysOnHudWheel` の 1 件（Task 6〜8・13）、bd albof.30（Task 7・15）・albof.34（Task 5・10・15）。§6.2 の必須: 部品と公開 API（Global Constraints）・画面専用の部品の置き場（`Screens/Hotbar/Parts/`）・つまみの同名上書き（File Structure の表）・写すテスト名と件数と `<web-test>`（File Structure の表・各テスト）・撮影の解像度（Task 11）・録画（Task 12）・登録の行数 2（Task 9）・新しい共有部品の申告の手順（Global Constraints）。漏れなし
2. **Placeholder scan**: `<bd>`・`<番号>`・`<PR番号>`・`<HEAD 10 桁>`・`<sha>`・`<空きポート>`・`<値>`・`<n>`・manifest の `<… のパス>`・`FILLED`（Web の CSS から読む）・`<ビルドメニューの件の第 1 引数>`（grep で読む）は実行時にしか決まらず、取り方を同じタスクに書いた。「TBD」「適切に」は無い。ビルドメニュー（S3-buildmenu。指揮の決定どおりの形）と C3-b・S2-hud の型は想定で、Task 1 Step 1 で実物と照合し、違えば読み替えて記録する手順を書いた
3. **Type consistency**: `PlacementTargetIcon`・`PlacementTargetBlockIcon(BlockId)`・`PlacementTargetTrainCarIcon(Guid)`・`PlacementTargetConnectToolIcon(Guid)`・`PlacementTargetBlueprintIcon(Guid)`・`PlacementTargetNameOnlyIcon.Instance`・`PlacementTargetIconResolver.Resolve(IPlacementTarget, IBlueprintThumbnailLookup)`・`PlacementTargetIcons.TryToUiIcon(PlacementTargetIcon, out UiIcon)`・`IHotbarDropReceiver.Receive(HotbarDragSource)`（以上 S3-buildmenu が作る）、`UiCommandResult`・`UiCommandRejectReason.HotbarSelectNotAccepted/ModalResponseStale`・`UiCommandResultToaster.Report`、`HotbarSlotViewModel`・`HotbarEmptySlot.Instance`・`HotbarUnresolvedSlot(Guid)`・`HotbarAssignedSlot(Guid, string, PlacementTargetIcon)`・`HotbarSelection.None/Of/FromOrigin/TryGetSlot/IsSlot`・`HotbarViewModel(IReadOnlyList<HotbarSlotViewModel>, HotbarSelection, bool, bool)`・`HotbarViewModelFactory.Create(slots, origin, uiState, screen, blockingSkit)`・`HotbarSelectRule.Accepts`・`HotbarModel.Current`・`IHotbarCommands.Select（UiCommandResult）/Apply`・`HotbarCommands(ClientHotbarDatastore, UIStateControl).SelectCommandName`、`HotbarSlotDropMark(int, IHotbarCommands)`・`HotbarRowGapDropMark.Instance`・`HotbarSlotDragPayload(int, HotbarSlotViewModel, IHotbarCommands, UiCommandResultToaster)`・`Foundation.Input.WheelPassthroughMark.ClassName/Mark/IsPassthrough`・`new HotbarCalcKnobs(VisualElement)`・`HotbarCalcKnobs.ApplyToRebuiltTabs`・`HotbarNumberTab(int)`・`HotbarCell(int, IHotbarCommands, PlacementTargetIcons, UiCommandResultToaster)`・`HotbarHudView(IHotbarCommands, PlacementTargetIcons, UiCommandResultToaster)`・`HotbarHudPresenter(GameUiRoot, HotbarModel, IHotbarCommands, PlacementTargetIcons, UiCommandResultToaster)`、`ModalLogic.TryGetConfirmableText/HasConfirmableText`・`IModalCommands.Respond`・`ModalCommands(ModalRequestService).RespondCommandName`・`ModalView(ModalRequest, IModalCommands, UiCommandResultToaster, IObservable<Unit>)`・`ModalButtonVariants.From`・`ModalScreenPresenter(UiModalHost, ModalRequestService, IModalCommands, UiCommandResultToaster)` は定義と使用（テスト・View・Presenter・登録・EDC・録画）で同じ綴り。USS のクラス `hotbar-hud`・`__row`・`__row--select-disabled`・`__cell`・`__cell--first`・`__cell--unresolved`・`__number-tab`・`__number`・`modal-view`・`__message`・`__input`、つまみ `--hotbar-hud-row-offset`・`--hotbar-hud-number-tab-overhang` とトークン 8 つは C#・USS・テストで一致。testid（`hotbar-hud`・`hotbar-grid`・`hotbar-slot-{i}`・`modal`・`modal-title`・`modal-message`・`modal-input`・`modal-button`・`modal-backdrop`）は Web の `data-testid` と同名
4. **保留・縮退経路**: (a) 選択を受けない UI 状態のタップ → `HotbarCommands.Select` が良性の拒否を返し、源が `UiCommandResultToaster` へ渡して LogWarning（D-20）。(b) 空き枠のドラッグ → `HotbarSlotDragPayload` が `silent-ok` 付きで何もしない（Web と同じ。テストが固定。D-26）。(c) 列の隙間の落とし → `HotbarRowGapDropMark.Receive` が `silent-ok` 付きで無操作（受けたので源は解除しない。テストが固定）。(d) 他の画面の印 → 型が `IHotbarDropReceiver` でないので Foundation が飛ばし `OnDroppedOutside` になり、枠外として解除（枠の源）・無操作（ビルドメニューの源。S3-buildmenu の `silent-ok`）。(e) つまみが読めない → `UssCustomStyleReader` が LogError（`HotbarCalcKnobs`）。(f) モーダルの空白だけの確定 → ボタンが押せず、それでも来たら `ModalView` が LogWarning（D-12）。(g) 古い応答 → `ModalCommands` が良性の拒否 `ModalResponseStale` を返しトースト口が LogWarning。(g2) ESC 以外でフォーカスが外れた（欄の外の押下・閉じたときの外れ）→ `ModalView.CancelIfEscapeBlurred` が `silent-ok` で取り消さない（`ModalScreenPresenterTest` の「欄の外を押しても閉じない」が固定）。フォーカスの無い ESC は View が聞かず、既存のゲーム入力の経路が取り消す（D-11。録画の流れ 7b）。(h) 同じ要求の通知の重なり → `silent-ok` 付きで描き直さない（D-13）。(i) Task 1・7・8・9・10・11・12 の「止める」経路は、解消の主体（指揮・integrator）と記録先（`done.md` の「integrator への依頼」「要裁定」「未確認」）を書いた
5. **「決定的にする」を根拠にした選択**: D-13（要求ごとに View を作り直す）は Web の `key={id}` の写し。D-18 は間引きを撤回し、順序は購読の順（実物の行）と録画の 1 段の実測で確かめる。ほかに無し
6. **呼び出し側への責務漏出**: 選択の受理は `HotbarSelectRule` の 1 か所を Model（減光）と操作（受理）が読む（D-4）。落としの解き方は印（State の `IHotbarDropReceiver` を実装）が持ち、源はビルドメニューもホットバーも `Receive` を呼ぶだけ（D-33）。絵の出どころの解き方と絵への変換は S3-buildmenu が作る `PlacementTargetIconResolver`・`PlacementTargetIcons` の 1 か所をビルドメニューと共有（D-6）。確定の文の規則と空白の除去は `ModalLogic.TryGetConfirmableText`・`HasConfirmableText` の 1 か所を View・ブリッジが読み、View の可否は 1 回だけ導く（D-12・D-39）。応答と選択は [A-8] のコマンドで返し、拒否はトースト口 1 本（D-20・D-34）。ボタンの色の写しは `ModalButtonVariants` の 1 か所（[A-28]）。部品の見た目はつまみで変え、画面 USS は部品のクラスを書かない（番号タブの暗い縁は、このレーンが部品に足す縁の色のつまみで渡す。D-7）

---

## 判断記録（ADR）

従う裁定・設計: Global Constraints の正本、設計書 `docs/uitk-migration/execution/component-architecture.md`（§1.1・§1.2・§2.3・§2.5・§3.2・§3.3・§4・§5.1、[A-8]・[A-28]・[A-40]・[A-41]・[A-42]・[A-47]・[A-52]・[A-56]・[A-57]・[A-58]・[A-78]・[A-84]）、実行計画 `docs/uitk-migration/execution/execution-plan.md`（§1.4 S3-hotbar-modal 行・§4.3・§5・§6.2・E-1・E-13）。設計書へ足す行の案（A-105〜A-109）は本体の scratchpad の `s3-hotbar-modal-design-rows.md` に置いた（指揮が設計書 §7.1 へ写す）。

| ID | 判断 | 棄却した案と理由 | 出所 |
|---|---|---|---|
| D-1 | ホットバーは `UiLayer.Chrome`・`UiFamily.Viewport` に置く | `Stage` 族: Web の `.viewportOverlay`（z31）は実画面いっぱいで、帯は実画面の下端に付く。`OverlayPanel`: Web の重なりは 31 | agent 既定（前例 S2-mode-tooltip D-1） |
| D-2 | モーダルは Model を作らず、`ModalRequestService` の `OnPendingChanged` を Presenter が購読する | `ModalModel` を挟む: 状態の正が既に State にあり、写すだけの層になる | agent 既定（[A-57]・[A-8]） |
| D-3 | 枠の中身は判別 union（空き・未解決・解決済み）で持つ | `Guid?` と bool の組: 未解決と空きの白面の違いを分岐で読み直すことになる | agent 既定（前例 `HotbarDropAction.cs`） |
| D-4 | 選択の受理の規則は State の `HotbarSelectRule` 1 か所で、Model の減光と `HotbarCommands` の受理が同じものを読む | View が受理を判断: Web は `uiStateAcceptsHotbarSelect` と `HotbarSelectActionHandler` に二重に持つ。UITK では 1 か所にする | agent 既定 |
| D-5 | 落とし先の印（`HotbarSlotDropMark`・`HotbarRowGapDropMark`）は `Screens/Hotbar/Parts/` に置き、State の `IHotbarDropReceiver`（S3-buildmenu が作る）だけを実装して `UiDragDrop.MarkTarget<IHotbarDropReceiver>` で付ける。枠の源 `HotbarSlotDragPayload` も `IUiDragPayload<IHotbarDropReceiver>` で、`OnDropped` はホットバーの印にだけ来るので `receiver.Receive(source)` を呼ぶだけ（入替か無操作かは受け手の多態で解く）。他の画面の印は Foundation が飛ばし `OnDroppedOutside`（解除）になる | Foundation/DragDrop へ置く: ホットバーの語彙で汎用基盤にドメイン語彙を入れることになる。ビルドメニューの側へ置く: 落とし先の持ち主はホットバー。中身の無い印 interface と `IHotbarDropReceiver` を両方実装し源が `is` で確かめる: 外れたら黙って枠外になる（A-132） | agent 既定（設計書 §5.1-2・AGENTS.md の設計原則）。I-fix3 で C3-b 要裁定 3 を推奨 A で反映（設計書 A-132） |
| D-6 | 配置対象の絵の出どころ（ID だけの `PlacementTargetIcon` 系・`PlacementTargetIconResolver`・DI のクラス `PlacementTargetIcons.TryToUiIcon`）と State の口 `IHotbarDropReceiver.Receive` は S3-buildmenu が最終の置き場に作り、このレーンは使うだけ。S3-buildmenu のファイルは書き換えない | このレーンが S3-buildmenu の型を `git mv` で昇格し呼び側を書き換える: 並走中の他レーンのファイルに手を入れ、取り込みの衝突と置き換え漏れの穴を作る。ホットバーに別の解き方を書く: 同じ規則（Web の橋 `ResolveIconUrl`）が 2 つになる | 指揮の決定 |
| D-7 | 番号タブの暗い縁は、部品 `OutlinedCountLabel` に縁の色のつまみ `--outlined-count-label-stroke`（既定 `--icon-text-stroke-light`）（C4-a 計画 D-5・設計書 A-97 と同じ名前・既定。どちらのレーンが先でも同じ 1 本になる）を足し、タブでそのつまみを `--icon-text-stroke-dark` に上書きして出す。部品の L1（`OutlinedCountLabelStrokeKnobTest`）を同じタスクで足す。つまみの既定は末尾の名札付きブロックでなく、部品の既定の並び（`DerivedTokens.uss` の `--outlined-count-label-color` の次）に置く（部品の既定は部品の行の隣に置き、画面のブロックに部品の既定を混ぜない） | タブの部分木で共有トークン `--icon-text-stroke-light` を受け直す: トークンの意味（白い縁）を部分木で裏返し、タブの中に後から置く他の部品の縁まで黒くなる。画面 USS で `.outlined-count-label--stroke` を書く: 画面 USS に部品のクラスを書くのは規約違反。integrator へ依頼して待つ: 既存部品へのつまみ 1 つの追加で、C3-a はマージ済み。同じつまみが要る C4-a とは「先に入ったほうが足し、後は使うだけ」で衝突を避ける（設計書 A-108・実行計画の C4-a 行。Task 6 Step 3 の頭で `git grep` して分岐） | agent 既定（設計書 §3.2「部品の見た目はつまみで変える」） |
| D-8 | Web の `calc()`（タブの `top`）と `translateX`（列の横ずれ）は `HotbarCalcKnobs` が customStyle から読んで当てる | 換算値を USS に直書き: トークンの変更に追従しない | 設計書 [A-47] |
| D-9 | albof.34 の照合は、複製した `GameUiPanelSettings` の RT（ランタイムと同じ Linear 合成）で明るい地と暗い地を描いて画素を読み、Web の sRGB 合成の式の値と比べる。明るい地で 7 段以内を合格とする | 部品ギャラリーで比べる: sRGB 合成なので差が出ない。Web を実際に撮って比べる: 世界の絵が地になり画素が定まらない | 指揮の判断（albof.34 の回送）→ agent 既定（C3-a の基準 7 段） |
| D-10 | 退く間（blocking スキット・常駐 HUD を出さない画面）は帯ごと `display: none` にする | 子を消す: Web は `null` を返して木から外す。UITK は作り直しを避けて隠すだけで、見た目と入力は同じ | agent 既定 |
| D-11 | モーダルは ESC 1 回で取り消す（Web の Mantine と同じ）。入力欄では `GameUiInputRules` が ESC を消費してフォーカスを外し、`ModalView` は入力欄の `FocusOutEvent` を、このフレームの ESC が消費済みのときだけ取消として読む（押下でフォーカスが外れただけなら閉じない）。フォーカスの無い ESC は View が聞かず、既存のゲーム入力の経路（`PlaceBlockState` の `CloseUI` → `TryCancelInProgressOperation` → `BlueprintNameInputState.Close` → ブリッジの `CancelPendingRequest`）が 1 回で取り消す。変換中の ESC は規則がフォーカスを外さないので閉じない | 1 回目はフォーカスだけ外し 2 回目で閉じる: Web と押す回数が違う（ゴールの操作面から外れる）うえ、入力欄の ESC は規則が Blur と `StopImmediatePropagation` をするので 2 回目の `KeyDownEvent` は View に届かない（`GameUiInputRules.cs` を読んで確かめた）。View がパネルの根で `KeyDownEvent` を聞く: GameUi は入力欄以外にフォーカスを持たせないので届かず、届いても入力の第 2 経路になる（設計書 A-83）。規則に「モーダルでは閉じる」を足す: Foundation の規則にモーダルの語彙を持ち込む | agent 既定（ゴールに合わせる・前例 A-83） |
| D-12 | 確定の文の規則は `ModalLogic.TryGetConfirmableText`（可否と除去後の文を 1 回で導く）と `HasConfirmableText` の 1 か所。ブリッジは `HasConfirmableText` で二重に守り、空白の除去は状態側（`BlueprintNameInputState.Confirm`）の既存の `Trim` のまま。負債が残る: 空白の規則が `ModalLogic` と `BlueprintNameInputState.Confirm`（`Client.Game`）の 2 か所にある。`Client.Game` は `Client.Ui.State` を参照できないので、このレーンで 1 か所に寄せられない。寄せるのは段階 7 の uGUI 撤去で `BlueprintNameInputState` を `Client.Ui.State` へ移すとき | View だけで守る: ブリッジが別の描き手（Web）からの文も受ける。状態側の検証を外す: ブリッジ以外の呼び手が無くても、状態の不変条件（空白の名前を確定しない）を状態自身が持たなくなる | agent 既定 |
| D-13 | モーダルの View は要求 1 件につき 1 つ作り、入力の持ち越しを構造で無くす。入力付きは載った次のフレームでフォーカスする | 1 つの View を使い回して ID の変化で空にする: リセット漏れの分岐が要る。Web は `key={id}` で作り直す | agent 既定（Web の写し） |
| D-14 | `placementIconCentering.spec.ts` はホットバーの件だけを写す。ビルドメニューの件は S3-buildmenu が写す | このレーンで写す: ビルドメニューの画面を組まないと主張が立たない | 指揮の決定 |
| D-15 | `hotbar.spec.ts` 5 件目は、ホットバーが退く側だけ写す。会話窓が残る側は S2-skit | 両方写す: 会話窓は S2-skit の画面 | agent 既定 |
| D-16 | `hotbar.spec.ts` 3 件目と C7 のエントリの件は、S3-buildmenu の本物の `BuildMenuEntryDragPayload` を単独で画面に置いて写す（使うだけ） | テスト用の代わりの源: 本物がマージ済みで、代わりは本物とずれ得る。ビルドメニューのパネルごと載せる: 横断は S3-ladder の持ち物 | 指揮の決定 |
| D-17 | Model の配線の L1 は持たず録画で見る。解放パケットの直後に枠が解決済みになること（D-18 の順序）は、L1 でなく録画の 1 段（Task 12 流れ 8）でパケットの配送の中の値を読んで確かめる | 源の偽を全部作る L1: 源が Client.Game の具象（MonoBehaviour・ネットワークの購読を含む）で、偽ではパケットの配送の順を再現できない | agent 既定（前例 S2-mode-tooltip。指揮の「L1 に 1 件」を本物の源で満たす） |
| D-18 | 源の変化ごとに同期で組み直す（間引きは持たない）。解放の事象の順は、解放状態の持ち主 `ClientGameUnlockStateData` が ctor で同じタグを先に購読しているので、Model が読む時点で更新済みの見込み（`ClientGameUnlockStateDatastore.cs:112`。推測で、録画の 1 段で確かめる）。崩れたら間引きを足さず `done.md` に実測付きで申告 | フレーム末に 1 回だけ組む（Web HotbarTopic の写し）: [A-57] の同期の組み直しと前例（`ModeHudModel`・`BuildMenuModel`）から外れ、順序の懸念は実測でない | agent 既定（[A-57]） |
| D-19 | 絵と名前は ViewModel に値（ID・表示名・ID だけの `PlacementTargetIcon`）で運び、変化は等価の外れで描き直す。Texture は運ばない。BP の絵だけは参照の等価（S3-buildmenu）で、`HotbarAssignedSlot.Equals` が `Icon.Equals` を比べるので、撮り直し（`OnThumbnailChanged` → 組み直し）のたびに等しくない VM が配られ絵が替わる（L0 の等価のテストと L1「同じBPの撮り直しで枠の絵が替わる」が固定）。版の数（`PresentationRevision`）は持たない | 版の数を足す: 値で運べば不要になり、増やし忘れの穴が残る | agent 既定 |
| D-20 | 枠の源のタップは常に `Select` を呼び、受理は `HotbarCommands` が規則で決める。受けない状態は良性の拒否 `HotbarSelectNotAccepted`（wire `invalid_state`。Web `HotbarSelectActionHandler` と同じコード）を返し、源が `UiCommandResultToaster` へ渡す（良性なのでログだけ） | View が減光中はタップを出さない: 受理の判断が 2 か所になる。`void` のままログ: [A-8] のコマンドの形から外れる | agent 既定（[A-8]） |
| D-21 | `WheelPassthroughMark` は最初から `Foundation/Input` に置き、印（`Mark`）と読み手の判定（`IsPassthrough`。Web `isWheelPassthrough` の `closest`）を同じファイルに持つ。判定を使う装備のホイールは S4-inventory | 画面内に置いて 2 機能目で昇格: Web で既に 2 機能（`activeLayer.ts:22`・HotbarPanel・EquipmentPanel）が使うので §5.1-1 に当たる。印だけ置いて判定は S4-inventory: 読み手の無い印は L1 が印の有無しか見られず、Web の判定（祖先をたどる）と同じかを確かめる場所が無い。新規ファイルで他レーンと重ならない | agent 既定（§5.1-1・[A-58]） |
| D-22 | 登録は画面ごとに 1 行（2 行）。`HotbarCommands` はホットバーの登録に入れる | State の登録へ入れる: GameUi の系でしか使わない | agent 既定（実行計画 §6.2） |
| D-23 | モーダルの並べ比べは 720p・1080p・2560×1080。1080p の Web 参照は master の worktree で撮る | 1080p を省く: 完了条件が 720p・1080p を名指す | 完了条件（[A-41]） |
| D-24 | L3（EditModeInPlayingTest）は持たない | — | 実行計画（段の締め S3-ladder） |
| D-25 | 操作の偽は文字列で記録する（`RecordingHotbarCommands`） | モックの枠組み: Web の `payloadsOf` と同じ読み方で十分 | agent 既定 |
| D-26 | 空き枠をドラッグに確定させた後は何もしない（タップにも落とさない） | 解除を送る: 空き枠に解除するものが無い。タップに落とす: 建築モードを抜けてしまう（Web のテスト 6 件目） | Web の写し |
| D-27 | 番号タブは拾う（タブの上で離しても枠の落とし先になる） | 拾わない: Web の pointer-events は既定で、タブの上で離すと `closest` が枠を返す | Web の写し |
| D-28 | 枠の間は 2 枠目以降の `margin-left` と `--first` のクラスで写す | `gap`・`:first-child`: UITK に無い・規約で禁止 | 規約 |
| D-29 | モーダルの幅は `DialogFrame` の `--modal-size`（440px）のまま上書きしない | Web の `w={320}` に合わせる: Mantine の flex-basis に負けて実寸は 440（参照の実測 x 420〜860） | C3-b D-11 と参照の実測 |
| D-30 | 未解決の枠は白面（`--filled`）のまま中身を出さず、枠ごと減光する | 空き枠と同じ面: 外せる割当があることが見えない（Web の data-unresolved） | Web の写し |
| D-31 | 並べ比べの地は参照から読んだ単色にする | 実ゲームの世界を地にする: 世界の絵が参照と違い差の判定ができない | 前例 S2-mode-tooltip |
| D-32 | 半透明 3 色は `handWrittenDerivedRows.mjs` の前計算の表へ載せて再生成し、照合で決め直したら `uss:` と決め方の文に書き換える | 手で書くだけ: 生成器の照合から外れる | 前例 S2-hud [A-55]・`--slot-frame-face` |
| D-33 | 落としは印が `IHotbarDropReceiver.Receive` で受けて自分で解き、操作（`IHotbarCommands`）は印が持つ。断る組は印の側（`HotbarNoDropAction` を `HotbarCommands` が silent-ok で受ける）。枠の源は印がホットバーの口でなければ自分で枠外の解除をする。隙間の印は受けて無操作 | 源が操作を持って解く: ビルドメニューの源にホットバーの操作を渡すことになる。解き方の中継（`HotbarDropDispatch`）を挟む: 口が State にあれば型の判定 1 行で済む | 指揮の決定 → agent 既定 |
| D-34 | モーダルの応答は `Client.Ui.State/Modal/IModalCommands`（`UiCommandResult Respond(ModalResponse)`）にし、`ModalView` が確定・ESC・幕の押下（`UiModalHost.OnBackdropClicked` を ctor で受けて購読）で直接呼ぶ。Presenter は表示と撤去だけ。古い応答は良性の拒否 `ModalResponseStale`（wire `no_pending_modal`。Web `ModalRespondActionHandler` と同じ）でトースト口がログに残す | View の `OnResponded` を Presenter が `ModalRequestService.Respond` へ渡す: [A-8] のコマンドの形から外れ、取消と確定で返し方が 2 本になる。幕だけ Presenter が呼ぶ: 応答の呼び手が 2 か所になる | agent 既定（[A-8]） |
| D-35 | 未解決の割当は ID だけを持ち、名前を出さない | 前の名前を覚えて出す: Web も出さない（`unresolved` は id だけ） | Web の写し |
| D-36 | 名前だけの絵は判別子で描き分ける。`PlacementTargetIcons.TryToUiIcon` が偽（NameOnly・絵の欠け）なら枠が `SetNameTarget`、真なら `SetIconTarget`。null の判定は持たない | `UiIcon` の null で分ける: 判別子で決まることを値の有無で読み直す | 指揮の決定（S3-buildmenu の API） |
| D-37 | 枠の源には bool の割当の有無でなく `HotbarSlotViewModel` を渡し、掴める物の判定は `TryGetDragSource` の 1 か所に置く | `bool assigned` を渡して `OnDropped`・`OnDroppedOutside` で 2 回判定: 同じ判定が 2 か所になる | agent 既定 |
| D-38 | 表示名が空の対象は、Web と同じく空の名前をそのまま出す（代わりの文を出さない）。Web は `HotbarTopic` が BP だけ原文を運び、他はクライアントが GUID のキーで引き、`PlacementTargetFace` は `displayName` をそのまま描く | 代わりの文（ID 等）を出す: Web と違う見た目になり、空の名前は BP 名の確定規則（空白だけを弾く）で通常は起きない | Web の写し |
| D-39 | モーダルの確定の可否は文の変化ごとに `ModalLogic.TryGetConfirmableText` で 1 回だけ導き（`_confirmable`・`_confirmedText`）、ボタンと確定の両方が読む。空白の除去は `ModalLogic` の private `Trim` の 1 か所 | View が `Trim` と `CanConfirm` を別々に呼ぶ: 同じ導出が 2 本になる | agent 既定 |
| D-44 | 独立評価 5 巡目（opus）の反映: (a) Task 15 の取り込みの後に縁の色のつまみの分岐を確かめ直し、C4-a が先に入っていたら自分の 5 か所を捨てて相手の形を採る（両方は採らない） (b) 参照の地を読む点を列の外（348,692）へ (c) 空き枠の early return 2 か所の直前に silent-ok (d) 入力欄にフォーカスがあるまま外から閉じたときにゲームのキー入力が戻ることを L1 で主張 (e) 減光の値は α → 未解決の opacity → 受理外の opacity の順で決め、7 段を超えたら止める | — | agent 既定（opus 5 巡目） |
| D-43 | 再検査 3 の反映: `ModalLogic.CanConfirm` は本番の呼び手がテストだけなので消し（デバッグ/テスト専用 public を残さない）、既存の `ModalLogicTest` 2 件を `TryGetConfirmableText` で書き換える。空白の判定は private `IsConfirmable` の 1 か所、`Trim` は 1 経路 1 回 | `CanConfirm` を委譲で残す: テスト専用の public が残る | agent 既定（再検査 3） |
| D-42 | 独立評価 4 巡目の反映: (a) 縁の色のつまみは C4-a と先に入ったほうが足し、後は使うだけ（Task 6 Step 3 の頭で `git grep` して分岐。実行計画の C4-a 行と設計書 A-108 に同じ規則） (b) 解放の録画の流れ 8 は無料設置を切ってから未解決を作る (c) 減光の照合を空き枠・割当済みの白面・未解決の重なりの 3 つで測る (d) `Registration/GameUi/` が 11 本以上なら integrator の再編が入るまで完了・マージしない (e) 録画が 200 行を超えたら流れ 8 を別シナリオへ (f) 番号タブの数字の縦位置を並べ比べで 1px 以内 | (a) このレーンだけが足す: 先に走る C4-a が同じ部品の同じ行を書いてぶつかる (b) 既定の無料設置のまま: 未解放のブロックも解決され、未解決の枠が作れない | agent 既定（Codex 4 巡目・opus 4 巡目） |
| D-41 | 独立評価 3 巡目と再検査 2 の反映: (a) 一部だけ写した 2 件は `<web-test>` だけを付け、処遇の表に行を足さない。残りの範囲は `done.md` の「## 部分移植の残り」（処遇の語は 4 つだけで、タグと処遇の行の併存は整合の誤り） (b) albof.30 の「写す予定」の行は、`HotbarHudPickingTest` にタグを付ける Task 7 のコミットで消し、束 2・3 の突き合わせで終了コード 2 が無いことを完了の条件にする (c) `ModalLogic.CanConfirm` は `TryGetConfirmableText` に委ね、`TryGetConfirmableText` は `HasConfirmableText` を通す（空白の規則を 1 か所に。Phase 2.6 弱 1 件の採用） (d) `PauseMenuCommandsTest` を `$FILTER` と取り込みの衝突の一覧へ | (a) 処遇「一部」: ツールが書式の誤りで落とす (c) 判定を 2 本のまま: 片方だけ直すと食い違う | agent 既定（Codex 3 巡目・再検査 2） |
| D-40 | `HotbarCell` の面へ当てる分岐（絵か名前か）は枠が持ち、`PlacementTargetIcons` は値（`UiIcon`）を返すだけ | Content が面に当てる: Content が部品の面を知ることになる | 指揮の決定（S3-buildmenu の API） |

---

## 未確認

- C3-b の公開 API と、S3-buildmenu が作る型（指揮の決定どおりの形。`PlacementTargetIcons` の ctor の引数は想定）・テストの補助（`RecordingBuildMenuEntryHandler`）は想定。Task 1 Step 1 で実物と照合する
- 撮り直しの描き直しは L1 で `Render` に新しい VM を渡して見る。Model が `OnThumbnailChanged` で組み直して配ることは Task 12 の録画（BP の絵が出る）で見る
- フォーカスの無い ESC が既存のゲーム入力の経路で 1 回で閉じ、ポーズが開かないこと（Task 8 Step 0 で経路をコードで確かめ、Task 12 の録画の流れ 7b で実入力で見る）。本番で建築モードの外からモーダルを出す呼び手が増えたら、その経路は無い（Step 0 の 4 行目で見張る）
- D-18 の順序（解放状態の更新がホットバーの組み直しより先）は推測。Task 12 の流れ 8 で実測する
- `ui-modal-host--shown` が `modal-scope` に付くこと（C3-b の計画から写した）
- `TextField.Focus()` の後の `focusController.focusedElement` が `TextField` か内側の要素か（主張は両方を受ける）
- 番号タブの枠の明るい半透明（α^2.2 の式。未実測）は Task 11 の並べ比べで初めて見る
- 暗い地で albof.34 の補正が外れる幅（Task 10 の実測まで不明。外れたら LX-76）

## 本PR外のリファクタ提案

- Web 側の `HotbarSelectActionHandler` と `uiStateAcceptsHotbarSelect` が受理の規則を二重に持つ（UITK は `HotbarSelectRule` 1 か所）。切替（5a）で Web が消えるまで残る
- `ModalRespondActionHandler` も `IModalCommands` を受ければ、古い応答の写しが `ModalCommands` 1 本になる（前例 [A-84]）
- `HotbarActions.cs:33`（Web の `HotbarSelectActionHandler` の受理の条件）と `HotbarSelectRule` が同じ規則を 2 か所に持つ。`HotbarSelectRule` の前例自体が Web 側の漏出形（ハンドラが状態を直に読む）なので、Web ホストのハンドラも `IHotbarCommands` を受ける形（前例 S2-skit [A-84] の `SkitActionHandlers`）にすれば 1 か所になる
- `HotbarTopic.cs:111-130`（`ResolveSlotDto`）と `HotbarModel.ResolveSlot` が空き・未解決・解決済みの分け方を 2 か所に持つ。切替（5a）で Web の橋が消えるまで残る
- `BlueprintNameInputState.cs:39-40`（`Client.Game`）の空白の検証・除去と `ModalLogic.TryGetConfirmableText`・`HasConfirmableText` が同じ規則を 2 か所に持つ。`Client.Game` から `Client.Ui.State` は参照できないので、1 か所にするには段階 7 の uGUI 撤去で `BlueprintNameInputState` を `Client.Ui.State` へ移す（D-12 の負債）
- 絵か名前かを面へ当てる分岐（`TryToUiIcon` の真偽で `SetIconTarget`／`SetNameTarget`）が、ホットバーの `HotbarCell` とビルドメニューで同じ形になる。3 機能目が来たら 1 関数にまとめる

レーンの起動は指揮が行う。
