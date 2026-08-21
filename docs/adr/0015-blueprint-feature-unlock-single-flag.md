# ブループリント機能は単一フラグのアンロックで解放する

ゲーム開始直後からブループリントを使わせないため、ブループリント機能にアンロックを導入する。機能全体（コピーツール・保存済みBPの選択とペースト・作成/削除プロトコル・ホットバーへのBP系割当）を単一のアンロック状態で束ね、ConnectTool同型の3点セット（イベントパケット＋初期データ＋クライアント購読）で同期する。解放は研究ノードの clearedActions に置く新gameAction `unlockBlueprint`（パラメータ無し）で行う。

## 決定と出所

- ロック範囲は機能全体を1フラグで束ねる。ツール単体ロックやbuildTools汎用化はしない。
  出所: ユーザー裁定 2026-08-18 [[2026-08-18-ブループリントは機能全体を1フラグでロックする]]
- 実マスタでの発火源は研究ノードの clearedActions とする（gameActionなのでチャレンジからも呼べる）。
  出所: ユーザー裁定 2026-08-18 [[2026-08-18-ブループリント解放は研究ノードのclearedActionsで行う]]
- 未解放中はビルドメニューに非表示（既存ロック挙動と同じ除外方式）。解放告知はachievementトースト。
  出所: ユーザー裁定 2026-08-18 [[2026-08-18-ロック中のBPエントリはビルドメニューに非表示とする]]
- サーバー側でも BlueprintProtocol の Create/Delete とホットバーBP系割当を NotUnlocked で拒否する。
  出所: ユーザー裁定 2026-08-18 [[2026-08-18-BP未解放はサーバー側でも拒否する]]
- 旧セーブは未解放扱い・BPデータは保持。ローダーでの自動解放補完はしない。
  出所: ユーザー裁定 2026-08-18 [[2026-08-18-旧セーブのBPは未解放扱いでデータは保持する]]
- 状態は `BlueprintUnlockStateHolder`（単一bool）で持ち、`UnlockEventType.Blueprint` を既存 `va:event:unlocked` に追加、`GetGameUnlockStateProtocol` の初期データとクライアントミラーに載せる。シード値は buildMenu.yml のルートへ追加した `blueprintInitialUnlocked`（boolean, default false）で与え、Holderはこの1値を読むだけにする。
  出所: ユーザー裁定 2026-08-18 [[2026-08-18-BPの初期解放はbuildMenuルートの専用キーで宣言する]]
- クライアントのゲート点は `PlacementTargetCatalog.UnlockedEntries` の BlueprintCopy/Blueprint ハードコードtrueをアンロック状態参照へ置換する（単一判定点でビルドメニュー・ホットバー・配置解決へ一括波及）。
  出所: agent前提（設置対象カタログが唯一のアンロック判定点である既存設計）
- BlueprintProtocol の GetAll（一覧取得）は読み取り専用のため未解放でも開放のままとする。
  出所: agent前提（ハンドシェイク/キャッシュ単純化。拒否対象は状態を変える操作のみ）
- 研究詳細ペインの解放物セクション（ADR 0014）に `unlockBlueprint` 用ラベルを追加する（研究UI改修の解放物セクション実装後に bd:moorestech-c0y で行う）。
  出所: agent前提（ADR 0014「全clearedActions種別をセクション表示する」の帰結）

## 既知の負債

- `unlockBlueprint` gameAction は兄弟（`unlockConnectTool` 等）と違い引数を持たない。機能全体を1フラグで束ねた帰結であり、`buildTools` が2件目を持つ時点で per-row 化（`buildTools[].initialUnlocked` ＋ Guid索引Holder ＋ `unlockBuildTool(foreignKey)`）へ移行する。移行タスクは bd:moorestech-gob。

## Consequences

- どの研究ノードに置くか・consumeItems の内容はマスタデータ（moorestech_master）側の後続タスク。
- 未解放の間、旧セーブ由来のホットバーBP割当は解決不能スロットとして減光表示される（既存の解決不能スロット挙動を流用）。
