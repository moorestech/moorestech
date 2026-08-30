# ビルドメニューのscroll-spyは全カテゴリ群をResizeObserverで監視する

- 日付: 2026-08-30
- 対象: `moorestech_web/webui/src/features/buildMenu/hooks/useBuildMenuCategoryScroll.ts`（ADR 0045 の実装）

## 決定

フックに `attachGroup(element)` を足し、各カテゴリ群 `<section>` を ResizeObserver へ登録する。寸法変化のコールバックで末尾スペーサの測り直しに加え、現在ハイライト中カテゴリの再判定とジャンプ目標座標の取り直しも行う。

## 棄却した案

- 呼び出し側（`BuildMenuPanel`）が「各カテゴリの内容量を表す文字列」をレイアウト署名としてフックへ渡し、それを再計算キーにする案。

## 理由

署名案はエントリ増減は拾えるが、カテゴリGUID列も件数も変わらずウィンドウ高さだけ動くリサイズを拾えず、症状（ハイライトが古いカテゴリで固着する）が残る。ResizeObserver 案は両方を同じ経路で拾う。前例は `shared/tutorialAnchor/anchorRegistry.ts`（位置依存要素をすべて監視し寸法変化で位置解決全体を更新する形）。

## リンク

- `docs/adr/0045-build-menu-single-scroll-with-category-jump-sidebar.md`
- `docs/superpowers/plans/2026-08-30-build-menu-single-scroll-category-jump.md`
