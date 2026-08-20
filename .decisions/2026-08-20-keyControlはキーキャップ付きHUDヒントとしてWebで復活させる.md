# keyControlはキーキャップ付きHUDヒントとしてWebで復活させる

決定: 休眠中の tutorialType `keyControl` をWeb UIで表示する。presentation に kind `keyControl` を追加し、画面下中央（ホットバーの上。ユーザー裁定 2026-08-20、左上目標HUD直下・既存ショートカットヒント列は棄却）のHUDヒントとして「[Tab] インベントリを開く」形式（キーキャップ＋説明文。既存 `LocalizedShortcutHint` と同じ見た目）で描く。schema の keyControl に `keyName`（例 "Tab"）を追加し `controlText` は説明文に限定する。`uiState` が現在のUI状態と一致する間だけ表示し、schema の uiState enum は実 `UIStateEnum` に揃える（BlockInventory→SubInventory、ResearchTree/BuildMenu/ChallengeList を追加）。

棄却案:
- 文言のみで復活（キーキャップ無し・schema不変。キー名を文中に埋めるためキーの視認性が落ち、既存ショートカットヒントと見た目が揃わない）
- 復活させずチャレンジtitleに操作を含めて代替（コード変更なし。左上HUDのtitleが長くなり、UI状態に応じて出し分けできない）

理由: ユーザー裁定 2026-08-20「HUDヒント＋キーキャップ付きで復活」。2026-08-19裁定「keyControlは将来使うので残す」の「将来」が今回。

リンク: [[2026-08-19-keyControlチュートリアルは将来使うので残す]]、[[2026-08-20-枠線ハイライトに文言ラベルを描く]]
