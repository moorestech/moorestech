# 0044. 研究ノードカードに状態を文字ラベルで明示する

日付: 2026-08-30
状態: 採択

## Context

研究ツリー（`moorestech_web/webui/src/features/research/`）のノードカードは「研究名＋アイコン」だけを描き、状態は枠色4系統（未解放=減光45%／アイテム不足=グレー枠／研究可能=シアン枠／研究済み=白枠、ADR 0014・2026-08-18裁定）でしか表現していない。文字による状態表示は詳細ペインの研究ボタン（`研究済み`/`研究する`）とその下のツールチップ文のみで、ツリーを見渡したときに各ノードが「完了済み／研究可能／研究不可」のどれかが分かりづらい。

チャレンジツリー（`ChallengeNodeCard.tsx`）は「名前／要約／状態（未解放・進行中・完了）」の3段で状態を文字表示する前例がある。

## Decision

- **各ノードカードのアイコン下に状態ラベル1行を追加する。** チャレンジカードと同型の3段構成。
  出所: ユーザー裁定 2026-08-30 原文「研究UIの「完了済み」「研究可能」「研究不可」が分かりづらいのでわかりやすく文字で明示する」→ 選択「各ノードカードに1行追加」
  棄却案: 詳細ペインだけに状態行を出す／カードと詳細ペインの両方に出す

- **ラベルは3語に畳む。** `completed`→「完了済み」、研究可能（interactable）→「研究可能」、それ以外3状態→「研究不可」。アイテム不足と前提未達の区別はカードでは出さず、詳細ペインの既存ツールチップ文が担う。
  出所: ユーザー裁定 2026-08-30 選択「依頼どおり3語に畳む」
  棄却案: 枠色4状態と1対1の4語（アイテム不足・前提研究が未完了）

- **文言は依頼原文どおり新規キー3件で持つ。** `ui.research.stateCompleted`=完了済み/Completed/Abgeschlossen、`ui.research.stateAvailable`=研究可能/Available/Verfügbar、`ui.research.stateUnavailable`=研究不可/Unavailable/Nicht verfügbar。既存 `ui.research.completed`（研究済み）はボタン用として残す。
  出所: ユーザー裁定 2026-08-30 選択「依頼原文どおり」
  棄却案: 既存語彙「研究済み」を再利用して3語を揃える
  キー名・英独訳は agent前提（`ui.challenge.stateLocked/…` の命名前例）

- **枠色4状態は現状維持し、文字は補助として重畳する。** ラベル色は `--text-default` のみで状態別の色付けはしない（webui-design の装飾語彙を増やさない）。
  出所: ユーザー裁定 2026-08-30 選択「枠色は現状維持し文字を追加」
  棄却案: 研究可能ラベルだけ `--select-cyan` 文字にする

## Consequences

- `deriveNodeCardState` の `{completed, ready, locked}` から3語への写像を `researchLogic.ts` に置き、`ResearchNodeCard.tsx` が描く。ロジック側にユニットテストを足す
- `Localization/localization.csv` にキー3件を追加し、生成物（webui `localizationKeys.ts`・C#側）を再生成する（localization.csv は force-recompile が要る）
- カード高さが1行分伸びる。ノード配置座標はマスタ由来なので重なりが出ないか実表示で確認する
- 裁定記録: `.decisions/2026-08-30-研究ノードカードに状態を3語の文字ラベルで明示する.md`
