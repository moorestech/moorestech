# pr-independent-review スキル設計

## 背景 / 目的

レビュー免責ロンダリング事故（PR1063系統・`CommonBlockPlaceSystem`の電気ドメイン汚染）の調査から、
実装セッション自身が書いたレビューcontextが「合意済みトレードオフ」として指摘を握り潰す構造欠陥が確認された。
また過去レビューコメントの実測（PR950以降・実質的指摘63件）で、現行moores-code-reviewハーネスの実名機構が
76%（boundary系100%・pattern系93%）を捕捉できることが分かった。

本スキルは「人間レビュー無しのgreenマージ」へ向けたシャドー運用の第一歩として、
**実装セッションと完全に独立したセッション**でPRをレビューし、verdict付きダイジェストを出力する。
実装セッションの自己申告（context・合意主張）を一切受け取らないことが独立性の核。

## スコープ

- 手動発火スキル。freshセッションでPR URL（または番号）を渡して起動する
- v1は静的レビュー専業。マージ判断の自動化・PRコメント投稿・コンパイル/テスト実行はスコープ外
- 成果物: ダイジェストレポート（ローカル保存）＋シャドー台帳への1行追記

## フロー

```
入力: PR URL or 番号
  ↓
1. PR取得        gh pr view（本文・ブランチ・ベース）
  ↓
2. checkout      レビュー専用worktreeに gh pr checkout（使い回し・実装treeに触らない）
  ↓
3. patch生成     git diff <base>...HEAD -- . ':(exclude)*.meta' ':(exclude)*.prefab'
                 ':(exclude)*.asset' ':(exclude)画像/バイナリ'（exclude方式・yml/jsonは残す）
  ↓
4. context再構成  PR本文＋リポジトリ内spec/planの判断台帳（ADR）のみから4カテゴリcontextを作る
                 出所ラベル必須: ユーザー裁定=[ADR引用] / それ以外=[agent前提]（免責力なし）
  ↓
5. 新規性ゲートL1  新設スクリプト: using新ペア（汎用層起点・層境界逆行）/ asmdef参照追加 /
                 文法要素新設（interface・基底クラス・Subject・プロトコル・スキーマ）→ 新形フラグ
  ↓
6. 本体レビュー    moores-code-review 5系統を発火（PATCH=手順3、cwd=レビューworktree）
                 起動promptで統合ルールを上書き: 免責は消音でなく降格（suppressed-byタグ保持）、
                 [agent前提]出所は免責事由にならない
  ↓
7. ダイジェスト    verdict（自動マージ可 / 新形につき裁定行き / Critical差し戻し）＋
                 判断台帳＋suppressed一覧＋新形フラグ一覧 → records/ に保存・端末報告
  ↓
8. シャドー台帳    PR番号・verdict・新形数・suppressed数・日付を1行追記
                 （後日、人間の実マージ判断と突き合わせて見逃し率を実測する）
```

## コンポーネント

| 要素 | 新規/既存 | 内容 |
|---|---|---|
| SKILL.md | 新規 | 上記フローのオーケストレーション |
| 新規性ゲートL1スクリプト | 新規 | usingペア表構築＋diff照合＋文法要素検出（Python） |
| レビューworktree管理 | 新規（手順） | `git worktree add`＋`gh pr checkout`。場所は `~/moorestech-worktrees/pr-review` 固定・使い回し |
| moores-code-review | 既存 | レビューエンジン本体。無改変で呼び、上書きは起動prompt側で行う |
| records/シャドー台帳 | 新規 | スキル配下 `records/shadow-ledger.md`（moores-code-reviewのrecords/前例踏襲） |

## verdict判定規則

- **Critical差し戻し**: 統合後Criticalが1件以上（決定論confirmed含む。200行超過は除外＝努力目標）
- **新形につき裁定行き**: Criticalなし、かつ新形フラグ or 設計判断ありが1件以上
- **自動マージ可**: 上記いずれも無し
- suppressedされた指摘はverdictに影響しないが、ダイジェストに必ず全件列挙する（Critical/Warning級）

## エラー処理・縮退

- `gh`未認証・PR不存在: 即座に明示エラーで終了（黙って縮退しない）
- codex不在等のmoores-code-review内縮退: 同スキルの既存規約に従い報告に明記
- レビューworktreeが他PRのcheckoutを保持: `gh pr checkout`で上書き（使い回し前提・状態は毎回リセット）

## 判断記録（ADR）

### ユーザー裁定
- **台帳承認方式の採用**（AskUserQuestion 2026-07-26）: agent前提の提示は台帳1行リスト方式。都度質問・事後可視化のみは不採用
- **完全手動発火・独立セッション・PR URL入力**（ユーザー発言 2026-07-27「完全に独立したセッションでPRのURLを渡し、そこから差分を取ってチェックするskillとして実装したい。一旦完全手動発火skillとして実装していく」）
- **patchフィルタはexclude方式**（設計提示→ユーザー「ok」2026-07-27）: cs/ts限定のincludeではなく.meta/.prefab/.asset/画像を除外。yml/json系レンズ（master-data-defense等・実測data系指摘16件）の盲目化を防ぐ

### agent前提（拒否権つき・免責力なし）
- PRコメント投稿はしない（シャドー期の外向き発信不要・判断汚染回避）
- L2前例引用照合はv1に入れない（引用義務は実装agent側の協力が前提。事後独立レビューでは前例探索をprecedent-alignmentレンズが担う）
- コンパイル・テスト実行はv1スコープ外（レビューworktreeでのUnity起動はライセンス・ポート・時間の制約）
- AskUserQuestion不使用。設計判断も含め全部ダイジェストへ書き出して終了（発火者は結果を後読みする運用）
- シャドー台帳の置き場はスキル配下records/（moores-code-reviewのrecords/前例）
- 並列セッションの原則①②本体改修を待たず、起動prompt上書きで暫定実装。本体改修マージ後に上書きを削る
