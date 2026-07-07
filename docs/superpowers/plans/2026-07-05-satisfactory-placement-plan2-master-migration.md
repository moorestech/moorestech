# プラン2: 本番マスタ正式移行 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 本番マスタ（moorestechAlphaMod_8）の暫定解放を撤廃し、通常ブロックに requiredItems・正式な解放進行（research の unlockBlock）を機械変換スクリプトで投入する。

**Architecture:** moorestech_master リポジトリに冪等な Python 移行スクリプトを置き、dry-run レポートで内容を人間レビューしてから apply する。C# コード変更はゼロ（プラン1のローダー・バリデータ・GameActionExecutor が既に対応済み）。プラン4対象の特殊設置系ブロック（レール橋脚・駅・プラットフォーム・電柱・チェーンポール）と列車車両は requiredItems 投入から**除外**し、増殖経路を塞いだまま解放進行だけ正式化する。

**Tech Stack:** Python 3 (stdlib json のみ) / moorestech_master (データリポジトリ) / uloop (Unity 検証)

## Global Constraints

- **増殖経路の順序制約**（スペック L66）: スロット指定消費5プロトコル（プラン4）未改修のため、それらが設置する blockType `TrainRail` / `TrainStation` / `TrainItemPlatform` / `TrainFluidPlatform` / `ElectricPole` / `GearChainPole` のブロックと列車車両（train.json trainCars）には requiredItems を**投入しない**。これらのクラフトレシピ・アイテムは存続させる（旧プロトコルが消費するため）
- **items.json からのブロックアイテム削除は禁止**: `BlockMasterUtil.Initialize` が全ブロックの `itemGuid` を `ItemMaster.GetItemId` で解決するため、アイテム削除はプラン5（itemGuid フィールド削除と同時）まで不可。プラン2では「レシピ削除で入手経路を閉じる」に留める
- **垂直オーバーライド複製**: `overrideVerticalBlock` の up/down 先ブロックには基底と**同一内容**の requiredItems を複製する（ロード時バリデーション `OverrideVerticalRequiredItemsValidation` が不一致を拒否する）
- **mooreseditor.app を終了してから JSON を書き換える**（起動中は数秒で書き戻される）。作業完了後の再起動は自由
- moorestech_master は現在 **detached HEAD (8beb0f2)** + blocks.json に未コミット drift（mooreseditor による maxWireConnectionCount/maxWireLength 追記）。ブランチ化と drift コミットを先に行う
- JSON 書き出しは `indent=2, ensure_ascii=False` + 末尾改行（現行フォーマット準拠）。git diff が意図した変更のみになることをレビューで確認する
- 本プラン完了後も残る既知の暫定状態（申し送りに記録）: 除外6種+車両は「解放後は無償設置・アイテムも並行入手可」/ 木のシャフトはレシピ存続（機械レシピ材料のため）/ block の imagePath・category は未投入（item 側 imagePath が全件空文字のため移植元が無い。アイコンは itemGuid 経由解決を継続）

## 事前調査で確定した事実（実装者は再調査不要）

- blocks.json 67ブロック、全て interim で `initialUnlocked: true`。requiredItems/sortPriority キーは 鉄の歯車(590)・木の歯車(310) の sortPriority 2件のみ既存 → **既存値は維持**
- items.json 146アイテム（うち67がブロックアイテム）。全アイテムに sortPriority あり、imagePath は全件空文字。initialUnlocked:true は 小石・石器・原木・木の板・木の棒 の5件のみ（ブロックアイテムは全件 false）
- craftRecipes.json 102レシピ、全て `initialUnlocked: true`、`isRemain` 使用ゼロ。78レシピがブロックアイテムを産出。machineRecipes にブロックアイテムを**出力**するものは無し
- research.json 47ノード。解放は `unlockItemRecipeView`（`gameActionParam.unlockItemGuids`）のみ + `giveItem` 3件（原始研究5/7/8 が 燃料式風車 `a3cada69-0366-461b-ad67-14b9ff2c443b` ×1 を付与）。challenges.json は空
- gameAction スキーマ（VanillaSchema/ref/gameAction.yml）: `unlockBlock` → `unlockBlockGuids` (L69) / `unlockTrainCar` → `unlockTrainCarGuids` (L81)
- 木のシャフト item `24a63965-fc83-4eff-b0b0-00e264d23c1f` は機械レシピ（原始的な加工機: 鉄インゴット+木のシャフト→鉄のロッド）の**材料** → 素材レシピ `98b86740-6bb2-4e7a-9d5f-6c845c1692b8`（木の棒1+砕いた石材1）を存続。変換レシピ `a59f5401-...`（木の縦シャフト→木のシャフト）は削除
- 垂直オーバーライド variant（up/down 専用ブロック）は8件: 上り/下り × {ベルトコンベア, 高速ベルトコンベア, 直線歯車ベルトコンベア, 鉄の歯車ベルトコンベア}。基本土台は全参照が自分自身なので variant ではない。variant は「基底の requiredItems を複製・initialUnlocked=false・unlockBlock 対象外（解放判定は基底ブロックで行われるためビルドメニューにも出さない）」
- ベルト系レシピは craftResultCount 3〜5（個数割りで端数が出たら切り上げ+警告ログ）
- `railItems`（train.json）は既に独立した「レール」item `5be3a22c-...` を参照しており橋脚 item とは別物 → スペック記載の「レール素材アイテム分離」は**現状不要（作業なし）**
- 列車車両3種の (itemGuid, trainCarGuid): 蒸気機関車 (`582503fe-9e74-432d-b33d-c22e7c56d286`, `9e3215d5-175e-4600-adee-2c32f786d124`) / 貨車 (`fe0c64b0-fe2a-4355-ad70-a7797b23f535`, `f998f54b-7619-491c-a100-ee88955f9058`) / ディーゼル機関車 (`019eea23-14d5-71b9-96f0-f9126405d208`, `019f0d20-d52e-7172-8b55-bd9b79b6feb1`)

---

### Task 1: リポジトリ整備（コーディネータがインラインで実施）

**Files:**
- moorestech_master リポジトリの git 操作のみ

- [ ] **Step 1: mooreseditor を終了**

```bash
osascript -e 'quit app "mooreseditor"' 2>/dev/null; sleep 2; pgrep -fl mooreseditor || echo "mooreseditor stopped"
```
Expected: `mooreseditor stopped`

- [ ] **Step 2: detached HEAD をブランチ化し drift をコミット**

```bash
cd /Users/katsumi/moorestech_master
git switch -c plan2-master-migration
git add server_v8/mods/moorestechAlphaMod_8/master/blocks.json
git commit -m "chore: mooreseditorによるワイヤー系blockParamデフォルト値の書き戻しをコミット"
git log --oneline -2
```
Expected: 新ブランチ `plan2-master-migration`、HEAD に chore コミット、その親が 8beb0f2

---

### Task 2: 移行スクリプト作成 + dry-run レポート

**Files:**
- Create: `/Users/katsumi/moorestech_master/tools/plan2_migration/migrate.py`

**Interfaces:**
- Produces: `python3 tools/plan2_migration/migrate.py`（dry-run: レポートを stdout、書き込みなし）/ `--apply`（blocks.json / craftRecipes.json / research.json を書き換え + 事後アサーション）。Task 3 はこの CLI を使う

- [ ] **Step 1: スクリプトを作成**

以下を完全に実装する（アルゴリズムは変更禁止。コード整形・ログ文言の調整は可）:

```python
#!/usr/bin/env python3
# プラン2: Satisfactory式設置システムの本番マスタ正式移行
# Plan 2: production-master migration for the Satisfactory-style placement system
#
# 使い方: python3 tools/plan2_migration/migrate.py [--apply]
# --apply なしは dry-run（レポートのみ・書き込みなし）
import argparse
import json
import math
import sys
from pathlib import Path

MASTER_DIR = Path(__file__).resolve().parents[2] / 'server_v8' / 'mods' / 'moorestechAlphaMod_8' / 'master'

# プラン4で移行する特殊設置系はrequiredItems投入から除外（増殖経路防止）
EXCLUDED_BLOCK_TYPES = {'TrainRail', 'TrainStation', 'TrainItemPlatform',
                        'TrainFluidPlatform', 'ElectricPole', 'GearChainPole'}
# 木のシャフト: 機械レシピ（鉄のロッド）の材料のため素材レシピを存続する例外
WOOD_SHAFT_ITEM_GUID = '24a63965-fc83-4eff-b0b0-00e264d23c1f'
# 燃料式風車: 研究報酬giveItemをunlockBlockへ置換する対象
FUEL_WINDMILL_ITEM_GUID = 'a3cada69-0366-461b-ad67-14b9ff2c443b'


def load(name):
    return json.loads((MASTER_DIR / name).read_text(encoding='utf-8'))


def save(name, obj):
    text = json.dumps(obj, ensure_ascii=False, indent=2) + '\n'
    (MASTER_DIR / name).write_text(text, encoding='utf-8')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()

    blocks = load('blocks.json')
    items = load('items.json')
    crafts = load('craftRecipes.json')
    research = load('research.json')
    train = load('train.json')

    item_by_guid = {i['itemGuid']: i for i in items['data']}
    block_by_item = {b['itemGuid']: b for b in blocks['data']}
    block_by_guid = {b['blockGuid']: b for b in blocks['data']}
    block_item_guids = set(block_by_item)
    train_car_items = {c['itemGuid']: c['trainCarGuid'] for c in train['trainCars']}
    excluded_item_guids = {b['itemGuid'] for b in blocks['data']
                           if b['blockType'] in EXCLUDED_BLOCK_TYPES}
    keep_recipe_results = excluded_item_guids | {WOOD_SHAFT_ITEM_GUID}

    # 垂直オーバーライドvariant（up/down先。自己参照は除く）
    variant_guids = set()
    for b in blocks['data']:
        ov = b.get('overrideVerticalBlock') or {}
        if not isinstance(ov, dict):
            continue
        for key in ('upBlockGuid', 'downBlockGuid'):
            guid = ov.get(key)
            if guid and guid != b['blockGuid']:
                variant_guids.add(guid)
    variant_base = {}  # variant blockGuid -> 基底 block
    for b in blocks['data']:
        ov = b.get('overrideVerticalBlock') or {}
        if isinstance(ov, dict):
            for key in ('upBlockGuid', 'downBlockGuid'):
                if ov.get(key) in variant_guids:
                    variant_base[ov[key]] = b

    recipes_by_result = {}
    for r in crafts['data']:
        recipes_by_result.setdefault(r['craftResultItemGuid'], []).append(r)

    warnings = []

    def has_block_ingredient(recipe):
        return any(x['itemGuid'] in block_item_guids for x in recipe['requiredItems'])

    def pick_recipe(item_guid):
        # 素材のみのレシピを正、無ければ変換レシピ1本を展開対象にする
        candidates = recipes_by_result.get(item_guid, [])
        pure = [r for r in candidates if not has_block_ingredient(r)]
        if len(pure) == 1:
            return pure[0]
        if len(pure) > 1:
            sys.exit(f'ERROR: 素材レシピが複数: {item_by_guid[item_guid]["name"]}')
        conv = [r for r in candidates if has_block_ingredient(r)]
        if len(conv) == 1:
            return conv[0]
        sys.exit(f'ERROR: 正レシピを特定できない: {item_by_guid[item_guid]["name"]} '
                 f'(pure={len(pure)}, conv={len(conv)})')

    def per_unit_cost(item_guid, visiting):
        # ブロックアイテム材料を純素材まで再帰展開し、1個あたりコストを返す
        if item_guid in visiting:
            sys.exit(f'ERROR: レシピ循環: {item_by_guid[item_guid]["name"]}')
        recipe = pick_recipe(item_guid)
        total = {}
        for ing in recipe['requiredItems']:
            if ing['itemGuid'] in block_item_guids:
                sub = per_unit_cost(ing['itemGuid'], visiting | {item_guid})
                for g, c in sub.items():
                    total[g] = total.get(g, 0) + c * ing['count']
            else:
                total[ing['itemGuid']] = total.get(ing['itemGuid'], 0) + ing['count']
        result_count = recipe['craftResultCount']
        cost = {}
        for g, c in total.items():
            if c % result_count != 0:
                warnings.append(f'端数切り上げ: {item_by_guid[item_guid]["name"]} の '
                                f'{item_by_guid[g]["name"]} {c}/{result_count}')
            cost[g] = math.ceil(c / result_count)
        return cost

    # --- blocks.json: requiredItems / sortPriority / initialUnlocked ---
    report_costs = []
    for b in blocks['data']:
        item = item_by_guid[b['itemGuid']]
        if b['blockType'] in EXCLUDED_BLOCK_TYPES:
            b.pop('requiredItems', None)
            label = '(除外: プラン4対象)'
        elif b['blockGuid'] in variant_guids:
            base = variant_base[b['blockGuid']]
            base_cost = per_unit_cost(base['itemGuid'], set())
            b['requiredItems'] = [{'itemGuid': g, 'count': c} for g, c in base_cost.items()]
            label = f'(基底 {base["name"]} を複製)'
        else:
            cost = per_unit_cost(b['itemGuid'], set())
            b['requiredItems'] = [{'itemGuid': g, 'count': c} for g, c in cost.items()]
            label = ''
        if 'sortPriority' not in b:
            b['sortPriority'] = item['sortPriority']
        b['initialUnlocked'] = bool(item.get('initialUnlocked', False))
        cost_str = ', '.join(f'{item_by_guid[x["itemGuid"]]["name"]}x{x["count"]}'
                             for x in b.get('requiredItems', []))
        report_costs.append(f'  {b["name"]}: {cost_str or "コスト無し"} {label}')

    # --- craftRecipes.json: ブロックレシピ削除（例外は素材レシピのみ存続） ---
    kept, deleted = [], []
    for r in crafts['data']:
        result = r['craftResultItemGuid']
        if result in block_item_guids and not (
                result in keep_recipe_results and not has_block_ingredient(r)):
            deleted.append(r)
        else:
            kept.append(r)
    crafts['data'] = kept

    # --- research.json: view置換 + unlockBlock/unlockTrainCar追加 + 風車報酬置換 ---
    unlocked_block_guids = set()
    research_report = []
    for node in research['data']:
        new_actions = []
        add_block_guids = []
        add_traincar_guids = []
        for action in node['clearedActions']:
            if action['gameActionType'] == 'unlockItemRecipeView':
                guids = action['gameActionParam']['unlockItemGuids']
                keep_guids = [g for g in guids
                              if g not in block_item_guids or g in keep_recipe_results]
                for g in guids:
                    if g in block_item_guids and block_by_item[g]['blockGuid'] not in variant_guids:
                        add_block_guids.append(block_by_item[g]['blockGuid'])
                    if g in train_car_items:
                        add_traincar_guids.append(train_car_items[g])
                if keep_guids:
                    action['gameActionParam']['unlockItemGuids'] = keep_guids
                    new_actions.append(action)
            elif action['gameActionType'] == 'giveItem':
                rewards = action['gameActionParam']['rewardItems']
                remain = [x for x in rewards if x['itemGuid'] != FUEL_WINDMILL_ITEM_GUID]
                if len(remain) != len(rewards):
                    add_block_guids.append(block_by_item[FUEL_WINDMILL_ITEM_GUID]['blockGuid'])
                if remain:
                    action['gameActionParam']['rewardItems'] = remain
                    new_actions.append(action)
            else:
                new_actions.append(action)
        if add_block_guids:
            new_actions.append({'gameActionType': 'unlockBlock',
                                'gameActionParam': {'unlockBlockGuids': add_block_guids}})
            unlocked_block_guids.update(add_block_guids)
            research_report.append(
                f'  {node["researchNodeName"]}: unlockBlock '
                + ', '.join(block_by_guid[g]['name'] for g in add_block_guids))
        if add_traincar_guids:
            new_actions.append({'gameActionType': 'unlockTrainCar',
                                'gameActionParam': {'unlockTrainCarGuids': add_traincar_guids}})
            research_report.append(f'  {node["researchNodeName"]}: unlockTrainCar x{len(add_traincar_guids)}')
        node['clearedActions'] = new_actions

    # --- 検証: 全ブロックが到達可能 / requiredItemsが純素材 / 複製一致 ---
    errors = []
    for b in blocks['data']:
        gated = not b['initialUnlocked'] and b['blockGuid'] not in variant_guids
        if gated and b['blockGuid'] not in unlocked_block_guids:
            errors.append(f'未到達ブロック: {b["name"]}')
        for x in b.get('requiredItems', []):
            if x['itemGuid'] in block_item_guids:
                errors.append(f'requiredItemsにブロックアイテム: {b["name"]}')
            if x['itemGuid'] not in item_by_guid or x['count'] <= 0:
                errors.append(f'不正requiredItems: {b["name"]}')
    for variant_guid, base in variant_base.items():
        if block_by_guid[variant_guid].get('requiredItems') != base.get('requiredItems'):
            errors.append(f'複製不一致: {block_by_guid[variant_guid]["name"]}')
    surviving_results = {r['craftResultItemGuid'] for r in crafts['data']}
    for r in crafts['data']:
        for ing in r['requiredItems']:
            g = ing['itemGuid']
            craftable = g not in block_item_guids or g in surviving_results
            if not craftable:
                errors.append(f'死にレシピ: {item_by_guid[r["craftResultItemGuid"]]["name"]} が入手不能材料 {item_by_guid[g]["name"]} を要求')

    print('=== ブロック建設コスト ===')
    print('\n'.join(report_costs))
    print(f'\n=== レシピ削除 {len(deleted)}件 / 存続 {len(kept)}件 ===')
    print('\n'.join(f'  削除: {item_by_guid[r["craftResultItemGuid"]]["name"]} ({r["craftRecipeGuid"]})' for r in deleted))
    print('\n=== research 変更 ===')
    print('\n'.join(research_report))
    print(f'\n=== 警告 {len(warnings)}件 ===')
    print('\n'.join(f'  {w}' for w in warnings))
    if errors:
        print(f'\n=== エラー {len(errors)}件 ===')
        print('\n'.join(f'  {e}' for e in errors))
        sys.exit(1)
    if args.apply:
        save('blocks.json', blocks)
        save('craftRecipes.json', crafts)
        save('research.json', research)
        print('\nAPPLIED: blocks.json / craftRecipes.json / research.json を書き換えました')
    else:
        print('\nDRY-RUN: 書き込みは行っていません（--apply で実行）')


if __name__ == '__main__':
    main()
```

- [ ] **Step 2: dry-run を実行して検証**

```bash
cd /Users/katsumi/moorestech_master && python3 tools/plan2_migration/migrate.py
```

Expected（すべて満たすこと。満たさない場合は原因を調査して報告）:
- exit code 0、エラー0件
- ブロック67件全てのコスト行が出る。除外ラベル `(除外: プラン4対象)` はちょうど **9件**（レール橋脚・蒸気機関車駅・貨物プラットフォーム・液体プラットフォーム・電柱・高圧電柱・広範囲電柱・歯車チェーンポール・コンパクト歯車チェーンポール）
- `(基底 ... を複製)` はちょうど **8件**（上り/下り×4ベルト系）
- レシピ削除は **68件・存続34件**（78ブロックレシピのうち存続は「除外9種の素材レシピ + 木のシャフト素材レシピ」の10件。全102 − 68 = 34。**実測値が一致しない場合は内訳を出して報告**。変換レシピ（上り/下りベルト・縦シャフト系・木のシャフト逆変換 a59f5401）が削除側に入っていることを目視確認）
- 端数切り上げ警告にベルトコンベア系（craftResultCount 3〜5）以外が混ざっていないか確認
- `git status --short` が空（dry-run で書き込みなし）

- [ ] **Step 3: スクリプトのみコミット**

```bash
cd /Users/katsumi/moorestech_master && git add tools/plan2_migration/migrate.py && git commit -m "feat: プラン2本番マスタ移行スクリプトを追加"
```

- [ ] **Step 4: dry-run レポート全文をタスク成果物として報告**（レビュアーがコスト表・レシピ削除一覧・research 変更を精査する）

---

### Task 3: 適用 + コミット

**Interfaces:**
- Consumes: Task 2 の `migrate.py`（レビュー承認済み dry-run レポート）

- [ ] **Step 1: mooreseditor が起動していないことを再確認**

```bash
pgrep -fl mooreseditor && echo "STOP: mooreseditorを終了せよ" || echo OK
```

- [ ] **Step 2: 適用**

```bash
cd /Users/katsumi/moorestech_master && python3 tools/plan2_migration/migrate.py --apply
```
Expected: dry-run と同一レポート + `APPLIED`、exit 0

- [ ] **Step 3: diff がノイズなしであることを確認**

```bash
cd /Users/katsumi/moorestech_master && git diff --stat
git diff server_v8/mods/moorestechAlphaMod_8/master/craftRecipes.json | head -60
```
Expected: 変更3ファイルのみ（blocks/craftRecipes/research）。フォーマット全体の書き換え（全行diff）になっていないこと。craftRecipes の diff がレシピオブジェクト単位の削除であること

（注: スクリプトは非冪等 — 適用後はブロックレシピが消えているため再実行は「正レシピを特定できない」で止まる。これは仕様。整合性検証は apply 時に書き込み前の in-memory データで実施済みで、書き込み後の実ロード検証は Task 4 が担う）

- [ ] **Step 4: コミット**

```bash
cd /Users/katsumi/moorestech_master && git add -A && git commit -m "feat: プラン2 本番マスタ正式移行（requiredItems投入・レシピ削除・unlockBlock化）"
```

---

### Task 4: Unity 検証（本番マスタロード + 回帰）

**Interfaces:**
- Consumes: Task 3 適用済みの moorestech_master

- [ ] **Step 1: コンパイル確認**

```bash
cd /Users/katsumi/moorestech && uloop compile --project-path ./moorestech_client
```
Expected: Success, ErrorCount 0

- [ ] **Step 2: 本番マスタロード検証（プレイモード起動）**

uloop でプレイモードに入り、起動完了まで待機（初回ロード60秒程度）、Error ログを確認して退出:
```bash
uloop control-play-mode --project-path ./moorestech_client --action enter
sleep 60
uloop get-logs --project-path ./moorestech_client --log-type Error
uloop control-play-mode --project-path ./moorestech_client --action exit
```
Expected: MooresmasterLoaderException / マスタバリデーションエラー / requiredItems 関連エラーが **0件**。（PlayerMovement 系や既知の無関係エラーは無視してよいが、全文を成果物に含めること）

- [ ] **Step 3: 回帰テスト（プラン3と同じスイート）**

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(Tests\.CombinedTest|Tests\.UnitTest)"
```
Expected: サーバー側 122件 PASS 相当（プラン3時と同水準。テストは専用テストマスタを使うため本変更の影響は無い想定 — 差分が出たら報告）。ドメインリロードエラーが出たら45秒待ってリトライ、固着したら `uloop fix`

---

### Task 5: ピン更新 + 記録（コーディネータがインラインで実施）

- [ ] **Step 1: `.moorestech-external-revisions.json` の moorestech_master ピンを Task 3 のコミットハッシュへ更新**（RepositorySync の巻き戻り防止。更新後 moorestech_master 側で `git log --oneline -1` によりブランチ先頭のままであることを確認）
- [ ] **Step 2: moorestech 側でピン更新をコミット**
- [ ] **Step 3: `.superpowers/sdd/progress.md` にプラン2完了を記録。申し送り（2026-07-05-satisfactory-placement-handoff.md）に以下を追記:**
  - プラン4残件: 除外9ブロック+車両3種への requiredItems 投入は5プロトコル改修と同時に行う（migrate.py の EXCLUDED_BLOCK_TYPES を空にして再適用…は不可。同スクリプトを参考に追補スクリプトを書く）
  - プラン5残件: ブロックアイテム削除時に (a) 木のシャフトの機械レシピ材料参照の置換、(b) ベルト/シャフト変換レシピの残骸なし確認、(c) ビルドメニューのアイコン解決を itemGuid 経由から block imagePath へ切替（現状 item imagePath は全件空文字なので画像パス整備が必要）
  - 既存セーブの解放状態: research 済みでも clearedActions は再実行されないため、旧セーブではブロック未解放の可能性あり。新規セーブでの動作確認を正とする

## Self-Review 済み事項

- スペック「マスタJSON移行」6項目との対応: (1)レシピ変換+削除=Task2-3 ✅ (2)アイテム削除=プラン5送り（BlockMasterUtil.Initialize の itemGuid 解決制約のため）✅ (3)imagePath/category/sortPriority= sortPriority のみ投入、imagePath は移植元空文字のため対象外・category はクライアント未使用のため未投入（申し送り）✅ (4)unlock 置換=Task2 ✅ (5)レール素材分離=調査の結果現状不要 ✅ (6)敷設素材存続=レシピ削除対象がブロックアイテム産出レシピのみなので自動的に満たす ✅
- 増殖経路: requiredItems 投入ブロックのうちアイテム入手経路が残るのは 木のシャフト のみで、craft コスト==建設コストのため往復収支ゼロ（中立）。燃料式風車の giveItem は unlockBlock へ置換して入手経路ごと閉鎖 ✅
