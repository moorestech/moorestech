# MapMaking Visual Parity Implementation Plan v2（PR1145着地後リベース版）

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**本planは `2026-08-16-mapmaking-visual-parity.md`（origin/master側）を置き換える。** 旧planは 8/16 時点のmaster（PR #1145 未マージ）前提で書かれたが、その後の状況変化で前提が3点崩れた:

1. **旧Phase A（Task 1〜4: Transform貫通）はPR #1145 が実装済み。** `PlacedMapObject` のRotation(四元数)/Scale、`MapInfoJson` の rotationX..W/scaleX..Z（`Required.Always`）、`MapObjectLayoutMessagePack` Key(5)〜(14)、クライアントInstantiate適用、MapAuthoring exporter/importer往復、テンプレmap.json必須キー移行、転記テスト（MapInfoJsonBuilderTest/GetMapDataProtocolTest）まで全て完了。ワイヤ形式は旧planのオイラー6フィールドでなく**四元数4成分＋スケール3軸が正**（ADR-0010の「Transform相当の3要素」を満たす実装済み形式に合わせる）
2. **旧Phase C（Task 9: クライアント側距離場）はADR-0012で棄却対象。** 草密度はサーバーが焼いてチャンク配信する方針（[ADR-0012](../../adr/0012-server-baked-terrain-visuals.md)）のため、距離フィルタの有効化は焼く場所＝サーバー側（bd `moorestech-pt8`）で行う。クライアント距離場は「後で捨てる過渡実装」でありADR-0012がクラスタ再導出案を棄却したのと同じ理由で作らない
3. **旧Task 10（generateDetail/generateTexture削除）は前提が偽になった。** 「参照0件の死にフラグ」はmaster時点の話で、PR #1145 では `TerrainTileVisualProvider` が両フラグを生きたゲートとして使う（裁定 2026-08-14/15 で generateDetail=true 化済み）。削除はpt8でビジュアル生成がサーバーへ移る時に自然死させる

AB比較実験（`docs/research/2026-08-16-impl-model-comparison.md`）は一旦見送り（裁定: `.decisions/2026-08-17-AB比較実験は一旦見送り計画のみ作り直す.md`）。本planは通常のSDDで実施する。

**Goal:** generatedワールドの木・岩の見た目（樹種・スケール・回転）を MapMaking プロジェクト（`TmpUnityPjt/MapMaking`）のバイオームプリセットと同一にする。草分布の同一化（距離フィルタ）はpt8（サーバー焼き移行）へ委譲。

**Tech Stack:** Unity 6000.3.8f1 / URP 17.3.0 / uloop CLI / mooresmaster SourceGenerator / Python 3（データ生成スクリプト）/ NUnit

## 全体フェーズと依存

```
Phase 0（前提・本planの管轄外）: PR #1145 のwip整理→レビュー完了→マージ
Phase 1（本plan Task 1）      : Phase A残差の消化（HPバー逆スケール補正）
Phase 2（本plan Task 2〜5）    : 樹種・岩登録（旧Task 5〜8のリベース）
Phase 3（本plan Task 6〜7）    : 視覚検収（樹種・岩のみ）+ moores-code-review
Phase 4（別plan・bd moorestech-pt8）: 地形見た目のサーバー焼き移行。着手時 grill-first（HARD GATE）。
    旧Task 9（草距離場）・旧Task 10（generateDetail/generateTexture削除）・クラスタ3キー削除・
    terrainSurroundEffectType削除・草分布の視覚検収はここへ吸収。前提調査だったbd moorestech-7pkは解明済み（下記「planning後の判明事項」）。
    実データ是正はbd moorestech-iiu（クラスタ配置の復元）が担い、surround移設の実機検証はこれが前提になる
```

## Requirements

- R1: MapMakingの有効バイオーム（Forest/Grassland/Savanna/Mesa）の全有効樹種・岩をmapObjectとして登録する。disabledエントリ（Desert Olivebush・Mesa 3種・Savanna Bush）は登録対象から除外しない（樹種登録は全樹種）が、treePlacementへは載せない
- R2: Jungle/Woodsは旧スキーマプリセットの樹種リスト（Kapokier/Banana/Tropica/Musa、PineTree/BirchTree）を移植する。バイオーム自体は無効のまま
- R3: 新規mapObjectのhp・ドロップ・採掘設定は既存値の複製（木=既存「木」と同値、岩=石ドロップのMining、小型PebbleのみPickUp）
- R4: generation.jsonの各バイオームtreePlacementがMapMakingプリセットと同一のパラメータ・樹種構成になる（受け入れ: forest prototype 0の再生成結果が既存portと一致 ※mapObjects guid以外）
- R5: HPバーが個体スケールの影響を受けず等倍表示される（受け入れ: スケール適用済みmapObjectでHPバーが歪まない）
- R6: 最終検収として generatedワールドのスクショと MapMaking のスクショを外部監査で突き合わせる（樹種構成・密度・スケール分布・向きの多様性。**草の分布はpt8後の検収に送る**）
- やらないこと: 草の距離フィルタ有効化（→pt8）、generateDetail/generateTexture削除（→pt8）、クラスタ3キーの削除（→pt8）、描画環境（RPアセット/Volume/風）の同期、既存「木」(Birch)/「ブッシュ」の見た目変更、templateマップの見た目変更、Bush.prefabのRayTarget欠落修正（別タスク）、objectConfig（クラスタ配置）の有効化

## Global Constraints

- 作業ブランチ: `feat/mapmaking-species-parity`（**PR #1145 マージ後の origin/master 起点**）。SDDはworktree隔離必須（`.decisions/2026-08-13-SDDはworktree隔離を必須ゲートにする.md`）
- .csファイル変更後は必ず `uloop compile --project-path ./moorestech_client` を実行（サーバーコードもクライアントプロジェクトからコンパイルされる）
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`
- partial禁止・`Func<>`禁止・try-catch原則禁止・1ファイル200行以下・1ディレクトリ10コードファイルまで・デフォルト引数禁止
- コメントは日本語・英語の2行セット（各1行）
- optionalフォールバック禁止: 新フィールドは必須として全JSON一括更新（ADR-0010）
- .metaファイル手動作成禁止。プレハブの生成・編集はEditorスクリプト/`uloop execute-dynamic-code`経由のみ（テキスト直編集禁止）
- masterデータ変更は `../moorestech_master` の**現行ピン**（`.moorestech-external-revisions.json` 参照。PR #1145 マージ後の値）にブランチを切ってコミットし、ピンを更新する
- MapMakingプロジェクト（`TmpUnityPjt/MapMaking`）とBKアセット（`moorestech_client/Assets/PersonalAssets/moorestech-client-private/BK/`）は読み取り専用。一切変更しない
- 新規Pythonスクリプトは `scripts/mapmaking-parity/` に置く（再実行可能・冪等に作る）

---

## File Structure

```
[Task 1: Phase A残差]
Modify: moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs   … HPバーの逆スケール補正

[Task 2〜5: 樹種・岩登録]
Create: scripts/mapmaking-parity/extract_mapmaking_species.py   … MapMakingプリセット→species-inventory.json
Create: scripts/mapmaking-parity/species-inventory.json         … 抽出結果（コミットする・後続スクリプトの入力）
Create: scripts/mapmaking-parity/gen_map_master.py              … master map.jsonへmapObjects追記
Create: scripts/mapmaking-parity/gen_generation_treeplacement.py … generation.jsonのtreePlacement再生成
Create: moorestech_client/Assets/Scripts/Editor/MapObjectWrapperGenerator/MapObjectWrapperGeneratorMenu.cs
Create: moorestech_client/Assets/Scripts/Editor/MapObjectWrapperGenerator/WrapperPrefabFactory.cs
Create: moorestech_client/Assets/Scripts/Editor/MapObjectWrapperGenerator/WrapperAddressableRegistrar.cs
Create: moorestech_client/Assets/AddressableResources/Environment/Tree/<Pack>/<Name>.prefab（生成物 約67個）
Create: moorestech_client/Assets/AddressableResources/Environment/Rock/<Pack>/<Name>.prefab（生成物 約27個）
Create: moorestech_client/Assets/Scripts/Client.Tests/EditModeTest/MapObjectAddressableLoadTest.cs  … 全mapObjectアドレスのロード検証
Modify: ../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json
Modify: ../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/generation.json
Modify: .moorestech-external-revisions.json                     … masterピン更新
```

注意: 新規mapObjectのmap.jsonエントリはPR #1145 で入った**必須キー**（`rotationX..W`/`scaleX..Z`/`clusterId`/`clusterCenterX/Z` 等、`MapInfoJson.cs` の `Required.Always` 群）を持つ必要は**ない**（それは配置インスタンス側 map.json の話。マスタ側 map.json の mapObjects 定義とはスキーマが別）。スクリプト実装時に `VanillaSchema/map.yml` の現行スキーマを正として確認すること。

---

### Task 1: HPバーの逆スケール補正（Phase A残差）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs`

**背景:** PR #1145 でmapObjectに個体スケールが適用されるようになったが、HPバー（`hpBarView`）は親のスケールをそのまま受けて歪む。MapMakingにHPバーは無く、スケール適用でUIが歪むのは同一化の意図外。

- [ ] **Step 1: 逆スケール補正を実装** — `Initialize` 内（rayTargets初期化の後）に追加:

```csharp
            // 個体スケールがUI表示に波及しないようHPバーは逆スケールで等倍を保つ
            // Counter-scale the HP bar so per-instance scaling never distorts the UI
            if (hpBarView)
            {
                var lossy = hpBarView.transform.parent.lossyScale;
                hpBarView.transform.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);
            }
```

- [ ] **Step 2: コンパイル** — Run: `uloop compile --project-path ./moorestech_client` → Expected: エラー0
- [ ] **Step 3: 実機確認** — generatedワールドをエディタ専用ボタン（`.decisions/2026-08-12-generatedワールドプレイはエディタ専用ボタンで提供する.md`）で起動し、スケールの掛かった木・岩へ照準してHPバーが等倍表示されることをスクショ確認。PlayMode停止
- [ ] **Step 4: コミット** — `git commit -m "fix(client): mapObjectのHPバーを逆スケール補正し個体スケールの歪みを防ぐ"`

---

### Task 2: MapMakingプリセットから樹種インベントリを抽出する

**Files:**
- Create: `scripts/mapmaking-parity/extract_mapmaking_species.py`
- Create: `scripts/mapmaking-parity/species-inventory.json`（実行結果・コミットする）

**Interfaces:**
- Consumes: `TmpUnityPjt/MapMaking/Assets/MapGenerator/Presets/Biomes/*.asset`（読み取り専用）、`moorestech_client/Assets/PersonalAssets/moorestech-client-private/**/*.meta`（guid逆引き）
- Produces: `species-inventory.json`:

```json
{
  "species": [
    {
      "key": "Redwood/Sequoia1",
      "prefabGuid": "<unity guid>",
      "prefabPath": "Assets/PersonalAssets/moorestech-client-private/BK/PureNature_Redwood/Prefabs/Trees/Sequoia1.prefab",
      "kind": "tree",
      "address": "Vanilla/Environment/Tree/Redwood/Sequoia1",
      "wrapperPath": "Assets/AddressableResources/Environment/Tree/Redwood/Sequoia1.prefab",
      "mapObjectGuid": "<uuid5決定論guid>",
      "mapObjectName": "Sequoia1"
    }
  ],
  "biomes": {
    "forest": { "prototypes": [ { "mapObjects": [{"mapObjectGuid": "..."}], "scaleHeightRange": [0.4, 0.8], "…schema準拠の全キー…": "…" } ] },
    "jungle": { "speciesFill": ["<guid>", "…"] },
    "woods": { "speciesFill": ["<guid>", "…"] }
  }
}
```

- [ ] **Step 1: スクリプトを書く** — 要点仕様（コード全文はこの仕様に従い実装する）:
  - Unity YAMLパース: `.asset` を `--- !u!` でドキュメント分割し、`yaml.SafeLoader` に `yaml.add_multi_constructor('tag:unity3d.com,2011:', ...)` と `!u!` 無視を登録して読む（pyyaml使用。`pip install pyyaml` 前提）
  - 対象: `Forest.asset` `Grassland.asset` `Savanna.asset` `Mesa.asset`（現行スキーマ: `treePlacement.prototypes[].prefabs[]` guid配列）+ `Jungle.asset` `Woods.asset`（旧スキーマ: `- prefab:` 単数。樹種リストのみ抽出）
  - `disabled: 1` のプロトタイプはtreePlacement出力から除外する（裁定: 有効4+旧プリセット意図採用）。ただし species 一覧には含める（R1: 登録は全樹種）
  - guid→パス: `moorestech_client/Assets/PersonalAssets/moorestech-client-private` 以下の全 `.prefab.meta` を先に走査して `guid→prefabパス` 辞書を作る。**未解決guidは即例外**（fail-fast）
  - kind判定: プレハブパスに `/Rocks/` を含む→`rock`、プレハブ名が `Pebble` で始まる→`pebble`、それ以外→`tree`
  - address/wrapperPath: `Vanilla/Environment/{Tree|Rock}/<パック短縮名>/<プレハブ名>`（パック短縮名は `PureNature_Redwood`→`Redwood`、`PureNature`→`Base`）。pebbleはRock側に置く
  - mapObjectGuid: `uuid.uuid5(<スクリプト内定数の名前空間UUID>, f"moorestech.mapobject.{key}")` の決定論採番（再実行で不変）
  - prototypesの変換: Unity YAMLのフィールド名→スキーマキーは同名（`VanillaSchema/mapGenerate/treePlacementConfig.yml` を正としてキー一覧をスクリプトに定数化）。`Vector2 {x,y}`→`[x,y]`。`prefabs` guid配列→順序保存で `mapObjects` guid配列。ネスト（`densityConfig`/`understoryConfig`/`rockProximityConfig`/`slopeFilter`/`curvatureFilter`/`clusterNoise`/`clusterNoise2`）も全キー転写。**スキーマに無いキー・見つからないキーは即例外**（黙って落とさない）
  - **注意（v2追記）**: PR #1145 で `VanillaSchema/mapGenerate/placementNoise.yml` 等が変更されている（配置ノイズのワールド座標基準化）。キー定数はマージ後スキーマの現物から起こすこと
- [ ] **Step 2: 実行** — Run: `python3 scripts/mapmaking-parity/extract_mapmaking_species.py` → Expected: `species-inventory.json` 生成。件数目安: species 約94（tree約67/rock約24/pebble約3）、forest prototypes 7・grassland 18・savanna 2・mesa 2、jungle speciesFill 7・woods 4
- [ ] **Step 3: 既存portとの突合で検証** — forest prototype 0 の抽出結果と、現行 `../moorestech_master/.../master/generation.json` の `algorithmParam.forest.treePlacement.prototypes[0]` を `mapObjects` 以外のキーで比較するワンライナーを実行し、差分をレビューする（既存portは同じプリセット由来なので原則一致。差分があればMapMaking側のその後の調整であり、プリセット現在値を正とする）。注: 現行generation.jsonのforestは既に7プロトタイプ（0-4=木系、5-6=小石系）あり、突合は同種プロトタイプ同士で行う
- [ ] **Step 4: コミット** — `git add scripts/mapmaking-parity/ && git commit -m "feat: MapMakingプリセットの樹種インベントリ抽出"`

---

### Task 3: master map.json へ樹種・岩mapObjectを一括追記する

**Files:**
- Create: `scripts/mapmaking-parity/gen_map_master.py`
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json`

**Interfaces:**
- Consumes: `species-inventory.json`（Task 2）
- Produces: master `map.json` の `mapObjects` に約94件追加（既存エントリは不変）

- [ ] **Step 1: 生成スクリプトを書く** — 冪等（同guidが既にあれば置換）。テンプレートは既存値の複製（裁定: `.decisions/2026-08-16-新規mapObjectの採掘設定は既存値の複製で統一.md`）:

```python
#!/usr/bin/env python3
"""species-inventory.jsonからmaster map.jsonへmapObjectsを一括追記する（冪等）。"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
INVENTORY = ROOT / "scripts/mapmaking-parity/species-inventory.json"
MASTER = ROOT.parent / "moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json"

WOOD_ITEM = "aafce615-6c30-48c4-a29e-3c5b3266748f"   # 原木（既存「木」のドロップ）
STONE_ITEM = "582040ec-093b-4c8e-8fe3-f4ec030cf1ca"  # 石（既存「小石」のドロップ）
TREE_TOOLS = [
    {"toolItemGuid": "4c5fefbd-60a4-42ea-b70a-38a83b96e25e", "damage": 25, "attackSpeed": 1},
    {"toolItemGuid": "76174235-48fb-4944-bca7-ad268385d68c", "damage": 10, "attackSpeed": 2},
]

def build_entry(species: dict) -> dict:
    kind = species["kind"]
    if kind == "pebble":
        return {
            "mapObjectGuid": species["mapObjectGuid"], "mapObjectName": species["mapObjectName"],
            "addressablePath": species["address"], "hp": 1, "earnItemHps": [0],
            "soundEffectType": "stone",
            "terrainSurroundEffectType": "rockBareGround",
            "earnItems": [{"itemGuid": STONE_ITEM, "minCount": 1, "maxCount": 1}],
            "miningType": "PickUp", "earnItemHpInterval": 1, "miningParam": {"miningTools": []},
        }
    is_tree = kind == "tree"
    return {
        "mapObjectGuid": species["mapObjectGuid"], "mapObjectName": species["mapObjectName"],
        "addressablePath": species["address"], "hp": 100, "earnItemHps": [0],
        "soundEffectType": "tree" if is_tree else "stone",
        "terrainSurroundEffectType": "treeRootPatch" if is_tree else "rockBareGround",
        "earnItems": [{"itemGuid": WOOD_ITEM if is_tree else STONE_ITEM, "minCount": 1, "maxCount": 4}],
        "miningType": "Mining", "miningParam": {"miningTools": TREE_TOOLS},
        "earnItemHpInterval": 10,
    }

def main() -> None:
    inventory = json.loads(INVENTORY.read_text(encoding="utf-8"))
    master = json.loads(MASTER.read_text(encoding="utf-8"))
    by_guid = {o["mapObjectGuid"]: i for i, o in enumerate(master["mapObjects"])}
    added = replaced = 0
    for species in inventory["species"]:
        entry = build_entry(species)
        if entry["mapObjectGuid"] in by_guid:
            master["mapObjects"][by_guid[entry["mapObjectGuid"]]] = entry
            replaced += 1
        else:
            master["mapObjects"].append(entry)
            added += 1
    MASTER.write_text(json.dumps(master, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"added={added} replaced={replaced} total={len(master['mapObjects'])}")

if __name__ == "__main__":
    main()
```

  - **注意（v2.1追記）**: `terrainSurroundEffectType` はPR #1145 新設の必須キー（`MapObjectKindSplitter` の分類正本）。kindから機械決定する（tree→`treeRootPatch`、rock/pebble→`rockBareGround`）。このキー自体はpt8で削除予定の過渡キー（裁定: `.decisions/2026-08-18-terrainSurroundEffectTypeの削除はpt8送りにする.md`）だが、pt8までは必須のため省略しない
  - **注意（v2追記）**: vein手掘り（PR #1127）でmapObjectスキーマに `durabilityType` / `outcropMapObjectGuid` / `miningType: None` 系のキーが入っている可能性がある。`VanillaSchema/map.yml` の現行必須キーを確認し、欠けがあれば既存「木」「小石」エントリの現物値を複製して埋める（fail-fast: スキーマ必須キーを黙って省略しない）
- [ ] **Step 2: 実行** — Run: `python3 scripts/mapmaking-parity/gen_map_master.py` → Expected: `added=約94 replaced=0`
- [ ] **Step 3: ロード検証** — `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObjectMaster"` でマスタロード系テストがPASS（該当テストが無ければ任意のCombinedTest 1本でマスタロードが通ることを確認）
- [ ] **Step 4: コミット** — moorestech側: スクリプトを `git commit -m "feat: 樹種・岩mapObjectのmaster生成スクリプト"`。master側: `git -C ../moorestech_master add server_v8 && git -C ../moorestech_master commit -m "feat: BK樹種・岩mapObjectを一括追加"`

---

### Task 4: ラッパープレハブの一括生成とAddressable登録

**Files:**
- Create: `moorestech_client/Assets/Scripts/Editor/MapObjectWrapperGenerator/MapObjectWrapperGeneratorMenu.cs`
- Create: `moorestech_client/Assets/Scripts/Editor/MapObjectWrapperGenerator/WrapperPrefabFactory.cs`
- Create: `moorestech_client/Assets/Scripts/Editor/MapObjectWrapperGenerator/WrapperAddressableRegistrar.cs`
- Create: `moorestech_client/Assets/AddressableResources/Environment/Tree/**` `Rock/**`（生成物 約94プレハブ）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EditModeTest/MapObjectAddressableLoadTest.cs`

**Interfaces:**
- Consumes: `species-inventory.json`（Task 2）、`MapObjectGameObject`（SerializeField: `outlineObject: GameObject` / `hpBarView: MapObjectHpBarView`）、`MapObjectRayTarget`
- Produces: 各speciesの `wrapperPath` にプレハブ（アドレス=`address`、グループ=`Vanilla Asset Group`）

ラッパー構造はBush.prefabの先行パターン（BKプレハブをネストしルートへコンポーネント追加）+ Tree.prefabのRayTarget構造:
1. ルート = `PrefabUtility.InstantiatePrefab(BKプレハブ)` の結果（名前=species名）。`MapObjectGameObject` をAddComponent
2. `Assets/Asset/Environment/Prefab/MapObjectHpBar.prefab` を子としてネスト（`PrefabUtility.InstantiatePrefab(hpBarPrefab, root.transform)`）。位置はレンダラーboundsの頂部 `(0, bounds.max.y + 0.5f, 0)`
3. `Outline` 子GameObject: BKプレハブのLOD0配下の各MeshRenderer/MeshFilterを複製した子を持ち、マテリアルを全スロット `Assets/Asset/Common/Shader/Outline/Outline.mat` に差し替え。初期状態 `SetActive(false)`
4. `RayTargetCollider` 子GameObject: `BoxCollider(isTrigger=true)` を全レンダラー合成boundsで設定 + `MapObjectRayTarget` をAddComponent
5. `SerializedObject` で `outlineObject`→Outline、`hpBarView`→HPバーの `MapObjectHpBarView` を配線し `PrefabUtility.SaveAsPrefabAsset(root, wrapperPath)`
6. Addressable登録: `AddressableAssetSettingsDefaultObject.Settings` から `Vanilla Asset Group` を取得し `CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(wrapperPath), group)`、`entry.address = species.address`

- [ ] **Step 1: 失敗するテストを書く** — `MapObjectAddressableLoadTest.cs`（EditModeテスト。配置先ディレクトリの既存asmdefに従う）。全mapObjectのアドレスが実プレハブへ解決でき、`MapObjectGameObject` を持つこと、新規生成分（`Vanilla/Environment/Tree/` `Rock/` 配下）は `MapObjectRayTarget` も持つことを検証（既存BushのRayTarget欠落は既知バグで別タスクのため対象外）。MasterHolderロードはTests内の既存前例に倣う。EditModeでのAddressables実ロードが動かない場合は `AddressableAssetSettings` のentry検査（アドレス→アセットguid→`AssetDatabase.LoadAssetAtPath`）へ切り替える
- [ ] **Step 2: テスト実行して失敗確認** — Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObjectAddressableLoadTest"` → Expected: FAIL（新アドレス約94件がload失敗）
- [ ] **Step 3: Editorスクリプト3ファイルを実装** — 上記構造どおり。`MapObjectWrapperGeneratorMenu.cs` は `[MenuItem("Tools/MapObjectWrapper/Generate All")]` で `species-inventory.json`（パスは `Application.dataPath + "/../../scripts/mapmaking-parity/species-inventory.json"`）を読み全件生成。各ファイル200行以下・エディタ専用のため `Editor/` 配下（asmdefはMapAuthoringと同じEditorアセンブリに同居）
- [ ] **Step 4: コンパイル** — Run: `uloop compile --project-path ./moorestech_client` → Expected: エラー0
- [ ] **Step 5: 生成実行** — Run: `uloop execute-menu-item` で `Tools/MapObjectWrapper/Generate All`（またはexecute-dynamic-codeで同メソッド呼び出し）→ Expected: コンソールに生成件数ログ・エラー0（`uloop get-logs --log-type Error` で確認）
- [ ] **Step 6: テスト実行** — Run: Step 2と同じ → Expected: PASS
- [ ] **Step 7: コミット** — 生成プレハブ・`.meta`・AddressableAssetsData差分・Editorスクリプト・テストをまとめて `git commit -m "feat(client): BK樹種・岩のラッパープレハブ一括生成とAddressable登録"`

---

### Task 5: generation.json の treePlacement をプリセット同期する

**Files:**
- Create: `scripts/mapmaking-parity/gen_generation_treeplacement.py`
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/generation.json`
- Modify: `.moorestech-external-revisions.json`

**Interfaces:**
- Consumes: `species-inventory.json` の `biomes`（Task 2）
- Produces: forest/grassland/savanna/mesa の `treePlacement.prototypes` 全置換、jungle/woods の各prototypeの `mapObjects` を speciesFill で置換

- [ ] **Step 1: スクリプトを書く** — 仕様:
  - forest/grassland/savanna/mesa: `algorithmParam.<biome>.treePlacement.prototypes` を inventory の prototypes で**全置換**（disabled除外済み・スキーマ全キー持ち）
  - jungle/woods: 既存prototypesの配置パラメータは維持し、各prototypeの `mapObjects` 配列だけを `speciesFill` の全guid（等確率）で置換（裁定: 樹種リストのみ意図採用・バイオームは無効のまま）
  - 書き出し後、スキーマ非準拠キーが無いことを `treePlacementConfig.yml` のキー集合と突合して検証（不一致は例外）
- [ ] **Step 2: 実行** — Run: `python3 scripts/mapmaking-parity/gen_generation_treeplacement.py` → Expected: 置換サマリ表示（forest 7 / grassland 18 / savanna 2 / mesa 2 prototypes）
- [ ] **Step 3: サーバー生成テストで検証** — Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapGeneration|TreeRuntimeConfig"` → Expected: PASS（ロード不能な参照guid・キー欠落があればここで落ちる）
- [ ] **Step 4: masterコミットとピン更新** — `git -C ../moorestech_master commit -am "feat: treePlacementをMapMakingプリセット同期"`。masterのHEADハッシュを `.moorestech-external-revisions.json` に反映し `git commit -m "chore: masterピン更新(樹種同期)"`
- [ ] **Step 5: generatedワールド目視確認** — uloopでgeneratedワールドを起動（エディタ専用ボタン経由）し、スクショで樹種・サイズ・向きの多様性を確認、Errorログ0。停止してコミット（スクショは `../moorestech_logs` 側へ）

---

### Task 6: unityプレイ録画テストと外部監査による視覚検収（樹種・岩）

**Files:**
- 生成物: 録画・スクショ（`../moorestech_logs` 側へ保存。コードrepoにはコミットしない）

- [ ] **Step 1: generatedワールドをunityプレイ録画テストで起動** — `unity-playmode-recorded-playtest` スキルのプレイテストDSLでgeneratedワールドを起動・周囲を見回すシナリオを実行し録画を取得（masterデータは更新済みピンのworktreeを使用。スキーマ不整合の無言死に注意）
- [ ] **Step 2: MapMaking側の参照スクショを取得** — MapMaking側は変更せず、`TmpUnityPjt/MapMaking` の既存出力スクショ（`Docs/`や過去成果物）を使う。無ければMapMakingをuloopで開きForest/Grassland付近のSceneスクショを撮る（読み取り専用操作のみ）
- [ ] **Step 3: 外部監査で突き合わせ** — codex-auditスキルで両スクショを渡し「樹種構成・密度・スケール分布・向きの多様性がMapMakingと同等か」を評価基準にして監査。**草の分布はpt8前なので評価対象から明示的に外す**。指摘があれば該当タスクへ戻って修正（データ側の差異はスクリプト再実行で反映）
- [ ] **Step 4: 全テストスイート回帰** — `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObject|MapData|MapGeneration|Terrain|Detail"` → Expected: PASS

---

### Task 7: moores-code-review（必須・省略不可）

- [ ] **Step 1:** 必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。指摘対応後、残課題はbdへ積む

---

## 旧planからの委譲先（pt8スコープ・本planでは実装しない）

| 旧タスク | 委譲先 | 根拠 |
|---|---|---|
| Task 9: クライアント側草距離場（DetailDistanceFieldBuilder等） | bd `moorestech-pt8`（サーバー焼きのdetail密度計算内で距離フィルタを有効化） | ADR-0012。クライアント距離場は「後で捨てる過渡実装」 |
| Task 10: generateDetail/generateTexture削除 | bd `moorestech-pt8`（ビジュアル生成のサーバー移設でゲートごと自然死） | PR #1145 で生きたゲートになった。死にフラグ前提が偽 |
| クラスタ3キー（ClusterId/ClusterCenterX/Z）の転送・永続化削除 | bd `moorestech-pt8`（surround描画のサーバー移設と同時） | ADR-0012 過渡措置裁定 |
| map.yml `terrainSurroundEffectType` の削除（スキーマ・全map.json・`MapObjectKindSplitter`） | bd `moorestech-pt8`（サーバー焼き移設でクライアント側分類＝Splitterごと自然死。サーバーは生成時に配置元prototype/objectConfigを知るためマスタ分類が不要になる） | 転送レイアウトがGUIDのみのため、pt8まではマスタが分類正本として必須。裁定: `.decisions/2026-08-18-terrainSurroundEffectTypeの削除はpt8送りにする.md` |
| 草分布の視覚検収 | pt8完了後の検収 | 距離フィルタ実装がpt8側のため |

pt8は着手時に **grill-first（HARD GATE）**。

## planning後の判明事項（2026-08-17 追記）

- **ADR-0012の未解決事項「勾配依存テクスチャの高さ基準」は解消済み。** MapMaking原本 `TerrainGenerator.cs` の実測で「①splatは摂動前高さで焼く → ②木の高さ摂動 → ③木の根元テクスチャをsplatへ追い焼き → ④detail用slopesは摂動後高さ」の4段ルールと確定。PR #1145 のR12（`2026-08-14-map-autogen-5x5-and-visual-restore.md`）が既に文書化・実装済み（`TreePerturbationApplier` / `TreeSurroundTexturePainter`）。pt8のサーバー焼きも同ルールを踏襲するだけでよい（摂動は決定論のためサーバーで再現可能）
- **bd `moorestech-7pk`（ClusterId>=0が0件）は原因特定済みでクローズ。** コード経路（スキーマ→ランタイム変換・useClusterMode分岐・ClusterId採番・転記）は健全で、原因はv8実データの2欠陥: ①`algorithmParam.generateObject: false` でオブジェクト配置ステージ全体がスキップ（`TilePlacementRunner.cs:104`、2026-07-24導入） ②grassland/forestの `objectConfig` 16エントリが `prefabs: []`（移植時のGUID変換落ちの疑い）。是正は bd `moorestech-iiu`（generateObject有効化・prefabs復元・実データバリデーションテスト）へ。クラスタ描画・surround経路の実機検証はこの是正が前提
- **要確認:** masterデータの `generateDetail` も false であることを調査中に確認。裁定 2026-08-14/15「generateDetailをtrueにする」との食い違いの可能性があるため、moorestech-iiu 着手時にピンと突き合わせて確認する

## 判断記録（ADR）

設計セッションの裁定（正本・旧planから継承）:
- [ADR-0010 mapObjectの配置データはTransform相当のフル3要素を運ぶ](../../adr/0010-mapobject-placement-carries-full-transform.md)
- [ADR-0011 MapMakingとの見た目同一化は樹種ごとの個別mapObject登録で行う](../../adr/0011-mapmaking-parity-via-per-species-mapobjects.md)
- [ADR-0012 地形の見た目データはサーバーが焼いてチャンク配信する](../../adr/0012-server-baked-terrain-visuals.md)（v2でのPhase C解体の根拠）
- `.decisions/2026-08-16-treePlacement同期は有効4バイオーム+旧プリセット意図採用.md`
- `.decisions/2026-08-16-MapMaking同一化に岩も含める.md`
- `.decisions/2026-08-16-新規mapObjectの採掘設定は既存値の複製で統一.md`
- `.decisions/2026-08-16-mapObject配置データはTransform相当の3要素を通す.md`
- `.decisions/2026-08-16-BushのRayTarget欠落修正は別タスクに切る.md`（本planのスコープ外根拠）
- `.decisions/2026-08-17-地形の見た目データはサーバーが焼いてチャンク配信する.md`
- `.decisions/2026-08-17-PR1145のクラスタ3キーは後で消える前提で現状維持する.md`
- `.decisions/2026-08-17-AB比較実験は一旦見送り計画のみ作り直す.md`
- `.decisions/2026-08-18-terrainSurroundEffectTypeの削除はpt8送りにする.md`

v2 planning中の判断（出所: agent前提。異議があれば実装前に裁定へ）:
- ワイヤ・JSON形式は旧planのオイラー6フィールド案でなく、PR #1145 実装済みの四元数4成分＋スケール3軸を正とする（ADR-0010の要件は満たし、実装のやり直しは無価値）
- 旧planのTask 2（template map.json形式移行）・`Required.Always` fail-fast化はPR #1145 で実施済みのため本planから削除
- HPバー逆スケール補正のみPhase A残差として本planで拾う（PR #1145 には積まない: レビュー済みコードを増やさない。マージ後の本ブランチで対応）
- 樹種・岩登録（Phase 2）はADR-0012と無矛盾のため先行実装する: mapObject+Transformは目標アーキテクチャでも同じ語彙であり、pt8を待つ理由がない
- 旧planの「機能パリティ死活表」のうちTransform貫通に関する行はPR #1145 で担保済み。本planで新たに死活が問われるのは「新樹種・岩の照準・採掘」（Task 4のRayTarget+テストで担保）のみ

## 配置と前例

| 決定 | 前例 |
|---|---|
| ラッパープレハブ構造（BKネスト+MapObjectGameObject+Outline+HPバー） | `moorestech_client/Assets/AddressableResources/Environment/Bush.prefab` |
| RayTarget構造（トリガーコライダー+MapObjectRayTarget） | `moorestech_client/Assets/AddressableResources/Environment/Tree.prefab` |
| masterデータのスクリプト生成 | `scripts/` 配下の既存運用（データ一括変換） |
| Editor一括生成メニュー | `moorestech_client/Assets/Scripts/Editor/MapAuthoring/`（MenuItem+SerializedObject配線の前例） |
