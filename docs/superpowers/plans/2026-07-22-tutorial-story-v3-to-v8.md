# チュートリアル/ストーリー v3→v8 移植 実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** v3のチュートリアル/ストーリー体験（チャレンジ25個・スキット2本・キャラ3体）を、v8の実進行（石器→青銅）に合わせて再構成した challenges.json / characters.json として v8 modへ移植する。

**Architecture:** 成果物はマスターデータJSONのみ（クライアント/サーバーのC#変更なし）。生成はマスターリポジトリの前例（`tools/*_migration/*.py`）に倣い、v8 masterから名前→GUID解決するPythonスクリプトで行う。チャレンジGUIDは uuid5 による決定的生成で再実行冪等。検証は (1) スクリプト内機械検証 (2) クライアントの契約テスト (3) プレイテストDSL実走 の3段。

**Tech Stack:** Python 3 (標準ライブラリのみ) / uloop / プレイテストDSL

**Spec:** `docs/superpowers/specs/2026-07-22-tutorial-story-v3-to-v8-migration-design.md`

## Global Constraints

- 成果物の置き場: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/`（challenges.json / characters.json）。スクリプトは `../moorestech_master/tools/tutorial_v3_port/`（前例: `tools/plan6_nodegraph_migration/` 等）
- マスターリポジトリ（`../moorestech_master`）は別gitリポジトリ。コミットはそちらで行う
- GUIDの手打ちは禁止。アイテム/ブロック/mapObjectのGUIDは必ずv8 masterから名前引きで解決し、解決失敗は即エラー（フォールバック禁止）
- チャレンジGUIDは `uuid.uuid5(NAMESPACE, "tutorial-v8:<title>")` で決定的に生成（再実行冪等）
- チャレンジ側で `unlockItemRecipeView` 等の解放系アクションは使わない（解放のSSOTはresearch.json）。チャレンジのアクションは `playSkit` のみ
- `uiHighLight` の `highLightUIObjectId` に使ってよい値は `craftButton` のみ（`TutorialAnchorIdMapper.UiAnchors` に存在する唯一のキー。`moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/TutorialAnchorIdMapper.cs`）
- tutorialType は mapObjectPin / uiHighLight / itemViewHighLight / keyControl の4種のみ使用（v3の使用実績と同じ。blockPlacePreviewは使わない）
- JSONのフィールド名・構造は `VanillaSchema/challenges.yml`・`VanillaSchema/characters.yml`・`VanillaSchema/ref/gameAction.yml`・`VanillaSchema/ref/graphViewSettings.yml` に厳密一致させる
- スキットのAddressableパスは既存の2本のみ: `Vanilla/Skit/skits/100_start_game`（normal）/ `Vanilla/Skit/skits/200_star_background`（background）
- クライアント側の.cs変更なし → コンパイル不要。ただしテスト・プレイテストは実行する

## チャレンジ列定義（このプランの正）

v8序盤進行から抽出した20個・1カテゴリ「生きる基盤」。researchによる解放との整合を「前提research」列で明示する（チャレンジのタスクにresearch完了を直接ゲートする型は存在しないため、解放が必要なチャレンジはkeyControlチュートリアルで研究画面へ誘導する）。

| # | title | task | 対象(名前引き) | 個数 | tutorials | 前提research |
|---|---|---|---|---|---|---|
| 1 | 小石を3個拾う | inInventoryItem | 小石 | 3 | mapObjectPin(小石,「左クリックで拾う」) | なし |
| 2 | 石器を作る | createItem | 石器 | - | uiHighLight(craftButton,「クラフトボタンで作成」), itemViewHighLight(石器,「石器のレシピを確認」) | なし |
| 3 | 木を伐採して原木を入手する | inInventoryItem | 原木 | 3 | mapObjectPin(木,「石器で木を伐採」) | なし |
| 4 | 木の板を5枚作る | inInventoryItem | 木の板 | 5 | itemViewHighLight(木の板,「原木から木の板を作る」) | なし |
| 5 | 木の棒を5本作る | inInventoryItem | 木の棒 | 5 | なし | なし |
| 6 | 石を採掘する | inInventoryItem | 石 | 5 | mapObjectPin(石鉱脈,「石鉱脈から石を採掘」) | なし |
| 7 | 砕いた石材を5個作る | inInventoryItem | 砕いた石材 | 5 | なし | なし |
| 8 | 石の斧を作る | createItem | 石の斧 | - | なし | なし |
| 9 | 風力掘削機を設置する | blockPlace | 風力掘削機 | 1 | keyControl(GameScreen,「研究画面で原始研究1〜3を完了して解放」) | 原始研究3 |
| 10 | 粘土を入手する | inInventoryItem | 粘土 | 1 | なし（summaryで掘削機採掘を案内） | 原始研究3 |
| 11 | レンガを作る | createItem | レンガ | - | itemViewHighLight(レンガ,「粘土からレンガを作る」) | 原始研究3 |
| 12 | 青銅の鉱石を5個採掘する | inInventoryItem | 青銅の鉱石 | 5 | なし（summaryで「鉱脈上に風力掘削機を設置」を案内） | 原始研究3 |
| 13 | 青銅鉱石の粉を3個作る | inInventoryItem | 青銅鉱石の粉 | 3 | なし | なし |
| 14 | 石窯を設置する | blockPlace | 石窯 | 1 | keyControl(GameScreen,「原始研究4を完了して石窯を解放」) | 原始研究4 |
| 15 | 青銅インゴットを作る | inInventoryItem | 青銅インゴット | 1 | なし（summaryで石窯精錬を案内） | 原始研究4 |
| 16 | 青銅シートを作る | createItem | 青銅シート | - | なし | 原始研究4 |
| 17 | 木釘を9本作る | inInventoryItem | 木釘 | 9 | なし | なし |
| 18 | 合板を作る | createItem | 合板 | - | なし | なし |
| 19 | 補強棒材を作る | createItem | 補強棒材 | - | なし | なし |
| 20 | 木のフレームを作る | createItem | 木のフレーム | - | なし | なし |

- チャレンジ1の `startedActions` にスキット2本（100_start_game=normal, 200_star_background=background, playSortPriority 0/1）。v3と同じ結線
- 接続は直列（各チャレンジの `prevChallengeGuids` = 直前1個、#1のみ空）。`unlockAllPreviousChallengeComplete: true`
- `displayListParam`: `UIPosition = [300*(i%5), 250*(i//5)]`、`IconItem` = タスク対象アイテム、`UIScale = [1,1,1]`。**注意: blocks.jsonのブロックはitems.jsonにエントリを持たない**（blockにitemGuidフィールド無し・IconItemのforeignKeyはitems限定）ため、blockPlaceチャレンジ（#9/#14）のアイコンは素材アイテム（#9=砕いた石材、#14=レンガ）で代用する
- 根拠データ（調査済み）: 初期解放ハンドクラフトに 石器/木の板/木の棒/砕いた石材/石の斧/木釘/合板/レンガ/青銅鉱石の粉/青銅シート/補強棒材/木のフレーム が含まれる。原始研究3→風力掘削機解放、原始研究4→石窯解放。青銅の鉱石と粘土の正規獲得ルートは**地中鉱脈**（`map/map.json` の `itemMapVeins` に青銅の鉱石×100・粘土×91）を風力掘削機（mineSettingsに両方含む）で採掘する経路。石窯の機械レシピ「青銅鉱石の粉x1+原木x1→青銅インゴット」あり
- **ブッシュは参照禁止**: ブッシュのドロップが青銅の鉱石(5-15)になっているのはマスタ移行ミスで、ブッシュ自体が廃止予定（ユーザー裁定 2026-07-22）。チャレンジ・チュートリアルからブッシュを一切参照しない。ドロップ修正・ブッシュ削除自体は本プランのスコープ外（別タスク）

## 配置と前例

- 生成スクリプト → `../moorestech_master/tools/tutorial_v3_port/`（前例: `tools/plan2_migration/migrate.py`, `tools/plan6_nodegraph_migration/migrate_nodegraph.py`）
- マスターデータJSON → v8 mod `master/` 直下（既存の全マスタJSONと同居）
- クライアント/サーバーのコード・スキーマ変更なし。新規パターンなし

---

### Task 1: 生成スクリプト作成・実行・機械検証

**Files:**
- Create: `../moorestech_master/tools/tutorial_v3_port/generate_challenges.py`
- Modify(生成): `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/challenges.json`
- Modify(生成): `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/characters.json`

**Interfaces:**
- Consumes: v8 master JSON群（items/blocks/mapObjects/craftRecipes/research）、v3 characters.json
- Produces: スキーマ準拠の challenges.json（1カテゴリ20チャレンジ）と characters.json（3キャラ）。Task 2/3 はこの2ファイルを検証する

- [ ] **Step 1: スクリプトを書く**

`../moorestech_master/tools/tutorial_v3_port/generate_challenges.py` を以下の内容で作成:

```python
#!/usr/bin/env python3
# v3のチュートリアル/ストーリーをv8進行に合わせて再構成しchallenges.json/characters.jsonを生成する
# Regenerates challenges.json/characters.json porting the v3 tutorial/story onto the v8 progression
import json, uuid, sys, os

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
V8 = os.path.join(ROOT, 'server_v8', 'mods', 'moorestechAlphaMod_8', 'master')
V3 = os.path.join(ROOT, 'server', 'mods', 'moorestechAlphaMod_3', 'master')
NS = uuid.UUID('7b0aa3a4-2f5d-4c19-8e60-9f21c67d3a55')

def load(base, name):
    with open(os.path.join(base, name), encoding='utf-8') as f:
        return json.load(f)

def load_map():
    with open(os.path.join(ROOT, 'server_v8', 'map', 'map.json'), encoding='utf-8') as f:
        return json.load(f)

items = {x['name']: x['itemGuid'] for x in load(V8, 'items.json')['data']}
blocks = {b['name']: b['blockGuid'] for b in load(V8, 'blocks.json')['data']}
map_objects = {m['mapObjectName']: m['mapObjectGuid'] for m in load(V8, 'mapObjects.json')['data']}
crafts = load(V8, 'craftRecipes.json')['data']
research = load(V8, 'research.json')['data']

def guid_for(title):
    return str(uuid.uuid5(NS, 'tutorial-v8:' + title))

# チャレンジ定義表（プランの表と1:1対応）
# Challenge definition table (1:1 with the plan document)
# (title, summary, task, target_name, count, tutorials, icon_name)
# task: 'item'=inInventoryItem, 'craft'=createItem, 'block'=blockPlace
def pin(name, text): return ('mapObjectPin', {'mapObjectGuid': map_objects[name], 'pinText': text})
def ui(text): return ('uiHighLight', {'highLightUIObjectId': 'craftButton', 'highLightText': text})
def iv(name, text): return ('itemViewHighLight', {'highLightItemGuid': items[name], 'highLightText': text})
def key(state, text): return ('keyControl', {'uiState': state, 'controlText': text})

CHALLENGES = [
    ('小石を3個拾う', '地面の小石を左クリックで3個拾おう', 'item', '小石', 3,
     [pin('小石', '左クリックで拾う')], '小石'),
    ('石器を作る', '小石3個からインベントリで石器をクラフトしよう', 'craft', '石器', None,
     [ui('クラフトボタンで作成'), iv('石器', '石器のレシピを確認')], '石器'),
    ('木を伐採して原木を入手する', '石器で木を伐採して原木を3個集めよう', 'item', '原木', 3,
     [pin('木', '石器で木を伐採')], '原木'),
    ('木の板を5枚作る', '原木から木の板を5枚クラフトしよう', 'item', '木の板', 5,
     [iv('木の板', '原木から木の板を作る')], '木の板'),
    ('木の棒を5本作る', '木の板から木の棒を5本クラフトしよう', 'item', '木の棒', 5, [], '木の棒'),
    ('石を採掘する', '石鉱脈から石を5個採掘しよう', 'item', '石', 5,
     [pin('石鉱脈', '石鉱脈から石を採掘')], '石'),
    ('砕いた石材を5個作る', '石から砕いた石材を5個クラフトしよう', 'item', '砕いた石材', 5, [], '砕いた石材'),
    ('石の斧を作る', '木の棒と砕いた石材で石の斧を作ろう', 'craft', '石の斧', None, [], '石の斧'),
    ('風力掘削機を設置する', '研究画面で原始研究1〜3を完了し、風力掘削機を作って設置しよう', 'block', '風力掘削機', 1,
     [key('GameScreen', '研究画面で原始研究1〜3を完了して解放')], '砕いた石材'),
    ('粘土を入手する', '風力掘削機で粘土を採掘して1個入手しよう', 'item', '粘土', 1, [], '粘土'),
    ('レンガを作る', '粘土からレンガをクラフトしよう', 'craft', 'レンガ', None,
     [iv('レンガ', '粘土からレンガを作る')], 'レンガ'),
    ('青銅の鉱石を5個採掘する', '青銅の鉱脈の上に風力掘削機を設置して青銅の鉱石を5個採掘しよう', 'item', '青銅の鉱石', 5,
     [], '青銅の鉱石'),
    ('青銅鉱石の粉を3個作る', '青銅の鉱石から青銅鉱石の粉を3個クラフトしよう', 'item', '青銅鉱石の粉', 3, [], '青銅鉱石の粉'),
    ('石窯を設置する', '原始研究4を完了し、石窯を作って設置しよう', 'block', '石窯', 1,
     [key('GameScreen', '原始研究4を完了して石窯を解放')], 'レンガ'),
    ('青銅インゴットを作る', '石窯に青銅鉱石の粉と原木を入れて青銅インゴットを精錬しよう', 'item', '青銅インゴット', 1, [], '青銅インゴット'),
    ('青銅シートを作る', '青銅インゴット3個から青銅シートをクラフトしよう', 'craft', '青銅シート', None, [], '青銅シート'),
    ('木釘を9本作る', '木の棒から木釘を9本クラフトしよう', 'item', '木釘', 9, [], '木釘'),
    ('合板を作る', '木釘と木の板で合板をクラフトしよう', 'craft', '合板', None, [], '合板'),
    ('補強棒材を作る', '木の棒と青銅シートで補強棒材をクラフトしよう', 'craft', '補強棒材', None, [], '補強棒材'),
    ('木のフレームを作る', '補強棒材と合板で木のフレームをクラフトしよう', 'craft', '木のフレーム', None, [], '木のフレーム'),
]

# 機械検証: 到達可能性（各アイテムが序盤の獲得手段を持つか）
# Validation: obtainability (every target item has an early acquisition path)
initial_craft_results = {c['craftResultItemGuid'] for c in crafts if c.get('initialUnlocked')}
map_drops = set()
for m in load(V8, 'mapObjects.json')['data']:
    # ブッシュは移行ミス残骸で廃止予定のため獲得手段として数えない
    # Bushes are deprecated migration leftovers; never count them as a source
    if m['mapObjectName'] == 'ブッシュ':
        continue
    for e in m.get('earnItems') or []:
        map_drops.add(e['itemGuid'])
# 地中鉱脈×掘削機mineSettingsの交差を正規の採掘ルートとして数える
# Count underground veins intersected with miner mineSettings as the mining route
vein_items = {v['veinItemGuid'] for v in load_map()['itemMapVeins']}
miner_mines = set()
for b in load(V8, 'blocks.json')['data']:
    for ms in (b.get('blockParam', {}).get('mineSettings') or []):
        miner_mines.add(ms['itemGuid'])
miner_mines &= vein_items
machine_outputs = set()
for mr in load(V8, 'machineRecipes.json')['data']:
    for o in mr.get('outputItems') or []:
        machine_outputs.add(o['itemGuid'])
research_blocks = set()
for r in research:
    for a in r.get('clearedActions') or []:
        for g in a.get('gameActionParam', {}).get('unlockBlockGuids', []):
            research_blocks.add(g)

errors = []
for title, _, task, target, _, _, _ in CHALLENGES:
    if task == 'block':
        g = blocks[target]
        if g not in research_blocks:
            errors.append(f'{title}: ブロック {target} を解放するresearchが無い')
    else:
        g = items[target]
        if g not in initial_craft_results | map_drops | miner_mines | machine_outputs:
            errors.append(f'{title}: アイテム {target} の獲得手段が見つからない')
if errors:
    print('\n'.join(errors)); sys.exit(1)

# challenges.json 構築 / Build challenges.json
out = []
prev_guid = None
for i, (title, summary, task, target, count, tutorials, icon) in enumerate(CHALLENGES):
    c = {
        'challengeGuid': guid_for(title),
        'title': title,
        'summary': summary,
        'unlockAllPreviousChallengeComplete': True,
        'prevChallengeGuids': [prev_guid] if prev_guid else [],
        'tutorials': [{'tutorialType': t, 'tutorialParam': p} for t, p in tutorials],
        'startedActions': [],
        'clearedActions': [],
        'displayListParam': {
            'UIPosition': [300 * (i % 5), 250 * (i // 5)],
            'UIScale': [1, 1, 1],
            'IconItem': items[icon],
        },
    }
    if task == 'item':
        c['taskCompletionType'] = 'inInventoryItem'
        c['taskParam'] = {'itemGuid': items[target], 'itemCount': count}
    elif task == 'craft':
        c['taskCompletionType'] = 'createItem'
        c['taskParam'] = {'itemGuid': items[target]}
    else:
        c['taskCompletionType'] = 'blockPlace'
        c['taskParam'] = {'blockGuid': blocks[target], 'itemCount': count}
    prev_guid = c['challengeGuid']
    out.append(c)

# 開幕スキット2本をチャレンジ1へ結線（v3と同じ構成）
# Wire the two opening skits to challenge 1 (same as v3)
out[0]['startedActions'] = [
    {'gameActionType': 'playSkit', 'gameActionParam': {
        'skitAddressablePath': 'Vanilla/Skit/skits/100_start_game', 'playSkitType': 'normal', 'playSortPriority': 0}},
    {'gameActionType': 'playSkit', 'gameActionParam': {
        'skitAddressablePath': 'Vanilla/Skit/skits/200_star_background', 'playSkitType': 'background', 'playSortPriority': 1}},
]

challenges_json = {'data': [{
    'categoryGuid': guid_for('category:生きる基盤'),
    'categoryName': '生きる基盤',
    'categoryDescription': 'この星で生き延びるための最初の技術を身につける',
    'displayOrder': 100,
    'IconItem': items['石器'],
    'initialUnlocked': True,
    'challenges': out,
}]}

# characters.json はv3の3キャラをスキーマ準拠フィールドのみで移植
# Port the three v3 characters keeping only schema-defined fields
chars = load(V3, 'characters.json')['data']
characters_json = {'data': [{
    'characterId': c['characterId'],
    'displayName': c['displayName'],
    'modelAddresablePath': c['modelAddresablePath'],
    'skitModelAddresablePath': c['skitModelAddresablePath'],
} for c in chars]}

for name, payload in [('challenges.json', challenges_json), ('characters.json', characters_json)]:
    with open(os.path.join(V8, name), 'w', encoding='utf-8') as f:
        json.dump(payload, f, ensure_ascii=False, indent=2)
        f.write('\n')
print(f'OK: {len(out)} challenges, {len(characters_json["data"])} characters')
```

- [ ] **Step 2: 実行して機械検証を通す**

Run: `cd ../moorestech_master && python3 tools/tutorial_v3_port/generate_challenges.py`
Expected: `OK: 20 challenges, 3 characters`（検証エラーが出た場合はチャレンジ表の該当行を修正して再実行。名前引き失敗はKeyErrorで落ちる=GUID誤りの検知）

- [ ] **Step 3: 出力をスキーマと目視突合**

`challenges.json` の先頭チャレンジ1件について、`VanillaSchema/challenges.yml`・`ref/gameAction.yml`・`ref/graphViewSettings.yml` のkey名と一致していることを確認（taskParam の switch 分岐、tutorialParam の分岐、playSkit のパラメータ名）。`characters.json` は `characters.yml` の4フィールドと一致確認。

- [ ] **Step 4: 冪等性確認**

Run: `cd ../moorestech_master && python3 tools/tutorial_v3_port/generate_challenges.py && git diff --stat`
Expected: 2回目実行で差分ゼロ（`git diff --stat` が challenges.json/characters.json の再変更を出さない）

- [ ] **Step 5: マスターリポジトリでコミット**

```bash
cd ../moorestech_master
git add tools/tutorial_v3_port/generate_challenges.py \
        server_v8/mods/moorestechAlphaMod_8/master/challenges.json \
        server_v8/mods/moorestechAlphaMod_8/master/characters.json
git commit -m "feat(v8): チュートリアル/ストーリーをv3からv8進行に合わせて移植（チャレンジ20個+キャラ3体）"
```

### Task 2: クライアント契約テストによる整合検証

**Files:**
- Test(実行のみ): `moorestech_client/Assets/Scripts/Client.Tests/WebUi/TutorialAnchorContractTest.cs`

**Interfaces:**
- Consumes: Task 1 の challenges.json（uiHighLight の highLightUIObjectId が web アンカーに写像可能であることを検証するテストが既存）
- Produces: なし（検証ゲート）

- [ ] **Step 1: チュートリアルアンカー契約テストを実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TutorialAnchor"`
Expected: PASS。「Unity is reloading」エラー時は45秒待ってリトライ

- [ ] **Step 2: チャレンジ/マスタ関連の既存テストを実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Challenge"`
Expected: PASS（既存のチャレンジ系テストがv8 modを読む場合の回帰検知。読まない場合もPASSのままで害なし）

- [ ] **Step 3: 失敗があれば原因を particular に修正して再実行**

失敗内容が challenges.json のデータ不備なら Task 1 のチャレンジ表を直してスクリプト再実行→再コミット。テスト側の期待値が古い場合はユーザーに報告（テスト改変はこのプランのスコープ外）。

### Task 3: プレイテストDSLによる実走検証

**Files:**
- 必要時Create: プレイテストシナリオ（`unity-playmode-recorded-playtest` スキルの規約に従う。配置先はスキル参照）

**Interfaces:**
- Consumes: Task 1 のマスターデータ（テストプレイModは `../moorestech_master/server_v8/mods` からロードされる）
- Produces: 録画付きの実走エビデンス

- [ ] **Step 1: unity-playmode-recorded-playtest スキルを起動して序盤シナリオを実行**

検証項目:
1. 起動が MooresmasterLoaderException 無しで完了する（スキーマ不整合の無言死検知）
2. チャレンジ#1「小石を3個拾う」がHUDに表示され、開幕スキット（100_start_game）が再生される
3. mapObjectPin（小石ピン）がWeb HUDに表示される（現ブランチのWorldPin経路）
4. 小石3個入手でチャレンジ#1が完了し#2が解放される

注意（メモリ由来の既知の罠）: masterピン留めworktreeを使う運用があるが、本検証は**今回変更した実master**（`../moorestech_master`）を読む構成で行うこと。ポート11564固定のため他worktreeのPlayModeと同時実行不可。

- [ ] **Step 2: 失敗時はdebug-workflowで切り分け**

ロード死→スキーマ突合（Task 1 Step 3）に戻る。ピン不表示→現ブランチのHUDピン実装の問題かデータ問題かを `uloop get-logs --log-type Error` で切り分け、データ問題ならチャレンジ表を修正。

- [ ] **Step 3: エビデンスと結果を記録してクライアント側もコミット**

シナリオファイルを新規作成した場合は moorestech 本体リポジトリでコミット:

```bash
git add <新規シナリオファイル>
git commit -m "test(playtest): v8チュートリアル序盤の実走シナリオを追加"
```

## 機能パリティ（死活表）

| 操作 | プラン後 | 根拠 |
|---|---|---|
| v8の既存進行（research/クラフト/建設） | 生きる | challenges.json は空→追加のみ。research.json 等は不変更 |
| 既存セーブのロード | 生きる | チャレンジ進行はセーブに無ければ未達扱いで開始されるだけ（データ追加はロード互換） |
| v3 mod 側の動作 | 不変 | v3側ファイルは読み取りのみ |

## リスクと対応（specから）

- uGUI由来ID → `craftButton` のみ使用（Global Constraints）+ Task 2 の契約テストで機械検証
- research整合 → チャレンジ表に前提research列を明記、blockPlace対象はスクリプトが research の unlockBlock と突合
- masterピン非互換 → Task 3 で実masterを読む構成を明示
