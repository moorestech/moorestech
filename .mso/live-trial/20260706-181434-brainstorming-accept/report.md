# brainstorming live-trial 統合レポート (2026-07-06)

3 trial 構成: 発火検証 A1/A2 + 実走受け入れ B。trial 実行 model はコスト削減のため opus 指定。

## 対象 skill + タスク

- 対象: `brainstorming` (プロジェクト skill: `.claude/skills/brainstorming/SKILL.md`、design-question-triage 節を含む)
- A1 (trigger-q): 素のプロンプト「歯車接続、インベントリ接続において、現状意図しない繋がり方をしてしまう。…これどうしたらいい？」(Skill 指示なし・疑問形)
- A2 (trigger-req): 素のプロンプト「…これを防ぐ仕組みを実装したい。設計から一緒に考えてほしい。」(Skill 指示なし・要求形)
- B (accept): task.md で `Skill({skill: "brainstorming", args: "チェスト（Chest）の中身をワンクリックで種類ごとに自動整列（ソート）できる機能を作りたい。設計から一緒に考えてほしい。"})` を明示呼び出し

## model 検証

| trial | requested_model | actual_model (jq) | 一致 |
|---|---|---|---|
| A1 | claude-opus-4-8 | claude-opus-4-8 | ✅ |
| A2 | claude-opus-4-8 | claude-opus-4-8 | ✅ |
| B | claude-opus-4-8 | claude-opus-4-8 | ✅ |

## timeline

- A1: boot 3s → send 18:15頃 → 1ターン完走 (Stop 18:17:36) → poll はマーカー無し設計のため tool-timeout で回収に移行 → kill
- A2: boot 3s → send 18:15頃 → AskUserQuestion gate 到達 → 証跡回収して kill (発火検証はここで目的達成)
- B: boot 4s → send 18:23頃 → gate1 (AskUserQuestion) 応答'1' → 設計提示 (18:53) → [ユーザー中断 2.5h] → reply-1 (設計承認+spec配置希望) → spec 書き出し+self-review (21:17) → reply-2 (spec承認+終了指示) → **poll DONE (exit 0, 1815s, via jsonl)** 21:2x
- nudge_count: 全 trial 0
- gate/対話応答: A1=0, A2=0, B=3 (AskUserQuestion 1 + 設計承認 1 + spec承認/終了 1)。対話型 skill の設計上の gate であり自走未達降格には該当しない (task.md 契約4項を対話許容に適合済み — 逸脱として明記)

## 契約からの逸脱 (明示)

- A1/A2 は発火検証のため task.md 5項目契約を適用せず素のプロンプトを送信 (Skill リテラル呼び出しを書くと発火検証にならないため)。完了マーカー無し → poll は終端せず transcript 機械判定で代替
- B の契約4項「質問せず自走」は対話型 skill と矛盾するため「AskUserQuestion 等による質問は可 (応答が返る)」に適合
- B は skill の terminal (writing-plans 遷移) 直前でユーザー指示により終了 (コスト境界)。HARD-GATE 検証には影響なし

## 成果物

- A1: `../20260706-181434-brainstorming-trigger-q/` — transcript.jsonl / pane.txt / prompt.md
- A2: `../20260706-181434-brainstorming-trigger-req/` — transcript.jsonl / pane.txt / prompt.md
- B: 本ディレクトリ — transcript.jsonl / pane.txt / task.md / spec/2026-07-06-chest-one-click-sort-design.md / out/status.json
- 完了マーカー引用: `{"status": "PASS", "design_doc": "/Users/katsumi/moorestech-worktrees/tree1/.mso/live-trial/20260706-181434-brainstorming-accept/spec/2026-07-06-chest-one-click-sort-design.md"}`
- git 副作用: trial 由来なし (spec は指定どおり .mso 配下・非コミット。`ConnectorShapeConnectionTest.cs` 等の変更は並行中の別セッション由来)

## 発火判定 (A1/A2)

**両方とも自己発火 ✅** — transcript の tool_use 2手目に `Skill{skill:"brainstorming"}` (機械確認)。疑問形 (A1) でも発火。fresh context の opus では description トリガーが機能している。

## goal 判定 (fresh evaluator, opus)

**88/100 — PASS**。要点:
- triage 高度準拠 (全件): 調査による前提宣言 (A/Bタグ付き)、A/B型を質問化せず、1回1問、C型に実在トレードオフ
- B: checklist 9項目を正順で完走、HARD-GATE 違反ゼロ (承認前コード0行)、spec は placeholder/矛盾なし・主張事実はコード実地照合で全て実在、ユーザーの配置/非コミット/停止希望を全て尊重
- 未達点: (1) spec に軽微な誇張 —「チェストで検証済み」と読める表現の実体は機械ブロックでのテスト (2) 案数が2案で下限 (3) 一括承認 (セクション毎承認の簡略化) (4) 質問ツール不統一 (A1=plain-text, A2/B=AskUserQuestion)

## 総合判定

- 発火 ✅ / 完走 ✅ (対話 gate 込み) / goal 適合 ✅ (88) → **✅ 合格**
- 補足: 本 trial の動機だった「メインセッション (Fable・長文脈) で brainstorming が発火しなかった」事象は fresh context では再現せず。発火失敗は description の弱さ (相談形・壁打ちが trigger 文言に無い) × 長文脈という条件依存と推定

## ハーネス側の発見 (run-skill-live-trial への申し送り)

- **GATE 検知漏れ**: B で AskUserQuestion の選択待ちが画面表示されている間、`live-trial-status.sh` の分類は `BUSY_GENERATING` のまま (poll-state.env: `prev_progress=jsonl:506927:BUSY_GENERATING`, `gate_ticks=0`)。poll が exit 4 (GATE) せず STALL カウントが進行していた。pending tool_use = AskUserQuestion の分類ロジック要修正
- plain-text 質問でターン終了するケース (A1) は現仕様どおり GATE 検知不能 → STALL 分岐表 #3/#4 で回収した

## 推奨アクション (skill 改善)

1. description に相談形トリガー (壁打ち/仕様相談/「どうしたらいい」型の設計相談) を明記 — メインセッション非発火の対策
2. 離散選択肢の質問は AskUserQuestion ツールに統一する旨を明文化
3. Spec Self-Review に「証拠範囲チェック」(『Xで検証済み』はXそのものを検証したテストがある場合のみ) を追加
4. 小規模設計の一括提示を明文化 (現行「セクション毎承認」との矛盾解消)

## 結果ビューア

- URL: http://localhost:4981
- 対象ディレクトリ: /Users/katsumi/moorestech-worktrees/tree1/.mso/live-trial
- 起動プロセス: PID 43564 (`node server.mjs --dir ... --port 4981`)
