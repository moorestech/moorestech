# 植物系mapObjectへの原木ドロップ割り当て Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** `map.json` で `earnItems` が空の植物系mapObject 53件に原木ドロップを割り当て、以後 earnItems 空が生まれないようValidatorで禁止する。

**Architecture:** 変更は2リポジトリに分かれる。データ側は `moorestech_master` の `server_v8/mods/moorestechAlphaMod_8/master/map.json` をPythonスクリプトで一括書き換え（addressablePathで大型/低木を機械分類）。コード側は本repoの `MapObjectMasterUtil.Validate` に空検査を1つ足し、既存の `MapObjectMasterValidationTest` に同型のテストを1本足す。最後に本repoの `.moorestech-external-revisions.json` のピンを、master側PRのpush済みコミットへ更新する。

**Tech Stack:** Unity 2022 / C# (NUnit) / Python 3（マスタJSON一括編集） / uloop CLI（コンパイル・テスト）

## Requirements

設計対話（2026-08-28）で確定した要件。全行が `docs/adr/0037-plant-mapobjects-drop-log.md` に対応する。

- R1: `map.json` の `earnItems` が空の53件を1件も削除しない。全件に原木 `aafce615-6c30-48c4-a29e-3c5b3266748f` を割り当てる。受け入れ基準: 変更後の mapObjects は195件のままで、`earnItems` が空の要素が0件。
- R2: 大型サボテン25件（addressablePath が `Tree/MesaDesert/(Cacactus|Grocactus|Saguaro|Senita)` にマッチ）は `hp:100` `earnItemHpInterval:10` を据え置き、`earnItems` に 原木 `minCount:1 / maxCount:4` を1件だけ持つ。受け入れ基準: 該当25件がこの3値を満たす。
- R3: 低木・草花28件（earnItems空のうちR2にマッチしない全て）は `hp:10` `earnItemHpInterval:10` に変更し、`earnItems` に 原木 `minCount:1 / maxCount:1` を1件だけ持つ。受け入れ基準: 該当28件がこの4値を満たす。
- R4: 低木・草花28件の `miningParam.miningTools` を「石の斧 `4c5fefbd-60a4-42ea-b70a-38a83b96e25e` damage25/attackSpeed1」と「石器 `76174235-48fb-4944-bca7-ad268385d68c` damage10/attackSpeed2」の2件ちょうどに統一する。受け入れ基準: 該当28件のツール構成が他190件と完全一致し、ブッシュのdamage5設定が消えている。
- R5: 新規アイテムを items.json へ追加しない。受け入れ基準: items.json の差分が0。
- R6: `generation.json` を変更しない。未配置22件は定義だけ埋める。受け入れ基準: generation.json の差分が0。
- R7: `MapObjectMasterUtil.Validate` が `earnItems` 空のmapObjectをエラーとして報告する。受け入れ基準: 空要素を含むmapで `Validate` が false を返し、ログに識別可能な文言が含まれる。
- R8: R7の振る舞いを `MapObjectMasterValidationTest` の自動テストで固定する。受け入れ基準: 新規テストが単独で通り、既存2テストも通る。
- R9: 本repoの `.moorestech-external-revisions.json` の `moorestech_master` ピンが、master側PRブランチのpush済みコミットを指す。受け入れ基準: そのコミットハッシュが `git ls-remote` で origin に存在する。
- R10: soundEffectType・スキーマ外キー `earnItemHps`・items.json・VanillaSchema/map.yml は一切変更しない。受け入れ基準: これらのファイル/キーの差分が0。

**やらないこと（スコープ境界）:**
- 未配置22件（Cacactus1-4 / Grocactus1-8 / Opuntia1-4 / Olivebush1-3 / Savanna Bush1-3）を generation.json へ配置すること（bd `moorestech-zlp7` へ分離済み）
- 植物繊維等の新規アイテム追加とそのレシピ設計
- 原木の需給バランス調整（min/max の再チューニング）
- 岩系・鉱脈側のドロップ再検討（ADR 0036で完了済み）
- クライアント側の採掘tooltip実装の変更（既存の `MapObjectMiningPresentation.GetEarnItemGuids` がそのまま原木名を出す）

## Global Constraints

- **partial 禁止・`Func<>` 禁止・try-catch 原則禁止**（AGENTS.md）。本planの変更範囲ではいずれも不要。
- **コメントは日本語・英語の2行セット**（`// 日本語` → `// English`）。各言語1行に収める。日本語本文の長さ目安は処理・変数20字、メソッド30字。
- **`#region Internal` はメソッド内ローカル関数をまとめる用途限定。** `MapObjectMasterUtil.Validate` は既にこの形なので、新しい検査もその `#region Internal` の内側にローカル関数として足す。
- **.cs を変更したら必ずコンパイルを実行する**（`uloop compile --project-path ./moorestech_client`）。
- **`.meta` ファイルは手動作成しない。** 本planは新規.csファイルを作らないので.metaは発生しない。
- **Prefab・シーン等のUnity固有YAMLをテキスト編集しない。** 本planの対象外。
- **マスタJSONの整形は 2スペースインデント・非ASCIIそのまま・末尾改行なし。** `json.dumps(d, ensure_ascii=False, indent=2)` が現行ファイルとバイト一致することを検証済み。この形以外で書き出すと全行が差分になる。
- **`.moorestech-external-revisions.json` のピンはUnityが書き戻すことがある。** ピン更新をコミットする際は `git add -A` でなく当該ファイルを明示指定し、コミット直前に中身を目視確認する。
- **別リポジトリ（moorestech_master）の変更もpushしてPRを作る（必須）。** ローカルコミット止まり・push only は禁止。
- **worktree運用:** 本planは使い捨てworktreeで実行する。`moorestech_master` 側は別リポジトリなので worktree を切らず、`feature/plant-mapobjects-drop-log` ブランチを origin/master から切って作業する。

## File Structure

| ファイル | 種別 | 責務 |
|---|---|---|
| `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json` | Modify | 53件の earnItems / hp / earnItemHpInterval / miningTools を更新。Task 1の唯一の成果物 |
| `moorestech_server/Assets/Scripts/Core.Master/Validator/MapObjectMasterUtil.cs` | Modify | `Validate` に earnItems 空の検査を1ローカル関数として追加 |
| `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Map/MapObjectMasterValidationTest.cs` | Modify | 空検査のテストを1本追加 |
| `.moorestech-external-revisions.json` | Modify | `moorestech_master` のピンをTask 1のpush済みコミットへ更新 |

新規ファイルは無い。既存3ファイル + 別repoの1ファイルのみ。

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置先 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | earnItems空の検査 | `Core.Master.Validator.MapObjectMasterUtil.Validate` | `Validate` 内のローカル関数が `logs` 文字列を積む | 同ファイルの `ItemGuidValidation()` / `MiningToolValidation()` と完全同型 |
| 2 | 検査のテスト | `Tests.UnitTest.Core.Map.MapObjectMasterValidationTest` | forUnitTest mod の map.json を `JObject` で読み、該当箇所だけ壊して `Validate` の false を確認 | 同ファイルの既存2テスト（`同じmapObject内でminingToolsのtoolItemGuidが重複すると失敗する` ほか）と完全同型 |
| 3 | データ変更 | `moorestech_master` の map.json | マスタJSONの直接編集 | ADR 0036 の岩系ドロップ変更（同じファイル・同じ手口） |

- 層責務: `Core.Master.Validator` はマスタデータの整合検査そのものが責務であり、earnItems 空の検出はドメイン語彙を持ち込まない純粋なマスタ検査。ドメイン層への配置は不要。
- 新規パターン: なし。機構選択の分岐点（既存機構の抑止・迂回・並行複製）も無い。
- データフロー: mapObject の採掘は「攻撃→HP境界跨ぎ→earnItems生成」の既存一方向連鎖のままで、書き手も読み手も増えない。
- 機能パリティ死活表: 本変更で失われるプレイヤー操作は無い。53件は現在「殴れるが何も出ない」状態で、変更後は「殴ると原木が出る」になるだけ。低木28件は hp100→10 で必要振り数が減るのみ（操作の消滅ではない）。

---

### Task 1: マスタデータ53件へ原木ドロップを割り当てる（moorestech_master リポジトリ）

**Files:**
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json`
- Test: なし（Unityテストの対象外リポジトリ。検証はPythonアサーションスクリプトで行う）

**Interfaces:**
- Consumes: なし（このタスクが起点）
- Produces: `moorestech_master` の `feature/plant-mapobjects-drop-log` ブランチにpush済みのコミットハッシュ。Task 3 がこれを `.moorestech-external-revisions.json` に書く

- [ ] **Step 1: master リポジトリでブランチを切る**

`moorestech_master` は本repoとは別リポジトリで、メインワークツリーのブランチ操作を止めるhookの対象外。worktreeは切らず直接ブランチを作る。

```bash
cd ../moorestech_master
git fetch origin
git switch --create feature/plant-mapobjects-drop-log origin/master
git log --oneline -1
git status --short
```

Expected: `origin/master` の最新コミットにHEADが乗る。`git status --short` は空。

- [ ] **Step 2: 対象53件のGUIDをスナップショットする**

変更後は「earnItems が空」で対象を特定できなくなるため、先にGUID一覧を固定する。作業ファイルはセッションのscratchpad直下に置く（リポジトリ内には置かない）。

```bash
cd <scratchpad>
python3 - <<'PY'
import json
PATH = "../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json"
d = json.load(open(PATH, encoding="utf-8"))
targets = [m["mapObjectGuid"] for m in d["mapObjects"] if not m.get("earnItems")]
assert len(targets) == 53, len(targets)
json.dump(targets, open("targets.json", "w", encoding="utf-8"))
print("targets:", len(targets))
PY
```

Expected: `targets: 53`。53以外ならmap.jsonが想定と違うので、先に差異を調べる。

- [ ] **Step 3: 変更後の期待状態を検査するスクリプトを書き、実行して失敗を確認する**

`<scratchpad>/assert_map.py` として保存する。

```python
# 変更後の期待状態を検査する。変更前は必ず失敗する
import json, re, sys

PATH = "../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json"
LOG_GUID = "aafce615-6c30-48c4-a29e-3c5b3266748f"
AXE = {"toolItemGuid": "4c5fefbd-60a4-42ea-b70a-38a83b96e25e", "damage": 25, "attackSpeed": 1}
STONE_TOOL = {"toolItemGuid": "76174235-48fb-4944-bca7-ad268385d68c", "damage": 10, "attackSpeed": 2}
LARGE = re.compile(r"Tree/MesaDesert/(Cacactus|Grocactus|Saguaro|Senita)")

TARGET_GUIDS = set(json.load(open("targets.json", encoding="utf-8")))

d = json.load(open(PATH, encoding="utf-8"))
mo = d["mapObjects"]
errors = []

if len(mo) != 195:
    errors.append(f"mapObjects count changed: {len(mo)}")
empty = [m for m in mo if not m.get("earnItems")]
if empty:
    errors.append(f"earnItems empty remains: {len(empty)}")

large = small = 0
for m in mo:
    if m["mapObjectGuid"] not in TARGET_GUIDS:
        continue
    earn = m["earnItems"]
    if len(earn) != 1 or earn[0]["itemGuid"] != LOG_GUID:
        errors.append(f"{m['mapObjectName']}: earnItems not single log")
        continue
    if LARGE.search(m["addressablePath"]):
        large += 1
        if (m["hp"], m["earnItemHpInterval"], earn[0]["minCount"], earn[0]["maxCount"]) != (100, 10, 1, 4):
            errors.append(f"{m['mapObjectName']}: large params mismatch")
    else:
        small += 1
        if (m["hp"], m["earnItemHpInterval"], earn[0]["minCount"], earn[0]["maxCount"]) != (10, 10, 1, 1):
            errors.append(f"{m['mapObjectName']}: small params mismatch")
        if m["miningParam"]["miningTools"] != [AXE, STONE_TOOL]:
            errors.append(f"{m['mapObjectName']}: miningTools not unified")

if large != 25:
    errors.append(f"large count: {large}")
if small != 28:
    errors.append(f"small count: {small}")

print("\n".join(errors) if errors else "OK")
sys.exit(1 if errors else 0)
```

Run: `python3 assert_map.py`
Expected: FAIL。`earnItems empty remains: 53` と `large count: 0` `small count: 0` が出る。

- [ ] **Step 4: 一括更新スクリプトを実行する**

```bash
cd <scratchpad>
python3 - <<'PY'
import json, re

PATH = "../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json"
LOG_GUID = "aafce615-6c30-48c4-a29e-3c5b3266748f"
AXE = {"toolItemGuid": "4c5fefbd-60a4-42ea-b70a-38a83b96e25e", "damage": 25, "attackSpeed": 1}
STONE_TOOL = {"toolItemGuid": "76174235-48fb-4944-bca7-ad268385d68c", "damage": 10, "attackSpeed": 2}
LARGE = re.compile(r"Tree/MesaDesert/(Cacactus|Grocactus|Saguaro|Senita)")

d = json.loads(open(PATH, encoding="utf-8").read())

large = small = 0
for m in d["mapObjects"]:
    if m.get("earnItems"):
        continue
    if LARGE.search(m["addressablePath"]):
        m["hp"] = 100
        m["earnItemHpInterval"] = 10
        m["earnItems"] = [{"itemGuid": LOG_GUID, "minCount": 1, "maxCount": 4}]
        large += 1
    else:
        m["hp"] = 10
        m["earnItemHpInterval"] = 10
        m["earnItems"] = [{"itemGuid": LOG_GUID, "minCount": 1, "maxCount": 1}]
        m["miningParam"]["miningTools"] = [dict(AXE), dict(STONE_TOOL)]
        small += 1

open(PATH, "w", encoding="utf-8").write(json.dumps(d, ensure_ascii=False, indent=2))
print("large:", large, "small:", small)
PY
```

Expected: `large: 25 small: 28`

- [ ] **Step 5: アサーションを再実行して通ることを確認する**

Run: `python3 assert_map.py`
Expected: `OK`（終了コード0）

- [ ] **Step 6: 差分の形を目視で確認する**

```bash
cd ../moorestech_master
git diff --stat
git diff -- server_v8/mods/moorestechAlphaMod_8/master/map.json | head -60
git status --short
```

Expected: 変更ファイルは `server_v8/mods/moorestechAlphaMod_8/master/map.json` の1つだけ（R5・R6・R10の確認）。差分は53件のブロックに閉じており、無関係な行の再整形が起きていないこと。`ensure_ascii=False, indent=2` は現行ファイルとバイト一致することを検証済みなので、全行差分になったら書き出し方を疑う。

- [ ] **Step 7: コミットしてpushし、PRを作る**

```bash
cd ../moorestech_master
git add server_v8/mods/moorestechAlphaMod_8/master/map.json
git commit -m "$(printf '%s\n' \
  'feat(map): 取得アイテムのない植物系mapObject53件へ原木ドロップを割り当てる' \
  '' \
  '大型サボテン25件は既存の木と同じhp100/interval10/原木1〜4。' \
  '低木・草花28件はhp10/interval10/原木1個固定とし、miningToolsを石の斧25+石器10へ統一する。' \
  'ADR: moorestech/docs/adr/0037-plant-mapobjects-drop-log.md')"
git push -u origin feature/plant-mapobjects-drop-log
gh pr create --base master \
  --title "取得アイテムのない植物系mapObject53件へ原木ドロップを割り当てる" \
  --body "$(printf '%s\n' \
    '## 概要' \
    '`earnItems` が空だった mapObject 53件に原木を割り当てます。削除は行いません。' \
    '' \
    '- 大型サボテン25件（Cacactus / Grocactus / Saguaro / Senita）: hp100・interval10 据え置きで 原木 1〜4（既存の木と同一）' \
    '- 低木・草花28件（ブッシュ / Opuntia / Bush / Olivebush / Brittlebush / DryGrass / Peanut / WildflowersYellow）: hp10・interval10 で 原木1個固定、miningTools を「石の斧25 + 石器10」へ統一' \
    '' \
    '## やらないこと' \
    '- 未配置22件の generation.json への配置（別タスク）' \
    '- 新規アイテムの追加' \
    '' \
    'ADR: `moorestech/docs/adr/0037-plant-mapobjects-drop-log.md`')"
git rev-parse HEAD
```

Expected: PR URLが表示される。`git rev-parse HEAD` のハッシュを控える。これがTask 3で使うピン値。

---

### Task 2: earnItems空をValidatorで禁止する

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/MapObjectMasterUtil.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Map/MapObjectMasterValidationTest.cs`

**Interfaces:**
- Consumes: 既存 `public static bool Validate(Map map, out string errorLogs)`（シグネチャ変更なし）
- Produces: `Validate` が earnItems 空を検出したとき `false` を返し、`errorLogs` に `has empty EarnItems` を含める。Task 3 のゲーム起動確認がこの挙動に依存する

- [ ] **Step 1: 失敗するテストを書く**

`MapObjectMasterValidationTest.cs` のクラス末尾、既存テストの下に追加する。

```csharp
        [Test]
        public void earnItemsが空のmapObjectがあると失敗する()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "map.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var miningMapObject = ((JArray)json["mapObjects"]).Children<JObject>()
                .Single(element => (string)element["miningType"] == "Mining");

            // 実在定義のearnItemsだけを空にし、他のマスタ整合性から独立させる
            // Empty only earnItems on a valid definition so other master consistency remains intact
            miningMapObject["earnItems"] = new JArray();
            var master = new MapObjectMaster(json);

            Assert.IsFalse(master.Validate(out var logs));
            StringAssert.Contains("has empty EarnItems", logs);
        }
```

- [ ] **Step 2: コンパイルしてテストを実行し、失敗を確認する**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectMasterValidationTest"
```

Expected: コンパイルはエラー0。新規テストのみ FAIL（`Expected: False But was: True` — Validateが通ってしまう）。既存2テストは PASS。

補足: `uloop run-tests` はCLI側が180秒でタイムアウトすることがあるが、それはテスト失敗ではない。結果の正本は `.uloop/outputs/TestResults` のXML。タイムアウトしても連打せずXMLを読む。"Unity is reloading (Domain Reload in progress)" が出たら45秒待ってリトライする。

- [ ] **Step 3: Validatorに検査を追加する**

`MapObjectMasterUtil.cs` の `Validate` 本体の連結行に1つ足す:

```csharp
        public static bool Validate(Map map, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += ItemGuidValidation();
            errorLogs += EarnItemsValidation();
            errorLogs += MiningToolValidation();
            return string.IsNullOrEmpty(errorLogs);
```

`#region Internal` の内側、`ItemGuidValidation()` の直後にローカル関数を足す:

```csharp
            string EarnItemsValidation()
            {
                // 取得アイテムのないmapObjectは殴っても何も出ない
                // A map object without earn items yields nothing when mined
                var logs = "";
                foreach (var mapObjectElement in map.MapObjects)
                {
                    if (mapObjectElement.EarnItems.Length == 0)
                    {
                        logs += $"[MapObjectMaster] Name:{mapObjectElement.MapObjectName} has empty EarnItems\n";
                    }
                }

                return logs;
            }
```

- [ ] **Step 4: コンパイルしてテストを実行し、通ることを確認する**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectMasterValidationTest"
```

Expected: コンパイルエラー0。3テストすべて PASS。

- [ ] **Step 5: マスタ検査が他のテストを巻き添えにしていないことを確認する**

```bash
uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObject|MapVein|MasterHolder"
```

Expected: 全て PASS。テストmod（forUnitTest / EditModeInPlayingTestMod）の map.json はどちらも earnItems 空を持たないことを調査済みなので、新検査で落ちるものは無い。落ちた場合はテストmod側のデータを埋める前に、落ちた理由を読む。

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Core.Master/Validator/MapObjectMasterUtil.cs \
        moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Map/MapObjectMasterValidationTest.cs
git commit -m "$(printf '%s\n' \
  'feat(master): earnItemsが空のmapObjectをValidatorで禁止する' \
  '' \
  '殴っても何も出ないmapObjectがマスタへ入るのを検知する。' \
  'ADR: docs/adr/0037-plant-mapobjects-drop-log.md')"
```

---

### Task 3: マスタピンを更新してゲート通過を確認する

**Files:**
- Modify: `.moorestech-external-revisions.json`
- Test: なし（PlayModeでの起動確認で代替）

**Interfaces:**
- Consumes: Task 1 の `git rev-parse HEAD`（push済みコミットハッシュ）、Task 2 の `EarnItemsValidation`
- Produces: 新ピンでゲームが起動し、Validatorがエラーを出さない状態

- [ ] **Step 1: ピン先コミットが origin に存在することを確認する**

```bash
cd ../moorestech_master
git rev-parse HEAD
git ls-remote origin feature/plant-mapobjects-drop-log
```

Expected: 両者のハッシュが一致する。一致しないならpushが済んでいない。ローカルコミット止まりのハッシュをピンに書いてはいけない。

- [ ] **Step 2: ピンを書き換える**

`moorestech_master` エントリの `commitHash` だけを差し替える（`moorestech_client_private` 側は触らない）。

```bash
cd <worktree>
NEW_HASH=<Step 1のハッシュ>
python3 - "$NEW_HASH" <<'PY'
import json, sys
path = ".moorestech-external-revisions.json"
d = json.load(open(path, encoding="utf-8"))
for repo in d["repositories"]:
    if repo["key"] == "moorestech_master":
        repo["commitHash"] = sys.argv[1]
open(path, "w", encoding="utf-8").write(json.dumps(d, ensure_ascii=False, indent=4) + "\n")
PY
git diff -- .moorestech-external-revisions.json
```

Expected: 差分は `moorestech_master` の commitHash 1行のみ。整形が変わって全行差分になったら、インデント幅（4スペース）と末尾改行を現行ファイルに合わせ直す。

- [ ] **Step 3: 新しいマスタでゲームが起動し、Validatorが黙ることを確認する**

worktreeのmasterピンworktreeを新ハッシュへ合わせてから、PlayModeで起動確認する。unity-playmode-recorded-playtest スキルのプレイテストDSL（`scripts/run-scenario.sh`）で起動のみのシナリオを回すのが最短。

```bash
uloop get-logs --project-path ./moorestech_client --log-type Error
```

Expected: `[MapObjectMaster]` を含むエラーが0件で、メインゲームシーンまで到達する。`MooresmasterLoaderException` が出る場合はピンworktreeが古いハッシュのままなので、ピンworktreeの同期から見直す。

- [ ] **Step 4: 実際に低木を殴って原木が出ることを確認する**

PlayModeで石の斧または石器を装備し、草むら（DryGrass / WildflowersYellow など）を1回叩く。

Expected: 1振りで対象が消え、原木が1個インベントリへ入る。採掘tooltipに「原木」が表示される（`MapObjectMiningPresentation.GetEarnItemGuids` が空配列を返さなくなるため）。原木が出ない場合はピンが古いか、大型/低木の分類がズレている。

- [ ] **Step 5: コミットする**

`.moorestech-external-revisions.json` はUnityが書き戻すことがあるため、`git add -A` を使わずファイルを明示指定し、addの直前に中身を再確認する。

```bash
cat .moorestech-external-revisions.json
git add .moorestech-external-revisions.json
git commit -m "$(printf '%s\n' \
  'chore: master dataピンを植物系mapObjectの原木ドロップへ更新する' \
  '' \
  'ADR: docs/adr/0037-plant-mapobjects-drop-log.md')"
```

---

### Task 4: 全ブランチレビュー（省略不可）

**Files:**
- Modify: レビュー指摘に応じて Task 1〜3 の対象ファイル

**Interfaces:**
- Consumes: Task 1〜3 の全コミット
- Produces: レビュー済みのブランチと、本repo・moorestech_master 双方のPR

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

`moores-code-review` スキルを起動し、master からの全差分をレビューする。これは自動実行であり、変更が小さいこと・データ変更が中心であることを理由に省略してはならない。

- [ ] **Step 2: 指摘のうち機械的修正を適用し、設計判断はユーザーへ諮る**

- [ ] **Step 3: 本repoのPRを作成する**

`pr-create` スキルで master への PR を作る。本文に `docs/adr/0037-plant-mapobjects-drop-log.md` と、対になる moorestech_master 側PRのURLを併記する。

- [ ] **Step 4: bd issue を閉じ、worktreeを畳む**

```bash
bd close moorestech-lmeu --reason="植物系mapObject53件へ原木ドロップを割り当て、Validatorで空を禁止した"
moores-wt rm <worktree名>
```

`moorestech-zlp7`（未配置22件の配置）は open のまま残す。

---

## 判断記録（ADR）

- 設計裁定の正本: `docs/adr/0037-plant-mapobjects-drop-log.md` / `.decisions/2026-08-28-取得アイテムのない植物系mapObjectは原木を落とす.md`
- 関連ADR: `docs/adr/0036-rock-mapobjects-drop-stone-not-pebble.md`（同じ map.json のドロップ整理の岩版。手口の前例）

planning中に新たに生じた判断:

- **大型/低木の分類を名前列挙でなく addressablePath の正規表現 `Tree/MesaDesert/(Cacactus|Grocactus|Saguaro|Senita)` で行う。** 実データで25件/28件になることを検証済みで、ADRの列挙と完全一致する。出所: agent前提（分類規則を機械化し、レビュアーが再現できる形にするため）
- **マスタJSONの一括編集をPythonの `json.dumps(..., ensure_ascii=False, indent=2)` で行う。** 現行ファイルとバイト一致することを実測済みで、無関係な行の再整形が起きない。出所: agent前提（ADR 0036の差分が対象ブロックに閉じていた前例に合わせる）
- **Task 1 は `moorestech_master` の origin/master から切る（現ピン先の 9b09966 からではない）。** 9b09966 は既に origin/master に取り込まれており、origin/master の先端は d5cdd0e。出所: agent前提（実測）
- **Validatorのエラー文言を `has empty EarnItems` とする。** 既存の `has invalid ItemGuid` / `has duplicate ToolItemGuid` と同じ `Name:{名前} has 〜` の型に揃えた。出所: agent前提（同ファイルの前例）
- **`EarnItemsValidation` の呼び出し順を `ItemGuidValidation` の直後に置く。** ItemGuidの妥当性 → 中身の有無 → ツールの妥当性という粒度順。出所: agent前提
- **テストmod（forUnitTest / EditModeInPlayingTestMod）の map.json は変更不要。** どちらも earnItems 空を持たないことを実測済み。出所: agent前提（実測）
