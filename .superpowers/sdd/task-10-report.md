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
