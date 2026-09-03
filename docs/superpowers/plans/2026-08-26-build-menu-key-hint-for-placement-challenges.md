# 設置チャレンジのビルドメニュー(B)キーヒント Implementation Plan

> **実施状況（2026-08-26）:** Task 1（master データ変更）と Task 3（push・PR・ピン更新）はユーザー指示により
> 本セッションで実行済み。master 側 PR は moorestech/moorestech_master#45。
> **未実施は Task 2（プレイテストシナリオによる実走検証）と Task 4（moores-code-review）。**
>
> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 「風力掘削機を設置する」「石窯を設置する」チャレンジに `keyControl{GameScreen, B, "ビルドメニューを開く"}` チュートリアルを追加し、中央下のキーヒントHUDに `[B] ビルドメニューを開く` を出す。

**Architecture:** C#・スキーマの変更はゼロ。`keyControl` チュートリアルは既に実装済み（スキーマ → `KeyControlTutorialManager` → `TutorialPresentationStateStore` → Web HUD）で、不足しているのは master データ側のエントリだけである。変更は別リポジトリ `moorestech_master` の生成スクリプト `tools/tutorial_v3_port/generate_challenges.py` と、その生成物 `challenges.json`、および `localization/localization.csv` の3ファイル。本リポジトリ側は `.moorestech-external-revisions.json` のピン更新と、検証用プレイテストシナリオの追加のみ。

**Tech Stack:** Python 3（master生成スクリプト）／ Unity C#（プレイテストDSLシナリオ）／ `unity-playmode-recorded-playtest` スキルの `run-scenario.sh`

## Requirements

- 「風力掘削機を設置する」チャレンジ（`a6497c0b-82eb-5280-82c7-d339bc32de14`）の `tutorials` に `keyControl{uiState: GameScreen, keyName: "B", controlText: "ビルドメニューを開く"}` を追加する。受け入れ基準: 当該チャレンジが現在目標のとき、`TutorialPresentationStateStore` に `TutorialKeyControlElementData{KeyName="B", UiState="GameScreen"}` が出現し、Web HUD の `key-control-hint` が描画される。
- 「石窯を設置する」チャレンジ（`603e84c0-10b1-501f-a03d-598584d34d58`）にも同一内容の `keyControl` を追加する。受け入れ基準: 生成後の `challenges.json` に当該チャレンジの `keyControl` が1件存在する。
- 追加は必ず既存 `tutorials` 配列の**末尾**へ行う。受け入れ基準: 既存の `tutorialGuid`（`a62599e4-4a0f-5773-b134-c51038475c19` / `46cfff1c-06f2-5bd0-afa7-cf89a32f669c` / `deca056f-2731-564f-afc2-1711ab478649` および他チャレンジ全件）が生成前後で1件も変化しない。
- 新規 `tutorialGuid` 2件の翻訳行を master の `localization/localization.csv` に追加する。受け入れ基準: `challengeTutorial.aa690d3f-370b-544d-81ff-4c554c7e7f05.text` と `challengeTutorial.bc12f222-f6e0-5b45-a5db-134166dbb0da.text` が存在し、`Source`/`japanese` が `ビルドメニューを開く`、`english` が `Open the build menu`。
- `challenges.json` は生成スクリプトの出力そのものであること。受け入れ基準: スクリプト再実行後に `git diff` が空になる。
- 本リポジトリの `.moorestech-external-revisions.json` の `moorestech_master.commitHash` を、master 側 PR の push 済みコミットへ更新する。
- master 側の変更は push して PR を作る（ローカルコミット止まり・push のみで PR 無しは禁止）。

**やらないこと（スコープ境界）:**

- `uiState` に `DeleteBar` / `BuildMenu` を足さない（GameScreen 単独。ADR 0035 で棄却済み）。
- C# / VanillaSchema / Web UI のコードは一切変更しない。
- 他チャレンジの `keyControl` 追加・既存文言の変更をしない。
- ADR 0034（独語ロケール）の german 列追加は本planの対象外。

## Global Constraints

- `tutorialGuid` は `uuid5(NS=7b0aa3a4-2f5d-4c19-8e60-9f21c67d3a55, "tutorial-v8-slot:<challenge key>#<slot index>")` で**スロット位置から**導出される。配列の途中に挿入すると後続要素の GUID が全て変わり、`localization.csv` の既存行が孤児になる。**必ず末尾に追加する。**
- `challenges.json` を手編集しない。正本は `tools/tutorial_v3_port/generate_challenges.py` であり、変更はスクリプト側に入れて再生成する（再生成一致は実測確認済み）。
- master 側の作業ブランチは、**本リポジトリの `master` ブランチが指すピン** `274b6d9fb8828e06a27c906d6122d8504dcaa9ce` から切る（`origin/master` の先端から切ると無関係な master 差分を巻き込み、作業中ブランチのピンから切ると master data が巻き戻る）。
- `moorestech_master` のメインクローン（`../moorestech_master`）は detached HEAD の共有クローンであり、ここで直接ブランチ作業をしない。専用 worktree を `../moorestech-master-worktrees/branch-build-menu-key-hint` に切る。
- 本リポジトリ側は `moores-wt new` で作った使い捨て worktree で作業する（メインワークツリーでのブランチ操作は hook が物理拒否する）。
- コメントは日本語1行 → 英語1行の2行セット。`Func<>` 禁止（ポーリングはインラインのループで書く）。
- Unity が `.moorestech-external-revisions.json` と `_CompileRequester.cs` を自動書き換えするため、コミット前に `git status` で確認する。
- `$SCRATCH` は本セッションのスクラッチパッドディレクトリを指す。実行前に `export SCRATCH=<スクラッチパッドの絶対パス>` しておく（一時ファイルを `/tmp` やリポジトリ内に置かない）。

## 前提: 設計成果物の持ち込み（着手前に必ず実施）

本plan・ADR・裁定の3ファイルは、設計セッションが moorestech のメインクローンに**未追跡のまま**置いた状態にある。
`moores-wt new` は未追跡のローカル規約ファイルしかコピーしないため、新しい worktree には現れない。
Task 2 Step 1 で worktree を作った直後に、次の3ファイルをメインクローンからコピーし、
作業ブランチの最初のコミットに含めること。

```bash
MAIN=<moorestechのメインクローンの絶対パス>
cd <作成したworktreeのルート>
cp "$MAIN/docs/adr/0035-build-menu-key-hint-for-placement-challenges.md" docs/adr/
cp "$MAIN/docs/superpowers/plans/2026-08-26-build-menu-key-hint-for-placement-challenges.md" docs/superpowers/plans/
cp "$MAIN/.decisions/2026-08-26-設置チャレンジのBキーヒントはGameScreenのみで石窯にも付ける.md" .decisions/
git add docs/adr docs/superpowers/plans .decisions
git commit -m "docs: 設置チャレンジのBキーヒントのADR・裁定・実装planを足す (ADR 0035)"
```

---

### Task 1: master 側に keyControl(B) を追加し challenges.json を再生成する

**Files:**
- Create: `../moorestech-master-worktrees/branch-build-menu-key-hint`（moorestech_master の worktree・ブランチ `feature/tutorial-build-menu-key-hint`）
- Create: `<scratchpad>/verify_build_menu_key_hint.py`（**コミットしない**一時検証スクリプト。moorestech_master に恒久ファイルを増やさない — 恒久的な退行ガードは Task 2 のプレイテストシナリオが担う）
- Modify: `tools/tutorial_v3_port/generate_challenges.py:94-95, 104-105`（master repo 相対）
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/challenges.json`（生成物・手編集禁止）
- Modify: `server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`

**Interfaces:**
- Consumes: 既存ヘルパ `key(state, key_name, text)`（`generate_challenges.py:59`。`('keyControl', {'uiState':…, 'keyName':…, 'controlText':…})` を返す）
- Produces: 新規 `tutorialGuid` `aa690d3f-370b-544d-81ff-4c554c7e7f05`（風力掘削機・slot 2）と `bc12f222-f6e0-5b45-a5db-134166dbb0da`（石窯・slot 1）。Task 2 のシナリオがこの2値を参照する。

- [ ] **Step 1: master 用の作業 worktree を作る**

```bash
cd ../moorestech_master   # moorestechルートからの相対
git worktree add ../moorestech-master-worktrees/branch-build-menu-key-hint \
  -b feature/tutorial-build-menu-key-hint 274b6d9fb8828e06a27c906d6122d8504dcaa9ce
```

Expected: `Preparing worktree (new branch 'feature/tutorial-build-menu-key-hint')` と `HEAD is now at 274b6d9`

- [ ] **Step 2: 失敗する検証スクリプトを書く**

スクラッチパッド（`$SCRATCH`）に `verify_build_menu_key_hint.py` を作る。**master リポジトリには置かない**（恒久ガードは Task 2 のシナリオ）:

```python
#!/usr/bin/env python3
# 設置チャレンジのBキーヒント追加が「末尾追加・GUID不変・翻訳行あり」を満たすか検証する
# Verify the build-menu key hint was appended without shifting any existing GUID, with translations present
import json, os, sys

ROOT = '.'  # master作業worktreeのルートをcwdにして実行する
MASTER = os.path.join(ROOT, 'server_v8', 'mods', 'moorestechAlphaMod_8', 'master')
L10N = os.path.join(ROOT, 'server_v8', 'mods', 'moorestechAlphaMod_8', 'localization')

# 追加前に実測した既存GUID。1件でも変わっていたらスロット位置がずれている
# GUIDs measured before the change; any drift here means a slot index shifted
FROZEN = {
    '風力掘削機を設置する': ['a62599e4-4a0f-5773-b134-c51038475c19', '46cfff1c-06f2-5bd0-afa7-cf89a32f669c'],
    '石窯を設置する': ['deca056f-2731-564f-afc2-1711ab478649'],
}
EXPECTED_NEW = {
    '風力掘削機を設置する': 'aa690d3f-370b-544d-81ff-4c554c7e7f05',
    '石窯を設置する': 'bc12f222-f6e0-5b45-a5db-134166dbb0da',
}

failures = []
challenges = {c['title']: c
              for cat in json.load(open(os.path.join(MASTER, 'challenges.json'), encoding='utf-8'))['data']
              for c in cat['challenges']}
csv_text = open(os.path.join(L10N, 'localization.csv'), encoding='utf-8').read()

for title, frozen in FROZEN.items():
    tutorials = challenges[title]['tutorials']
    guids = [t['tutorialGuid'] for t in tutorials]
    if guids[:len(frozen)] != frozen:
        failures.append(f'{title}: 既存GUIDがずれた {guids[:len(frozen)]} != {frozen}')
    if len(guids) != len(frozen) + 1:
        failures.append(f'{title}: tutorials件数が{len(frozen) + 1}でない ({len(guids)})')
        continue
    added = tutorials[-1]
    if added['tutorialGuid'] != EXPECTED_NEW[title]:
        failures.append(f'{title}: 新規GUIDが想定と違う {added["tutorialGuid"]}')
    if added['tutorialType'] != 'keyControl':
        failures.append(f'{title}: 末尾がkeyControlでない ({added["tutorialType"]})')
    else:
        p = added['tutorialParam']
        if (p['uiState'], p['keyName'], p['controlText']) != ('GameScreen', 'B', 'ビルドメニューを開く'):
            failures.append(f'{title}: tutorialParamが想定と違う {p}')
    row = f'challengeTutorial.{EXPECTED_NEW[title]}.text,ビルドメニューを開く,Open the build menu,ビルドメニューを開く'
    if row not in csv_text:
        failures.append(f'{title}: localization.csvに翻訳行が無い ({EXPECTED_NEW[title]})')

for f in failures:
    print('NG:', f)
print('OK' if not failures else f'FAILED: {len(failures)}件')
sys.exit(1 if failures else 0)
```

- [ ] **Step 3: 検証スクリプトを実行して失敗を確認する**

```bash
cd ../moorestech-master-worktrees/branch-build-menu-key-hint
python3 "$SCRATCH/verify_build_menu_key_hint.py"
```

Expected: FAIL（exit 1）。`NG: 風力掘削機を設置する: tutorials件数が3でない (2)` と `NG: 石窯を設置する: tutorials件数が2でない (1)` が出る。

- [ ] **Step 4: 生成スクリプトの定義表に keyControl を末尾追加する**

`tools/tutorial_v3_port/generate_challenges.py` の該当2行を書き換える。

風力掘削機（現在は `[vein(...), drag(...)]`）:

```python
    ('風力掘削機を設置する', '風力掘削機を設置する', 'Bでビルドメニューを開き、風力掘削機をホットバーへドラッグして粘土鉱脈の上に設置しよう', 'block', '風力掘削機', 1,
     [vein('粘土鉱脈', '粘土鉱脈の上に設置'), drag('風力掘削機', 'ホットバーへドラッグ'), key('GameScreen', 'B', 'ビルドメニューを開く')], '砕いた石材'),
```

石窯（現在は `[drag(...)]`）:

```python
    ('石窯を設置する', '石窯を設置する', 'Bでビルドメニューを開き、石窯をホットバーへドラッグして設置しよう', 'block', '石窯', 1,
     [drag('石窯', 'ホットバーへドラッグ'), key('GameScreen', 'B', 'ビルドメニューを開く')], 'レンガ'),
```

- [ ] **Step 5: challenges.json を再生成する**

```bash
python3 tools/tutorial_v3_port/generate_challenges.py
```

Expected: `OK: 26 challenges`

- [ ] **Step 6: 翻訳行を localization.csv に追加する**

`server_v8/mods/moorestechAlphaMod_8/localization/localization.csv` の
`challengeTutorial.46cfff1c-06f2-5bd0-afa7-cf89a32f669c.text` 行の直後に1行目を、
`challengeTutorial.deca056f-2731-564f-afc2-1711ab478649.text` 行の直後に2行目を挿入する。

```
challengeTutorial.aa690d3f-370b-544d-81ff-4c554c7e7f05.text,ビルドメニューを開く,Open the build menu,ビルドメニューを開く
```

```
challengeTutorial.bc12f222-f6e0-5b45-a5db-134166dbb0da.text,ビルドメニューを開く,Open the build menu,ビルドメニューを開く
```

- [ ] **Step 7: 検証スクリプトを実行して通ることを確認する**

```bash
python3 "$SCRATCH/verify_build_menu_key_hint.py"
```

Expected: PASS（`OK` / exit 0）

- [ ] **Step 8: 再生成が冪等であることを確認する**

```bash
python3 tools/tutorial_v3_port/generate_challenges.py && git diff --stat server_v8/mods/moorestechAlphaMod_8/master/challenges.json
```

Expected: `git diff --stat` の出力が空（再実行で差分ゼロ）

- [ ] **Step 9: 変更範囲が想定どおりかを目視確認する**

```bash
git status --short
git diff server_v8/mods/moorestechAlphaMod_8/master/challenges.json
```

Expected: 変更3ファイルのみ（検証スクリプトはスクラッチパッドにあり、リポジトリには現れない）。`challenges.json` の差分は2チャレンジへの `keyControl` ブロック追加と末尾改行1行のみで、既存 `tutorialGuid` の変更行が1つも無いこと。

- [ ] **Step 10: コミットする**

```bash
git add tools/tutorial_v3_port/generate_challenges.py \
        server_v8/mods/moorestechAlphaMod_8/master/challenges.json \
        server_v8/mods/moorestechAlphaMod_8/localization/localization.csv
git commit -m "feat(tutorial): 設置チャレンジにビルドメニュー(B)のkeyControlヒントを足す (ADR 0035)"
```

---

### Task 2: プレイテストシナリオで [B] ヒントの提示を実走検証する

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tutorial-build-menu-key-hint.cs`（本リポジトリ相対）
- Reference: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tutorial-equip-challenge.cs`（同型の先行シナリオ）

**Interfaces:**
- Consumes: Task 1 が生成した `tutorialGuid` `aa690d3f-370b-544d-81ff-4c554c7e7f05`、および Task 1 の worktree パス（`run-scenario.sh` の第3引数として渡す）
- Produces: `moorestech_client/PlaytestResults/<run>/result.json`（`success: true`）とスクリーンショット2枚

- [ ] **Step 1: 本リポジトリの作業 worktree を作る**

```bash
cd <moorestechのメインクローン>
moores-wt new feature/tutorial-build-menu-key-hint --dir build-menu-key-hint
```

Expected: worktree 作成 → PersonalAssets/Library の clonefile コピー → `uloop launch` まで完了（3分強）。以降の Step は作成された worktree のパスで作業する。

- [ ] **Step 2: 失敗するシナリオを書く**

`.agents/skills/unity-playmode-recorded-playtest/scenarios/tutorial-build-menu-key-hint.cs` を新規作成する:

```csharp
// シナリオ: ADR 0035「設置チャレンジのビルドメニュー(B)キーヒント」を実走検証する
// Scenario: end-to-end check of ADR 0035 (build-menu key hint on placement challenges)
// 足場生成やSetupDebugEnvironmentは呼ばない（自然なマップと通常のチャレンジ進行を保つため）
// Do NOT flatten ground or SetupDebugEnvironment (keep the natural map and the normal challenge flow)
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;

var pebbleChallenge = new Guid("bd5262ed-fbd4-51e0-a75d-2944f366e10a"); // 小石を3個拾う
var equipStoneTool = new Guid("24f72113-495c-5302-af05-8b1f0d0c1091"); // 石器を装備する
var research3Challenge = new Guid("8b2d87d1-3ee2-5af4-9f11-c8fe9e966930"); // 原始研究3を完了する
var windmillChallenge = new Guid("a6497c0b-82eb-5280-82c7-d339bc32de14"); // 風力掘削機を設置する
var stoneToolRecipe = new Guid("9c20aa73-1877-4e0e-adcc-9f725c9377da"); // 石器(小石x3)
var stoneAxeRecipe = new Guid("04932724-b122-45ea-8cb1-642d9c834444"); // 石の斧(木の棒x2・砕いた石材x3)
var research1 = new Guid("837e9697-8586-406e-a0f6-16a010050218"); // 原始研究1(木の板5・木の棒5)
var research2 = new Guid("424be8c1-c40c-4644-8104-06934c59b147"); // 原始研究2(木の板5・木の棒5・砕いた石材5)
var research3 = new Guid("07d6226c-ed14-4a6f-aa2a-6fa085fce8ec"); // 原始研究3(木の板10・木の棒5・砕いた石材10)
var buildMenuKeyTutorial = "aa690d3f-370b-544d-81ff-4c554c7e7f05"; // 「ビルドメニューを開く」keyControl(ADR 0035 新設)
var dragGuideTutorial = "46cfff1c-06f2-5bd0-afa7-cf89a32f669c"; // 風力掘削機のuiDragGuide(非退行確認用)

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("tutorial-build-menu-key-hint", options, async p =>
{
    var challengeStore = p.ServerService<Game.Challenge.ChallengeDatastore>();
    var tutorialStore = Client.Game.InGame.Tutorial.TutorialPresentationStateStore.Instance;

    // 開幕スキットを飛ばして最初のチャレンジから始める
    // Skip the opening skit and start from the first challenge
    p.Note("開幕スキットを飛ばす");
    await p.SkipOpeningSkit();

    // 研究で消費されるぶんも含めて素材を先に配る（inInventoryItem系はアンロック時のManualUpdateで達成する）
    // Hand out every material up front, research costs included (inInventoryItem tasks clear on the unlock-time ManualUpdate)
    p.Note("進行に必要な素材をまとめて付与する");
    p.GiveItemDirect("小石", 3);
    p.GiveItemDirect("原木", 3);
    p.GiveItemDirect("木の板", 40);
    p.GiveItemDirect("木の棒", 40);
    p.GiveItemDirect("石", 10);
    p.GiveItemDirect("砕いた石材", 40);
    var pebbleDone = await PollUntilCompleted(pebbleChallenge, 30);
    p.Assert(pebbleDone, "チャレンジ「小石を3個拾う」が完了した");

    // 石器はクラフト実績が要る（createItem系はインベントリ付与では達成しない）
    // The stone tool needs a real craft: createItem tasks never clear from a direct item grant
    p.Note("石器をクラフトして装備する");
    Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.Craft(stoneToolRecipe);
    var equipCurrent = await PollUntilCurrent(equipStoneTool, 30);
    p.Assert(equipCurrent, "チャレンジ「石器を装備する」が現在目標になった");
    await p.EquipItem("石器", 0);

    // 原始研究1〜3を順に完了する（研究2の完了で石の斧レシピが解放される）
    // Complete primitive research 1-3 in order (research 2 unlocks the stone axe recipe)
    p.Note("原始研究1〜3を順に完了する");
    Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.CompleteResearch(research1);
    await p.WaitSeconds(2f);
    Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.CompleteResearch(research2);
    await p.WaitSeconds(2f);
    p.Note("石の斧をクラフトして装備する");
    Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.Craft(stoneAxeRecipe);
    await p.WaitSeconds(2f);
    await p.EquipItem("石の斧", 0);
    Client.Game.InGame.Context.ClientContext.VanillaApi.SendOnly.CompleteResearch(research3);
    var research3Done = await PollUntilCompleted(research3Challenge, 30);
    p.Assert(research3Done, "チャレンジ「原始研究3を完了する」が完了した");

    // 検証1: 風力掘削機の設置チャレンジが現在目標になる
    // Verify 1: the windmill drill placement challenge becomes the current objective
    var windmillCurrent = await PollUntilCurrent(windmillChallenge, 30);
    p.Assert(windmillCurrent, "チャレンジ「風力掘削機を設置する」が現在目標になった");

    // 検証2: 新設のkeyControlがB・GameScreenで提示される（本ADRの本体）
    // Verify 2: the new keyControl is published with B / GameScreen (the point of this ADR)
    p.Note("Bキーヒントの提示を待つ");
    Client.Game.InGame.Tutorial.TutorialKeyControlElementData keyHint = null;
    for (var i = 0; i < 30 && keyHint == null; i++)
    {
        keyHint = FindElements().OfType<Client.Game.InGame.Tutorial.TutorialKeyControlElementData>()
            .FirstOrDefault(x => x.TutorialGuid == buildMenuKeyTutorial);
        if (keyHint == null) await p.WaitSeconds(1f);
    }
    p.Assert(keyHint != null, "keyControlヒントが新設のtutorialGuidで提示された");
    if (keyHint != null)
    {
        p.Assert(keyHint.KeyName == "B", $"keyNameがBである(実測{keyHint.KeyName})");
        p.Assert(keyHint.UiState == "GameScreen", $"uiStateがGameScreenである(実測{keyHint.UiState})");
    }

    // 検証3: 既存のドラッグガイドが同居して消えていない（末尾追加による非退行）
    // Verify 3: the existing drag guide still coexists (no regression from appending)
    var dragGuide = FindElements().OfType<Client.Game.InGame.Tutorial.TutorialDragGuideElementData>()
        .FirstOrDefault(x => x.TutorialGuid == dragGuideTutorial);
    p.Assert(dragGuide != null, "既存のドラッグ矢印が残っている");

    // 検証4: Web HUDに実際に描画される（文言と様式はスクリーンショットでのみ確認できる）
    // Verify 4: it actually renders in the web HUD (wording and styling are screenshot-only)
    var keyHintDom = false;
    for (var i = 0; i < 20 && !keyHintDom; i++)
    {
        keyHintDom = (await Client.Playtest.WebUi.PlaytestDomQuery.Query("key-control-hint", 1f)).Found;
        if (!keyHintDom) await p.WaitSeconds(1f);
    }
    p.Assert(keyHintDom, "key-control-hintがWeb HUDに描画された");
    await p.Screenshot("01-build-menu-key-hint");

    // 検証5: Bでビルドメニューへ遷移でき、GameScreen限定のヒントが引っ込む
    // Verify 5: B opens the build menu and the GameScreen-only hint steps aside
    p.Note("Bでビルドメニューを開く");
    await p.PressKey(UnityEngine.InputSystem.Key.B);
    await p.WaitUiState(Client.Game.InGame.UI.UIState.UIStateEnum.BuildMenu, 10f);
    await p.WaitSeconds(1f);
    await p.Screenshot("02-build-menu-opened");
    p.Note("検証完了");

    #region Internal

    // 同時currentな全challengeのsessionを平坦化する（提示はchallenge単位のsessionに分かれて載る）
    // Flatten the sessions of all simultaneously-current challenges (presentation is split per challenge session)
    IEnumerable<Client.Game.InGame.Tutorial.TutorialOverlayElementData> FindElements()
    {
        return tutorialStore.GetCurrent().Sessions.SelectMany(s => s.Elements);
    }

    // 完了待ち。失敗しても例外で中断せず後続の検証を続ける
    // Wait for completion; never abort on failure so later checks still run
    async UniTask<bool> PollUntilCompleted(Guid challengeGuid, int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            if (challengeStore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == challengeGuid)) return true;
            await p.WaitSeconds(1f);
        }
        return challengeStore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == challengeGuid);
    }

    // 現在目標になるまで待つ。同上で例外中断しない
    // Wait until it becomes a current objective; likewise never aborts
    async UniTask<bool> PollUntilCurrent(Guid challengeGuid, int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            if (challengeStore.CurrentChallengeInfo.CurrentChallenges.Any(c => c.ChallengeMasterElement.ChallengeGuid == challengeGuid)) return true;
            await p.WaitSeconds(1f);
        }
        return challengeStore.CurrentChallengeInfo.CurrentChallenges.Any(c => c.ChallengeMasterElement.ChallengeGuid == challengeGuid);
    }

    #endregion
});
```

- [ ] **Step 3: 変更前の master データでシナリオを実行して失敗を確認する**

作業 worktree のルートで、**Task 1 の worktree ではなく現行ピンの master worktree**を第3引数に渡す。

```bash
cd <Task 2 Step 1 で作成した worktree のルート>
uloop control-play-mode --project-path ./moorestech_client --action stop
SKILL=.claude/skills/unity-playmode-recorded-playtest
"$SKILL/scripts/run-scenario.sh" ./moorestech_client \
  "$SKILL/scenarios/tutorial-build-menu-key-hint.cs" \
  ../moorestech-master-worktrees/pin-274b6d9f
```

Expected: `result.json` の `success: false`。`keyControlヒントが新設のtutorialGuidで提示された` が失敗する（master に該当エントリが無いため）。ここまでの進行系 Assert（小石・装備・研究3・風力掘削機が現在目標）は PASS していること — PASS していなければシナリオの進行手順自体が誤りなので、そちらを先に直す。

- [ ] **Step 4: Task 1 の master worktree を指してシナリオを再実行し、通ることを確認する**

```bash
uloop control-play-mode --project-path ./moorestech_client --action stop
SKILL=.claude/skills/unity-playmode-recorded-playtest
"$SKILL/scripts/run-scenario.sh" ./moorestech_client \
  "$SKILL/scenarios/tutorial-build-menu-key-hint.cs" \
  ../moorestech-master-worktrees/branch-build-menu-key-hint
```

Expected: `result.json` の `success: true`、全 Assert が PASS。

- [ ] **Step 5: スクリーンショットを目視確認する**

`moorestech_client/PlaytestResults/<run>/01-build-menu-key-hint.png` を開き、中央下に `[B] ビルドメニューを開く` が出ていることを確認する。`02-build-menu-opened.png` でビルドメニューが開き、当該ヒントが中央下から消えていることを確認する。

Expected: 両方とも想定どおり。文言が `ビルドメニューを開く` になっている（翻訳行の欠落で GUID の生表示になっていない）。

- [ ] **Step 6: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/tutorial-build-menu-key-hint.cs
git commit -m "test(playtest): ビルドメニュー(B)キーヒントの実走シナリオを足す (ADR 0035)"
```

---

### Task 3: master を push・PR 作成し、本リポジトリのピンを更新する

**Files:**
- Modify: `.moorestech-external-revisions.json`（`moorestech_master.commitHash`）
- Reference: `docs/adr/0035-build-menu-key-hint-for-placement-challenges.md`

**Interfaces:**
- Consumes: Task 1 のコミット（`feature/tutorial-build-menu-key-hint` @ moorestech_master）
- Produces: push 済みコミットハッシュ。これが本リポジトリのピン値になる。

- [ ] **Step 1: master 側を push して PR を作る**

```bash
cd ../moorestech-master-worktrees/branch-build-menu-key-hint
git push -u origin feature/tutorial-build-menu-key-hint
gh pr create --repo moorestech/moorestech_master \
  --base master --head feature/tutorial-build-menu-key-hint \
  --title "feat(tutorial): 設置チャレンジにビルドメニュー(B)のkeyControlヒントを足す" \
  --body "設置チャレンジのビルドメニュー(B)キーヒント

風力掘削機・石窯の設置チャレンジは summary で「Bでビルドメニューを開き…」と言っているのに tutorials に keyControl が無く、中央下のキーヒントHUDに [B] が一度も出ていなかった。両チャレンジへ keyControl{uiState: GameScreen, keyName: B, controlText: ビルドメニューを開く} を追加する。

- 追加は tutorials 配列の末尾。tutorialGuid はスロット位置から導出されるため、途中挿入すると既存 GUID と翻訳行が壊れる
- challenges.json は generate_challenges.py の再生成物で、手編集していない
- 新規 tutorialGuid 2件の翻訳行を localization.csv へ追加（english: Open the build menu）
- 検証は moorestech 側のプレイテストシナリオ tutorial-build-menu-key-hint.cs（実プレイで [B] ヒントの提示を確認）
- 裁定: moorestech 側 docs/adr/0035-build-menu-key-hint-for-placement-challenges.md

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
```

Expected: PR URL が出力される。

- [ ] **Step 2: push 済みコミットハッシュを取得する**

```bash
git rev-parse HEAD
```

Expected: 40桁のハッシュ（以降 `<NEW_MASTER_SHA>` と呼ぶ）

- [ ] **Step 3: 本リポジトリのピンを更新してコミットする**

`.moorestech-external-revisions.json` の `moorestech_master` の `commitHash` を `<NEW_MASTER_SHA>` に書き換える（`moorestech_client_private` は触らない）。`run-scenario.sh` の worktree 自動解決は**コミット済みの**ピンファイルを見るため、次の Step の前にコミットまで済ませる。

```bash
cd <Task 2 Step 1 で作成した worktree のルート>
git diff .moorestech-external-revisions.json
git add .moorestech-external-revisions.json
git commit -m "chore: master dataピンをビルドメニューキーヒント対応へ上げる"
```

Expected: `commitHash` の1行だけが変わる差分。Unity が実チェックアウト値へ書き戻していたら、正しい値へ直してからコミットし直す。

- [ ] **Step 4: ピン用の master worktree を用意し、自動解決でシナリオが通ることを確認する**

```bash
SHORT=$(echo "<NEW_MASTER_SHA>" | cut -c1-8)
git -C ../moorestech_master worktree add \
  ../moorestech-master-worktrees/pin-$SHORT "<NEW_MASTER_SHA>"
uloop control-play-mode --project-path ./moorestech_client --action stop
SKILL=.claude/skills/unity-playmode-recorded-playtest
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/tutorial-build-menu-key-hint.cs"
```

Expected: 第3引数なし（自動解決）で `result.json` の `success: true`。

- [ ] **Step 5: 本リポジトリを push して PR を作る**

```bash
git push -u origin feature/tutorial-build-menu-key-hint
```

その後 pr-create スキルで PR を作成する（本文に ADR 0035 と master 側 PR URL を相互リンクする）。

---

### Task 4: 全ブランチのコードレビュー（省略不可）

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

`moores-code-review` スキルを起動し、本ブランチおよび moorestech_master 側ブランチの全差分をレビューする。
このタスクは**自動実行・ゴール文言による省略不可**。指摘が出たら修正し、修正後に該当箇所を再確認してからマージへ進む。

---

## 判断記録（ADR）

- 設計裁定の正本: `docs/adr/0035-build-menu-key-hint-for-placement-challenges.md`
- ユーザー裁定の蒸留: `.decisions/2026-08-26-設置チャレンジのBキーヒントはGameScreenのみで石窯にも付ける.md`
- タスク台帳: bd `moorestech-xu4o`

planning 中に生じた追加判断:

- **master 作業ブランチの分岐元は「本リポジトリの `master` が指すピン」`274b6d9f`**（`origin/master` 先端でも、作業中ブランチのピンでもない）。先端から切ると無関係な master 差分がピン更新に同乗し、作業中ブランチ（`fix/skit-ground-position-3x3`＝ピン `60e815a`）から切ると `master` のピンより古くなり master data が巻き戻る。実装時に一度 `60e815a` から切って取り違え、`274b6d9f` へ rebase して是正した。
  出所: agent前提（`git merge-base --is-ancestor` で `60e815a` が `274b6d9f` の祖先であることを実測確認済み）
- **GUID 不変・翻訳行の存在チェックは使い捨てスクリプトで行い、リポジトリには残さない**。moorestech_master の `tools/` は移行スクリプト置き場で、走らせ続ける検査スクリプトの前例が無く、CI からも呼ばれない恒久ファイルを増やすと持ち主のいないコードになる。恒久的な退行ガードは Task 2 のプレイテストシナリオ（実プレイで [B] の提示を確認する）が担う。
  出所: agent前提（`moorestech_master/tools/` の既存4ディレクトリが全て `*_migration` / `*_port` の一回性スクリプトであることを確認。またクライアント側に master データの翻訳行欠落を落とすテストが無いことも `ModLocalizationMergerValidationTest` 等で実測確認済み）
- **Task 2 のシナリオは既存 `tutorial-equip-challenge.cs` を役割同型の前例として踏襲する**（`PlaytestRunner.Run` + `p.GiveItemDirect` + `ServerService<ChallengeDatastore>()` + `PlaytestDomQuery`）。ただしポーリングヘルパは `Func<>` 禁止規約に合わせ、前例の `Func<bool>` 版ではなく Guid を受ける専用ローカル関数として書く。
  出所: agent前提（AGENTS.md「`Func<>` の使用は禁止」／前例は `.agents/skills/unity-playmode-recorded-playtest/scenarios/tutorial-equip-challenge.cs`）
- **独語ロケール（ADR 0034 / worktree `l10n-german`）とのマージ順**は本planで確定しない。先に merge された側の列構成へ後から合わせる。新規2行に german 値が必要になるのは german 列が master に入った後。
  出所: agent前提（ADR 0035 Consequences に記載済み）
