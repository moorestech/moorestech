# 無人applyのdirty耐性 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 無人レビューパイプラインの適用フェーズ（`pr-adjudicated-apply`）が、Unityの自動生成dirty 1件でPR対応ごと中止される構造を断つ。

**Architecture:** dirtyを「中止条件」から「分類して処理する入力」へ変える。分類はallowlistではなくエージェントがdiffを読んで毎回行い、意味のない自動変更は破棄、意味のある変更はPRブランチへ持ち越してcommitする。あわせて、実行中もUnityがdirtyを作り続ける事実に合わせ、addと破棄の対象を「自分が触ったパス」に限定する。

**Tech Stack:** Markdown（スキル定義 `.agents/skills/pr-adjudicated-apply/SKILL.md`）。実行時の唯一の消費者はheadlessのClaudeセッション（poller起動）。

## Requirements

設計対話（2026-08-15、grill）で確定した要件。受け入れ基準を各行に含める。

1. dirtyでも中止しない — `git status --porcelain --untracked-files=no` が非空でも、それ単独を理由にした失敗終了がSKILL.mdから消えていること
2. 分類は全面エージェント判断 — SKILL.mdに特定ファイル名のallowlist（`_CompileRequester.cs` 等の名指し例外規定）が残っていないこと。判定基準は「diffが人間・エージェントの意図を表しているか」という文で書かれていること
3. 意味のない自動変更は破棄 — `git checkout -- <パス>` で個別に破棄する手順が書かれていること
4. 意味のある変更はPRへ持ち越してcommit — stash退避を使わず、checkoutで運ばれた変更をStep 6のcommitに含め、Step 7のsummaryへ「持ち越し: <パス>」を書く手順があること
5. commitは自分が触ったパスのみ明示add — `git add -A` / `git add .` / `git commit -a` の禁止が明記され、add対象が「Step 4で編集したパス＋持ち越しパス」に限定されていること
6. 失敗時の破棄も自分が触ったパスのみ — `git reset --hard` がSKILL.mdから消え、`git checkout -- <Step 4で編集したパス>` に置き換わっていること
7. 持ち越し分は失敗時に失われない — 失敗終了しても持ち越しパスは未commitのまま元ブランチへ戻り、apply前と同じ状態に復元されること
8. 判定の証跡が残る — 破棄したパス・持ち越したパスがapply-result.jsonのsummaryから読み取れること
9. 持ち越しがcheckoutに拒否されても壊れない — PRブランチ側と衝突して `checkout -B` が失敗した場合の再分類・1回リトライ・失敗終了時の保全がSKILL.mdに書かれていること
10. 新規作成ファイルの始末が正しい — Step 8の破棄が「既存ファイルは `git checkout --` / 新規作成は `rm`」に書き分けられていること（未追跡パスをpathspecへ混ぜるとコマンド全体が失敗する）

**やらないこと（スコープ境界）:**

- ピン自動追随そのもの（`ExternalRepositorySyncEditor`）の挙動変更・gitignore化 — dirtyの発生源対策は本planの対象外
- `pr-independent-review` / `moores-code-review` の改修 — 実測の結果、同型の「dirtyなら中止」ゲートを持たない（前者は専用worktreeを毎回reset、後者はdirty込みで注記するのみ）
- `.cs` を含むプロダクションコードの変更 — 本planはスキル定義（Markdown）のみを触る

## Global Constraints

- 変更対象は `.agents/skills/` 配下のみ。`.claude/skills` と `.codex/skills` はsymlinkであり、実体の複製・同期は禁止（AGENTS.md「スキル配置と実行記録」）
- `pr-adjudicated-apply` は**無人実行**スキルである。書き足す手順にAskUserQuestion・人間へのエスカレーションを含めてはならない（SKILL.md「禁止事項」）
- SKILL.mdは実行時にLLMが読む指示書である。曖昧な語（「適切に」「必要に応じて」）ではなく、実行可能なコマンドと判定基準の文で書く
- 既存の $REPO / $RUNDIR プレースホルダ表記（「実値の絶対パスへ展開して書く」規約）を崩さない
- 本文の説明コメントは日本語のみ（SKILL.mdは既存全文が日本語であり、AGENTS.mdの日英2行セット規約はソースコードのコメントに対する規約）

---

### Task 1: Step 3 のdirtyゲートを分類処理へ置換する

**Files:**
- Modify: `.agents/skills/pr-adjudicated-apply/SKILL.md:88-94`（Step 3 手順1）
- Test: 自動テストなし（Markdown指示書）。検証はgrepによる受け入れ基準確認

**Interfaces:**
- Produces: 後続タスクが参照する2つの用語 — `CARRIED_PATHS`（Step 3で持ち越すと決めたパス集合）、`EDITED_PATHS`（Step 4で編集したパス集合）。Task 2 はこの2語をそのまま使う

- [ ] **Step 1: 置換前の状態を確認する**

Run: `grep -n "_CompileRequester\|即座に失敗として終了する（他作業を壊さないため）" .agents/skills/pr-adjudicated-apply/SKILL.md`

Expected: 88行目付近の「非空なら、ブランチ操作を一切せず即座に失敗として終了する」と、92-94行目の `_CompileRequester.cs` 例外規定がヒットする

- [ ] **Step 2: 手順1を丸ごと置換する**

SKILL.md の Step 3 手順1（現在の88-94行、「**dirtyチェックを最初に行う**」から「apply が恒常的に不能になる）」まで）を次で置き換える:

```markdown
1. **dirtyの分類を最初に行う**: `git -C <$REPOの実値> status --porcelain --untracked-files=no` を実行する。
   出力が空なら手順2へ進む。非空でも**中止しない** — 次の分類を行ってから続行する
   （自動生成の痕跡1件で無人パイプラインを止めないため。ユーザー裁定 2026-08-15）:

   1. dirtyな各パスについて `git -C <$REPOの実値> diff -- <パス>` を読み、2つに分類する。
      **特定ファイル名のallowlistは持たない**（列挙は必ず陳腐化し、載っていないだけで止まるため）。
      判定基準は「そのdiffが人間・エージェントの意図を1つでも表しているか」:
      - **意味のない自動変更** — 意図を表さない、ツールが実行のたびに書き換える痕跡
        （例: コンパイルトリガーの連番、兄弟クローンのHEADへ追随しただけの外部リビジョンピン）
      - **意味のある変更** — それ以外すべて（他セッションのコミット漏れ等）。判定に迷ったらこちらへ倒す
   2. 意味のない自動変更は `git -C <$REPOの実値> checkout -- <パス>` で破棄する
   3. 意味のある変更は破棄も退避もしない。そのまま手順4のcheckoutでPRブランチへ持ち越し、
      Step 6のcommitに含めてPRの一部とする（ユーザー裁定 2026-08-15。厳密な保全より続行を優先する）。
      持ち越すパスを `CARRIED_PATHS` として控え、Step 7のsummaryへ
      「持ち越し: <パス> — <diffの内容1行>」を、破棄したパスを「破棄: <パス>」として書く
   4. 未追跡ファイルは分類対象外（`--untracked-files=no` のため。checkoutを妨げないが、
      パス衝突時はcheckout自体が失敗して手順4で止まる）
   5. **持ち越しはcheckoutが拒否することがある**。tracked な未コミット変更が持ち越せるのは、
      現HEADとFETCH_HEADで**そのファイルの中身が同一の場合だけ**であり、PRブランチ側でも
      同じファイルが変更されていると手順4は
      `error: Your local changes to the following files would be overwritten by checkout` で失敗する。
      さらに、手順2の破棄から手順4のcheckoutまでの間に常駐Unityが同じファイルを書き戻すレースもある
      （ピンは5〜30秒毎に書き換わる）。手順4が失敗したら**本手順1へ戻って分類をやり直し**、
      意味のない自動変更を破棄したうえでcheckoutを1回だけリトライする。
      それでも失敗したら失敗として終了し（ブランチは切り替わっていないので後片付け不要）、summaryへ
      「checkout失敗: <パス> — PRブランチ側と衝突する持ち越しのため中止（持ち越し分は未commitのまま保全）」と書く
```

- [ ] **Step 3: 受け入れ基準を確認する**

Run: `grep -n "_CompileRequester\|CARRIED_PATHS\|allowlistは持たない" .agents/skills/pr-adjudicated-apply/SKILL.md`

Expected: `_CompileRequester` は0ヒット（要件2）、`CARRIED_PATHS` と `allowlistは持たない` がStep 3内にヒットする

- [ ] **Step 4: コミットする**

```bash
git add .agents/skills/pr-adjudicated-apply/SKILL.md
git commit -m "fix(skill): applyのdirtyを中止条件から分類処理へ変える

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Step 6 のaddとStep 8の破棄をパス限定にする

**Files:**
- Modify: `.agents/skills/pr-adjudicated-apply/SKILL.md`（Step 6 の commit 手順、Step 8 の後片付け）
- Test: 自動テストなし。検証はgrepによる受け入れ基準確認

**Interfaces:**
- Consumes: Task 1 が導入した `CARRIED_PATHS`

- [ ] **Step 1: 置換前の状態を確認する**

Run: `grep -n "採用finding単位\|reset --hard\|Step 3のdirtyチェックで存在しないことを保証済み" .agents/skills/pr-adjudicated-apply/SKILL.md`

Expected: Step 6の「採用finding単位、または意味的にまとまる単位でcommitする。」と、Step 8の `reset --hard` 文がヒットする

- [ ] **Step 2: Step 4 に `EDITED_PATHS` の記録指示を足す**

Step 4「## Step 4: 修正実装」の本文冒頭（「対象finding（Step 2で抽出したadopt分）それぞれについて、」で始まる段落）の直後に次の1行を挿入する:

```markdown
編集・新規作成したファイルのパスを `EDITED_PATHS` として控える（Step 6のadd対象・Step 8の破棄対象がこれに限定されるため）。既存ファイルの編集か新規作成かも区別して控えること（Step 8で始末の仕方が変わる）。
```

- [ ] **Step 3: Step 6 のcommit手順を置換する**

「- 採用finding単位、または意味的にまとまる単位でcommitする。コミットメッセージ末尾に必ず次を含める:」の行を次で置き換える（直下のCo-Authored-Byブロックはそのまま残す）:

```markdown
- 採用finding単位、または意味的にまとまる単位でcommitする。**`git add` は必ずパスを明示する** —
  対象は「Step 4で編集したパス（`EDITED_PATHS`）」と「Step 3で控えた `CARRIED_PATHS`」だけ。
  `git add -A` / `git add .` / `git commit -a` は禁止。
  apply実行中もUnityがdirtyを作り続けるため（Step 5の `uloop compile` はコンパイルトリガーを必ず書き換え、
  外部リビジョンピンは常駐Unityが数十秒ごとに書き換える）、全体addすると実行中に湧いた痕跡がPRのcommitへ混入する。
  コミットメッセージ末尾に必ず次を含める:
```

- [ ] **Step 4: Step 8 の後片付けを置換する**

Step 8 の「失敗終了で未commitの変更が残っている場合は、先に `git -C <$REPOの実値> reset --hard` で破棄してから戻る（push済みでない失敗applyの変更は再実行時にゼロから作り直すため、残す価値がない。元ブランチ側の作業はStep 3のdirtyチェックで存在しないことを保証済み）。」を次で置き換える:

```markdown
失敗終了で未commitの変更が残っている場合、**`git reset --hard` を使ってはならない**。
自分が作った変更だけを、既存ファイルと新規ファイルで**書き分けて**始末してから戻る:

    # Step 4で既存ファイルを編集した分
    git -C <$REPOの実値> checkout -- <EDITED_PATHSのうち既存ファイルの各パス>
    # Step 4で新規作成した分（未追跡なので checkout では消せない）
    rm -f <EDITED_PATHSのうち新規作成ファイルの各パス>

新規作成ファイルを `git checkout --` のpathspecに混ぜてはならない。未追跡パスは
`error: pathspec ... did not match any file(s) known to git` でコマンド**全体**が失敗し、
同時に指定した既存ファイルの復元まで行われない（`reset --hard` はtracked分を戻していたので機能的後退になる）。

`CARRIED_PATHS` は破棄しない。未commitのまま元ブランチへ戻れば、apply前と同じ位置にそのまま復元される
（他セッションのコミット漏れを無告知で消さないため。ユーザー裁定 2026-08-15）。
push済みでない失敗applyの変更は再実行時にゼロから作り直すため、残す価値がない。
```

あわせて Step 8 冒頭の「（＝対象findingが1件以上あり、dirtyチェックを通過した場合）」を
「（＝対象findingが1件以上あった場合）」に直す（dirtyでは中止しなくなったため）。

- [ ] **Step 5: 受け入れ基準を確認する**

Run: `grep -n "reset --hard\|git add -A\|EDITED_PATHS" .agents/skills/pr-adjudicated-apply/SKILL.md`

Expected: `reset --hard` は「使ってはならない」の文脈で1ヒットのみ（要件6）、`git add -A` は禁止文脈でヒット、`EDITED_PATHS` がStep 4・Step 6・Step 8の3箇所にヒットする

- [ ] **Step 6: コミットする**

```bash
git add .agents/skills/pr-adjudicated-apply/SKILL.md
git commit -m "fix(skill): applyのaddと破棄を自分が触ったパスに限定する

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: PR1140でapplyを再実行して実地検証する

**Files:**
- 変更なし（検証のみ）

**Interfaces:**
- Consumes: Task 1・Task 2 で更新した `.agents/skills/pr-adjudicated-apply/SKILL.md`

- [ ] **Step 1: 検証対象の前提を確認する**

Run: `ls ../moorestech_logs/harness/pr-independent-review/runs/ | grep 1140`

Expected: `pr-1140-r2`（最新run）が存在する。存在しなければ本タスクはスキップし、その旨を進捗台帳へ記録する

- [ ] **Step 2: 兄弟クローンの現在HEADを控える**

```bash
git -C ../moorestech_master rev-parse HEAD
```

Expected: SHAが出力される。Step 4で同じ位置へ戻すため控えておく（applyがピン追随dirtyを踏むかはこのHEADに依存する）

- [ ] **Step 3: applyを再実行する**

Run: `/pr-adjudicated-apply 1140` を新規セッションで起動する

Expected: `../moorestech_logs/harness/pr-independent-review/runs/pr-1140-r2/apply-result.json` の `summary` に「working treeがdirtyのため中止」が**現れない**こと。dirtyがあった場合は「破棄:」または「持ち越し:」の記載があること

- [ ] **Step 4: 兄弟クローンのHEADを元に戻す**

```bash
git -C ../moorestech_master checkout <Step 2で控えたSHA>
```

- [ ] **Step 5: 結果を進捗台帳へ記録する**

apply-result.jsonの `status` と `summary` を進捗台帳へ転記する。失敗していた場合は原因を切り分け、dirtyゲート起因ならTask 1・2へ戻る

---

### Task 4: moores-code-reviewで全ブランチレビューを実行する

**Files:**
- 変更なし（レビューのみ）

- [ ] **Step 1: moores-code-reviewスキルを起動する**

Run: `moores-code-review` スキルをブランチ全体に対して実行する

Expected: 指摘の統合結果を得る。機械的修正は適用し、設計判断はAskUserQuestionで裁定を仰ぐ

**このタスクは省略不可**（自動実行・ゴール文言による省略不可）。

---

## 判断記録（ADR）

設計セッション（2026-08-15 moores-grill-with-docs）の裁定は `.decisions/` が正典:

- [[2026-08-15-applyのdirty判定は全面エージェント判断にする]] — 出所: ユーザー裁定 2026-08-15（AskUserQuestion）
- [[2026-08-15-applyのdirtyは意味ありならPRへ持ち越してコミットする]] — 出所: ユーザー裁定 2026-08-15「基本的に意味を分析し、そのままコミットしてPRに含める。そもそもレアケースだしそんなに厳密に判定しない」
- [[2026-08-15-applyは自分が触ったパスだけをaddし破棄する]] — 出所: ユーザー裁定 2026-08-15（AskUserQuestion）
- [[2026-08-15-dirty耐性の是正対象はapplyとrepo-auto-pullとする]] — 出所: ユーザー裁定 2026-08-15（AskUserQuestion）

planning中に生じた判断:

- **前例の採用（パス単位の所有権）**: 未コミット変更の扱いは `subagent-driven-development` SKILL.md（commit `099ab21fb`）が先行しており、「所有者をパス単位で確認し、所有と確認できたパスだけを明示指定して扱う」形を取っている。本planの `EDITED_PATHS` / `CARRIED_PATHS` はこの前例に揃えた。出所: agent前提（前例一致・AGENTS.md「着手前に前例を探す」）
- **前例からの意図的な逸脱**: 前例のSDDは「所有者不明の変更が1件でもあれば人間へエスカレーション」するが、本planは持ち越してcommitする。SDDは人間が居るセッションで動くのに対し、apply は無人実行でエスカレーション先が存在せず、エスカレーション＝失敗＝ユーザーが明示的に棄却した挙動になるため。出所: ユーザー裁定 2026-08-15（上記2件目）
- **unityプレイ録画テストは実行しない**: 本planの変更はスキル定義（Markdown）のみで、ゲームランタイム挙動・入力・UI・エンティティ表示のいずれにも触れないため。実地検証はTask 3の実applyで代替する。出所: agent前提（writing-plansの必須検討項目に対する判断）
- **自動テストを書かない**: 変更対象がLLM向け指示書（Markdown）であり、実行主体がheadless Claudeセッションであるため単体テストの対象にならない。受け入れ確認はgrepによる文言検査（各タスクのStep）と、Task 3の実applyで行う。出所: agent前提
- **repo-auto-pull.py は本planに含めない**: 裁定4では是正対象に含まれていたが、事実確認で前提が崩れたため別扱いとする（詳細は下記「保留事項」）。出所: 事実確認による差し戻し

user-simulator review（2026-08-15・Fable判事）で指摘され、planへ**適用済み**の2件:

- **checkout持ち越しの失敗経路を規定した（要件9を新設）**: tracked な未コミット変更が `checkout -B <headRefName> FETCH_HEAD` を越えられるのは、現HEADとFETCH_HEADで**そのファイルの内容が同一**の場合だけであり、PRブランチ側でも変更されていれば exit 1 で拒否される（判事が実験で確認）。加えて破棄からcheckoutまでの間にピンが書き戻されるレースもある。Task 1の置換文面に「再分類→1回リトライ→失敗終了時はCARRIED_PATHS温存＋summary記載」を追加した。出所: シミュレーター予測（確信あり・反証済み）
- **Step 8の破棄を既存/新規で書き分けた（要件10を新設）**: `git checkout -- <未追跡パス>` は `pathspec did not match` で失敗し、複数pathspecに1つでも未追跡が混じるとtracked分の復元まで行われない。`reset --hard` はtracked分を戻していたため、書き分けないと**機能的後退**になる。Task 2に `rm -f` との書き分けを追加した。出所: シミュレーター予測（確信あり・反証済み）
- **Step 4への `EDITED_PATHS` 記録指示を追加**: Task 1が `Produces: EDITED_PATHS` を宣言する一方、SKILL.md本文に記録指示を足すステップが無かった（Warning）。Task 2 Step 2として追加。出所: シミュレーター予測（Warning）

**適用しなかった**指摘（判断と理由）:

- **Task 3でdirtyを人工的に誘発する手順の追加**: 実運用ではピン再書換えと `uloop compile` によりdirty遭遇はほぼ確実（pr-1140-r2のapply-result.jsonにも「masterに未コミット変更2件」の実績）。誘発手順は副作用リスクに見合わない。出所: agent前提（拒否権つき）
- **Step 6でcommit済み・push前に失敗した場合の持ち越し喪失**: 次回実行の `checkout -B FETCH_HEAD` が未pushローカルコミットを破棄しうる（要件7の縁）。二重障害のレアケースであり、裁定「そんなに厳密に判定しない」の範囲と判断してsummary記載のみとする。出所: agent前提（拒否権つき）

## 保留事項

`repo-auto-pull.py`（always-on サービス、`~/hermes-agent/data/services/always-on/scripts/`）の
「衝突dirtyなら blocked で何も触らない」挙動は、事実確認の結果:

- 事故ではなく**意図的な仕様**で、`tests/test_repo_auto_pull.py::test_conflicting_dirty_change_is_left_untouched` がそれを固定している
- 当初想定した stash 退避案は成立しない — 衝突とは定義上「ローカルもremoteも同じファイルを変えた」状態であり、ff-only merge 後の `stash pop` はほぼ確実にコンフリクトして無人のメインクローンをコンフリクト状態で放置する（現状より悪化する）
- 決定論スクリプトには「意味のない自動変更か」を読む能力がないため、applyと同じ解法（エージェント判断）を移植できない

**解決済み（2026-08-15 再裁定）**: 設定 `disposable_paths` に載ったパスだけを破棄して続行する形で決着し、
別planへ切り出した → `docs/superpowers/plans/2026-08-15-repo-auto-pull-disposable-paths.md`
（裁定は [[2026-08-15-repo-auto-pullは設定の破棄可パスで自動生成dirtyを越える]]）。

本planはapply側のみで単体で完結し、それだけで無人パイプラインの停止は解消する。
2つのplanに依存関係はないため、どちらから実装してもよい。
