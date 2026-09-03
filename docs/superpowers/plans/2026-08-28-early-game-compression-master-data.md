# 序盤圧縮のマスタデータ再構成（研究1〜3削除・要求数変更・チャレンジ26本） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** ADR 0038 決定1・2・3のマスタデータ部分を `moorestech_master` v8 mod に適用する — 原始研究1〜3の削除と改番、研究コスト・ブロック設置費の数値変更、初期解放フラグ、木のシャフトの解放前倒し、チャレンジ32→26本への再構成（石器ライン削除・木の鉱脈チュートリアル・風車→シャフト→粉砕機の接続チュートリアル）、localization.csv 追随、本体ピン更新。

**Architecture:** 変更は `moorestech_master`（v8 mod）に閉じ、本体repoはピン更新と検証テストのみ。challenges.json は正本スクリプト `tools/tutorial_v3_port/generate_challenges.py` の CHALLENGES 表を書き換えて再生成する（GUID は key 由来で安定・既存チャレンジの GUID は不変）。research.json / blocks.json / items.json は単発 Python で決定論的に書き換える。**前提: 姉妹plan `2026-08-28-placement-guided-tutorials-and-initial-equipment.md` がマージ済み**（新tutorialType `veinRestrictedPlacement` / `relativeBlockPlacePreview` と新taskType `blockPlaceOnVein` / `gearConnectedBlock` が本体に存在し、`items.json` に `initialEquipmentItems` がある）。

**Tech Stack:** Python3（JSON/CSV 書き換え）、Unity 6000.3 / uloop CLI（EditMode・PlayMode遷移テスト）、gh CLI。

## Requirements

設計ADR: `docs/adr/0038-early-game-compression-and-placement-guided-tutorials.md`。裁定: `.decisions/2026-08-27-*.md` 4件。

- R1 research.json から 原始研究1（`837e9697-…`）・2（`424be8c1-…`）・3（`07d6226c-…`）を削除し、旧原始研究4（`858bcb10-…`）の `prevResearchNodeGuids` を `[]` にする。受け入れ: 3 GUID が v8 mod のどのファイルにも現れず、根ノードが `858bcb10-…` の1件。
- R2 表示名を改番する（GUID不変）: 旧4→原始研究1、旧5→2、旧6→3、旧7→4、旧8→5、旧9→6。`researchNodeDescription` も新内容に置き換え、localization.csv の `research.<guid>.name/.description` を追随。受け入れ: `原始研究7`〜`原始研究9` の文字列が v8 mod に残らない。
- R3 研究コスト（`consumeItems`）を次にする（0 は行削除）: 新1 板3・棒3・砕石2／新2 レンガ30／新3 青銅インゴット5・レンガ30／新4 青銅インゴット40・レンガ94・砕石84／新5 板10・青銅シート5・砕石30／新6以降は不変。
- R4 木のシャフトの `unlockBlock` を旧7（`0d76f2e5-…`）から旧6（`bc5e7786-…`）へ移す（`unlockItemRecipeView` の木のシャフトは旧7のまま）。受け入れ: 旧6の `unlockBlockGuids` に `3dda0801-…` があり旧7に無い。
- R5 ブロック設置費（`requiredItems`）: 風力掘削機 板2・砕石1／石窯 砕石5・レンガ5／原始的な粉砕機 合板1・砕石5・青銅シート1／燃料式風車 レンガ30・棒5・青銅シート3／原始的な加工機 合板2・青銅シート4。`placementsPerCost` は不変。
- R6 初期解放: 風力掘削機 `initialUnlocked: true`、items の 石・砕いた石材・石の斧 `initialUnlocked: true`。旧研究1・2の `unlockItemRecipeView`（石・砕石・石の斧）は削除と同時に消える。
- R7 challenges.json を ADR「チャレンジ構成（確定・32→26）」の26本に再生成する（表は Task 3 に逐語で載せる）。削除: 小石を3個拾う／石器を作る／石器を装備する／石の斧を作る／石の斧を装備する／旧研究1・2・3完了。新設: 木の鉱脈に風力掘削機を設置する（`blockPlaceOnVein`＋`veinRestrictedPlacement`＋`veinPin`）、燃料式風車を設置する、木のシャフトで風車と繋ぐ（`relativeBlockPlacePreview`）、粉砕機を設置して動かす（`gearConnectedBlock`＋`relativeBlockPlacePreview`）。個数変更: 板5→3、棒5→3、石5→3、砕石5→3。開幕スキット2本は先頭「木を伐採して原木を入手する」へ。クラフトUI説明（`recipe.craft-button` ハイライト・Tab）は「木の板を3枚作る」へ。既存チャレンジの GUID は不変。
- R8 localization.csv: 削除チャレンジの `challenge.*`/`challengeTutorial.*` 行と削除研究の `research.*` 行を消し、新規・変更分を追加（english/japanese/german の3言語）。受け入れ: `challengeTutorial.*` の GUID 集合 == challenges.json の文言付き tutorial の GUID 集合、`challenge.*` の GUID 集合 == 26本の集合。
- R9 研究ツリー座標を `tools`/`.claude/skills/master-refine/scripts/recalc_research_positions.py` で再計算し、`.mooreseditor/nodeGraph.v1.json` に削除ノードの `masterGuid` を残さない。
- R10 接続チュートリアルの座標データ（シャフト offset `(-1,0,2)` East、粉砕機 offset `(-4,0,2)` North）で実際に歯車が回ることを、本体の EditMode テストで検証する（風車に原木を入れて `CurrentRpm > 0`）。
- R11 マスタを push・PR し、本体 `.moorestech-external-revisions.json` をその push 済みコミットへ更新して PR を作る。
- やらないこと: レシピ（craft/machine）のアイテム構成変更／機械の解放時期変更（木のシャフト以外）／研究6（旧9）以降の変更／燃料式風車の二重解放（新3と「燃料式風車の作成」）の是正／鉱脈名ラベル／C# の変更。

## Global Constraints

- 本体の作業場所: `moores-wt new feature/early-game-compression-master --dir early-game-compression --from master --fetch` → `~/hermes-agent/data/repos/moorestech-worktrees/early-game-compression`（以下 `$WT`）。姉妹planマージ後の master から切ること（`git log --oneline -20 | grep placement-guided-tutorials` で確認）。
- マスタの作業場所: `~/hermes-agent/data/repos/moorestech-master-worktrees/early-game-compression`（branch `feature/early-game-compression`、Task 1 で作成。以下 `$MW`）。
- マスタJSONの書き戻しは既存インデントを判定して同じ indent で `json.dump(ensure_ascii=False)`。末尾改行の有無も元ファイルに合わせる（master-refine: v8 の master JSON は末尾改行なし）。
- localization.csv は行単位のテキスト操作（`csv` モジュールで全体を書き戻さない）。ヘッダ `key,Source,english,japanese,german`。カンマを含む文言は二重引用符で囲む。
- challenges.json は直接編集せず `python3 tools/tutorial_v3_port/generate_challenges.py` で再生成する。
- 実際の GUID・commitHash は必ず実行結果から取る（planの値は照合用）。
- テスト: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<正規表現>"`。
- コミットメッセージ末尾: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` と `Claude-Session: https://claude.ai/code/session_01Ts2pLxAukyhiJyiiqk4bXs`。
- Editor 起動時の `ExternalRepositorySyncService` によるピン書き戻し差分は `git checkout -- .moorestech-external-revisions.json` で捨て、意図した commitHash だけをコミットする。

---

## File Structure

マスタrepo（`$MW`、いずれも `server_v8/mods/moorestechAlphaMod_8/`）:
- Modify: `master/research.json` — 3ノード削除・改番・説明・コスト・木のシャフト移動
- Modify: `master/blocks.json` — 設置費5件・風力掘削機 initialUnlocked
- Modify: `master/items.json` — 石・砕いた石材・石の斧 initialUnlocked
- Modify: `master/challenges.json` — 再生成（26本）
- Modify: `localization/localization.csv`
- Modify: `.mooreseditor/nodeGraph.v1.json`（削除ノードの除去。パスは `find $MW -name 'nodeGraph.v1.json'` で確認）
- Modify: `tools/tutorial_v3_port/generate_challenges.py` — 表・新helper・validator

本体repo（`$WT`）:
- Modify: `.moorestech-external-revisions.json`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EarlyGame/EarlyGameGearTutorialLayoutTest.cs`（新規。ピン済みマスタの v8 で風車→シャフト→粉砕機が回ることを検証）

### 配置と前例（spec-architecture-review）

- チャレンジの正本は `generate_challenges.py` の CHALLENGES 表（0029/0033 plan の前例）。
- 研究・ブロック・アイテムの書き換えは `tools/` 配下の単発 Python ではなく plan 内の heredoc（1回限りの数値変更で再実行の需要が無い。0033 plan Task M1 と同形）。
- C# 側のローダー・Validator には手を入れない（「マスタ防御をローダーで吸収しない」）。
- 機能パリティ（死活表）: 消えるのは石器ライン（小石拾い・石器クラフト・装備ドラッグ説明）と研究1〜3。いずれもユーザー裁定で意図的に撤去。クラフトUI説明は木の板へ移設、Rキー説明は原始研究1（旧4）完了チャレンジに残る。

---

### Task 0: 本体 worktree の作成

- [ ] **Step 1: worktree を作る**

```bash
cd ~/hermes-agent/data/repos/moorestech && git fetch -q origin && git log --oneline origin/master -30 | grep -i "placement-guided-tutorials\|ADR 0038" 
moores-wt new feature/early-game-compression-master --dir early-game-compression --from master --fetch
```
Expected: 1行目で姉妹PRのマージコミットが見える（見えなければ姉妹planを先に完了させる）。worktree `~/hermes-agent/data/repos/moorestech-worktrees/early-game-compression` が作られる。

---

### Task M1: マスタ worktree と research.json（削除・改番・コスト・シャフト移動）

**Files（`$MW`）:**
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/research.json`
- Modify: `.mooreseditor/nodeGraph.v1.json`（存在すれば）

**Interfaces:**
- Produces: 根ノード `858bcb10-b8ba-478e-9bc5-473ca61281a2`（原始研究1）、以降 `b47c5e3c-…`(2) `bc5e7786-…`(3) `0d76f2e5-…`(4) `48f75a7e-…`(5) `3bca3b97-…`(6)。Task M3 の `research_by_name` はこの新名で引く。

- [ ] **Step 1: マスタ worktree を作る**

```bash
git -C ~/hermes-agent/data/repos/moorestech_master fetch -q origin
git -C ~/hermes-agent/data/repos/moorestech_master worktree add -b feature/early-game-compression ~/hermes-agent/data/repos/moorestech-master-worktrees/early-game-compression origin/master
cd ~/hermes-agent/data/repos/moorestech-master-worktrees/early-game-compression && git log --oneline -1 && grep -c initialEquipmentItems server_v8/mods/moorestechAlphaMod_8/master/items.json
```
Expected: HEAD は姉妹PR（`initialEquipmentItems`）マージ後、`grep -c` が 1。

- [ ] **Step 2: 削除前の参照件数を記録する（失敗するチェック）**

```bash
cd $MW && grep -rc "837e9697-8586-406e-a0f6-16a010050218\|424be8c1-c40c-4644-8104-06934c59b147\|07d6226c-ed14-4a6f-aa2a-6fa085fce8ec" server_v8 .mooreseditor 2>/dev/null | grep -v ":0$"
```
Expected: research.json・challenges.json・localization.csv（・nodeGraph）に一致行がある。

- [ ] **Step 3: research.json を書き換える**

```bash
cd $MW && python3 - <<'EOF'
import json
M='server_v8/mods/moorestechAlphaMod_8/master/'
raw=open(M+'research.json',encoding='utf-8').read(); d=json.loads(raw)
items={x['name']:x['itemGuid'] for x in json.load(open(M+'items.json',encoding='utf-8'))['data']}
blocks={b['name']:b['blockGuid'] for b in json.load(open(M+'blocks.json',encoding='utf-8'))['data']}
R1,R2,R3='837e9697-8586-406e-a0f6-16a010050218','424be8c1-c40c-4644-8104-06934c59b147','07d6226c-ed14-4a6f-aa2a-6fa085fce8ec'
R4,R5,R6,R7,R8,R9='858bcb10-b8ba-478e-9bc5-473ca61281a2','b47c5e3c-1b58-42c5-a477-d485d2eae747','bc5e7786-6759-4271-8095-836703b54490','0d76f2e5-be1c-4ad4-b460-97a8aad0495f','48f75a7e-36f3-4845-a0bc-f8de8b3d7baf','3bca3b97-14d7-4cc1-a661-2266670bb6cb'
by={r['researchNodeGuid']:r for r in d['data']}
for g in (R1,R2,R3): assert g in by, g
before=len(d['data']); d['data']=[r for r in d['data'] if r['researchNodeGuid'] not in (R1,R2,R3)]; assert len(d['data'])==before-3
by={r['researchNodeGuid']:r for r in d['data']}
assert by[R4]['prevResearchNodeGuids']==[R3]; by[R4]['prevResearchNodeGuids']=[]
def cost(*pairs): return [{'itemGuid':items[n],'itemCount':c} for n,c in pairs]
rename={R4:('原始研究1','石窯と木のチェストを解放する。粘土とレンガの精錬が始まる'),
        R5:('原始研究2','青銅の鉱石を石窯で精錬する技術。青銅インゴットが作れるようになる'),
        R6:('原始研究3','燃料式風車・木のシャフト・原始的な粉砕機を解放する。歯車動力で石と鉱石を砕く'),
        R7:('原始研究4','木の歯車と歯車ベルトコンベアを解放する。動力と搬送を組み合わせる'),
        R8:('原始研究5','原始的な加工機を解放する。板・棒・木釘・青銅シートを機械で作る'),
        R9:('原始研究6','原始的な採掘機を解放する')}
for g,(name,desc) in rename.items(): by[g]['researchNodeName']=name; by[g]['researchNodeDescription']=desc
by[R4]['consumeItems']=cost(('木の板',3),('木の棒',3),('砕いた石材',2))
by[R5]['consumeItems']=cost(('レンガ',30))
by[R6]['consumeItems']=cost(('青銅インゴット',5),('レンガ',30))
by[R7]['consumeItems']=cost(('青銅インゴット',40),('レンガ',94),('砕いた石材',84))
by[R8]['consumeItems']=cost(('木の板',10),('青銅シート',5),('砕いた石材',30))
# 木のシャフトのブロック解放を旧7→旧6へ / Move the wooden shaft block unlock from old-7 to old-6
shaft=blocks['木のシャフト']; moved=0
for a in by[R7]['clearedActions']:
    if a['gameActionType']=='unlockBlock' and shaft in a['gameActionParam']['unlockBlockGuids']:
        a['gameActionParam']['unlockBlockGuids'].remove(shaft); moved+=1
assert moved==1
for a in by[R6]['clearedActions']:
    if a['gameActionType']=='unlockBlock': a['gameActionParam']['unlockBlockGuids'].append(shaft); moved+=1; break
assert moved==2
indent=4 if raw.lstrip().startswith('{\n    ') else 2
open(M+'research.json','w',encoding='utf-8').write(json.dumps(d,ensure_ascii=False,indent=indent)+('\n' if raw.endswith('\n') else ''))
print('ok', len(d['data']))
EOF
git diff --stat
```
Expected: `ok 44`。diff は research.json のみで全文差分になっていない。

- [ ] **Step 4: nodeGraph から削除ノードを外す（ファイルがある場合）**

```bash
cd $MW && f=$(find . -name 'nodeGraph.v1.json' -path '*moorestechAlphaMod_8*' | head -1); echo "$f"; [ -n "$f" ] && python3 - "$f" <<'EOF'
import json,sys
p=sys.argv[1]; raw=open(p,encoding='utf-8').read(); d=json.loads(raw)
dead={'837e9697-8586-406e-a0f6-16a010050218','424be8c1-c40c-4644-8104-06934c59b147','07d6226c-ed14-4a6f-aa2a-6fa085fce8ec'}
def prune(obj):
    if isinstance(obj,list): return [prune(x) for x in obj if not (isinstance(x,dict) and x.get('masterGuid') in dead)]
    if isinstance(obj,dict): return {k:prune(v) for k,v in obj.items()}
    return obj
d=prune(d)
open(p,'w',encoding='utf-8').write(json.dumps(d,ensure_ascii=False,indent=2)+('\n' if raw.endswith('\n') else ''))
print('pruned')
EOF
```
Expected: ファイルがあれば `pruned`、無ければ空行（何もしない）。

- [ ] **Step 5: 研究ツリー座標を再計算する**

```bash
cd $MW && python3 ~/hermes-agent/data/repos/moorestech/.claude/skills/master-refine/scripts/recalc_research_positions.py --mod-dir $MW/server_v8/mods/moorestechAlphaMod_8 && git diff --stat
```
Expected: research.json の `UIPosition` が更新される（根 x=0、主鎖 y=0）。

- [ ] **Step 6: コミットする**

```bash
cd $MW && git add -A server_v8 .mooreseditor 2>/dev/null; git add -A server_v8 && git commit -m "data(v8): 原始研究1〜3を削除し改番・コスト圧縮・木のシャフトを研究3へ (moorestech ADR 0038)"
```

---

### Task M2: blocks.json / items.json（設置費・初期解放）

**Files（`$MW`）:**
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/blocks.json`、`items.json`

- [ ] **Step 1: 変更前の値を記録する（失敗するチェック）**

```bash
cd $MW && python3 - <<'EOF'
import json
M='server_v8/mods/moorestechAlphaMod_8/master/'
b={x['name']:x for x in json.load(open(M+'blocks.json',encoding='utf-8'))['data']}
i={x['name']:x for x in json.load(open(M+'items.json',encoding='utf-8'))['data']}
for n in ('風力掘削機','石窯','原始的な粉砕機','燃料式風車','原始的な加工機'):
    print(n, b[n]['initialUnlocked'], [(i2['itemCount']) for i2 in b[n]['requiredItems']])
for n in ('石','砕いた石材','石の斧'): print(n, i[n]['initialUnlocked'])
EOF
```
Expected: 風力掘削機 False [10,5]／石窯 False [20,5]／粉砕機 False [3,15,3]／風車 False [30,20,10]／加工機 False [5,10]、石・砕石・石の斧 False。

- [ ] **Step 2: 書き換える**

```bash
cd $MW && python3 - <<'EOF'
import json
M='server_v8/mods/moorestechAlphaMod_8/master/'
def rw(name, fn):
    p=M+name; raw=open(p,encoding='utf-8').read(); d=json.loads(raw); fn(d)
    indent=4 if raw.lstrip().startswith('{\n    ') else 2
    open(p,'w',encoding='utf-8').write(json.dumps(d,ensure_ascii=False,indent=indent)+('\n' if raw.endswith('\n') else ''))
items={x['name']:x['itemGuid'] for x in json.load(open(M+'items.json',encoding='utf-8'))['data']}
COSTS={'風力掘削機':[('木の板',2),('砕いた石材',1)],'石窯':[('砕いた石材',5),('レンガ',5)],
       '原始的な粉砕機':[('合板',1),('砕いた石材',5),('青銅シート',1)],'燃料式風車':[('レンガ',30),('木の棒',5),('青銅シート',3)],
       '原始的な加工機':[('合板',2),('青銅シート',4)]}
def blocks(d):
    hit=0
    for b in d['data']:
        if b['name'] in COSTS:
            # 既存行の構成（アイテム種別と順序）が同じことを確認してから個数だけ書き換える / Confirm same item set and order, then change counts only
            assert [r['itemGuid'] for r in b['requiredItems']]==[items[n] for n,_ in COSTS[b['name']]], b['name']
            for r,(n,c) in zip(b['requiredItems'],COSTS[b['name']]): r['itemCount']=c
            hit+=1
        if b['name']=='風力掘削機': b['initialUnlocked']=True
    assert hit==5, hit
def itemsfn(d):
    hit=0
    for x in d['data']:
        if x['name'] in ('石','砕いた石材','石の斧'): x['initialUnlocked']=True; hit+=1
    assert hit==3
rw('blocks.json',blocks); rw('items.json',itemsfn); print('ok')
EOF
git diff --stat
```
Expected: `ok`。2ファイルの小さな差分。`requiredItems` のキー名が `itemCount` でなく `count` 等なら実ファイルに合わせる（Step 1 の出力で確認済み）。

- [ ] **Step 3: コミットする**

```bash
cd $MW && git add -A server_v8 && git commit -m "data(v8): 序盤ブロックの設置費を圧縮し風力掘削機・石・砕石・石の斧を初期解放にする (moorestech ADR 0038)"
```

---

### Task M3: generate_challenges.py を26本の表へ組み替えて再生成

**Files（`$MW`）:**
- Modify: `tools/tutorial_v3_port/generate_challenges.py`
- Regenerate: `server_v8/mods/moorestechAlphaMod_8/master/challenges.json`

**Interfaces:**
- Consumes: Task M1 の新研究名（`原始研究1`〜`6`）、Task M2 の `initialUnlocked`、姉妹planの新type名（`blockPlaceOnVein` / `gearConnectedBlock` / `veinRestrictedPlacement` / `relativeBlockPlacePreview`）
- Produces: 26本の challenges.json。新規4本の GUID は key から導出（`guid_for('木の鉱脈に風力掘削機を設置する')` 等）

- [ ] **Step 1: helper と validator を足す**

`generate_challenges.py` の `def research_node_ui(...)` の直後に追加:

```python
# 鉱脈限定設置: チャレンジ中は対象鉱脈だけを強調し、そのブロックは対象鉱脈にしか置けない（ADR 0038 決定3）
# Vein-restricted placement: only the target vein is highlighted and the block may only go there during the challenge (ADR 0038 decision 3)
def vein_restrict(vein_name, block_name): return ('veinRestrictedPlacement', {'veinGuid': veins[vein_name], 'blockGuid': blocks[block_name]})
# 相対座標ゴースト: 最寄りのアンカーブロック原点＋offset に対象ブロックのゴーストを出す
# Relative ghost: shows the target block's ghost at nearest-anchor origin + offset
def relative_preview(anchor_name, block_name, offset, direction, text): return ('relativeBlockPlacePreview', {
    'anchorBlockGuid': blocks[anchor_name], 'blockGuid': blocks[block_name], 'offset': list(offset), 'blockDirection': direction, 'message': text})
```

`# task:` のコメント行を次に置き換える:

```python
# task: 'item'=inInventoryItem, 'craft'=createItem, 'equip'=equipItem, 'block'=blockPlace, 'research'=completeResearch,
#       'blockOnVein'=blockPlaceOnVein（target_name=(block, vein) のタプル）, 'gearConnected'=gearConnectedBlock
```

validator の `if task == 'block':` ブロックを次に置き換え（初期解放ブロックを許容し、新2種を扱う）:

```python
    if task in ('block', 'gearConnected'):
        g = blocks[target]
        if g not in research_blocks and not block_initial_unlocked[g]:
            errors.append(f'{title}: ブロック {target} を解放するresearchが無く初期解放でもない')
    elif task == 'blockOnVein':
        g = blocks[target[0]]
        if g not in research_blocks and not block_initial_unlocked[g]:
            errors.append(f'{title}: ブロック {target[0]} を解放するresearchが無く初期解放でもない')
        if target[1] not in veins:
            errors.append(f'{title}: 鉱脈 {target[1]} が見つからない')
```
`research_blocks = set()` の直前に追加:

```python
block_initial_unlocked = {b['blockGuid']: bool(b.get('initialUnlocked')) for b in load(V8, 'blocks.json')['data']}
```

構築ループの `else:`（blockPlace）の前に追加:

```python
    elif task == 'blockOnVein':
        c['taskCompletionType'] = 'blockPlaceOnVein'
        c['taskParam'] = {'blockGuid': blocks[target[0]], 'veinGuid': veins[target[1]]}
    elif task == 'gearConnected':
        c['taskCompletionType'] = 'gearConnectedBlock'
        c['taskParam'] = {'blockGuid': blocks[target]}
```

- [ ] **Step 2: CHALLENGES 表を置き換える**

`CHALLENGES = [ ... ]` 全体を次に置き換える（key は既存行は旧titleのまま・新規4行は新title）:

```python
CHALLENGES = [
    ('木を伐採して原木を入手する', '木を伐採して原木を入手する', '装備している石の斧で木を伐採し、原木を3個集めよう', 'item', '原木', 3,
     [earn_pin('原木', '石の斧で木を伐採'), ui('challenge.current-hud', '左上で現在の目標を確認する')], '原木'),
    ('木の板を5枚作る', '木の板を3枚作る', 'Tabでインベントリを開き、原木から木の板を3枚クラフトしよう', 'item', '木の板', 3,
     [ui('recipe.craft-button', '②クラフトボタンを長押し'), iv('木の板', '①木の板を選択'), key('GameScreen', 'Tab', 'インベントリを開く')], '木の板'),
    ('木の棒を5本作る', '木の棒を3本作る', '木の板から木の棒を3本クラフトしよう', 'item', '木の棒', 3,
     [iv('木の棒', '木の板から木の棒を作る')], '木の棒'),
    ('石を採掘する', '石を3個採掘する', '石鉱脈を石の斧で叩いて石を3個採掘しよう', 'item', '石', 3,
     [vein('石鉱脈', '石鉱脈から石を採掘')], '石'),
    ('砕いた石材を5個作る', '砕いた石材を3個作る', '石から砕いた石材を3個クラフトしよう', 'item', '砕いた石材', 3,
     [iv('砕いた石材', '石から砕いた石材を作る')], '砕いた石材'),
    ('風力掘削機を設置する', '風力掘削機を設置する', 'Bでビルドメニューを開き、風力掘削機を石鉱脈の上に設置しよう。掘削機は動力なしで鉱脈を掘り続ける', 'block', '風力掘削機', 1,
     [vein('石鉱脈', '石鉱脈の上に設置'), key('GameScreen', 'B', 'ビルドメニューを開く')], '砕いた石材'),
    ('木の鉱脈に風力掘削機を設置する', '木の鉱脈に風力掘削機を設置する', '原木は木の鉱脈からも採れる。ハイライトされた木の鉱脈の上に風力掘削機を設置しよう', 'blockOnVein', ('風力掘削機', '原木鉱脈'), None,
     [vein_restrict('原木鉱脈', '風力掘削機'), vein('原木鉱脈', '木の鉱脈。掘削機を置くと原木が自動で採れる')], '原木'),
    ('原始研究4を完了する', '原始研究1を完了する', 'Rキーで研究画面を開き、木の板3枚・木の棒3本・砕いた石材2個で原始研究1を完了して、石窯を解放しよう', 'research', '原始研究1', None,
     [research_node_ui('原始研究1', '原始研究1を完了する'), key('GameScreen', 'R', '研究画面を開く'), key('PlayerInventory', 'R', '研究画面を開く')], '砕いた石材'),
    ('粘土を入手する', '粘土を入手する', '粘土鉱脈の上に風力掘削機を設置して粘土を1個入手しよう', 'item', '粘土', 1,
     [vein('粘土鉱脈', '粘土鉱脈の上に掘削機を設置')], '粘土'),
    ('レンガを作る', 'レンガを作る', '粘土からレンガをクラフトしよう', 'craft', 'レンガ', None,
     [iv('レンガ', '粘土からレンガを作る')], 'レンガ'),
    ('石窯を設置する', '石窯を設置する', 'Bでビルドメニューを開き、石窯を設置しよう。石窯は粘土と原木からレンガを焼く', 'block', '石窯', 1,
     [key('GameScreen', 'B', 'ビルドメニューを開く')], 'レンガ'),
    ('原始研究5を完了する', '原始研究2を完了する', '研究画面でレンガ30個を使い原始研究2を完了して、青銅の精錬を解放しよう', 'research', '原始研究2', None,
     [research_node_ui('原始研究2', '原始研究2を完了する')], 'レンガ'),
    ('青銅の鉱石を5個採掘する', '青銅の鉱石を5個採掘する', '青銅の鉱脈の上に風力掘削機を設置して青銅の鉱石を5個採掘しよう', 'item', '青銅の鉱石', 5,
     [vein('青銅の鉱石鉱脈', '青銅の鉱脈の上に掘削機を設置')], '青銅の鉱石'),
    ('青銅鉱石の粉を3個作る', '青銅鉱石の粉を3個作る', '青銅の鉱石から青銅鉱石の粉を3個クラフトしよう', 'item', '青銅鉱石の粉', 3, [], '青銅鉱石の粉'),
    ('青銅インゴットを作る', '青銅インゴットを作る', '石窯に青銅鉱石の粉と原木を入れて青銅インゴットを精錬しよう', 'item', '青銅インゴット', 1, [], '青銅インゴット'),
    ('原始研究6を完了する', '原始研究3を完了する', '研究画面で青銅インゴット5個・レンガ30個を使い原始研究3を完了して、燃料式風車・木のシャフト・原始的な粉砕機を解放しよう', 'research', '原始研究3', None,
     [research_node_ui('原始研究3', '原始研究3を完了する')], '青銅インゴット'),
    ('燃料式風車を設置する', '燃料式風車を設置する', 'Bでビルドメニューを開き、燃料式風車を設置しよう。風車は原木を燃料に歯車動力を生む', 'block', '燃料式風車', 1,
     [key('GameScreen', 'B', 'ビルドメニューを開く')], 'レンガ'),
    ('木のシャフトで風車と繋ぐ', '木のシャフトで風車と繋ぐ', '風車の歯車の隣にゴーストが出る。木のシャフトをそこに設置して動力を伝えよう', 'block', '木のシャフト', 1,
     [relative_preview('燃料式風車', '木のシャフト', (-1, 0, 2), 'East', 'ここに木のシャフトを設置')], '木の棒'),
    ('粉砕機を設置して動かす', '粉砕機を設置して動かす', 'シャフトの先のゴーストに原始的な粉砕機を設置し、風車に原木を入れて粉砕機を回そう', 'gearConnected', '原始的な粉砕機', None,
     [relative_preview('燃料式風車', '原始的な粉砕機', (-4, 0, 2), 'North', 'ここに粉砕機を設置')], '砕いた石材'),
    ('青銅シートを作る', '青銅シートを作る', '青銅インゴット3個から青銅シートをクラフトしよう', 'craft', '青銅シート', None, [], '青銅シート'),
    ('木釘を9本作る', '木釘を9本作る', '木の棒から木釘を9本クラフトしよう', 'item', '木釘', 9, [], '木釘'),
    ('合板を作る', '合板を作る', '木釘と木の板で合板をクラフトしよう', 'craft', '合板', None, [], '合板'),
    ('原始研究7を完了する', '原始研究4を完了する', '研究画面で青銅インゴット40個・レンガ94個・砕いた石材84個を使い原始研究4を完了して、木の歯車と歯車ベルトコンベアを解放しよう', 'research', '原始研究4', None,
     [research_node_ui('原始研究4', '原始研究4を完了する')], '合板'),
    ('原始研究8を完了する', '原始研究5を完了する', '研究画面で木の板10枚・青銅シート5個・砕いた石材30個を使い原始研究5を完了して、原始的な加工機を解放しよう', 'research', '原始研究5', None,
     [research_node_ui('原始研究5', '原始研究5を完了する')], '青銅シート'),
    ('原始研究9を完了する', '原始研究6を完了する', '研究画面で木の板100枚・木の棒200本・砕いた石材50個・レンガ50個・青銅シート30個を使い原始研究6を完了して、原始的な採掘機を解放しよう', 'research', '原始研究6', None,
     [research_node_ui('原始研究6', '原始研究6を完了する')], '青銅シート'),
    ('木材の組み立てを完了する', '木材の組み立てを完了する', '研究画面で木の板200枚・木の棒200本・木釘600本・砕いた石材150個・青銅シート100個を使い木材の組み立てを完了して、補強棒材と木のフレームを解放しよう', 'research', '木材の組み立て', None,
     [research_node_ui('木材の組み立て', '木材の組み立てを完了する')], '木釘'),
    ('補強棒材を作る', '補強棒材を作る', '木の棒と青銅シートで補強棒材をクラフトしよう', 'craft', '補強棒材', None, [], '補強棒材'),
    ('木のフレームを作る', '木のフレームを作る', '補強棒材と合板で木のフレームをクラフトしよう', 'craft', '木のフレーム', None, [], '木のフレーム'),
]
```
（28行。ADR の「26本」は 25〜26 番目を「木材の組み立て・補強棒材・木のフレーム（据え置き）」と1行にまとめた表記で、実体は28本。以降「28本」を正とし ADR にも追記する。）

`out[0]['startedActions'] = [...]`（開幕スキット2本）は先頭が「木を伐採して原木を入手する」になるので**変更不要**。カテゴリの `IconItem` は `items['石器']` のまま。

- [ ] **Step 3: 再生成して確認する**

```bash
cd $MW && python3 tools/tutorial_v3_port/generate_challenges.py && python3 - <<'EOF'
import json
d=json.load(open('server_v8/mods/moorestechAlphaMod_8/master/challenges.json',encoding='utf-8'))
cs=d['data'][0]['challenges']; print(len(cs))
for c in cs: print(c['taskCompletionType'].ljust(18), c['title'], [t['tutorialType'] for t in c['tutorials']], 'skit' if c['startedActions'] else '')
keep={'a6497c0b-82eb-5280-82c7-d339bc32de14':'風力掘削機を設置する','14f3b765-be4d-51ef-983f-685c043c265b':'粘土を入手する','603e84c0-10b1-501f-a03d-598584d34d58':'石窯を設置する','90a98c1f-2eda-5e7a-8fee-099c40f639e0':'木の板を3枚作る','7b9ddaf3-2d63-5876-83ed-03602bf44742':'原始研究1を完了する'}
by={c['challengeGuid']:c['title'] for c in cs}
for g,t in keep.items(): assert by[g]==t,(g,by.get(g))
print('guid-stable ok')
EOF
```
Expected: `OK: 28 challenges`、一覧の先頭が `inInventoryItem 木を伐採して原木を入手する [mapObjectPin, uiHighLight] skit`、7番目が `blockPlaceOnVein ... [veinRestrictedPlacement, veinPin]`、18/19番目が `blockPlace 木のシャフトで風車と繋ぐ [relativeBlockPlacePreview]` / `gearConnectedBlock 粉砕機を設置して動かす [relativeBlockPlacePreview]`、`guid-stable ok`。

- [ ] **Step 4: 削除GUIDの残存が無いことを確認する**

```bash
cd $MW && grep -rc "837e9697-8586-406e-a0f6-16a010050218\|424be8c1-c40c-4644-8104-06934c59b147\|07d6226c-ed14-4a6f-aa2a-6fa085fce8ec\|bd5262ed-fbd4-51e0-a75d-2944f366e10a\|7bafc2cf-d55c-5141-805f-99e0b78a9945\|24f72113-495c-5302-af05-8b1f0d0c1091\|d94f0d27-1acb-5bb4-8174-810b7f4bb934\|14bb62b0-d7a9-5538-b11d-ab5353bd6795\|b49073fb-b8b6-5bf1-a13c-99db179feb20\|31aef233-8892-5f35-9fa9-7976e2d98778\|8b2d87d1-3ee2-5af4-9f11-c8fe9e966930" server_v8/mods/moorestechAlphaMod_8/master | grep -v ":0$"; echo "exit=$?"
```
Expected: `exit=1`（master 配下に一致なし。localization.csv は Task M4 で消す）。

- [ ] **Step 5: コミットする**

```bash
cd $MW && git add tools/tutorial_v3_port/generate_challenges.py server_v8/mods/moorestechAlphaMod_8/master/challenges.json && git commit -m "data(v8): 生きる基盤チャレンジを石器ライン抜きの28本へ再構成し木の鉱脈・歯車接続チュートリアルを足す (moorestech ADR 0038)"
```

---

### Task M4: localization.csv の追随（研究・チャレンジ・チュートリアル）

**Files（`$MW`）:**
- Modify: `server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`

- [ ] **Step 1: 現状の孤児行数を出す（失敗するチェック）**

```bash
cd $MW && python3 - <<'EOF'
import json,re
M='server_v8/mods/moorestechAlphaMod_8/'
lines=open(M+'localization/localization.csv',encoding='utf-8').read().split('\n')
ch=json.load(open(M+'master/challenges.json',encoding='utf-8'))['data'][0]['challenges']
rs=json.load(open(M+'master/research.json',encoding='utf-8'))['data']
cg={c['challengeGuid'] for c in ch}; tg={t['tutorialGuid'] for c in ch for t in c['tutorials']}; rg={r['researchNodeGuid'] for r in rs}
orphan=[l for l in lines if (l.startswith('challenge.') and l.split('.')[1] not in cg) or (l.startswith('challengeTutorial.') and l.split('.')[1] not in tg) or (l.startswith('research.') and l.split('.')[1] not in rg)]
print('orphans', len(orphan))
EOF
```
Expected: 0 より大きい（削除チャレンジ8本×2行＋その tutorial 行＋研究3本×2行）。

- [ ] **Step 2: 同期スクリプトを実行する**

```bash
cd $MW && python3 - <<'EOF'
import json
M='server_v8/mods/moorestechAlphaMod_8/'
CSV=M+'localization/localization.csv'
lines=open(CSV,encoding='utf-8').read().split('\n')
ch=json.load(open(M+'master/challenges.json',encoding='utf-8'))['data'][0]['challenges']
rs=json.load(open(M+'master/research.json',encoding='utf-8'))['data']
def q(s): return '"'+s.replace('"','""')+'"' if (',' in s or '"' in s) else s
def row(key,ja,en,de): return ','.join([key,q(ja),q(en),q(ja),q(de)])
# 新規・変更分の英独文言。キーは日本語文言 / English and German for new or changed texts, keyed by the Japanese text
T={
 '装備している石の斧で木を伐採し、原木を3個集めよう':('Chop trees with the equipped stone axe and collect 3 logs','Fälle Bäume mit der ausgerüsteten Steinaxt und sammle 3 Stämme'),
 '石の斧で木を伐採':('Chop the tree with the stone axe','Fälle den Baum mit der Steinaxt'),
 '左上で現在の目標を確認する':('Check the current goal at the top left','Prüfe das aktuelle Ziel oben links'),
 '木の板を3枚作る':('Make 3 Wooden Planks','Stelle 3 Holzbretter her'),
 'Tabでインベントリを開き、原木から木の板を3枚クラフトしよう':('Open the inventory with Tab and craft 3 wooden planks from logs','Öffne das Inventar mit Tab und fertige 3 Holzbretter aus Stämmen'),
 '②クラフトボタンを長押し':('2. Hold the craft button','2. Halte den Fertigen-Knopf gedrückt'),
 '①木の板を選択':('1. Select the wooden plank','1. Wähle das Holzbrett'),
 'インベントリを開く':('Open the inventory','Öffne das Inventar'),
 '木の棒を3本作る':('Make 3 Wooden Sticks','Stelle 3 Holzstäbe her'),
 '木の板から木の棒を3本クラフトしよう':('Craft 3 wooden sticks from wooden planks','Fertige 3 Holzstäbe aus Holzbrettern'),
 '石を3個採掘する':('Mine 3 Stones','Baue 3 Steine ab'),
 '石鉱脈を石の斧で叩いて石を3個採掘しよう':('Hit the stone vein with the stone axe and mine 3 stones','Schlage mit der Steinaxt auf die Steinader und baue 3 Steine ab'),
 '砕いた石材を3個作る':('Make 3 Crushed Stones','Stelle 3 zerkleinerte Steine her'),
 '石から砕いた石材を3個クラフトしよう':('Craft 3 crushed stones from stone','Fertige 3 zerkleinerte Steine aus Stein'),
 'Bでビルドメニューを開き、風力掘削機を石鉱脈の上に設置しよう。掘削機は動力なしで鉱脈を掘り続ける':('Open the build menu with B and place the wind drill on the stone vein. The drill keeps mining the vein without power','Öffne das Baumenü mit B und platziere den Windbohrer auf der Steinader. Der Bohrer baut die Ader ohne Energie ab'),
 '石鉱脈の上に設置':('Place it on the stone vein','Platziere ihn auf der Steinader'),
 '木の鉱脈に風力掘削機を設置する':('Place a Wind Drill on the Wood Vein','Platziere einen Windbohrer auf der Holzader'),
 '原木は木の鉱脈からも採れる。ハイライトされた木の鉱脈の上に風力掘削機を設置しよう':('Logs also come from wood veins. Place a wind drill on the highlighted wood vein','Stämme kommen auch aus Holzadern. Platziere einen Windbohrer auf der hervorgehobenen Holzader'),
 '木の鉱脈。掘削機を置くと原木が自動で採れる':('Wood vein. A drill here gathers logs automatically','Holzader. Ein Bohrer hier sammelt automatisch Stämme'),
 '原始研究1を完了する':('Complete Primitive Research 1','Schließe Primitive Forschung 1 ab'),
 'Rキーで研究画面を開き、木の板3枚・木の棒3本・砕いた石材2個で原始研究1を完了して、石窯を解放しよう':('Open the research screen with R and complete Primitive Research 1 with 3 planks, 3 sticks and 2 crushed stones to unlock the stone kiln','Öffne den Forschungsbildschirm mit R und schließe Primitive Forschung 1 mit 3 Brettern, 3 Stäben und 2 zerkleinerten Steinen ab, um den Steinofen freizuschalten'),
 'Bでビルドメニューを開き、石窯を設置しよう。石窯は粘土と原木からレンガを焼く':('Open the build menu with B and place the stone kiln. It fires bricks from clay and logs','Öffne das Baumenü mit B und platziere den Steinofen. Er brennt Ziegel aus Lehm und Stämmen'),
 'ビルドメニューを開く':('Open the build menu','Öffne das Baumenü'),
 '原始研究2を完了する':('Complete Primitive Research 2','Schließe Primitive Forschung 2 ab'),
 '研究画面でレンガ30個を使い原始研究2を完了して、青銅の精錬を解放しよう':('Complete Primitive Research 2 with 30 bricks to unlock bronze smelting','Schließe Primitive Forschung 2 mit 30 Ziegeln ab, um das Bronzeschmelzen freizuschalten'),
 '原始研究3を完了する':('Complete Primitive Research 3','Schließe Primitive Forschung 3 ab'),
 '研究画面で青銅インゴット5個・レンガ30個を使い原始研究3を完了して、燃料式風車・木のシャフト・原始的な粉砕機を解放しよう':('Complete Primitive Research 3 with 5 bronze ingots and 30 bricks to unlock the fuel windmill, wooden shaft and primitive crusher','Schließe Primitive Forschung 3 mit 5 Bronzebarren und 30 Ziegeln ab, um Brennstoff-Windmühle, Holzwelle und primitiven Brecher freizuschalten'),
 '燃料式風車を設置する':('Place a Fuel Windmill','Platziere eine Brennstoff-Windmühle'),
 'Bでビルドメニューを開き、燃料式風車を設置しよう。風車は原木を燃料に歯車動力を生む':('Open the build menu with B and place the fuel windmill. It burns logs to produce gear power','Öffne das Baumenü mit B und platziere die Brennstoff-Windmühle. Sie verbrennt Stämme und erzeugt Zahnradkraft'),
 '木のシャフトで風車と繋ぐ':('Connect a Wooden Shaft to the Windmill','Verbinde eine Holzwelle mit der Windmühle'),
 '風車の歯車の隣にゴーストが出る。木のシャフトをそこに設置して動力を伝えよう':('A ghost appears next to the windmill gear. Place a wooden shaft there to carry the power','Neben dem Windmühlenzahnrad erscheint ein Geist. Platziere dort eine Holzwelle, um die Kraft weiterzuleiten'),
 'ここに木のシャフトを設置':('Place the wooden shaft here','Platziere die Holzwelle hier'),
 '粉砕機を設置して動かす':('Place and Run the Crusher','Platziere und betreibe den Brecher'),
 'シャフトの先のゴーストに原始的な粉砕機を設置し、風車に原木を入れて粉砕機を回そう':('Place the primitive crusher on the ghost past the shaft, then put logs into the windmill to run it','Platziere den primitiven Brecher auf dem Geist hinter der Welle und lege Stämme in die Windmühle, um ihn zu betreiben'),
 'ここに粉砕機を設置':('Place the crusher here','Platziere den Brecher hier'),
 '原始研究4を完了する':('Complete Primitive Research 4','Schließe Primitive Forschung 4 ab'),
 '研究画面で青銅インゴット40個・レンガ94個・砕いた石材84個を使い原始研究4を完了して、木の歯車と歯車ベルトコンベアを解放しよう':('Complete Primitive Research 4 with 40 bronze ingots, 94 bricks and 84 crushed stones to unlock wooden gears and gear belt conveyors','Schließe Primitive Forschung 4 mit 40 Bronzebarren, 94 Ziegeln und 84 zerkleinerten Steinen ab, um Holzzahnräder und Zahnrad-Förderbänder freizuschalten'),
 '原始研究5を完了する':('Complete Primitive Research 5','Schließe Primitive Forschung 5 ab'),
 '研究画面で木の板10枚・青銅シート5個・砕いた石材30個を使い原始研究5を完了して、原始的な加工機を解放しよう':('Complete Primitive Research 5 with 10 planks, 5 bronze sheets and 30 crushed stones to unlock the primitive processor','Schließe Primitive Forschung 5 mit 10 Brettern, 5 Bronzeblechen und 30 zerkleinerten Steinen ab, um die primitive Verarbeitungsmaschine freizuschalten'),
 '原始研究6を完了する':('Complete Primitive Research 6','Schließe Primitive Forschung 6 ab'),
 '研究画面で木の板100枚・木の棒200本・砕いた石材50個・レンガ50個・青銅シート30個を使い原始研究6を完了して、原始的な採掘機を解放しよう':('Complete Primitive Research 6 with 100 planks, 200 sticks, 50 crushed stones, 50 bricks and 30 bronze sheets to unlock the primitive miner','Schließe Primitive Forschung 6 mit 100 Brettern, 200 Stäben, 50 zerkleinerten Steinen, 50 Ziegeln und 30 Bronzeblechen ab, um den primitiven Bergbauer freizuschalten'),
 '原始研究1':('Primitive Research 1','Primitive Forschung 1'),'原始研究2':('Primitive Research 2','Primitive Forschung 2'),'原始研究3':('Primitive Research 3','Primitive Forschung 3'),
 '原始研究4':('Primitive Research 4','Primitive Forschung 4'),'原始研究5':('Primitive Research 5','Primitive Forschung 5'),'原始研究6':('Primitive Research 6','Primitive Forschung 6'),
 '石窯と木のチェストを解放する。粘土とレンガの精錬が始まる':('Unlocks the stone kiln and wooden chest. Clay and brick firing begins','Schaltet Steinofen und Holzkiste frei. Das Brennen von Lehm und Ziegeln beginnt'),
 '青銅の鉱石を石窯で精錬する技術。青銅インゴットが作れるようになる':('Smelting bronze ore in the stone kiln. Bronze ingots become available','Bronzeerz im Steinofen schmelzen. Bronzebarren werden verfügbar'),
 '燃料式風車・木のシャフト・原始的な粉砕機を解放する。歯車動力で石と鉱石を砕く':('Unlocks the fuel windmill, wooden shaft and primitive crusher. Crush stone and ore with gear power','Schaltet Brennstoff-Windmühle, Holzwelle und primitiven Brecher frei. Zerkleinere Stein und Erz mit Zahnradkraft'),
 '木の歯車と歯車ベルトコンベアを解放する。動力と搬送を組み合わせる':('Unlocks wooden gears and gear belt conveyors. Combine power and transport','Schaltet Holzzahnräder und Zahnrad-Förderbänder frei. Kombiniere Kraft und Transport'),
 '原始的な加工機を解放する。板・棒・木釘・青銅シートを機械で作る':('Unlocks the primitive processor. Make planks, sticks, nails and bronze sheets by machine','Schaltet die primitive Verarbeitungsmaschine frei. Fertige Bretter, Stäbe, Nägel und Bronzebleche maschinell'),
 '原始的な採掘機を解放する':('Unlocks the primitive miner','Schaltet den primitiven Bergbauer frei'),
}
def text_of(t):
    p=t['tutorialParam']; return p.get('pinText') or p.get('highLightText') or p.get('controlText') or p.get('message')
wanted={}
for c in ch:
    wanted[f"challenge.{c['challengeGuid']}.title"]=c['title']; wanted[f"challenge.{c['challengeGuid']}.summary"]=c['summary']
    for t in c['tutorials']:
        s=text_of(t)
        if s: wanted[f"challengeTutorial.{t['tutorialGuid']}.text"]=s
for r in rs:
    wanted[f"research.{r['researchNodeGuid']}.name"]=r['researchNodeName']; wanted[f"research.{r['researchNodeGuid']}.description"]=r['researchNodeDescription']
existing={l.split(',',1)[0]:l for l in lines if l}
out=[]; added=0; replaced=0; removed=0
for l in lines:
    if not l: out.append(l); continue
    key=l.split(',',1)[0]
    if key.startswith(('challenge.','challengeTutorial.','research.')):
        if key not in wanted: removed+=1; continue
        ja=wanted[key]
        # 既存行の日本語が同じなら据え置き。違えば英独を辞書から引いて置き換える / Keep the row when the Japanese is unchanged; otherwise rebuild it from the dictionary
        cur=l.split(',')
        if ja in l: out.append(l); continue
        assert ja in T, ('no translation for', key, ja)
        out.append(row(key, ja, T[ja][0], T[ja][1])); replaced+=1
    else: out.append(l)
for key,ja in wanted.items():
    if key in existing: continue
    assert ja in T, ('no translation for', key, ja)
    out.append(row(key, ja, T[ja][0], T[ja][1])); added+=1
open(CSV,'w',encoding='utf-8').write('\n'.join(out))
print('added',added,'replaced',replaced,'removed',removed)
EOF
```
Expected: `added` ≈ 20（新チャレンジ4本×2＋新チュートリアル≈9＋改番研究の説明6）、`replaced` ≈ 20、`removed` ≈ 30。`AssertionError: no translation` が出たらその日本語文言を `T` に追加して再実行（既存行から流用できる文言は既存行が残るので辞書不要）。

- [ ] **Step 3: 整合を確認する**

Step 1 のスクリプトを再実行。Expected: `orphans 0`。さらに:

```bash
cd $MW && grep -c "原始研究[789]" server_v8/mods/moorestechAlphaMod_8/localization/localization.csv server_v8/mods/moorestechAlphaMod_8/master/*.json; echo "exit=$?"
```
Expected: 全ファイル `0`。

- [ ] **Step 4: コミットする**

```bash
cd $MW && git add server_v8/mods/moorestechAlphaMod_8/localization/localization.csv && git commit -m "data(v8): 研究改番とチャレンジ再構成に localization.csv を追随させる (moorestech ADR 0038)"
```

---

### Task M5: マスタの機械検証・push・PR

- [ ] **Step 1: 到達可能性と参照整合を機械検証する**

```bash
cd $MW && python3 tools/tutorial_v3_port/generate_challenges.py && git status --short
```
Expected: `OK: 28 challenges` かつ差分なし（再生成が冪等）。

- [ ] **Step 2: push と PR**

```bash
cd $MW && git push -u origin feature/early-game-compression
gh pr create --repo moorestech/moorestech_master --base master --head feature/early-game-compression --title "data(v8): 序盤圧縮 — 原始研究1〜3削除・要求数圧縮・チャレンジ28本再構成 (moorestech ADR 0038)" --body "moorestech ADR 0038。本体PR: <後で追記>

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01Ts2pLxAukyhiJyiiqk4bXs"
git rev-parse HEAD
```
Expected: PR URL とコミットハッシュ（Task 1 のピンに使う）。

---

### Task 1: 本体ピン更新と接続チュートリアル座標の検証テスト

**Files（`$WT`）:**
- Modify: `.moorestech-external-revisions.json`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EarlyGame/EarlyGameGearTutorialLayoutTest.cs`

- [ ] **Step 1: ピンを更新する**

```bash
cd $WT && python3 - <<'EOF'
import json,subprocess,os
MW=os.path.expanduser('~/hermes-agent/data/repos/moorestech-master-worktrees/early-game-compression')
sha=subprocess.check_output(['git','-C',MW,'rev-parse','HEAD']).decode().strip()
p='.moorestech-external-revisions.json'; d=json.load(open(p))
for r in d['repositories']:
    if r['key']=='moorestech_master': r['commitHash']=sha
json.dump(d,open(p,'w'),indent=2); open(p,'a').write('\n'); print(sha)
EOF
git diff .moorestech-external-revisions.json
```
Expected: `commitHash` だけの差分。Editor を再起動または `uloop compile` でピンが `checkout --detach` されることを `git -C ../moorestech_master log --oneline -1` で確認。

- [ ] **Step 2: 失敗するテストを書く（ピン済み v8 で風車→シャフト→粉砕機が回る）**

```csharp
using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using NUnit.Framework;
using Server.Boot;
using UnityEngine;

namespace Client.Tests.EarlyGame
{
    /// <summary>
    ///     challenges.json の接続チュートリアル（風車→シャフト(-1,0,2) East→粉砕機(-4,0,2) North）が実際に動力を伝えることを、ピン済み v8 マスタで確かめる
    ///     Proves the connection tutorial layout in challenges.json (windmill → shaft (-1,0,2) East → crusher (-4,0,2) North) really carries gear power on the pinned v8 master
    /// </summary>
    public class EarlyGameGearTutorialLayoutTest
    {
        private static readonly Vector3Int WindmillOrigin = new(100, 0, 100);
        private static readonly Vector3Int ShaftOffset = new(-1, 0, 2);
        private static readonly Vector3Int CrusherOffset = new(-4, 0, 2);

        [Test]
        public void 風車の隣のシャフトと粉砕機が回る()
        {
            var v8 = Support.PinnedMasterRepository.ResolveServerDirectory("server_v8");
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(v8));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(BlockByName("燃料式風車"), WindmillOrigin, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var windmill);
            world.TryAddBlock(BlockByName("木のシャフト"), WindmillOrigin + ShaftOffset, BlockDirection.East, Array.Empty<BlockCreateParam>(), out var shaft);
            world.TryAddBlock(BlockByName("原始的な粉砕機"), WindmillOrigin + CrusherOffset, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var crusher);

            // 風車は原木を燃やして初めて回る
            // The windmill only turns once it burns logs
            InsertFuel(windmill, "原木", 10);
            for (var i = 0; i < 10; i++) GameUpdater.UpdateOneTick();

            Assert.Greater(shaft.GetComponent<IGearEnergyTransformer>().CurrentRpm.AsPrimitive(), 0f, "the shaft is not connected to the windmill");
            Assert.Greater(crusher.GetComponent<IGearEnergyTransformer>().CurrentRpm.AsPrimitive(), 0f, "the crusher is not connected to the shaft");
        }

        private static BlockId BlockByName(string name)
        {
            return MasterHolder.BlockMaster.GetBlockAllIds().First(id => MasterHolder.BlockMaster.GetBlockMaster(id).Name == name);
        }

        private static void InsertFuel(IBlock windmill, string itemName, int count)
        {
            var itemId = MasterHolder.ItemMaster.GetItemAllIds().First(id => MasterHolder.ItemMaster.GetItemMaster(id).Name == itemName);
            windmill.GetComponent<Game.Block.Interface.Component.IBlockInventory>().InsertItem(ServerContext.ItemStackFactory.Create(itemId, count));
        }
    }
}
```
`Support.PinnedMasterRepository` の実メソッド名は `moorestech_client/Assets/Scripts/Client.Tests/Support/PinnedMasterRepository.cs` を読んで合わせる（v8 の mods ディレクトリを返すもの）。`IBlockInventory`/`GetItemAllIds`/`GetItemMaster` の実名は `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/` と `Core.Master/ItemMaster.cs` で確認する。燃料投入は `EditModeInPlayingTestUtil.InsertItemToBlock` の実装を参考にする。

- [ ] **Step 3: 実行する**

```bash
cd $WT && uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "EarlyGameGearTutorialLayoutTest"
```
Expected: PASS。**FAIL した場合の手順（座標・向きの是正）**: (1) `python3 -c` で v8 blocks.json から 燃料式風車・木のシャフト・原始的な粉砕機 の `gearConnects`（offset/directions/option.meshingAxis）と `blockSize` を印字する。(2) 風車コネクタ `[0,0,2]` dir `[-1,0,0]` の向き先セルは原点+(-1,0,2)。シャフトはそのセルに置き、`directions` `[0,0,±1]` を `[±1,0,0]` に回す `BlockDirection`（East か West）を選ぶ。(3) 粉砕機コネクタ `[2,0,0]` dir `[1,0,0]` がシャフトセル（原点+(-1,0,2)）を向くよう、粉砕機原点 = シャフトセル − (2,0,0) − (1,0,0) = 原点+(-4,0,2) で North。meshingAxis が合わなければ粉砕機を East/West に回して原点を再計算する。(4) 確定した offset/direction で `generate_challenges.py` の `relative_preview(...)` 2行を直し Task M3 Step 3〜M5 をやり直してピンを更新する。是正内容を本planの「判断記録」に追記する。

- [ ] **Step 4: 既存のマスタ連動テストを回す**

```bash
cd $WT && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "TutorialAnchorContractTest|SkitLocalizationRuntimeContentTest|StartGameTest|ChallengeMasterValidationTest|Localization"
```
Expected: 全件PASS。

- [ ] **Step 5: コミットして本体 PR を作る**

```bash
cd $WT && git add .moorestech-external-revisions.json moorestech_client/Assets/Scripts/Client.Tests/EarlyGame && git commit -m "chore: 序盤圧縮マスタへピンを更新し接続チュートリアル配置の検証テストを追加する (ADR 0038)"
git push -u origin feature/early-game-compression-master
gh pr create --base master --title "chore: 序盤圧縮のマスタデータへピン更新 (ADR 0038)" --body "$(cat <<'EOF'
## Summary
- moorestech_master PR <URL> のピン更新（原始研究1〜3削除・改番・要求数圧縮・チャレンジ28本・木のシャフト前倒し・初期解放）
- 風車→シャフト→粉砕機の接続チュートリアル座標が動力を伝えることを検証する EditMode テストを追加

ADR: docs/adr/0038-early-game-compression-and-placement-guided-tutorials.md

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01Ts2pLxAukyhiJyiiqk4bXs
EOF
)"
```
Expected: PR URL。マスタPR本文の `<後で追記>` を `gh pr edit --repo moorestech/moorestech_master` で本体PR URLに置き換える。

---

### Task 2: unityプレイ録画テストで序盤の通しを確認する

- [ ] **Step 1: 新規ワールドで先頭〜「粉砕機を設置して動かす」まで通す**

`unity-playmode-recorded-playtest`（`Client.Playtest`、`docs/playtest-dsl.md`）で新規ワールドを開始し、以下を確認して録画を `../moorestech_logs/harness/` に残す:
1. 開始時点で装備スロットに石の斧があり、開幕スキット2本が再生される
2. 先頭チャレンジが「木を伐採して原木を入手する」で、小石・石器のチャレンジが存在しない
3. 「木の鉱脈に風力掘削機を設置する」で設置プレビューを出すと木の鉱脈だけが緑で強調され、石鉱脈の上ではプレビューが赤＋ツールチップ「ハイライトされた鉱脈の上に設置してください」になる
4. 「木のシャフトで風車と繋ぐ」で風車の隣にシャフトのゴーストが出る。シャフトを持つと風車のコネクタへの線が出る
5. 「粉砕機を設置して動かす」が風車に原木を入れた後に完了する

Expected: 5点すべて確認。3〜5で不一致があれば座標・文言を直し Task M3〜M5・Task 1 を再実行。

---

### Task 3: 最終レビュー（省略不可）

- [ ] **Step 1: 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

`moores-code-review` スキルを本体ブランチ `feature/early-game-compression-master` とマスタブランチ `feature/early-game-compression` の両方に対して実行し、指摘を修正してコミット・pushする。記録は `../moorestech_logs/harness/` へ。

- [ ] **Step 2: 撤収**

```bash
moores-wt rm early-game-compression
git -C ~/hermes-agent/data/repos/moorestech_master worktree remove ~/hermes-agent/data/repos/moorestech-master-worktrees/early-game-compression
```

---

## 判断記録（ADR）

設計ADR: `docs/adr/0038-early-game-compression-and-placement-guided-tutorials.md`。裁定: `.decisions/2026-08-27-*.md` 4件。

planning中の判断:
- **実本数は28**: ADR の「26本」は末尾3本を1行に畳んだ表記で、実体は28本（据え置きの「木材の組み立て・補強棒材・木のフレーム」が3本）。ADR に追記する。出所: agent前提（数え直し）。
- **研究チャレンジの key は旧title**: GUID安定のため `('原始研究4を完了する', '原始研究1を完了する', ...)` のように key を旧名で固定し title だけ改番する（0033 plan と同じ規則）。出所: agent前提。
- **最初の掘削機は石鉱脈**: ADR チャレンジ6は `veinPin(石)`。現行の「原木鉱脈の上に設置」から変更。石の斧で手掘りした石鉱脈の上に置くことで「掘削機が採掘を引き継ぐ」体験を先に見せ、木の鉱脈は次のチャレンジで制限付きに教える。出所: ADR チャレンジ構成（ユーザー承認 2026-08-27「良さそう」）。
- **接続チュートリアルの座標**: 風車コネクタ `[0,0,2]` dir `[-1,0,0]` / `[2,0,2]` dir `[1,0,0]`、シャフト `directions [0,0,±1]`、粉砕機 `[0,0,0]` dir `[-1,0,0]` / `[2,0,0]` dir `[1,0,0]` から、シャフト offset `(-1,0,2)` East、粉砕機 offset `(-4,0,2)` North と算出。meshingAxis の整合は Task 1 のテストで実証し、外れたら Step 3 の手順で是正する。出所: agent前提（blocks.json の gearConnects）。
- **粉砕機チャレンジの完了条件は稼働**: `gearConnectedBlock` は RPM>0 を要求するため、風車への燃料投入（原木）まで含めて1チャレンジにする。summary に明記。出所: ユーザー裁定 2026-08-28「シャフトがつながるようにブロックを設置しない機械が動かないから、そのチュートリアルが必要」。
- **木のシャフトの `unlockItemRecipeView` は旧7に残す**: ブロック設置は `requiredItems`（棒1・砕石1）で行い、アイテムとしての木のシャフトのレシピ表示は接続チュートリアルに不要。出所: agent前提。
- **研究説明文は新規に書き下ろす**: 旧文は削除ノードの内容や旧番号を含むため置換。英独は planning で用意した辞書を使う。出所: agent前提（文言は最終レビューで確認）。
- **燃料式風車の二重解放は据え置き**: 新3と「燃料式風車の作成」（旧9の枝）の両方が風車を解放する現状は ADR の Consequences どおり別issue。出所: ユーザー裁定の範囲外（agent前提で不変）。
