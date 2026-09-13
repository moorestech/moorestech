# 計画・プロセス系スキルの剪定と writing-plans 再設計 — 検討引き継ぎ

2026-09-13 の壁打ちセッションの到達点。実装はまだ何もしていない（`.decisions/` 1件の記録のみ）。

> **進捗（2026-09-13 同日）**: 下記 A・C・D と B の大半（dot図・利点節・ワークフロー例・14問自己レビュー・TDD証拠）を本PRで実施済み。論点4は「規則本文はWHY一句、事案は `writing-plans/references/incidents.md`」で適用した。235件分類の第1パスは `moorestech_logs/harness/writing-plans-extension/critical-classification-2026-09-13.md`。残りは第2パス（X群のplan照合）と論点1〜3の拡張設計。

出発点: 指示ファイル剪定監査（`moorestech_logs/harness/pr-independent-review/docs/skill-prune-audit.html`）の
「計画・プロセス系（241KB → 95KB）」を実行しようとして、調べたら前提が変わっていたので方針ごと立て直した。

---

## 結論1: この category は「KB削減」ではなく「矛盾除去」として立て直す（ユーザー裁定済み）

監査の 241KB → 95KB は**達成不可、というか追うべきでない**。理由は2つ。

1. **SDD は監査後に既に分割されている。** SKILL.md は PR #1334（2026-09-09）で 41KB → 13KB へ圧縮済み。
   ただし中身は `references/` 5本（45KB）へ**移っただけ**で、監査が指摘した内容レベルの欠陥は1つも直っていない。
   KB目標は名目上消化されているのに、誤った指示は全部生き残っている。
2. **writing-plans の「削れ」判定は外している。** 後述（結論2）。

---

## 結論2: writing-plans の検査規則群は削ってはいけない（監査判定の否定）

監査は writing-plans の `Self-Review 1〜3` と埋め込み `spec-architecture-review` を
「指針B（自己検証儀式）」として短縮対象に置いていたが、実物を読んだ結果この判定は誤り。

- Self-Review は実際には**1〜6項**あり、4〜6（保留の恒久化・決定性と正しさの混同・呼び出し側への責務漏出）は
  **全てユーザー差し戻し起源**で日付と事案が付いている。指針Bが削れと言っているのは「必ずダブルチェックせよ」型の
  中身のない自問であって、これは指針H（理由を伝えると性能が上がる）側の資産。
- 埋め込み `spec-architecture-review`（L249-459・21KB）は**層マップを書き写していない** —
  「規約表（`references/moorestech-layer-map.md`）を必ず読む」と正本を指している。
  AGENTS.md と重なる Red Flags の4行も、平叙文の規約を「この思考が出たら → 現実」の対に**翻案**したもので、
  複製ではなく別レイヤの実装。消すと失われるものがある。
- 埋め込みはユーザーが移行時に**意図的に**行ったもの（本人証言）。

**「同じ観点が plan 段階とコード段階で二重に発火する」ことは問題ではない**（多段検査であり、早いほど安い）。
監査の 横断パターン が問題にしているのは「同じ**規則本文**が2箇所に書かれて片方だけ更新され矛盾に育つ」ことであって、
役割の重複ではない。この区別を次セッションでも取り違えないこと。

---

## 確定した裁定

### worktree の後始末（2026-09-13・記録済み）

`.decisions/2026-09-13-worktree後始末の正本はCLAUDE.local.mdとしSDDからは具体指示を抜く.md`

`references/workspace-isolation.md:63`「worktreeは完了後も削除しない」を削除。正本は CLAUDE.local.md
（PRを出したらその場で `moores-wt rm`）。

### グローバル `~/.agents` 側は同時に同一化する

repo 版と `~/.agents/skills` 版が別物になっている（SDD 59KB / writing-plans 36KB、references 分割も未反映）。
postmortem が PR #1346 でやったのと同じく repo 版を正とする。

**未解決の副問題**: moorestech 固有の記述（`moores-wt`・`uloop`・master ピン）が他 repo へ漏れる。
同一化するなら「環境ローカル規約に従う」というポインタ形にして具体コマンドを抜く必要がある。
worktree 裁定の棄却案C がまさにこの形。

---

## 実行すべき修正リスト（確定・未着手）

### A. SDD — 誤り・矛盾の除去（本丸。KBは小さい）

| 場所 | 削る/直す | 理由 |
|---|---|---|
| `references/workspace-isolation.md:62` | 「ポート11564は固定のため他worktreeのPlayModeと同時実行できない。計画では1本ずつ動かす」 | `ServerConnectionInitializer` はポート0/BoundPort。**事実として誤り**。無駄な直列化を強制している |
| `references/workspace-isolation.md:63` | 「worktreeは完了後も削除しない」 | 裁定済み |
| `references/workspace-isolation.md:12` | 「コンテキスト残量が3割を切った時点で作業を止め引き渡す」 | 指針G。メモリ `dont-stop-fanout-use-file-handoff` と衝突 |
| `SKILL.md:11` | 「ナレーション: ツール呼び出しの間は最大1行」 | 指針D |
| `references/per-task-mode.md:65` | ワークフロー例の `~/.config/superpowers/hooks/` | Superpowers 原文の残骸 |

### B. SDD — 重複・儀式の除去（削っても挙動は変わらない＝安全だが利得も小さい）

| 場所 | 削る | 効果 |
|---|---|---|
| `references/background.md:43-107` dot プロセス図 | SKILL.md の手順を英語ノード名の dot で三重化 | −5.3KB |
| `references/background.md:109-117`「利点とコスト」 | 「vs. 手動実行」「vs. Executing Plans」= Superpowers 原文の売り込み節 | −1.5KB |
| `references/per-task-mode.md:50-110` ワークフロー例 | 架空の hook インストールタスクの寸劇 | −2.2KB |
| `references/single-subagent-mode.md:41-59` ワークフロー例 | 同上 | −0.8KB |
| `implementer-contract.md:87-112` 自己レビュー14問 | 指針B直撃（中身のない自問リスト） | −1.0KB |
| `implementer-contract.md` TDD RED/GREEN 証拠 | AGENTS.md は TDD を要求していない | −0.4KB |
| `task-reviewer-contract.md:107-140` 報告テンプレ全文 | moores-code-review の出力契約と重複。要検討 | — |

合計 SDD dir 89KB → 約 75KB。**A を優先すること**（B ばかりやって A が残ると、SDD は今後も
「PlayModeは1本ずつ」「worktreeは残せ」と誤った指示を出し続ける）。

### C. writing-plans — 壊れている箇所だけ（−3KB 程度）

| 場所 | 問題 |
|---|---|
| L252, L283, L403 | **不在スキル参照3件** — `design-question-triage`（2）、`csharp-event-pattern`（1）。repo にも `~/.agents` にも存在しない |
| L35 | 「最初に必ず `git pull`」— 使い捨て worktree 運用と矛盾（`moores-wt new` が複製＋`--fetch` 済み） |
| L63-70 | 「各ステップは1アクション（2〜5分）」＋ RED/GREEN 5ステップ — Superpowers 原文の TDD |
| L155, L167 | `pytest tests/path/test.py::test_name -v` — このrepoに Python テストは無い。`uloop run-tests` が正 |
| L28 | 想定読者「センスが怪しいエンジニア」「良いテスト設計にもあまり詳しくない」— 指針A |
| L32 | 「開始時に宣言: writing-plansスキルを使って実装計画を作成します」— 指針D |
| L459 | `# 追加SKILL:spec-architecture-review` という埋め込み形式（実害が出ているかは未検証） |

### D. 小物

- `.claude/commands/remove-git-worktree.md`（tracked）に別マシンの旧パス `/Users/sato-katsumi/moorestech` が3箇所。
  この環境では `moores-wt rm` が正。

---

## 結論3: 本題は剪定ではなく writing-plans の拡張（ユーザーの再検討動機）

ユーザーの過去メモ（本人が貼付）:

> write planのフォーマットとかももっと改善できないかなぁ
> たとえば、設計段階頻出するレビューのクリティカル観点を分析して、あらかじめ潰しておくとか
> write-planするとき、「リファクタの勘所」をつける
> なんか重複してない？なんかドメイン越境してない？そういう時に人間側に
> 「こういうことがあるんだけど、どう思う？リファクタしたほうが良いと思うんだよね〜」提示するみたいな。
> たとえば、コンテキストを引き継いだsubagentとかを使って、いままでreadしたコード等を含めて再チェックし、
> リファクタ観点がないかとかをチェックするとか

また writing-plans は https://github.com/obra/superpowers/blob/main/skills/writing-plans/SKILL.md がベース
（上流の raw 取得は WebFetch 側で拒否されたため未取得。次セッションで必要なら `curl` で取る）。

### 実データがある — 観点は推測しなくてよい

`moorestech_logs/harness/pr-independent-review/runs/pr-*/` に独立レビューの構造化台帳がある。

- 72 run / **1149 findings**（suppressed 104）
- severity: critical 334 / high 218 / medium 318 / low 175
- `adjudications.json` が 55 run 分あり、決定は A:728 / B:11 / other:10 / C:2 / **reject:9**
  （＝レビュアー精度 98.8%。採用された指摘はほぼ全部「本物」とみなしてよい）
- **裁定で採用された critical = 235件**

タイトルを目視した範囲で、ユーザーのメモの狙いは corpus に実在している:

- **重複**: 「同一規約が2箇所に分かれ、同じguidが2つのEntryに載る」「歯車の役割判定の正本がサーバーとクライアントで二重化」
  「スクローラのDOM契約だけが集約から漏れ2ファイルへ逐語複製」「formatAmountの別名再exportが整形関数1本化を未達に」
- **ドメイン越境**: 「汎用`NotificationService`に呼び出し元都合のカテゴリ別ポリシーを埋め込んだ」
  「呼び出し元がpainterごとに正しいreach種別を選び分ける責任を負う」
- **plan段階では原理的に取れない**: 「参照等価比較のため恒常的にfalse（死んだ分岐）」「scrollWidth-clientWidthで恒真化」

### 次の一手（未着手・ユーザー未裁定）

**235件の採用済み critical を「plan段階で防げたか」で分類する。**
これをやらずに観点を足すと、勘で書いた観点が増えて plan の false positive が増える。

期待される分類バケツ:
- plan の**テスト指定**を足せば防げた群（「テストが1本も無い」「削除しても赤くならない」「無被覆」が目視でも多い）
- **重複・単一正本**の検出で防げた群 ← メモ前半
- **ドメイン越境・責務漏出**で防げた群 ← メモ後半。spec-architecture-review 検査1の既存射程
- **決定論チェック**で取れる規約違反（`DateTime.UtcNow`・コメント折り返し）← plan の話ですらない
- plan 段階では原理的に取れない実装バグ

86 run のファイルを読む作業なのでサブエージェント並列が要る。
（この分類作業は「Agent ツールを使うな」の既定に対する明示的な例外としてユーザーに確認すること）

---

## 設計上の論点（未裁定・次セッションで詰める）

### 論点1: 「コンテキストを引き継いだ subagent」は独立した目にならない

`fork` は別コンテキストではなく**同じコンテキストのコピー**なので、plan を書く過程で作った思い込み
（誤った前例の選択、読み飛ばしたファイル）をそのまま引き継ぐ。PR1145 の「前例適合で通過させた」型の事故は再現する。

ただしユーザーの狙いは独立性ではなく「**今まで読んだコードを全部知っている**」ことと読める。ならば代案:

> plan 作成中に読んだファイルパスを記録しておき、**fresh context** の subagent にそのファイル群だけ渡す。
> 「何を読んだか」は渡すが「どう解釈したか」は渡さない。カバレッジは同じで独立性が得られ、
> 会話履歴を積まない分コストも下がる。

### 論点2: リファクタ提案を plan に書き込むとスコープが壊れる

既存コードの重複・越境を拾うと必ず今回の変更と無関係なものが混ざる。plan のタスクにすると
AGENTS.md「依頼範囲が成果物」（指針E）と正面衝突する。

ユーザーのメモ自身が答えを持っている（「**人間側に提示する**」）。つまり plan には入れず別バケツ。
spec-architecture-review は既に「違反は質問せず修正する／前例のない新規パターンだけ注目点に列挙」の
2バケツ構造を持つので、**3つ目のバケツ「本PR外のリファクタ提案」**を足す形が素直。
plan の実装タスクには一切入らず、ユーザー裁定で拾われたものだけが bd に積まれる。

### 論点3: 観点を増やすと plan が防御的になる失敗モード

corpus に実例がある。**PR978** は「既存JSONを壊さないため `optional: true` + `?? Default` + ローダープリフィル」の
3重防御を plan 段階で書いて全面差し戻しになった。観点を増やすと「気をつける」方向の記述が増え、
結果として防御コードが設計に入る。

spec-architecture-review が「迷ったら ok に倒す」「レビューの信頼は false positive で最も速く壊れる」を
明記しているのはこの対策。**新設する検査も同じ規律を継承させること。**

### 論点4: writing-plans の構成比（提起したが未議論）

現在 459行/41.8KB の内訳:
- plan を書く手順（L24-195、テンプレ込み）— 約40%
- plan を検査する規則群（Self-Review 6項 + spec-arch の4検査/Red Flags/実例5件）— 約55%
- 引き継ぎ手順（L225-248）— 5%

検査側は**事案が起きるたびに1段落ずつ増える構造**（Self-Review 4 だけで実例3件・約1.5KB、
spec-arch の実例は5件）。正しく機能している証拠でもあるが、置き場としてこのまま伸ばし続けるのか、
事案は台帳（`.decisions/`・`moorestech_logs`）に置いて規則側は一行に保つのか、という分岐。

**結論3（拡張）を進めると検査規則はさらに増えるので、この論点は先に片付けたほうがよい。**

---

## 次セッションへの推奨手順

1. 上の **A/C/D**（誤り・矛盾・死んだポインタ）を1本の PR で片付ける。B は同PRに混ぜてよいが優先しない。
   グローバル `~/.agents` 同一化は同PRの最後に（moorestech 固有記述のポインタ化を伴う）。
2. 235件分類をユーザーに諮って走らせる。
3. 分類結果を見て、論点1〜4を裁定 → writing-plans の拡張設計へ。

CLAUDE.local.md に従い、1 は使い捨て worktree（`moores-wt new`）で行うこと。
このドキュメント自体は master 上で未コミット（`.decisions/` の裁定ファイルも同様）。
