# Task 10 レポート: webuiコンポーネント実装（様式準拠の全面書き換え）

## ステータス
完了。コミット `617c650cd`。

## 実装ファイル（全て `moorestech_web/webui/src/features/buildMenu/`）
- `BuildMenuSlot.tsx`（36→34行, rewrite）: SlotFrameベース。64px直書き・生onClick・Mantine Tooltip全廃。testid `build-menu-entry-${entryType}-${entryKey}` と tutorialAnchor（`build-menu.entry-...`.toLowerCase()）維持。`export default`→named export へ変更。
- `BuildMenuSearchInput.tsx`（20行, new）: §8.9様式。素input+`--gauge-track`背景。`data-testid=build-menu-search`。
- `BuildMenuDetailPreview.tsx`（34行, new）: §8.7固定高。ホバー中エントリ→無ければ案内テキスト（2段フォールバック無し）。requiredItemsをItemSlot（itemId+count）で表示。下端FadeRule。`data-testid=build-menu-preview`。
- `BuildMenuCategoryGrid.tsx`（44行, new）: サブカテゴリ見出し+`SlotGrid cols={8}`。検索中は複合見出し。`data-testid=build-menu-section-${category}-${subCategory}`。
- `CategorySidebar.tsx`（26行, new）: 縦ModeSwitch。`testId=build-menu-sidebar`、各選択肢 `build-menu-category-${name}`、disabled=検索中。
- `BuildMenuPanel.tsx`（74行, rewrite）: GamePanel化・状態束ね（selectedCategory/query/hovered）。純関数4つで導出。`build_menu.select`/`blueprint.delete`/`ui_state.request` 契約不変。
- `style.module.css`（126行, rewrite）: %指定・ハードコード色ゼロ。`.panel`=viewer-start/items-end・759.3px/525px（.panelLarge前例）、§8.10ネイビースクロールバー、§8.9フォーカスoutline。

## テスト結果
lint 0 errors（残警告1件はSkitPresentation.tsxの既存unused-disable、非当該）／vitest 359 passed（61ファイル）／build（tsc -b && vite build）成功。

## マウント位置
App.tsx変更不要。BuildMenuPanelは既に `App.tsx:92` の stage グリッド内（ResearchTreePanel:91と同一階層）にマウント済み。旧CSSの`position:fixed`をやめ`.panel`に`grid-column: viewer-start / items-end`を付与したことでグリッド参加が有効化される。

## 制約充足
全ファイル200行以下。buildMenuディレクトリ=10ファイル（上限ちょうど）。Mantine残存はScrollAreaのみ（§8.10許可、CloseButton/Tooltip/Title/Groupゼロをgrep確認）。表示リテラルはt()経由（検索/カーソル案内/ビルドメニュー/閉じる/該当なし）、カテゴリ・サブカテゴリ・entry.labelはraw。

## 懸念
1. **サブカテゴリ区切りのFadeRule省略**: §8.11は「サブカテゴリ見出し=--text-mutedラベル + FadeRule」と記すが、briefが「FadeRuleはPreview内に含めた」と明示的に指示したため、セクション間はラベル付き見出し + `.gridArea`の`gap:12px`で区別している（§4の無札並置禁止は各群が見出しを持つため充足）。briefに従ったが、厳密な§8.11文言との差分として記録。
2. **複合見出しのlint回避**: `${category} / ${subCategory}` の " / " が no-jsx-visible-literal に該当したため、見出し文字列をJSX外のローカル関数 `sectionHeading()` で組み立てた（マスタ由来のためt()不要）。
3. **既存e2e** `e2e/tests/regression/buildMenu.spec.ts` は旧UI前提で未修整（Task 11で再構成予定、本タスクscope外）。
4. **目視QA未実施**: SKILL §10のmockホストスクショ確認は未実行。端のフェード帯干渉・中央対称・グリッドはみ出しは実画面での確認が望ましい。

## Fix: 見出しFadeRuleの追加（レビュー指摘対応）
懸念1で記録した省略はレビューでImportant指摘として差し戻された。webui-design SKILL §8.11・design spec §4.1の通り、サブカテゴリ見出しの様式は「--text-mutedラベル + FadeRule」であり、Preview内のFadeRuleとは別に各見出し直下にも必要と判断し追加した。

- `BuildMenuCategoryGrid.tsx`: `FadeRule`を`@/shared/ui`からimportし、`<h3 className={styles.sectionHeading}>`直後に`<FadeRule />`を追加。`section`要素に`className={styles.section}`を付与（縦flexでheading/rule/gridを束ねる）。
- `style.module.css`: `.sectionHeading`の`margin: 0 0 4px`を`margin: 0`に変更（gapで間隔管理へ移行）。新規`.section`（`display:flex; flex-direction:column; gap:6px`、固定px・%指定なし）を追加し、見出し→罫線→グリッドの間隔を統一。

### 実行コマンドと結果
- `npm run lint` → 0 errors（既存の無関係な警告1件のみ、SkitPresentation.tsx）
- `npm run test -- --run src/features/buildMenu` → 9 tests passed（1 file）
- `npm run build` → tsc -b && vite build 成功

---

# [別タスク] moorestech_web の契約更新と dist 反映（energizedRangeVisible削除）

上記は本ファイルに既存だった別タスク（webuiコンポーネント実装）のレポート。以下は今回のタスク「moorestech_web の契約更新と dist 反映」の報告。ファイル名衝突のため追記した（team-leadへ確認要）。

## 重要な訂正（brief の前提を上書き）

`moorestech_web` は独立リポジトリではなく、`/Users/katsumi/moorestech` と `tree1` ワークツリーが**同一 git リポジトリ内で共有するトラック済みディレクトリ**。各ワークツリー（メインチェックアウトの `master` と `tree1` ブランチ）はそれぞれ独立した `moorestech_web/webui` の作業コピーを持つため、片方での編集はもう片方に自動反映されない。

さらに `moorestech_client/Assets/StreamingAssets/WebUi/dist` は **完全に gitignore 対象**（`.gitignore:127`）で、Unity の `WebUiProductionArtifactBuilder.cs`（`IPreprocessBuildWithReport`）が Player ビルド直前に `pnpm build` を自動実行し再生成する仕組み。tree1 には `dist` ディレクトリ自体が存在しなかった（未ビルド）。したがって「dist をビルドしてコピー・コミット」という手順は不要と判断し、**両ワークツリーそれぞれの `moorestech_web/webui` ソースに同一の編集を直接適用してコミット**する方式に変更した。

## grep 列挙（全ヒット、5箇所）

```
webui/src/bridge/contract/validators.test.ts:13
webui/src/bridge/contract/schemas/ui.ts:33
webui/src/features/modeHud/PlacementModeHud.tsx:24
webui/e2e/mock-host/topics/topicFixtures.ts:24
webui/e2e/mock-host/topics/topicControls.ts:22
```

上記5箇所を両ワークツリー（main repo / tree1）で同一に削除。5箇所とも1行の機械的削除だったため codex への委譲は行わず直接編集した。

### 触れなかった箇所（意図的）
`/Users/katsumi/moorestech/moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/placement_mode.json` および対応する C# 側（`WireContractC2Test.cs` / `PlacementModeTopic.cs`）は **master ブランチでは未移行**（C#側の `EnergizedRangeVisible` 撤去は tree1 ブランチでのみ実施済み）。master 側のテストはこのフィクスチャに `energizedRangeVisible: true` を明示的に期待しているため、変更すると master 自身のテストを壊す。zod スキーマはデフォルトで未知キーを無視するため、フィールド削除後も `npm test` は master 側フィクスチャに対して問題なく PASS した（実測済み、下記）。

## テスト結果

### moorestech_web (main repo, `feature/remove-energized-range-webui` ブランチ)
`npm test` (vitest run): **Test Files 65 passed / Tests 367 passed**（wireContract.test.ts 26件、validators.test.ts 29件含む）

### moorestech_web (tree1 ワークツリー)
同一編集を適用後 `npm test`: **Test Files 65 passed / Tests 367 passed**（同上、tree1側の C# 撤去済みフィクスチャに対しても一致）

### ビルド確認（main repo側で実施、コミットはせず動作確認のみ）
`npm run build` (`tsc -b && vite build`) 成功。`dist/index.html`, `dist/assets/index-*.css`, `dist/assets/index-*.js` 生成。gitignore対象のためこの成果物は破棄（tree1側はUnity Playerビルド時に自動再生成される）。

### C#側契約テスト（tree1, uloop）
`uloop compile --project-path ./moorestech_client` → `ErrorCount: 0, WarningCount: 0`
`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContract"` → **TestCount 32, PassedCount 32, FailedCount 0**（初回はドメインリロード中で失敗、45秒後リトライで成功）

## コミット

- 主リポジトリ (`/Users/katsumi/moorestech`, ブランチ `feature/remove-energized-range-webui` を master から新規作成): `6160bd1e7` "feat: placement_mode契約からenergizedRangeVisibleを削除"（moorestech_web/webui 配下5ファイルのみステージ、事前に汚れていた `.claude/skills/*`・`.moorestech-external-revisions.json`・`_CompileRequester.cs`・未追跡 `spec-plan-review/` は一切触れず）。作業後 `master` へチェックアウトして復帰済み。
- tree1 (`moorestech-worktrees/tree1`, ブランチ `tree1`): `686bbb7a4` "feat: placement_mode契約からenergizedRangeVisibleを削除"（同じく moorestech_web/webui 配下5ファイルのみステージ。他エージェントの作業中ファイル `.moorestech-external-revisions.json`・`.superpowers/sdd/task-1-report.md`・`task-4-report.md` には触れず）。dist反映コミットは不要と判断し実施していない（上記理由）。

## メインチェックアウトのクリーンさ確認

作業前後で main repo (`/Users/katsumi/moorestech`) の `git status --porcelain` は以下2ファイルの変更のみで不変（無関係な既存汚れ、私は触れていない）:
```
M .moorestech-external-revisions.json
M moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
```

## 懸念事項

1. **master ブランチの C# 側は未移行**。`feature/remove-energized-range-webui` は master から分岐しているが、`EnergizedRangeVisible` の C# 撤去自体は tree1 側の別タスクで完了しており、master にはまだマージされていない。将来 master 側で同フィールドを撤去する際は、上記フィクスチャ・`WireContractC2Test.cs`・`PlacementModeTopic.cs` も合わせて更新する必要がある（今回スコープ外）。
2. `feature/remove-energized-range-webui` ブランチは main repo に作成したのみで、PRやマージは行っていない（指示になかったため）。
3. **task-10-report.md のファイル名衝突**: 本ファイルには既に別タスク（webuiコンポーネント実装、コミット `617c650cd`）のレポートが存在していたため上書きせず追記した。team-leadでの整理を推奨。
