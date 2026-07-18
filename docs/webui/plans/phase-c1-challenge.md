# Phase C1 実行計画: チャレンジ（リスト + ツリー + HUD）

親: `../MIGRATION.md` / 進捗: `../TODO.md`
旧台帳 FEAT-CHAL-1/2 相当。大物画面の先頭。**着手時に本書を元へ writing-plans 形式の
詳細計画（完全コード付き）を作成してから実装する。**
着手依存: WU1〜9 完了 + A5（i18n/anchor 規約）。完了ゲート依存: A1（実機検証）+
A4（完了イベントの Topic 規約 — `challenge.current` は revision 準拠で実装する）。

## スコープと uGUI 側の正

- 生きている実体は `InGame/UI/Challenge/` の **`ChallengeListView`**（`ChallengeListState` が保持、
  `MainGameStarter` が `ChallengeListView`/`ChallengeManager` を配線）。`T` キーで開く
  （`UIStateEnum.ChallengeList`）。構成: `ChallengeTreeView` + `ChallengeTreeViewElement`（接続線付き
  ツリー）+ `ChallengeListViewCategoryElement`（カテゴリ）+ `TreeViewAdjuster`
- 常駐 HUD: `CurrentChallengeHudView` + `CurrentChallengeHudViewElement`
  （`CompletedChallengeEventPacket` 購読で進行中チャレンジを表示）
- **死コード（移行せず削除）**: `ChallengeListUI.cs`/`ChallengeListUIElement.cs`（DI 未登録・未参照の
  死んだ旧実装）+ `InGame/UI/ChallengeList/` の3ファイル（中身ゼロの空スタブ）。計5ファイル

## 実装ステップ

1. **uGUI 実コード確認**: カテゴリ構造・ノード状態（未解放/進行中/完了）・接続線の描画規則・
   HUD の表示条件を `ChallengeListView`/`ChallengeManager` から確定
2. **Topic**: `challenge.tree`（カテゴリ + ノード + 状態 + 依存エッジ）と `challenge.current`
   （進行中 + 完了イベント）。データ源はサーバイベント + 初期 snapshot + 購読の3点セット
   （`CompletedChallengeEventPacket` 中継）。research.tree topic の DTO 設計が先行事例
3. **契約**: `protocol.ts` / WireFixtures / 両側契約テスト
4. **Web ビュー**: `src/features/challenge/` 新設
   - ツリー描画は **`src/features/research/` の接続線 + ノード配置実装を共通化**して再利用する。
     ただし**2段階に分ける**: まず共通基盤を抽出して research を載せ替え、visual/e2e 回帰を通して
     コミット → 次のコミットで challenge を実装（research 回帰と challenge 新規不具合を混ぜない）
   - 共通化の範囲は**描画機構に限定**する（viewport/パン/ズーム/座標変換/接続線/anchor まで。
     ノード DTO・カード見た目・状態色・操作は feature 側の render prop に残す — research には
     所持数解決等の研究固有ロジックが混在しており、それらを基盤へ持ち込まない）
   - 常駐 HUD は screen ルーティング外の常時表示レイヤー（`App.tsx` のオーバーレイ層。
     toast/再接続オーバーレイが先行事例）
5. **ルーティング + ゲート**: `ui_state.current` の `ChallengeList` で表示。`ChallengeListView` に
   `&& !WebUiScreenGate.IsWebUiMode` ゲート追加。HUD 側 `CurrentChallengeHudView` も同様
6. **死コード削除**: 上記5ファイルを削除（uGUI 温存方針の例外 — 生きていないコードは温存対象外）

## 完了条件

- T キーでチャレンジツリーが Web 表示され、カテゴリ切替・ノード状態・接続線が uGUI と同等
- 進行中チャレンジ HUD が常駐表示され、完了で更新される
- ツリー描画共通基盤が research と共有されている
- e2e（ツリー表示・カテゴリ・HUD 更新）green

## 検証

vitest / e2e / `uloop compile` / PlayMode スモーク（T 開閉 + HUD）/ コミット + `../TODO.md` 更新
