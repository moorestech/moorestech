# Task 15 統合検証レポート（generatedワールド5x5の実機確認）

- 実施日: 2026-08-16
- worktree: `/Users/katsumi/moorestech-worktrees/map-autogen-5x5`（BASE `e891bb935`）
- master pin: `3651ecd5ab1df28c772d701641e74014984a5f25`（`../moorestech_master` symlink → `/Users/katsumi/moorestech-master-worktrees/mapobject-scale-cluster-keys`、preflight [3/5] PASS）
- ステータス: **DONE_WITH_CONCERNS**

---

## サマリ

| Step | 結果 |
|---|---|
| Step 1 フル生成スモーク | **PASS** `WorldProvisionerTest` 7/7・MapGeneration 名前空間 87/87。5x5生成 **1977ms** |
| Step 2 クライアント通し | **PASS** `TerrainVisualCacheReuse` / `TerrainCacheFetch` / `PlayerStartsOnBuiltTerrain` 3/3（初回は worktree の web 未セットアップで1件落ちた。環境修復後グリーン） |
| Step 3 unityプレイ録画テスト | **部分実施** 25タイル構築・シーム数値・スポーンは取得。**見た目(splat/detail/テクスチャ)の目視は worktree に PersonalAssets が無いため実施不能** |
| Step 4 記録とコミット | 完了（テスト設定を戻して検証済み） |

**最重要の発見**: bd `moorestech-edd.5`（R12 木の高さ摂動の halo 無し Slice）が**実機で再現・数値化された。最悪 14.1m の段差**がタイル境界に立つ。

---

## Step 1: フル生成のスモーク

```
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WorldProvisionerTest"
→ {"Success": true, "TestCount": 7, "PassedCount": 7, "FailedCount": 0}   02:03:21→02:03:26

uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests\.UnitTest\.Game\.MapGeneration.*"
→ {"Success": true, "TestCount": 87, "PassedCount": 87, "FailedCount": 0}  02:04:08→02:04:41 (33s)
```

**所要時間の実測（サーバー側生成）**

```
[WorldProvisionerTest] generated mode EnsureWorld elapsed=1977ms
```

`forUnitTest` mod の `generation.json` は `gridSizeX/Z = 5`（`resolutionPreset: _512`）なので、これは
**5x5=25タイルのフル生成が 1.98 秒**という実測値。Console に Error は 0 件。

---

## Step 2: クライアント通し（EditModeInPlayingTest）

テスト時間短縮のため `EditModeInPlayingTestMod/master/generation.json` の `gridSizeX/Z` を 5 → **3** に変更して実行。

### 実行時の落とし穴（次にやる人向け）

1. **`uloop run-tests` の `--test-mode` 既定値は PlayMode(1)**。EditModeInPlayingTest は EditMode テストなので
   `--test-mode EditMode` を明示しないと、テスト自体は走るのに Test Runner が
   `InvalidOperationException: This cannot be used during play mode` /
   `TestRunner: Unexpected assembly reload happened while running tests` で自壊し、
   CLI には毎回 `Unity is reloading (Domain Reload in progress)` しか返らない（45秒待っても永久に直らない）。
2. `--test-mode EditMode` を付けても、**テストが PlayMode に入る瞬間に CLI は切断され、その回の呼び出しは
   「Domain Reload in progress」で返る**。テストは裏で完走しているので、**50秒空けてもう一度同じコマンドを叩くと
   前回の結果が返る**。この2回叩きが実質の正しい運用。

### 結果（1回目・環境不備あり）

```
{"Success": false, "TestCount": 3, "PassedCount": 2, "FailedCount": 1,
 "XmlPath": ".../TestResults/20260816_022629.xml"}
```

| テスト | 結果 | 所要 |
|---|---|---|
| `TerrainCacheFetchTest.TerrainCacheRestoreReuseAndRefetchTest` | Passed | 15.55s |
| `TerrainVisualCacheReuseTest.SecondBuildReusesEveryTileFromTheVisualCache` | Passed | 21.27s |
| `PlayerStartsOnBuiltTerrainTest.PlayerStandsAtHandshakePositionAndEveryWaitTargetIsRegistered` | **Failed** | 15.85s |

失敗の中身は**地形と無関係の環境不備**だった:

```
Unhandled log message: '[Error] [WebUiHost] Node binary not found at
  .../moorestech_web/node/mac-arm64/bin/node. Run moorestech_web/setup.sh (or setup.ps1) first.'
```

このテストだけ `LogAssert.ignoreFailingMessages = false` で起動中の LogError を拾う設計なので、
worktree で `moorestech_web/setup.sh` が未実行だと必ず落ちる。地形側の assert は全て通っており、
ログにも正常値が出ていた:

```
[PlayerStartsOnBuiltTerrainTest] handshake:(500.00, 67.58, 500.00) player:(499.89, 67.63, 499.88)
                                ground:True/(499.89, 67.43, 499.88) lowestY:2.51
```

### 環境修復 → 再実行（グリーン）

```
bash moorestech_web/setup.sh          # node/pnpm を worktree へ配置（.gitignore 対象）
moorestech_web/node/.../pnpm install  # webui/node_modules
```

```
{"Success": true, "TestCount": 3, "PassedCount": 3, "FailedCount": 0}
```

### Task 13 申し送りの確認

- **キャッシュ引き当て経路（`TerrainTileVisualProvider` へ移動した `TryLoad`）**: 健全。
  `TerrainVisualCacheReuseTest` が要求する 3 段が実ログで確認できた（3x3=9タイル）:

  ```
  [TerrainRuntimeBuilder] Generated terrain built: tiles=9 visualCacheHits=0 elapsedMs=4679  ← 起動時ビルド
  [TerrainRuntimeBuilder] Generated terrain built: tiles=9 visualCacheHits=0 elapsedMs=3139  ← ①空キャッシュ
  [TerrainRuntimeBuilder] Generated terrain built: tiles=9 visualCacheHits=9 elapsedMs=1281  ← ②全ヒット
  ```

  キャッシュヒットで **3139ms → 1281ms（約2.4倍高速）**。

- **`TerrainDetailPrototypeList.Build` の `CreateAsync` 前倒し**: **テストマスタでは落ちないが、実マスタでは落ちた**（後述・Step 3）。

---

## Step 3: unityプレイ録画テスト

`unity-playmode-recorded-playtest` スキルのプレイテストDSLで実施。
新規シナリオを追加した: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/generated-world-5x5-terrain-survey.cs`

### 3-a. 実マスタそのままでは起動しない（本ブランチの実マスタ構成 × worktree環境）

```
PLAYTEST_WORLD_DIRECTORY=.../worlds/task15-5x5-seed196 PLAYTEST_MAP_MODE=generated PLAYTEST_SEED=196 \
  scripts/run-scenario.sh ./moorestech_client scenarios/misc/generated-world-5x5-terrain-survey.cs
→ preflight PASS(5/5) → boot → NG: game not ready within 300s
```

Editor.log の真因:

```
初期化処理中にエラーが発生しました: System.InvalidOperationException
  Detail prototype mesh 'Vanilla/Environment/Terrain/Detail/Redwood/Grass1' was not resolved before detail generation.
  at DetailPrototypeConfig.ThrowIfUnresolved()
  at TerrainDetailPrototypeList.Build()            ← Task 13 でワールド1回へ移動した箇所
  at TerrainTileVisualProvider..ctor()
  at GeneratedTerrainSource.CreateAsync()          ← ここで前倒し発火
  at TerrainRuntimeBuilder.BuildGeneratedTerrainAsync() : 89
  at MainGameInitializationFinalizer.FinalizeAsync()
```

その手前に Addressables のロードエラーが **24 件**（detail prefab 21種すべて）:

```
Addressables Load Error: Vanilla/Environment/Terrain/Detail/{Redwood,Mountains,Savanna}/*
  InvalidKeyException: No Asset found for Key=... with Type=UnityEngine.GameObject.
  Key exists as Type=UnityEditor.BrokenPrefabAsset, which is not assignable ...
```

**由来の切り分け**:
これは**コードの退行ではなく worktree の環境不備**。
`AddressableResources/Environment/Terrain/Detail/*/*.prefab` はすべて **prefab variant** で、base prefab は
`Assets/PersonalAssets/moorestech-client-private/BK/PureNature_*/Prefabs/Plants/*.prefab`（main worktree のみ・40GB）にある。
base 欠落で `BrokenPrefabAsset` 化するため Addressables が `GameObject` として引けない。
TerrainLayer のテクスチャも同様（例 `Oasis/MudDry` の diffuse guid `11cdd1d0...` = `BK/PureNature_Oasis/Textures/Surfaces/MudDry_a.png`）で全欠落する。

ただし**「Task 13 の master `generateDetail: true` 化によって、このアセット欠落が起動不能へ格上げされた」のは事実**。
`generateDetail: false` の間は `TerrainTileVisualProvider` 側のゲートで `Build` を通らないため、同じ欠落でも起動できていた。
本番環境（PersonalAssets あり）では prefab は正常に引けるので実害は無いと判断するが、**判断はコントローラに委ねる**。

→ bd **`moorestech-edd.7`** を起票。

### 3-b. `generateDetail: false` の一時マスタコピーで数値検証を実施

出荷マスタは触らず、`server_v8` をスクラッチへコピーして `generateDetail` だけ false にし、
`run-scenario.sh` の第3引数で差し替えて起動した（実マスタの `gridSize` は 5 のまま）。

```
成果物: moorestech_client/PlaytestResults/20260816_024004/generated-world-5x5-terrain-survey/
  recording.mp4 / result.json / 01〜12 の png 16枚
```

#### 25タイル構築ログと所要時間（実測・実マスタ解像度 2048）

```
[TerrainDataFetcher] 地形チャンク取得開始 worldId=8532122054c7c1a7 total=1202
[TerrainRuntimeBuilder] Generated terrain built: tiles=25 visualCacheHits=0 elapsedMs=128012
```

- **25タイル・コールドビルド = 128.0 秒**（visualCacheHits=0）
- 転送チャンク 1202 本
- シーン: `activeTerrains=25` / `Terrain_x_z` 命名 25枚 → **PASS**
- タイル寸法 `(1000, 600, 1000)` / heightmapRes `2049` / alphamapRes `2048` / TerrainLayer **19層**

#### スポーン位置

```
handshake=(500.00, 21.31, 500.00)  player=(500.00, 21.30, 500.00)  ground=True/(500.00, 21.31, 500.00)
```

- 自機の真下に地形あり → **PASS**
- 地表との差 0.01m → **PASS**
- スクショ `01-spawn-view.png` で自機が地面に立ち、遠景に樹木が並ぶのを確認

---

## R 別の観察結果

| R | 観察 | 判定 |
|---|---|---|
| **R2 / R11**（splat/Detail のシーム） | 全40タイル対で「Aの端列 vs Bの端列」の19層L1差 (`seamMean`) を「Aの端2列同士」の自然変化 (`baseMean`) と比較。**40対中37対で `seamMean <= baseMean`**。上回った3対も (3,1→4,1: 0.0612/0.0588) (3,3→3,4: 0.2098/0.1768) (2,4→3,4: 0.0696/0.0347) とタイル内変動と同オーダー。**系統的な直線シームは無し** | **OK（数値）／目視は不能** |
| **R6**（岩surround） | spawn/center タイル(2,2) の splat 被覆に **`MudDry` が 1.29%** 出ている（他: Grass01 75.37% / Gravel2 10.17% / Mud01 7.62% / Mud02 5.55%）。「オアシスの乾いた泥」が実際に塗られている | **OK（データ上）／色の目視は不能** |
| **R7**（木の根元surround） | 出ない。**ブリーフ通り正常**（欠陥として扱っていない） | 対象外 |
| **R8**（台地デバッグオーバーレイ） | 実マスタ `alpineEnabled: false`。**検証不能・単体テスト済み扱い** | 対象外 |
| **R9**（generateフラグ / 草） | **未確認**。実マスタ `generateDetail: true` では detail prefab が全部 BrokenPrefabAsset で起動不能、回避のため false で走らせたので `detailPrototypes=0 / detailRes=0`。**草が出るかは本 worktree では確認できていない** | **未検証（bd edd.7）** |
| **R12**（木の高さ摂動） | **境界に段差が出た。最悪 14.10m。**（下記） | **既知欠陥 edd.5 を確認** |

### タイル境界の高さ連続性（全40対・共有辺101点サンプル）

36対が `max=0.000m` で完全連続。段差が出たのは 4 対のみ:

| 対 | max | mean |
|---|---|---|
| **3,3 → 3,4** | **14.100m** | 0.718m |
| 2,4 → 3,4 | 2.125m | 0.059m |
| 3,1 → 4,1 | 1.591m | 0.063m |
| 3,2 → 4,2 / 1,0 → 2,0 | 0.006m | 0.000m |

最悪箇所を 2001 点で細査（PlayMode を残して `uloop execute-dynamic-code` で直接プローブ）:

```
seam 3,3->3,4 worst d=14.23 at x=1835.5 seamZ=2000.0
  x=1801.5〜1875.0 に 0.57→14.23→0.71 と滑らかに立ち上がる幅74mの山
  x=1119.5〜1167.5 に 0.51→0.93→0.51 の幅50m・高さ0.93mの山
```

最悪点 (1835, 2000) の近傍 mapObject:

```
(1829.0,24.0,1998.7) (1824.4,24.0,1994.4) (1842.8,24.0,1989.0) (1826.3,24.0,1986.7)
(1853.0,24.0,1994.9) (1806.4,24.0,1994.7) (1864.5,24.0,1990.0)   ← 全て z<2000 = タイル(3,3)側
```

`GeneratedTerrainSource.cs:135` の `TileMapObjectSlicer.Slice`（halo=0）が、タイル(3,4)側の切り出しから
これらを丸ごと落とす。結果 (3,3) の辺だけがガウシアン摂動で持ち上がり、**境界に高さ14mの縦の崖**が立つ。
**bd `moorestech-edd.5` の予測どおり・第一容疑が的中**。摂動の実効半径は観測から最低 80m 相当あるので、
修正するなら `SliceWithHalo` にその半径を渡す必要がある。

### 海岸線（bd `moorestech-edd.1` の判定材料）

- 海岸がタイル境界を横切る実例を自動探索して発見: **(-612.50, 4.53, -1000.00)**
- その点が乗る対 `(1,0)→(1,1)` の高さ差は **max=0.000m**（完全連続）
- 木の摂動由来の4対を除き、全ての境界で高さは完全連続 → **CoastalSmoothJob 由来の高さズレは本ワールドでは観測されず**
- splat も上記のとおり系統的シーム無し
- スクショ `10-coast-on-seam-oblique.png` / `11-coast-on-seam-overhead.png` に境界の稜線・段差は見えない

**ただし**テクスチャ全欠落状態での判定なので、「帯状の色替わり」の目視は取れていない。
数値上は padding 不足の顕在化なし。**edd.1 を閉じるかはコントローラ判断**（bd に上記を note 済み）。

### その他の観察

- `mapObjects total=74338`（distinct guid = 3種）、**`ClusterId >= 0` が 0 件**。
  つまりこのワールドには岩クラスタが1つも無い（全て独立配置 `-1`）。R6 の岩まわり寄り写真は撮れなかった。
  スケール最大 `ScaleY=4.64`。これが正常なのか（岩クラスタが v8 で使われていないのか）は未判断。

---

## スクリーンショット／録画

すべて `moorestech_client/PlaytestResults/20260816_024004/generated-world-5x5-terrain-survey/`

| ファイル | 内容 |
|---|---|
| `recording.mp4` | 全編録画（オーバーレイ焼き込み） |
| `01-spawn-view.png` | スポーン地点・自機が地面に立つ |
| `02-world-overhead-ortho.png` | 5000m角を真上から（テクスチャ欠落で判読不能） |
| `03-world-oblique.png` | 世界全体の斜俯瞰 |
| `04-seam-x{1..4}-overhead.png` | 内部タテ境界4本を真上から（ortho 320m） |
| `05-seam-z{1..4}-overhead.png` | 内部ヨコ境界4本を真上から |
| `06-seam-crossing-oblique.png` | 中央の境界交点(1000,1000)を斜め低空から。地形は連続・樹木は描画される |
| `07-spawn-ground-detail.png` | 地表すれすれ（detail 無効のため草なし） |
| `10/11-coast-on-seam-*.png` | 海岸がタイル境界を横切る点 |
| `12-back-to-game-view.png` | ゲーム視点へ復帰 |

**注意**: 地形は全面 missing-material のチェッカー柄。splat の色・detail の草・岩や崖のテクスチャは
一切判定できない（bd `moorestech-edd.7`）。地形の起伏・樹木の配置・スポーン・境界の段差は判読できる。

---

## 見つかった問題

| # | 内容 | 由来 | 扱い |
|---|---|---|---|
| 1 | **タイル境界に最大 14.1m の高さ段差**（R12・木の高さ摂動の halo 無し Slice） | **Task 11**（`GeneratedTerrainSource.cs:135` の `Slice(halo=0)`）。既知欠陥 bd `moorestech-edd.5` | 実機で再現・数値化して bd に note。**修正するかはコントローラ判断**。14m は明確に見えるので優先度は高いと考える |
| 2 | 実マスタ `generateDetail: true` + PersonalAssets 欠落 worktree で **generatedワールドが起動不能** | **環境不備が主因**。ただし **Task 13** の master `generateDetail: true` 化がこの欠落を起動不能へ格上げした | bd `moorestech-edd.7` を起票。本番環境では実害なしと判断するが判断を仰ぐ |
| 3 | `PlayerStartsOnBuiltTerrainTest` が worktree で必ず落ちる（`moorestech_web/setup.sh` 未実行） | 環境不備・スコープ外 | worktree で `setup.sh` + `pnpm install` を実行して解消済み（どちらも .gitignore 対象・コミットなし） |
| 4 | `uloop run-tests` の `--test-mode` 既定が PlayMode で、EditModeInPlayingTest が Test Runner ごと自壊する | ツール運用の落とし穴・スコープ外 | 本レポートの Step 2 に手順として明記（bd 起票はしていない） |
| 5 | 岩クラスタ (`ClusterId>=0`) が 74338 mapObject 中 **0 件** | 未判断 | 報告のみ。v8 の想定として正しいか要確認 |

**このブランチの変更が原因の明確な退行は #1 のみ**（かつ Task 11 レビュー時点で既知・bd 済み）。

---

## 変更したテスト設定と復帰確認

| 対象 | 変更 | 復帰 |
|---|---|---|
| `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/generation.json` | `gridSizeX/Z` 5 → 3 | **5 に戻した**（`git status` に出ないことで確認済み） |
| 実マスタ `server_v8` | **一切変更していない**。`generateDetail: false` はスクラッチ (`/private/tmp/.../master_nodetail/server_v8`) のコピーに当て、`run-scenario.sh` の第3引数で渡した | 復帰不要 |
| `.moorestech-external-revisions.json` | 変更なし（`git diff` 空・pin `3651ecd5` のまま） | — |
| 共有 checkout `/Users/katsumi/moorestech_master` | 触っていない（`c610e13` のまま） | — |

`git status` は新規シナリオ1ファイルのみ:
```
?? .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/generated-world-5x5-terrain-survey.cs
```

---

## bd

| id | 内容 |
|---|---|
| `moorestech-edd.7` | **新規起票** — worktree に PersonalAssets が無く地形の見た目検証が実施不能 |
| `moorestech-edd.5` | note 追加 — 実機で再現・14.1m の数値と原因箇所を記録 |
| `moorestech-edd.1` | note 追加 — 高さ・splat とも境界で連続、padding 不足の顕在化は観測されず（ただし目視は不能） |

---

## 懸念

1. **R9（草）が一度も目視できていない。** Task 13 の主目的（`generateDetail: true`）の実物確認が本 worktree では
   構造的に不可能。**Task 16 の前に、main worktree でこのブランチを checkout して見た目だけ確認するのが望ましい。**
2. **R6 の「泥色の裸地帯」も同様に目視未確認。** splat 被覆に `MudDry` が 1.29% 出ていることまでしか言えない。
3. **14.1m の段差（edd.5）を出荷するかどうか。** 数値上は「4/40 の境界だけ・そのうち1つが致命的」なので、
   ワールドの seed によっては複数箇所に出る。修正コストは `SliceWithHalo(半径)` への差し替えだけで小さいと見える。
4. 岩クラスタが 0 件である件（上記 #5）。R6 のクラスタ経路が実ワールドで一度も走っていない可能性がある。
5. 25タイルのコールドビルド **128 秒**は初回起動体験としては長い。キャッシュヒット時は 3x3 の実測で 2.4 倍速だったので
   2回目以降は 50 秒程度と推測されるが、5x5 での 2 回目は実測していない。
