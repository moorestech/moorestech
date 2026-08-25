# 初期チュートリアル再編（研究4.5廃止と改番・研究チャレンジ全件化・機械レシピ解放の同期則）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** v8 マスタの初期チュートリアルチェーンを ADR 0033 の骨格へ並べ替え、原始研究4.5を廃止して改番し、研究1〜9＋木材の組み立ての研究チャレンジを揃え、機械レシピの解放ノードを「解放同期則」で全ノード機械整理し、孤児アイテム（低品質な鉄の塊・砂鉄）を削除する。

**Architecture:** 変更は原則 `moorestech_master`（v8 mod）に閉じる。challenges.json は `tools/tutorial_v3_port/generate_challenges.py` の表を並べ替えて再生成する（GUIDは key 由来で安定、既存チャレンジの GUID は不変）。research.json の改番は名前だけ（GUID維持）。機械レシピの解放ノード移動は新規スクリプト `tools/research_sync/sync_machine_recipe_unlocks.py` が同期則で決定論的に書き換える（手作業の付け替えはしない）。本体repo側は `.moorestech-external-revisions.json` のピン更新とマスタ連動テスト・実機確認のみ（C#変更なし）。

**Tech Stack:** Python3（マスタ生成・JSON/CSV 書き換え）、Unity 6000.3 / uloop CLI（EditMode テスト・録画プレイテスト）、gh CLI。

## Requirements

設計ADR: `docs/adr/0033-tutorial-research-chain-renumber-and-unlock-sync.md`。裁定: `.decisions/2026-08-25-*.md` 4件。用語: `CONTEXT.md`「解放同期則」。

- R1 チャレンジチェーンを次の直列順にする: …石の斧を装備する → 原始研究3を完了する → 風力掘削機を設置する → 原始研究4を完了する → 粘土を入手する → レンガを作る → 石窯を設置する → 原始研究5を完了する → 青銅の鉱石を5個採掘する → 青銅鉱石の粉を3個作る → 青銅インゴットを作る → 原始研究6を完了する → 青銅シートを作る → 木釘を9本作る → 合板を作る → 原始研究7を完了する → 原始研究8を完了する → 原始研究9を完了する → 木材の組み立てを完了する → 補強棒材を作る → 木のフレームを作る。受け入れ: challenges.json の `prevChallengeGuids` がこの順の直列で、既存 challengeGuid（例: 風力掘削機 `a6497c0b-…`、粘土 `14f3b765-…`、石窯 `603e84c0-…`）が変わらない。
- R2 風力掘削機を設置する の tutorials は `veinPin{原木鉱脈, "原木鉱脈の上に設置"}` のみ（粘土ピンと build-menu→hotbar ドラッグ誘導を外す）。受け入れ: 当該チャレンジの tutorials が veinPin 1件で `veinGuid = 56ab3155-…`（原木鉱脈）。
- R3 粘土を入手する に `veinPin{粘土鉱脈, "粘土鉱脈の上に掘削機を設置"}` を新設する。受け入れ: tutorials が veinPin 1件で `veinGuid = 18d2bd1f-…`。
- R4 石窯を設置する から build-menu→hotbar ドラッグ誘導を外し、代替案内を足さない（tutorials は空）。題名は「石窯を設置する」のまま。
- R5 研究チャレンジ（`completeResearch`）を 原始研究1〜9 の全件＋「木材の組み立てを完了する」1件にする。新設分の tutorials は既存4件と同型（`uiHighLight research.node-<guid>`）。受け入れ: challenges.json 内の completeResearch が10件で対象 researchNodeGuid が全て実在する。
- R6 原始研究4.5→5、旧5→6、旧6→7、旧7→8、旧8→9 に改番する。`researchNodeGuid`・prev参照・座標・コスト・説明文は変えず `researchNodeName` と localization の `research.<guid>.name` 行だけ変える。受け入れ: research.json と localization.csv に「原始研究4.5」が残らず、`原始研究1`〜`原始研究9` が1件ずつ存在する。
- R7 機械レシピの解放ノードを解放同期則で全ノード整理する（規則の精密化は「判断記録」参照）。受け入れ: `sync_machine_recipe_unlocks.py --check` が差分0で終了し、研究4の `unlockMachineRecipe` が `粘土+原木→レンガ`（`3e0459d2-…`）の1本だけになる。
- R8 低品質な鉄の塊・砂鉄を削除する: items.json の2定義、machineRecipes.json の `砂鉄+木炭→低品質な鉄の塊`（`8c31fd14-…`）、craftRecipes.json の `木の棒×5→砂鉄`（`6fe798d2-…`）、localization.csv の2行、research.json の研究4からの当該レシピGUID参照。受け入れ: v8 mod 配下のどのファイルにも `5f42fa8d-1058-4828-b96b-b4baddd02c4e` と `b79372a7-c34a-48f2-b6f5-87c410118db1` が現れない。
- R9 localization.csv を追従させる: 新チャレンジ6件の `challenge.<guid>.title/summary`、新チュートリアル7件（研究6件＋粘土ピン）の `challengeTutorial.<guid>.text`、風力掘削機ピン文言の更新、風力掘削機/石窯の summary 更新、研究名5行の改番。孤児行（challenges.json に無い tutorialGuid の行）を残さない。受け入れ: `challengeTutorial.*` 行の GUID 集合 == challenges.json の文言付き tutorial の GUID 集合。
- R10 マスタ変更は `moorestech_master` の feature ブランチへ push し PR を作る。本体 `.moorestech-external-revisions.json` の `moorestech_master.commitHash` をその push 済みコミットに向ける（マージ後にマージコミットへ追いコミット）。
- やらないこと: 研究コスト（レンガ45/50/94 等）の調整／研究説明文の改訂／クライアントテスト用Mod（`EditModeInPlayingTestMod`）の同名アイテム削除／研究9後の枝ノード（原始ロジスティクス改善・建築土台・燃料式風車の作成・新しい燃料・軸の変更）の研究チャレンジ化／`uiDragGuide` の代替となるキーヒント追加／C# の変更（スキーマ・サーバー・クライアントとも変更不要）。

## Global Constraints

- 本体repoの作業場所: `moores-wt new feature/tutorial-research-renumber-unlock-sync --dir tutorial-research-renumber` で作る worktree `~/hermes-agent/data/repos/moorestech-worktrees/tutorial-research-renumber`。メインワークツリーでは作業しない（CLAUDE.local.md）。
- マスタrepoの作業場所: `~/hermes-agent/data/repos/moorestech-master-worktrees/tutorial-research-renumber`（branch `feature/tutorial-research-renumber-unlock-sync`、Task M1 で作成）。以下 `$MW` と表記する。
- マスタJSONは Python の `json.dump(..., ensure_ascii=False, indent=2)` ＋末尾改行で書く（generate_challenges.py と同じ）。research.json 等は既存ファイルのインデントを `python3 -c "import json;print(open(f).read()[:200])"` で確認し、同じ indent で書き戻す（差分を最小に保つ）。
- localization.csv は `csv` モジュールで書き戻さず、行単位のテキスト操作で既存行を保全する（0029 plan の前例。CRLF・引用符脱落の落とし穴）。english は既存行の調子（例: `粘土鉱脈の上に設置` → `Place It on a Clay Vein`）に合わせる。
- zsh では `echo ===` や `--include=*.cs` が展開エラーになる。区切り文字列はクォートし、grep は `grep -rn ... | grep "\.json:"` の形にする。
- `../moorestech_master`（本体worktreeから見た相対）は `moorestech-worktrees/moorestech_master` → `moorestech-master-worktrees/pin-*` の共有symlink。Editor起動時に `ExternalRepositorySyncService` がピンを `checkout --detach` し、逆に pin 側 HEAD を `.moorestech-external-revisions.json` へ書き戻す。**pin 側に未コミット変更があると checkout はスキップされる**。書き戻し差分は `git checkout -- .moorestech-external-revisions.json` で捨て、意図した commitHash だけをコミットする（0029 plan Task 4 と同じ）。
- テスト実行: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<正規表現>"`（180秒タイムアウトは失敗ではない。結果は `.uloop/outputs/TestResults` の XML）。ドメインリロード中エラー時は45秒待って再試行。
- コミットメッセージ末尾: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` と `Claude-Session: https://claude.ai/code/session_01T3Knpq3n3x8McMWZLQDdvh`。
- 実際の commitHash・GUID は必ず実行結果から取り、本planに書いた値は照合用に使う（planの値と食い違ったら実行結果を正とし、その差を記録する）。

---

## File Structure

マスタrepo（`$MW`）:
- Modify: `tools/tutorial_v3_port/generate_challenges.py` — CHALLENGES 表の並べ替え・研究チャレンジ6件追加・ピン/ドラッグ変更
- Create: `tools/research_sync/sync_machine_recipe_unlocks.py` — 解放同期則で research.json の `unlockMachineRecipe` を再配分する決定論スクリプト（`--check` で差分検査）
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/items.json` — 低品質な鉄の塊・砂鉄の削除
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/machineRecipes.json` — `8c31fd14-…` 削除
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/craftRecipes.json` — `6fe798d2-…` 削除
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/research.json` — 改番5件・`unlockMachineRecipe` 再配分
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/challenges.json` — 再生成
- Modify: `server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`

本体repo（worktree `tutorial-research-renumber`）:
- Add（メインからコピー）: `docs/adr/0033-tutorial-research-chain-renumber-and-unlock-sync.md`、`.decisions/2026-08-25-*.md` 4件、`CONTEXT.md`（解放同期則の項）、本plan
- Modify: `.moorestech-external-revisions.json`（`moorestech_master.commitHash`）

### 配置と前例（spec-architecture-review）

- チャレンジ定義の正本は `generate_challenges.py` の CHALLENGES 表（0029 plan Task M1 の前例）。challenges.json を直接編集しない。
- 研究解放の再配分スクリプトは `tools/<用途>/` 配下の単発 Python（前例: `tools/tutorial_v3_port/generate_challenges.py`、`tools/plan*_migration/`）。C# 側の Validator・ローダーには手を入れない（「マスタデータ防御をローダーで吸収しない」原則）。
- スキーマ変更なし。よって本体側 SourceGenerator・`_CompileRequester` のトリガも不要。
- 機能パリティ（死活表）: 変更は初期チャレンジ列と研究解放のデータのみ。ゲーム内操作（ビルドメニュー・ホットバー・研究画面・インベントリ）は一切変わらない。唯一消えるのは「ホットバーへドラッグ」チュートリアル矢印2件（ユーザー裁定で意図的に撤去）。

---

### Task 0: 本体 worktree の作成と設計文書のコミット

**Files:**
- Add: `docs/adr/0033-tutorial-research-chain-renumber-and-unlock-sync.md`、`.decisions/2026-08-25-初期チュートリアルは研究4.5を廃止し改番した直線チェーンへ再編する.md`、`.decisions/2026-08-25-機械レシピは出力アイテムを解放する研究で解放する.md`、`.decisions/2026-08-25-低品質な鉄の塊と砂鉄は関連定義ごと削除する.md`、`.decisions/2026-08-25-建設ショートカット誘導は代替案内なしで撤去する.md`、`CONTEXT.md`、`docs/superpowers/plans/2026-08-25-tutorial-research-renumber-unlock-sync.md`

- [ ] **Step 1: worktree を作る（Editor も起動する。Task 1 で使う）**

```bash
cd ~/hermes-agent/data/repos/moorestech
moores-wt new feature/tutorial-research-renumber-unlock-sync --dir tutorial-research-renumber --from master --fetch
```
Expected: `~/hermes-agent/data/repos/moorestech-worktrees/tutorial-research-renumber` が作られ、Library コピーと `uloop launch` が走る（3分強）。

- [ ] **Step 2: メインワークツリーに未コミットで置かれている設計文書をコピーする**

```bash
MAIN=~/hermes-agent/data/repos/moorestech
WT=~/hermes-agent/data/repos/moorestech-worktrees/tutorial-research-renumber
cp "$MAIN/docs/adr/0033-tutorial-research-chain-renumber-and-unlock-sync.md" "$WT/docs/adr/"
cp "$MAIN"/.decisions/2026-08-25-*.md "$WT/.decisions/"
cp "$MAIN/CONTEXT.md" "$WT/CONTEXT.md"
cp "$MAIN/docs/superpowers/plans/2026-08-25-tutorial-research-renumber-unlock-sync.md" "$WT/docs/superpowers/plans/"
cd "$WT" && git status --short
```
Expected: 上記7ファイルが `??`/`M` で出る。`git diff CONTEXT.md` の差分が「解放同期則」の1項だけであること（それ以外の差分があればメイン側の CONTEXT.md が master より新しい印なので、その差分はコミットに含めず `git checkout -p CONTEXT.md` で落とす）。

- [ ] **Step 3: コミットする**

```bash
cd "$WT"
git add docs/adr/0033-tutorial-research-chain-renumber-and-unlock-sync.md .decisions/2026-08-25-*.md CONTEXT.md docs/superpowers/plans/2026-08-25-tutorial-research-renumber-unlock-sync.md
git commit -m "docs: ADR 0033 初期チュートリアル再編（研究4.5廃止・研究チャレンジ全件化・解放同期則）の裁定と計画"
```

- [ ] **Step 4: メイン側の未コミット文書を消す（worktree へ移したので二重管理にしない）**

```bash
cd ~/hermes-agent/data/repos/moorestech
git checkout -- CONTEXT.md
rm docs/adr/0033-tutorial-research-chain-renumber-and-unlock-sync.md .decisions/2026-08-25-*.md docs/superpowers/plans/2026-08-25-tutorial-research-renumber-unlock-sync.md
git status --short
```
Expected: メインが clean（他セッションの未コミット物が出たら触らない）。

---

### Task M1: マスタ worktree と孤児アイテム（低品質な鉄の塊・砂鉄）の削除

**Files（マスタrepo）:**
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/items.json`、`machineRecipes.json`、`craftRecipes.json`、`research.json`（研究4の `unlockMachineRecipeGuids` から `8c31fd14-742e-42bf-9ff0-20bd1fcfdc73` を除く）
- Modify: `server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`（`item.b79372a7-….name`、`item.5f42fa8d-….name` の2行削除）

**Interfaces:**
- Produces: 以降のタスクが読む v8 mod から GUID `5f42fa8d-1058-4828-b96b-b4baddd02c4e`（低品質な鉄の塊）・`b79372a7-c34a-48f2-b6f5-87c410118db1`（砂鉄）・`8c31fd14-742e-42bf-9ff0-20bd1fcfdc73`（機械レシピ）・`6fe798d2-7d70-4570-83fa-dc6dfc687454`（手クラフト）が消えている状態

- [ ] **Step 1: マスタ worktree を作る**

```bash
git -C ~/hermes-agent/data/repos/moorestech_master fetch -q origin
git -C ~/hermes-agent/data/repos/moorestech_master worktree add -b feature/tutorial-research-renumber-unlock-sync ~/hermes-agent/data/repos/moorestech-master-worktrees/tutorial-research-renumber origin/master
cd ~/hermes-agent/data/repos/moorestech-master-worktrees/tutorial-research-renumber && git log --oneline -1
```
Expected: origin/master（`2cb314e` 以降）の HEAD。以降 Task M* は `$MW=~/hermes-agent/data/repos/moorestech-master-worktrees/tutorial-research-renumber` で実行する。

- [ ] **Step 2: 削除前の参照件数を記録する（失敗するチェック）**

```bash
cd $MW && grep -rc "5f42fa8d-1058-4828-b96b-b4baddd02c4e\|b79372a7-c34a-48f2-b6f5-87c410118db1\|8c31fd14-742e-42bf-9ff0-20bd1fcfdc73\|6fe798d2-7d70-4570-83fa-dc6dfc687454" server_v8/mods/moorestechAlphaMod_8 | grep -v ":0$"
```
Expected: items.json 2、machineRecipes.json 3（レシピGUID・砂鉄・低品質）、craftRecipes.json 2、research.json 1、localization.csv 2 の行が出る（0件でない＝これから消す対象がある）。

- [ ] **Step 3: 削除スクリプトを実行する**

```bash
cd $MW && python3 - <<'EOF'
import json
M='server_v8/mods/moorestechAlphaMod_8/master/'
IRON_LUMP='5f42fa8d-1058-4828-b96b-b4baddd02c4e'; IRON_SAND='b79372a7-c34a-48f2-b6f5-87c410118db1'
MRECIPE='8c31fd14-742e-42bf-9ff0-20bd1fcfdc73'; CRAFT='6fe798d2-7d70-4570-83fa-dc6dfc687454'
def rw(name, fn):
    p=M+name; raw=open(p,encoding='utf-8').read(); d=json.loads(raw)
    fn(d)
    indent=2 if raw.lstrip().startswith('{\n  "') else 4
    with open(p,'w',encoding='utf-8') as f: json.dump(d,f,ensure_ascii=False,indent=indent); f.write('\n')
def items(d): d['data']=[i for i in d['data'] if i['itemGuid'] not in (IRON_LUMP,IRON_SAND)]
def mrec(d):
    before=len(d['data']); d['data']=[r for r in d['data'] if r['machineRecipeGuid']!=MRECIPE]; assert len(d['data'])==before-1
def craft(d):
    before=len(d['data']); d['data']=[r for r in d['data'] if r['craftRecipeGuid']!=CRAFT]; assert len(d['data'])==before-1
def research(d):
    hit=0
    for r in d['data']:
        for a in r['clearedActions']:
            if a['gameActionType']=='unlockMachineRecipe' and MRECIPE in a['gameActionParam']['unlockMachineRecipeGuids']:
                a['gameActionParam']['unlockMachineRecipeGuids'].remove(MRECIPE); hit+=1
    assert hit==1, hit
rw('items.json',items); rw('machineRecipes.json',mrec); rw('craftRecipes.json',craft); rw('research.json',research)
CSV='server_v8/mods/moorestechAlphaMod_8/localization/localization.csv'
lines=open(CSV,encoding='utf-8').read().split('\n')
keep=[l for l in lines if not l.startswith(f'item.{IRON_LUMP}.') and not l.startswith(f'item.{IRON_SAND}.')]
assert len(lines)-len(keep)==2, (len(lines),len(keep))
open(CSV,'w',encoding='utf-8').write('\n'.join(keep))
print('ok')
EOF
```
Expected: `ok`。

- [ ] **Step 4: 参照が消えたことを確認する**

```bash
cd $MW && grep -rc "5f42fa8d-1058-4828-b96b-b4baddd02c4e\|b79372a7-c34a-48f2-b6f5-87c410118db1\|8c31fd14-742e-42bf-9ff0-20bd1fcfdc73\|6fe798d2-7d70-4570-83fa-dc6dfc687454" server_v8/mods/moorestechAlphaMod_8 | grep -v ":0$"; echo "exit=$?"
git diff --stat
```
Expected: 一致行なし（`exit=1`）。diff は5ファイルで、json のインデント変更による全文差分が出ていないこと（出ていたら Step 3 の indent 判定を実ファイルに合わせて直し、`git checkout -- <file>` からやり直す）。

- [ ] **Step 5: コミットする**

```bash
cd $MW && git add -A server_v8 && git commit -m "data(v8): 孤児アイテム 低品質な鉄の塊・砂鉄 とそのレシピ・翻訳行を削除する (moorestech ADR 0033)"
```

---

### Task M2: 研究の改番（4.5廃止）と解放同期スクリプト

**Files（マスタrepo）:**
- Create: `tools/research_sync/sync_machine_recipe_unlocks.py`
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/research.json`（`researchNodeName` 5件、`unlockMachineRecipe` 再配分）
- Modify: `server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`（`research.<guid>.name` 5行）

**Interfaces:**
- Produces: research.json 内の名前 `原始研究5`(guid `b47c5e3c-1b58-42c5-a477-d485d2eae747`)・`原始研究6`(`bc5e7786-6759-4271-8095-836703b54490`)・`原始研究7`(`0d76f2e5-be1c-4ad4-b460-97a8aad0495f`)・`原始研究8`(`48f75a7e-36f3-4845-a0bc-f8de8b3d7baf`)・`原始研究9`(`3bca3b97-14d7-4cc1-a661-2266670bb6cb`)。Task M3 の generate_challenges.py はこの名前で `research_by_name` を引く。
- Produces: `python3 tools/research_sync/sync_machine_recipe_unlocks.py [--check]`。引数なしで research.json を書き換えてレポート出力、`--check` は書き換えずに差分があれば exit 1。

- [ ] **Step 1: 改番する（GUID・その他は不変）**

```bash
cd $MW && python3 - <<'EOF'
import json
M='server_v8/mods/moorestechAlphaMod_8/master/research.json'
RENAME={'b47c5e3c-1b58-42c5-a477-d485d2eae747':'原始研究5','bc5e7786-6759-4271-8095-836703b54490':'原始研究6',
        '0d76f2e5-be1c-4ad4-b460-97a8aad0495f':'原始研究7','48f75a7e-36f3-4845-a0bc-f8de8b3d7baf':'原始研究8',
        '3bca3b97-14d7-4cc1-a661-2266670bb6cb':'原始研究9'}
OLD={'b47c5e3c-1b58-42c5-a477-d485d2eae747':'原始研究4.5','bc5e7786-6759-4271-8095-836703b54490':'原始研究5',
     '0d76f2e5-be1c-4ad4-b460-97a8aad0495f':'原始研究6','48f75a7e-36f3-4845-a0bc-f8de8b3d7baf':'原始研究7',
     '3bca3b97-14d7-4cc1-a661-2266670bb6cb':'原始研究8'}
raw=open(M,encoding='utf-8').read(); d=json.loads(raw)
for r in d['data']:
    g=r['researchNodeGuid']
    if g in RENAME:
        assert r['researchNodeName']==OLD[g], (g, r['researchNodeName'])
        r['researchNodeName']=RENAME[g]
indent=2 if raw.lstrip().startswith('{\n  "') else 4
with open(M,'w',encoding='utf-8') as f: json.dump(d,f,ensure_ascii=False,indent=indent); f.write('\n')
CSV='server_v8/mods/moorestechAlphaMod_8/localization/localization.csv'
lines=open(CSV,encoding='utf-8').read().split('\n'); n=0
for i,l in enumerate(lines):
    for g,new in RENAME.items():
        if l.startswith(f'research.{g}.name,'):
            num=new[-1]
            lines[i]=f'research.{g}.name,{new},Primitive Research {num},{new}'; n+=1
assert n==5, n
open(CSV,'w',encoding='utf-8').write('\n'.join(lines)); print('ok')
EOF
grep -c "原始研究4.5" server_v8/mods/moorestechAlphaMod_8/master/research.json server_v8/mods/moorestechAlphaMod_8/localization/localization.csv
```
Expected: `ok`、両ファイルとも `0`。

- [ ] **Step 2: 同期スクリプトを書く**

`tools/research_sync/sync_machine_recipe_unlocks.py`:

```python
#!/usr/bin/env python3
# 機械レシピの解放ノードを「解放同期則」で決める（moorestech ADR 0033 / CONTEXT.md「解放同期則」）
# Assign each machine recipe's unlock node by the unlock-sync rule (moorestech ADR 0033)
#
# 規則: レシピの解放ノード = {出力アイテムの解放ノード, 入力アイテムの解放ノード, 機械ブロックの解放ノード} のうち
#       他の全てを祖先に持つノード（支配ノード）。initialUnlocked のアイテム/ブロックは要件に数えない。
#       支配ノードが無い（別枝に並列）場合は現状の解放ノードを維持し、レポートに列挙する。
#       出力/入力アイテムか機械ブロックがどの研究でも解放されないレシピはエラー（孤児）。
# Rule: unlock node = the node among {output-item nodes, input-item nodes, machine-block node} that has
#       every other one as an ancestor. initialUnlocked items/blocks are not requirements.
#       With no dominating node (parallel branches) the current node is kept and reported.
#       A recipe whose item or block is never unlocked by any research is an error (orphan).
import json, os, sys

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
V8 = os.path.join(ROOT, 'server_v8', 'mods', 'moorestechAlphaMod_8', 'master')
CHECK = '--check' in sys.argv

def load(name):
    with open(os.path.join(V8, name), encoding='utf-8') as f:
        return f.read()

research_raw = load('research.json')
research = json.loads(research_raw)['data']
items = {i['itemGuid']: i for i in json.loads(load('items.json'))['data']}
blocks = {b['blockGuid']: b for b in json.loads(load('blocks.json'))['data']}
recipes = json.loads(load('machineRecipes.json'))['data']
by_guid = {r['researchNodeGuid']: r for r in research}

# 祖先集合（自分を含む）を前提研究から再帰で作る
# Build the ancestor set (including self) recursively from prerequisites
ancestors = {}
def anc(g):
    if g in ancestors:
        return ancestors[g]
    s = {g}
    for p in by_guid[g].get('prevResearchNodeGuids') or []:
        s |= anc(p)
    ancestors[g] = s
    return s
for g in by_guid:
    anc(g)

item_nodes, block_nodes, recipe_nodes = {}, {}, {}
for r in research:
    for a in r['clearedActions']:
        p, t = a['gameActionParam'], a['gameActionType']
        if t == 'unlockItemRecipeView':
            for g in p['unlockItemGuids']:
                item_nodes.setdefault(g, []).append(r['researchNodeGuid'])
        elif t == 'unlockBlock':
            for g in p['unlockBlockGuids']:
                block_nodes.setdefault(g, []).append(r['researchNodeGuid'])
        elif t == 'unlockMachineRecipe':
            for g in p['unlockMachineRecipeGuids']:
                recipe_nodes.setdefault(g, []).append(r['researchNodeGuid'])

def deepest(nodes):
    return max(nodes, key=lambda n: len(ancestors[n]))

def name(g):
    return by_guid[g]['researchNodeName']

def describe(r):
    ins = '+'.join(items[i['itemGuid']]['name'] for i in r['inputItems'])
    outs = '+'.join(items[o['itemGuid']]['name'] for o in r['outputItems'])
    return f"{blocks[r['blockGuid']]['name']}: {ins} -> {outs}"

# 各レシピの目標ノードを決める / Decide the target node for each recipe
orphans, kept, moves = [], [], []
target_of = {}
for r in recipes:
    guid = r['machineRecipeGuid']
    required = set()
    missing = []
    for it in [o['itemGuid'] for o in r['outputItems']] + [i['itemGuid'] for i in r['inputItems']]:
        if items[it].get('initialUnlocked'):
            continue
        if it not in item_nodes:
            missing.append(items[it]['name'])
            continue
        required.add(deepest(item_nodes[it]))
    if not blocks[r['blockGuid']].get('initialUnlocked'):
        if r['blockGuid'] in block_nodes:
            required.add(deepest(block_nodes[r['blockGuid']]))
        else:
            missing.append(blocks[r['blockGuid']]['name'])
    current = recipe_nodes.get(guid, [])
    if missing:
        orphans.append((describe(r), missing))
        continue
    dominating = [n for n in required if all(o in ancestors[n] for o in required)]
    if not dominating:
        kept.append((describe(r), [name(n) for n in required], [name(c) for c in current]))
        target_of[guid] = current
        continue
    target_of[guid] = dominating
    if current != dominating:
        moves.append((describe(r), [name(c) for c in current], name(dominating[0])))

if orphans:
    for d, m in orphans:
        print(f'ORPHAN {d}: never unlocked -> {m}')
    sys.exit(2)

# research.json の unlockMachineRecipe を目標ノードどおりに組み直す（GUID順は元の相対順を保つ）
# Rebuild unlockMachineRecipe per target node, keeping the original relative GUID order
recipe_order = {r['machineRecipeGuid']: i for i, r in enumerate(recipes)}
desired = {g: [] for g in by_guid}
for guid, nodes in target_of.items():
    for n in nodes:
        desired[n].append(guid)
for r in research:
    want = sorted(desired[r['researchNodeGuid']], key=recipe_order.get)
    actions = [a for a in r['clearedActions'] if a['gameActionType'] != 'unlockMachineRecipe']
    if want:
        actions.insert(0, {'gameActionType': 'unlockMachineRecipe', 'gameActionParam': {'unlockMachineRecipeGuids': want}})
    r['clearedActions'] = actions

for d, cur, tgt in moves:
    print(f'MOVE {d}: {cur} -> {tgt}')
for d, req, cur in kept:
    print(f'KEEP {d}: no dominating node among {req}; stays at {cur}')
print(f'{len(moves)} moves, {len(kept)} kept, {len(recipes)} recipes')

if CHECK:
    sys.exit(1 if moves else 0)
indent = 2 if research_raw.lstrip().startswith('{\n  "') else 4
with open(os.path.join(V8, 'research.json'), 'w', encoding='utf-8') as f:
    json.dump({'data': research}, f, ensure_ascii=False, indent=indent)
    f.write('\n')
```

注: research.json のトップレベルが `{"data": [...]}` 以外のキーも持つ場合は、`json.loads(research_raw)` 全体を保持して `data` だけ差し替える形に直す（Step 3 の diff で判定）。

- [ ] **Step 3: `--check` で差分があることを確認してから適用する**

```bash
cd $MW && python3 tools/research_sync/sync_machine_recipe_unlocks.py --check; echo "check exit=$?"
```
Expected: `MOVE` 行が 27 件前後（本plan作成時の試算は 27 moves / 8 kept / 61 recipes。研究4の 木炭・青銅インゴット×2・鉄インゴット×2 が `新しい燃料`/`原始研究5`/`鉄の時代` へ、研究6(旧5)の 鉄鉱石の粉/銅鉱石の粉 が `鉄の時代`/`銅の採掘` へ移る行が含まれる）、`ORPHAN` 行なし、`check exit=1`。

```bash
cd $MW && python3 tools/research_sync/sync_machine_recipe_unlocks.py > /tmp/recipe_sync_report.txt && tail -1 /tmp/recipe_sync_report.txt
python3 tools/research_sync/sync_machine_recipe_unlocks.py --check > /dev/null; echo "recheck exit=$?"
```
Expected: 適用後の `--check` が `recheck exit=0`（冪等）。

- [ ] **Step 4: 研究4・研究5・研究6 の解放内容を目視確認する**

```bash
cd $MW && python3 - <<'EOF'
import json
M='server_v8/mods/moorestechAlphaMod_8/master/'
items={i['itemGuid']:i['name'] for i in json.load(open(M+'items.json'))['data']}
blocks={b['blockGuid']:b['name'] for b in json.load(open(M+'blocks.json'))['data']}
mr={r['machineRecipeGuid']:blocks[r['blockGuid']]+':'+'+'.join(items[i['itemGuid']] for i in r['inputItems'])+'->'+'+'.join(items[o['itemGuid']] for o in r['outputItems']) for r in json.load(open(M+'machineRecipes.json'))['data']}
for r in json.load(open(M+'research.json'))['data']:
    if r['researchNodeName'] in ('原始研究4','原始研究5','原始研究6','新しい燃料','鉄の時代','銅の採掘'):
        print('##',r['researchNodeName'])
        for a in r['clearedActions']:
            if a['gameActionType']=='unlockMachineRecipe': print('  ',[mr[g] for g in a['gameActionParam']['unlockMachineRecipeGuids']])
EOF
```
Expected: 原始研究4 = `['石窯:粘土+原木->レンガ']` のみ。原始研究5 = `石窯:青銅鉱石の粉+原木->青銅インゴット`。原始研究6 = 粉砕機の `石->砕いた石材`・`青銅の鉱石->青銅鉱石の粉` の2本（`石->砕いた石材` は出力が研究1解放・機械が研究6解放なので支配ノードは研究6）。新しい燃料 = 木炭レシピ＋`青銅鉱石の粉+木炭->青銅インゴット`＋ボイラー木炭。鉄の時代 = 鉄鉱石の粉・鉄インゴット系。

- [ ] **Step 5: コミットする**

```bash
cd $MW && git add tools/research_sync/sync_machine_recipe_unlocks.py server_v8 && git commit -m "data(v8): 原始研究4.5を廃止して5〜9へ改番し、機械レシピの解放ノードを解放同期則で再配分する (moorestech ADR 0033)"
```

---

### Task M3: generate_challenges.py の表を新チェーンへ組み替えて再生成

**Files（マスタrepo）:**
- Modify: `tools/tutorial_v3_port/generate_challenges.py`（CHALLENGES 表 `原始研究3を完了する` 以降）
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/challenges.json`（再生成）

**Interfaces:**
- Consumes: Task M2 の研究名 `原始研究5`〜`原始研究9`・`木材の組み立て`（`research_by_name`）
- Produces: 新チャレンジ key/GUID（`guid_for(key)`）: `原始研究5を完了する`=`2eab252f-cd6f-5ac6-9773-dc232c947ebd`、`原始研究6を完了する`=`2c65c32d-4149-55f8-9c5b-898cbc0e79bc`、`原始研究7を完了する`=`69fb3e56-de77-5fe3-b9a9-9d6139cc9602`、`原始研究8を完了する`=`76d33968-1d0e-56b7-8ff0-1739241768ef`、`原始研究9を完了する`=`5b4319eb-d3bf-5a61-be92-da222cf630a0`、`木材の組み立てを完了する`=`d086eb33-c3b4-5873-b72a-130cbd7a6ab1`。slot0 tutorialGuid: 順に `7e65c26b-…`, `c946457b-…`, `15db66bc-…`, `e14504c6-…`, `e3d0944c-…`, `e1da80e0-…`。`粘土を入手する` slot0 = `39473729-f5d0-5d7d-b6b9-a6c8940437d5`（Task M4 が使う）

- [ ] **Step 1: 表の `原始研究3を完了する` の次行から末尾までを次に置き換える**

（`原始研究3を完了する` の行は現状のまま。既存行の key（第1要素）は絶対に変えない）

```python
    ('風力掘削機を設置する', '風力掘削機を設置する', 'Bでビルドメニューを開き、風力掘削機を原木鉱脈の上に設置しよう', 'block', '風力掘削機', 1,
     [vein('原木鉱脈', '原木鉱脈の上に設置')], '砕いた石材'),
    ('原始研究4を完了する', '原始研究4を完了する', '研究画面で木の板20枚・木の棒20本・砕いた石材10個を使い原始研究4を完了して、粘土とレンガ、石窯を解放しよう', 'research', '原始研究4', None,
     [research_node_ui('原始研究4', '原始研究4を完了する')], '砕いた石材'),
    ('粘土を入手する', '粘土を入手する', '粘土鉱脈の上に風力掘削機を設置して粘土を1個入手しよう', 'item', '粘土', 1,
     [vein('粘土鉱脈', '粘土鉱脈の上に掘削機を設置')], '粘土'),
    ('レンガを作る', 'レンガを作る', '粘土からレンガをクラフトしよう', 'craft', 'レンガ', None,
     [iv('レンガ', '粘土からレンガを作る')], 'レンガ'),
    ('石窯を設置する', '石窯を設置する', 'Bでビルドメニューを開き、石窯を設置しよう', 'block', '石窯', 1, [], 'レンガ'),
    ('原始研究5を完了する', '原始研究5を完了する', '研究画面で木の板10枚・レンガ45個を使い原始研究5を完了して、青銅の精錬を解放しよう', 'research', '原始研究5', None,
     [research_node_ui('原始研究5', '原始研究5を完了する')], 'レンガ'),
    ('青銅の鉱石を5個採掘する', '青銅の鉱石を5個採掘する', '青銅の鉱脈の上に風力掘削機を設置して青銅の鉱石を5個採掘しよう', 'item', '青銅の鉱石', 5,
     [vein('青銅の鉱石鉱脈', '青銅の鉱脈の上に掘削機を設置')], '青銅の鉱石'),
    ('青銅鉱石の粉を3個作る', '青銅鉱石の粉を3個作る', '青銅の鉱石から青銅鉱石の粉を3個クラフトしよう', 'item', '青銅鉱石の粉', 3, [], '青銅鉱石の粉'),
    ('青銅インゴットを作る', '青銅インゴットを作る', '石窯に青銅鉱石の粉と原木を入れて青銅インゴットを精錬しよう', 'item', '青銅インゴット', 1, [], '青銅インゴット'),
    ('原始研究6を完了する', '原始研究6を完了する', '研究画面で青銅インゴット30個・レンガ50個・木の棒20本を使い原始研究6を完了して、青銅シートと合板、原始的な粉砕機を解放しよう', 'research', '原始研究6', None,
     [research_node_ui('原始研究6', '原始研究6を完了する')], '青銅インゴット'),
    ('青銅シートを作る', '青銅シートを作る', '青銅インゴット3個から青銅シートをクラフトしよう', 'craft', '青銅シート', None, [], '青銅シート'),
    ('木釘を9本作る', '木釘を9本作る', '木の棒から木釘を9本クラフトしよう', 'item', '木釘', 9, [], '木釘'),
    ('合板を作る', '合板を作る', '木釘と木の板で合板をクラフトしよう', 'craft', '合板', None, [], '合板'),
    ('原始研究7を完了する', '原始研究7を完了する', '研究画面で青銅インゴット40個・レンガ94個・砕いた石材84個・木の棒20本・木の板32枚を使い原始研究7を完了して、木のシャフトと歯車ベルトコンベアを解放しよう', 'research', '原始研究7', None,
     [research_node_ui('原始研究7', '原始研究7を完了する')], '合板'),
    ('原始研究8を完了する', '原始研究8を完了する', '研究画面で木の板100枚・青銅シート35個・砕いた石材30個を使い原始研究8を完了して、原始的な加工機を解放しよう', 'research', '原始研究8', None,
     [research_node_ui('原始研究8', '原始研究8を完了する')], '青銅シート'),
    ('原始研究9を完了する', '原始研究9を完了する', '研究画面で木の板100枚・木の棒200本・砕いた石材50個・レンガ50個・青銅シート30個を使い原始研究9を完了して、原始的な採掘機を解放しよう', 'research', '原始研究9', None,
     [research_node_ui('原始研究9', '原始研究9を完了する')], '青銅シート'),
    ('木材の組み立てを完了する', '木材の組み立てを完了する', '研究画面で木の板200枚・木の棒200本・木釘600本・砕いた石材150個・青銅シート100個を使い木材の組み立てを完了して、補強棒材と木のフレームを解放しよう', 'research', '木材の組み立て', None,
     [research_node_ui('木材の組み立て', '木材の組み立てを完了する')], '木釘'),
    ('補強棒材を作る', '補強棒材を作る', '木の棒と青銅シートで補強棒材をクラフトしよう', 'craft', '補強棒材', None, [], '補強棒材'),
    ('木のフレームを作る', '木のフレームを作る', '補強棒材と合板で木のフレームをクラフトしよう', 'craft', '木のフレーム', None, [], '木のフレーム'),
]
```

summary の消費アイテム数は research.json の `consumeItems` と一致させる（本planの値は 2026-08-25 の実データ。Step 2 の検証スクリプトが突合する）。`drag` ヘルパは呼び出しが無くなるが、関数定義は残す（他 mod 移植時の語彙。削除しない）。

- [ ] **Step 2: 再生成し、順序・GUID・消費数を機械検証する**

```bash
cd $MW && python3 tools/tutorial_v3_port/generate_challenges.py && python3 - <<'EOF'
import json, re
M='server_v8/mods/moorestechAlphaMod_8/master/'
items={i['itemGuid']:i['name'] for i in json.load(open(M+'items.json'))['data']}
res={r['researchNodeGuid']:r for r in json.load(open(M+'research.json'))['data']}
ch=json.load(open(M+'challenges.json'))['data'][0]['challenges']
titles=[c['title'] for c in ch]
EXPECT=['小石を3個拾う','石器を作る','石器を装備する','木を伐採して原木を入手する','木の板を5枚作る','木の棒を5本作る','原始研究1を完了する','石を5個採掘する','砕いた石材を5個作る','原始研究2を完了する','石の斧を作る','石の斧を装備する','原始研究3を完了する','風力掘削機を設置する','原始研究4を完了する','粘土を入手する','レンガを作る','石窯を設置する','原始研究5を完了する','青銅の鉱石を5個採掘する','青銅鉱石の粉を3個作る','青銅インゴットを作る','原始研究6を完了する','青銅シートを作る','木釘を9本作る','合板を作る','原始研究7を完了する','原始研究8を完了する','原始研究9を完了する','木材の組み立てを完了する','補強棒材を作る','木のフレームを作る']
assert titles==EXPECT, [t for t in titles if t not in EXPECT]+[t for t in EXPECT if t not in titles]
for i,c in enumerate(ch):
    assert c['prevChallengeGuids']==([ch[i-1]['challengeGuid']] if i else []), c['title']
FIXED={'風力掘削機を設置する':'a6497c0b-82eb-5280-82c7-d339bc32de14','粘土を入手する':'14f3b765-be4d-51ef-983f-685c043c265b','石窯を設置する':'603e84c0-10b1-501f-a03d-598584d34d58','青銅の鉱石を5個採掘する':'b05e6911-19cd-5185-b5f9-b012da854703','原始研究5を完了する':'2eab252f-cd6f-5ac6-9773-dc232c947ebd','木材の組み立てを完了する':'d086eb33-c3b4-5873-b72a-130cbd7a6ab1'}
by={c['title']:c for c in ch}
for t,g in FIXED.items(): assert by[t]['challengeGuid']==g, t
w=by['風力掘削機を設置する']['tutorials']; assert len(w)==1 and w[0]['tutorialType']=='veinPin' and w[0]['tutorialParam']['veinGuid'].startswith('56ab3155'), w
cl=by['粘土を入手する']['tutorials']; assert len(cl)==1 and cl[0]['tutorialParam']['veinGuid'].startswith('18d2bd1f'), cl
assert by['石窯を設置する']['tutorials']==[]
assert not any(t['tutorialType']=='uiDragGuide' and t['tutorialParam']['toAnchorId']=='hotbar.hud' for c in ch for t in c['tutorials'])
rc=[c for c in ch if c['taskCompletionType']=='completeResearch']; assert len(rc)==10, len(rc)
for c in rc:
    r=res[c['taskParam']['researchNodeGuid']]
    for ci in r['consumeItems']:
        assert f"{items[ci['itemGuid']]}{ci['itemCount']}" in c['summary'], (c['title'], items[ci['itemGuid']], ci['itemCount'], c['summary'])
print('ok', len(ch), 'challenges')
EOF
```
Expected: `OK: 32 challenges`（生成器）と `ok 32 challenges`（検証）。summary の消費数突合で AssertionError が出たら research.json の実値に summary を合わせる。

- [ ] **Step 3: コミットする**

```bash
cd $MW && git add tools/tutorial_v3_port/generate_challenges.py server_v8/mods/moorestechAlphaMod_8/master/challenges.json && git commit -m "feat(tutorial): チャレンジ列をADR 0033の骨格へ並べ替え、研究5〜9と木材の組み立てのチャレンジを追加し、風力掘削機ピンを原木鉱脈へ変える (moorestech ADR 0033)"
```

---

### Task M4: localization.csv（チャレンジ・チュートリアル行）

**Files（マスタrepo）:**
- Modify: `server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`

**Interfaces:**
- Consumes: Task M3 の challenges.json（GUID・title・summary・pinText/highLightText は全てここから読む）

- [ ] **Step 1: 差分を challenges.json から導出して書き換える**

```bash
cd $MW && python3 - <<'EOF'
import json
CSV='server_v8/mods/moorestechAlphaMod_8/localization/localization.csv'
ch=json.load(open('server_v8/mods/moorestechAlphaMod_8/master/challenges.json'))['data'][0]['challenges']
EN_TITLE={'原始研究5を完了する':'Complete Primitive Research 5','原始研究6を完了する':'Complete Primitive Research 6','原始研究7を完了する':'Complete Primitive Research 7','原始研究8を完了する':'Complete Primitive Research 8','原始研究9を完了する':'Complete Primitive Research 9','木材の組み立てを完了する':'Complete Wood Assembly'}
EN_SUMMARY={
 '風力掘削機を設置する':'Press B to open the build menu and place a Wind Drill on a Log Vein',
 '原始研究4を完了する':'In the research screen, use 20 Wood Planks, 20 Wood Sticks, and 10 Crushed Stone to complete Primitive Research 4 and unlock Clay, Bricks, and the Stone Furnace',
 '粘土を入手する':'Place a Wind Drill on a Clay Vein and obtain 1 Clay',
 '石窯を設置する':'Press B to open the build menu and place a Stone Furnace',
 '原始研究5を完了する':'In the research screen, use 10 Wood Planks and 45 Bricks to complete Primitive Research 5 and unlock bronze smelting',
 '原始研究6を完了する':'In the research screen, use 30 Bronze Ingots, 50 Bricks, and 20 Wood Sticks to complete Primitive Research 6 and unlock Bronze Sheets, Plywood, and the Primitive Crusher',
 '原始研究7を完了する':'In the research screen, use 40 Bronze Ingots, 94 Bricks, 84 Crushed Stone, 20 Wood Sticks, and 32 Wood Planks to complete Primitive Research 7 and unlock Wood Shafts and the Gear Belt Conveyor',
 '原始研究8を完了する':'In the research screen, use 100 Wood Planks, 35 Bronze Sheets, and 30 Crushed Stone to complete Primitive Research 8 and unlock the Primitive Processor',
 '原始研究9を完了する':'In the research screen, use 100 Wood Planks, 200 Wood Sticks, 50 Crushed Stone, 50 Bricks, and 30 Bronze Sheets to complete Primitive Research 9 and unlock the Primitive Miner',
 '木材の組み立てを完了する':'In the research screen, use 200 Wood Planks, 200 Wood Sticks, 600 Wood Nails, 150 Crushed Stone, and 100 Bronze Sheets to complete Wood Assembly and unlock the Reinforced Rod and the Wood Frame',
}
EN_TUTORIAL={'原木鉱脈の上に設置':'Place It on a Log Vein','粘土鉱脈の上に掘削機を設置':'Place the Drill on a Clay Vein',
 '原始研究5を完了する':'Complete Primitive Research 5','原始研究6を完了する':'Complete Primitive Research 6','原始研究7を完了する':'Complete Primitive Research 7','原始研究8を完了する':'Complete Primitive Research 8','原始研究9を完了する':'Complete Primitive Research 9','木材の組み立てを完了する':'Complete Wood Assembly'}
def q(s): return '"'+s.replace('"','""')+'"' if (',' in s or '"' in s) else s
def row(key, src, en): return f'{key},{q(src)},{q(en)},{q(src)}'
lines=open(CSV,encoding='utf-8').read().split('\n')
idx={l.split(',',1)[0]:i for i,l in enumerate(lines) if l}
# 1) 文言付きチュートリアルの正本集合 / Canonical set of text-bearing tutorials
want_tut={}
for c in ch:
    for t in c['tutorials']:
        p=t['tutorialParam']; text=p.get('pinText') or p.get('highLightText') or p.get('controlText')
        if text: want_tut[t['tutorialGuid']]=text
# 2) 孤児 challengeTutorial 行を削除 / Drop orphan challengeTutorial rows
before=len(lines)
lines=[l for l in lines if not (l.startswith('challengeTutorial.') and l.split('.',2)[1] not in want_tut)]
print('removed orphan tutorial rows:', before-len(lines))
idx={l.split(',',1)[0]:i for i,l in enumerate(lines) if l}
# 3) チャレンジ title/summary を更新 or 追加 / Update or add challenge title/summary rows
added=[]
for c in ch:
    g=c['challengeGuid']; kt=f'challenge.{g}.title'; ks=f'challenge.{g}.summary'
    if c['title'] in EN_TITLE and kt not in idx:
        added.append(row(kt,c['title'],EN_TITLE[c['title']]))
    if c['title'] in EN_SUMMARY:
        r=row(ks,c['summary'],EN_SUMMARY[c['title']])
        if ks in idx: lines[idx[ks]]=r
        else: added.append(r)
# 4) チュートリアル文言を更新 or 追加 / Update or add tutorial text rows
for g,text in want_tut.items():
    k=f'challengeTutorial.{g}.text'
    if k in idx:
        cur=lines[idx[k]]
        if text in EN_TUTORIAL and cur.split(',',2)[1]!=text: lines[idx[k]]=row(k,text,EN_TUTORIAL[text])
    else:
        assert text in EN_TUTORIAL, ('english missing for', text)
        added.append(row(k,text,EN_TUTORIAL[text]))
# 5) 追加行は末尾の空行の前に入れる / Insert new rows before the trailing empty line
tail = lines.pop() if lines and lines[-1]=='' else None
lines += added
if tail is not None: lines.append(tail)
open(CSV,'w',encoding='utf-8').write('\n'.join(lines))
print('added rows:', len(added))
EOF
```
Expected: `removed orphan tutorial rows: 0`（uiDragGuide には文言行が無いため）、`added rows: 19`（title 6 + summary 6 + tutorial 7）。

- [ ] **Step 2: 行集合の整合を検証する**

```bash
cd $MW && python3 - <<'EOF'
import json
CSV='server_v8/mods/moorestechAlphaMod_8/localization/localization.csv'
ch=json.load(open('server_v8/mods/moorestechAlphaMod_8/master/challenges.json'))['data'][0]['challenges']
keys={l.split(',',1)[0] for l in open(CSV,encoding='utf-8').read().split('\n') if l}
tut={t['tutorialGuid'] for c in ch for t in c['tutorials'] if any(k in t['tutorialParam'] for k in ('pinText','highLightText','controlText'))}
csv_tut={k.split('.')[1] for k in keys if k.startswith('challengeTutorial.')}
assert tut==csv_tut, (tut-csv_tut, csv_tut-tut)
for c in ch:
    assert f"challenge.{c['challengeGuid']}.title" in keys and f"challenge.{c['challengeGuid']}.summary" in keys, c['title']
import collections
dup=[k for k,n in collections.Counter(l.split(',',1)[0] for l in open(CSV,encoding='utf-8').read().split('\n') if l).items() if n>1]
assert not dup, dup
assert 'header ok' if open(CSV,encoding='utf-8').readline().rstrip('\n')=='key,Source,english,japanese' else 0
print('ok')
EOF
grep -n "原木鉱脈の上に設置\|Complete Primitive Research 9\|Complete Wood Assembly" server_v8/mods/moorestechAlphaMod_8/localization/localization.csv | cut -c1-120
```
Expected: `ok`、3行がヒット。

- [ ] **Step 3: コミットする**

```bash
cd $MW && git add server_v8/mods/moorestechAlphaMod_8/localization/localization.csv && git commit -m "data(localization): 研究5〜9・木材の組み立てチャレンジと粘土/原木ピン文言の行を追加し風力掘削機・石窯の説明を更新する (moorestech ADR 0033)"
```

---

### Task M5: マスタの機械検証・push・PR

- [ ] **Step 1: JSON/CSV の機械検証と到達可能性チェック**

```bash
cd $MW && for f in items machineRecipes craftRecipes research challenges; do python3 -m json.tool "server_v8/mods/moorestechAlphaMod_8/master/$f.json" > /dev/null && echo "ok $f"; done
grep -rn $'\u200b' server_v8/mods/moorestechAlphaMod_8/master server_v8/mods/moorestechAlphaMod_8/localization; echo "zero-width check exit=$?"
python3 tools/tutorial_v3_port/generate_challenges.py && git status --short
python3 tools/research_sync/sync_machine_recipe_unlocks.py --check > /dev/null; echo "sync check exit=$?"
grep -c "原始研究4.5\|5f42fa8d-1058\|b79372a7-c34a" -r server_v8/mods/moorestechAlphaMod_8 | grep -v ":0$"; echo "leftover exit=$?"
```
Expected: 5ファイル ok、zero-width `exit=1`、再生成後 `git status` が clean（冪等）、`sync check exit=0`、`leftover exit=1`。

- [ ] **Step 2: push と PR 作成（マージはしない）**

```bash
cd $MW && git push -u origin feature/tutorial-research-renumber-unlock-sync
gh pr create --repo moorestech/moorestech_master --title "feat(tutorial): 研究4.5廃止と改番・研究チャレンジ全件化・機械レシピ解放の同期則・孤児アイテム削除 (moorestech ADR 0033)" --body "$(cat <<'EOF'
## Summary
- チャレンジ列を ADR 0033 の骨格へ並べ替え（風力掘削機設置→研究4→粘土入手→レンガ→石窯設置→研究5(旧4.5)→青銅…→研究9→木材の組み立て→補強棒材→木のフレーム）。研究5〜9と木材の組み立ての completeResearch チャレンジを追加
- 風力掘削機設置のピンを原木鉱脈へ、粘土入手に粘土鉱脈ピンを新設、build-menu→hotbar のドラッグ誘導2件を撤去
- 原始研究4.5を廃止し 5〜9 へ改番（GUID維持・名前と翻訳行のみ）
- `tools/research_sync/sync_machine_recipe_unlocks.py` を追加し、機械レシピの解放ノードを解放同期則で再配分（研究4は 粘土+原木→レンガ の1本に）
- 孤児アイテム 低品質な鉄の塊・砂鉄 とそのレシピ・翻訳行を削除

本体側 ADR: moorestech `docs/adr/0033-tutorial-research-chain-renumber-and-unlock-sync.md`

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01T3Knpq3n3x8McMWZLQDdvh
EOF
)"
git rev-parse HEAD
```
Expected: PR URL と HEAD の commitHash（Task 1 で使う）。

---

### Task 1: 本体ピン更新とマスタ連動テスト・実機確認

**Files:**
- Modify: `.moorestech-external-revisions.json`（`moorestech_master.commitHash`）

- [ ] **Step 1: ピンをマスタブランチの HEAD に向ける**

```bash
cd ~/hermes-agent/data/repos/moorestech-worktrees/tutorial-research-renumber
MASTER_HEAD=$(git -C ~/hermes-agent/data/repos/moorestech-master-worktrees/tutorial-research-renumber rev-parse HEAD)
git checkout -- .moorestech-external-revisions.json
python3 - "$MASTER_HEAD" <<'EOF'
import json, sys
p='.moorestech-external-revisions.json'; d=json.load(open(p))
for r in d['repositories']:
    if r['key']=='moorestech_master': r['commitHash']=sys.argv[1]
json.dump(d, open(p,'w'), indent=4); open(p,'a').write('\n')
EOF
git diff .moorestech-external-revisions.json
git -C ../moorestech_master status --short
```
`../moorestech_master`（pin worktree）に未コミット変更があれば `git -C ../moorestech_master stash` で退避する。Editor の自動 checkout を待ち、`uloop get-logs --project-path ./moorestech_client --log-type Log --search "External repository"` で新コミットへ checkout されたことを確認する（出なければ `git -C ../moorestech_master fetch && git -C ../moorestech_master checkout --detach $MASTER_HEAD` を手で実行し、Editor をリフレッシュする）。

- [ ] **Step 2: マスタ連動テストを実行する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "TutorialAnchorContractTest|MasterSourceTextCollectorTest|ChallengeMaster|LocalizeContent|ResearchMaster|MachineRecipe"`
Expected: 全PASS。特に `TutorialAnchorContractTest` が新チャレンジの `research.node-<guid>` アンカー6件を語彙に解決し、マスタロード（`MasterHolder.Load`）が削除アイテムの参照切れで失敗しないこと。失敗したら XML（`.uloop/outputs/TestResults`）の message を読み、マスタ側の欠損（例: 削除アイテムを参照する別マスタ）ならマスタrepoで直して push し Step 1 からやり直す。

- [ ] **Step 3: unityプレイ録画テストで序盤チェーンを確認する（unity-playmode-recorded-playtest スキル）**

`.agents/skills/unity-playmode-recorded-playtest/scenarios/` に既存のチュートリアル序盤シナリオがあればそれを流用し、無ければ `references/write-scenario.md` に従い `tutorial-research-renumber.cs` を書く。確認項目（`result.json` と `uloop screenshot` で判定）:
1. 「原始研究3を完了する」達成後に「風力掘削機を設置する」が現在目標になり、ピンが**原木鉱脈の露頭**に刺さる（粘土露頭ではない）。ホットバーへのドラッグ矢印が出ない
2. 風力掘削機設置後は「原始研究4を完了する」が現在目標（粘土入手ではない）
3. 研究4達成後の「粘土を入手する」でピンが粘土鉱脈の露頭に刺さる
4. 研究画面（R）で 原始研究5〜9 の名前がその順で並び「4.5」が無い。研究4の解放物一覧に石窯レシピが `粘土+原木→レンガ` 以外出ない
5. Console に Error が出ない（`uloop get-logs --project-path ./moorestech_client --log-type Error` が0件）

スクリーンショットを PR 本文に添付する。

- [ ] **Step 4: コミットする**

```bash
git add .moorestech-external-revisions.json
git commit -m "chore(master-pin): ADR 0033 マスタブランチへピンを進める"
```

（マスタPRマージ後、commitHash を**マージコミット**へ差し替える追いコミットを入れる。先に本体をマージすると CI の master data checkout が失敗する。）

- [ ] **Step 5: 本体 PR を作る（pr-create スキル）**

本体側の変更は設計文書＋ピンのみ。PR 本文にマスタPRの URL と Step 3 のスクリーンショットを載せる。

---

### Task 2: 最終レビュー（省略不可）

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。対象は本体ブランチ `feature/tutorial-research-renumber-unlock-sync` とマスタブランチ `feature/tutorial-research-renumber-unlock-sync` の両方（マスタ側はデータ差分とスクリプト `sync_machine_recipe_unlocks.py` / `generate_challenges.py`）。指摘のうち機械的修正は適用し、設計判断は AskUserQuestion で仰ぐ。

- [ ] **Step 2: bd を閉じる**

```bash
bd close moorestech-hzkz --reason="ADR 0033 実装完了: マスタPR <URL> / 本体PR <URL>"
```

---

## 判断記録（ADR）

設計ADR: `docs/adr/0033-tutorial-research-chain-renumber-and-unlock-sync.md`（裁定7件、出所欄つき）。裁定ファイル: `.decisions/2026-08-25-*.md` 4件。

planning 中に新たに生じた判断:

1. **解放同期則の精密化（機械ブロックの解放ノードも要件に含める・支配ノードが無ければ現状維持）**
   ADR §4 の規則を文字どおり「出力アイテム（または後の入力アイテム）のノード」で適用すると、`原始的な加工機: 木の棒→木釘` が研究6（木釘の解放）へ移り、加工機自体は研究8で解放されるため「使えないレシピが先に見える」状態になる（試算で 46件中 19件がこの型）。レシピは機械が無いと使えないので、機械ブロックの解放ノードを要件集合に加え、要件集合の全てを祖先に持つ「支配ノード」を解放ノードとする。要件が別枝に並列で支配ノードが無い 8件（例: `電気粉砕機: 銅の鉱石→銅鉱石の粉` は「加工の電化v2」と「銅の採掘」が並列）は現状の解放ノードを維持しレポートに列挙する。
   出所: agent前提（ADR §4 の意図「アイテム解放と同時に機械レシピ解放」を、機械未解放で使えない状態を作らない方向へ閉じた）。**レビュー注目点**: この精密化と 8件の維持はユーザーへ未提示。実装後の PR レビューで裁定を仰ぐ。
2. **解放ノードの付け替えは決定論スクリプトで行い、手作業で付け替えない**
   `tools/research_sync/sync_machine_recipe_unlocks.py` を正本にし、`--check` で冪等性を CI 的に検査できる形にする。将来レシピが増えたとき同じ規則で再適用できる。前例: `generate_challenges.py`（challenges.json の正本を表＋生成器に置く）。出所: agent前提。
3. **研究チャレンジの summary は消費アイテム数を列挙する既存様式を踏襲する**
   既存4件が「研究画面で木の板20枚・木の棒20本・…を使い」形式なので新設6件も同形。数値は research.json の `consumeItems` と機械突合（Task M3 Step 2）。出所: agent前提（既存様式）。
4. **石窯設置チャレンジの summary から「ホットバーへドラッグ」を落とす**
   ドラッグ誘導撤去（ADR §6）に伴い、summary 文言の「石窯をホットバーへドラッグして」も消す（矛盾する案内を残さない）。風力掘削機設置も同様に「粘土鉱脈」→「原木鉱脈」へ。出所: agent前提（ADR §2/§6 の帰結）。
5. **Task 0 で設計文書をメインから worktree へ移す**
   grill セッションはメインワークツリー（hook でブランチ操作不可）で ADR・.decisions・CONTEXT.md を書いたため、未コミットのままメインに残っている。実装 worktree へコピーしてコミットし、メイン側は消す。出所: agent前提（CLAUDE.local.md「メインでブランチ操作しない」の帰結）。
