# moorestech 固有の設計原則（B判定の照合表）

トリアージの Phase 2 で、moorestech の設計対話ならこの表を必ず照合する。
ここに載っている問いは「原則自明型（B）」であり、ユーザーに質問せず前提として宣言する。

## ドメイン・層の責務

| 問い | 答え | 根拠 |
|---|---|---|
| マスタ（Core.Master / MasterHolder）に動的状態を持たせてよいか | 持たせない。マスタは JSON 由来の静的定義のみ | マスタは「マスター管理」ドメイン。実行時に変化する値は該当機能の Core.* / Game.* 層が所有する |
| アイテム関連の実行時状態はどこか | Core.Item（ItemStack 内部からは InternalItemContext 経由） | ItemStackFactory と同じアクセスパターンが既にある |
| よく使うシステムへのアクセス経路 | ServerContext / ClientContext 経由 | AGENTS.md 記載の既存規約 |

## アンロック・永続強化系

| 問い | 答え | 根拠 |
|---|---|---|
| 研究・チャレンジ由来の永続効果は increment か冪等か | 冪等（unlock / set-max 形式） | ResearchDataStore はロード時に clearedActions を再実行して状態復元する。非冪等だと二重適用する |
| 永続強化の実装パターン | 研究 → GameAction → 状態コントローラ（GameUnlockState と同型） | 既存の unlockCraftRecipe 等と同じ流れに乗る |
| 派生状態の永続化 | 冪等再実行で復元可能でも、ロード順の制約があれば GameUnlockState 同様に永続化して先頭でロード | WorldLoaderFromJson はインベントリ復元(103行)が研究ロード(107行)より先 |
| 研究/アンロック由来の派生値のクライアント同期に新プロトコルが要るか | 原則不要。クライアントは共有マスタ＋同期済み状態から導出する | 研究完了・アンロック状態は既に同期済み（InitialHandshake, GetResearchInfo, ResearchCompleteEventPacket, GetGameUnlockState, UnlockedEventPacket）。新設は既存同期情報から導出できないことを示してから |

## マスタデータ・スキーマ

| 問い | 答え | 根拠 |
|---|---|---|
| スキーマ変更の手順を聞くか | 聞かない。edit-schema スキル参照 | 手順はスキル化済み |
| foreignKey 追加後の確認 | validate-schema スキルで C# バリデーション追加漏れを確認 | スキル化済み |
| Mooresmaster.Model.* を手で書くか | 書かない。SourceGenerator 自動生成のみ | AGENTS.md |
| プロパティ廃止時の参照移行方法 | スキーマから消してコンパイルエラー駆動で置換 | 置換漏れをビルドが検出する |

## 実装規約（質問不要・AGENTS.md 既定）

- 後方互換・パフォーマンス最適化・将来拡張性は壁打ち段階で考慮しない
- イベントは C# 標準 event ではなく UniRx（csharp-event-pattern スキル）
- partial 禁止、1ファイル200行以下、try-catch 原則禁止
- クライアント同期の新設は creating-server-protocol スキルの型に従う

## C（ユーザー判断）に残りやすいもの

以下は原則では決まらないので、質問してよい:
- ゲームデザイン（獲得手段、成長カーブ、粒度、バランス）
- UI をどこまで作るか
- 既存マスタデータ移行の段取り（mooreseditor 側作業との分担）
- 機能スコープの線引き
