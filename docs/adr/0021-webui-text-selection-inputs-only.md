# Web UI のテキスト選択は入力欄のみ許可する

Web UI はゲーム画面そのものであり、パネル・HUD・スロットの文字はドラッグで選択できるべきものではない。実際にはアイテムのドラッグ操作中に文字が選択され青いハイライトが残る。局所的な `user-select: none`（`SlotFrame` / `FluidSlot`）が場当たり的に置かれているが、面ごとの抜け漏れが残っている。

出所: ユーザー裁定 2026-08-19「ゲーム全体のweb uiでインプットフィールド以外選択不可能にする」

## 決定

`app/index.css` の `body` に `user-select: none` を敷き、`input, textarea` だけ `user-select: text` へ戻す。選択可否のホワイトリストはこの1箇所が唯一の正とし、機能側CSSでの個別指定は禁止する。既存の局所 `user-select: none`（`SlotFrame` / `FluidSlot`）は重複なので削除する。

現行の入力欄は `BuildMenuSearchInput`（素 input）と `ModalHost`（Mantine `TextInput`）の2箇所で、どちらも `input` 要素なのでセレクタ2つで足りる。

出所: agent前提（AGENTS.md「同種の条件分岐は文脈が集まっている側の一箇所へ揃える」の適用）

## Considered Options

- **コンポーネントごとに `user-select: none` を足す**（却下）: 新規パネルごとに付け忘れが発生し、既に発生している。グローバル1箇所＋入力欄の例外の方が値源が少ない。

## Consequences

- スタックトレース等のテキストをユーザーがコピーする経路は無くなる。現状そうした生テキストを出す画面は無い（`AppErrorBoundary` は文言とボタンのみ）。将来コピーさせたい表示を作る場合はこのADRを更新し、選択可の要素を明示的に追加する。
