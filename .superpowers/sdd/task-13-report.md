# Task 13 報告: generate系フラグのゲートを復元（R9 / 移植漏れ③）

BASE: `a80da3a47` / worktree: `/Users/katsumi/moorestech-worktrees/map-autogen-5x5`

## 1. 何をどう実装したか

### (a) ゲート5箇所（移植元の3フラグに対応）

| フラグ | ゲート位置 | 移植元 |
|---|---|---|
| `generateHeightmap` | `TerrainDataAssembler.ApplyHeightmap()` — `heightmapResolution`/`size` を入れた**後**に `SetHeights` だけを飛ばす | `TerrainGenerator.cs:211-213`（`result.Heights = null` にして適用を止める） |
| `generateTexture` | `TerrainDataAssembler.ApplySplatmapAsync()` — `alphamapResolution`/`terrainLayers`/`ApplyAsync` を丸ごと飛ばす | `TerrainGenerator.cs:216`（`result.Splatmap`/`TerrainLayers` を設定しない） |
| `generateTexture` | `TerrainTileVisualProvider.Rebuild()` — `SplatmapRuntimeGenerator.Generate` を呼ばず alphamap を `null` にする | `TerrainGenerator.cs:792`（`SplatmapJob` 自体を飛ばす） |
| `generateDetail` | `TerrainTileVisualProvider` ctor — `DetailPrototypes` を空リストにする | `TerrainGenerator.cs:1259,1303`（`wantDetail` で Stage 5 ごと飛ばす） |
| `generateDetail` | `TerrainTileVisualProvider.Rebuild()` — `TerrainDetailBuilder.Build` を呼ばず空リストにする | 同上 |

detailの2箇所は**同じ1つの型（`TerrainTileVisualProvider`）が両方を所有する**形にした。ブリーフが警告している「片方だけスキップして `detailPrototypes.Count != detailMaps.Count` 例外に当たる」形を、所有者を1つに畳むことで構造的に作りにくくしている（テストでも同数であることを先に見る）。

さらに `Resolve` の入口に2本の早期脱出を置いた:

- `!generateTexture && !generateDetail`: 再構築の成果物が誰にも読まれないので、**分類（`TerrainClassificationContext.Initialize` = パディング窓）ごと回さず**空の `TerrainTileVisual` を返す。移植元も `needPlacement`（`TerrainGenerator.cs:228`）で生成フラグが全部落ちていれば配置ステージへ入らない
- `!generateTexture`: **見た目キャッシュを読みも書きもしない**（理由は §5-2）

### (b) `GeneratedTerrainSource.cs` の分割（198→154行）

上限200行に対し、ゲートを足すと確実に超えるため `Build/TerrainTileVisualProvider.cs`（133行）を新設して分割した。`partial` は使っていない。

- **切り出した責務**: 「タイル1枚ぶんの見た目を配る」＝キャッシュ引き当て → 再構築（分類・splat・detail）→ 書き戻し、および detail プロトタイプの所有
- `GeneratedTerrainSource` から消えたフィールド: `_biomeTypes` / `_visualSections` / `_layerTable` / `_treeSurroundSpecies` / `_visualCache`（すべて provider へ移動）。ctorは10引数→6引数
- `transferredBiomeIndices` の読み込み（`TerrainFileLoader.LoadBiomeIndices`）も provider の splat 経路の中へ移した。キャッシュヒット時には誰も読まないファイルを毎タイル読んでいたのが、必要なときだけになる
- `CreateTerrainDataAsync` から `#region Internal` の再構築ローカル関数が丸ごと無くなり、本体は「高さを読む → タイルConfig → 摂動 → 見た目をもらう → 数一致検査 → 組み立て」の一直線になった
- `Build/` の .cs は 8 → **9ファイル**（上限10、残1）

### (c) 付随する型の変更

- `TerrainDataAssembler.AssembleAsync` の `detailPrototypes` を `List<DetailPrototype>` → `IReadOnlyList<DetailPrototype>`（provider が公開する型に合わせた。`using System.Linq` を追加して `ToArray()`）

### (d) マスタ側（コントローラ追記1・ユーザー裁定）

- 変更ファイル: `server_v8/mods/moorestechAlphaMod_8/master/generation.json:460` `"generateDetail": false` → `true` の**1行のみ**
- 変更ブランチ: `feat/mapobject-scale-cluster-keys`（worktree `/Users/katsumi/moorestech-master-worktrees/mapobject-scale-cluster-keys`）
- masterコミット: **`b3d543fb28f91369a94381d337e7530aca106462`**（`feat(v8): generateDetail を true にして草の生成を有効化`）
- 他の json / 他の `server_vN` には一切触っていない（`git diff --stat` = 1 file changed, 1 insertion, 1 deletion）
- コード側 `.moorestech-external-revisions.json` の `moorestech_master` pin を `e351f4189b...` → `b3d543fb28...` へ更新

## 2. TDD の経過（RED はすべてアサーション失敗）

手順: ①`TerrainTileVisualProvider` を**ゲート無しの純粋な構造抽出**として先に作る（テストがコンパイルできる状態を作るため）→ ②ゲートを検証するテストを書いて RED → ③ゲート実装 → ④GREEN。

RED（`/tmp/t13_red.xml`, total=68 passed=62 failed=6・すべてアサーション失敗でコンパイルエラーは0件）:

```
--- LeavesTheTerrainFlatWhenTheHeightmapFlagIsOff      Expected: 0.0f +/- 0.001f  But was: 0.5f
--- LeavesTheDefaultAlphamapWhenTheTextureFlagIsOff    Expected: <empty>          But was: <2 TerrainLayer>
--- DropsThePrototypesAndTheDensityMapsTogether...Off  Expected: 0                But was: 1
--- LeavesTheAlphamapUnbuiltWhenTextureGenerationIsOff Expected: null             But was: <8x8x3 の実体>
--- NeitherReadsNorWritesTheCacheWhenTextureGener...Off Expected: False           But was: True
--- AppliesTheSplatmapWhenTheTextureFlagIsOn           Expected: 4                But was: 16   ← テスト側の不備
```

最後の1本はゲートではなく**テストフィクスチャの不備**だった（Unityは `alphamapResolution` を16未満へ落とせない／`heightmapResolution` は33未満へ落とせない）。定数を 16 / 33 に直した。この2つは「実行して初めて分かる」種類の落とし穴なので、定数の脇に2行コメントで理由を残してある。

GREEN（`/tmp/t13_green2.xml`）: **total=68 passed=68 failed=0**

## 3. ミューテーション注入の観測結果（3件・すべて検知）

フィルタは `TerrainDataAssembler|TerrainTileVisualProvider`（10テスト）。

### MUT-A: detailの**片方だけ**をスキップする（`TerrainDetailPrototypeList.Build` のゲートを外す）

ブリーフが名指ししている `detailPrototypes.Count != detailMaps.Count` 例外に当たる形そのもの。

```
total=10 passed=9 failed=1
--- DropsThePrototypesAndTheDensityMapsTogetherWhenDetailGenerationIsOff -> Failed
    プロトタイプと密度マップは同数 |   Expected: 1 |   But was:  0
```

プロトタイプ1本に対し密度マップ0本、という**食い違いそのものが失敗メッセージに出る**。
（アサーション順を「数一致 → 本数0」に並べ替える前は `Expected: 0 / But was: 1` で落ちていた。どちらの並びでも検知はするが、食い違いを直接見せる並びを採用した）

### MUT-B: ゲート5本すべてを逆向きにする

```
total=10 passed=1 failed=9
--- AppliesTheHeightsWhenTheHeightmapFlagIsOn        Expected: 0.5f +/- 0.001f  But was: 0.0f
--- AppliesTheSplatmapWhenTheTextureFlagIsOn         Expected: 2                But was: 0
--- LeavesTheDefaultAlphamapWhenTheTextureFlagIsOff  Expected: <empty>          But was: <2 TerrainLayer>
--- LeavesTheTerrainFlatWhenTheHeightmapFlagIsOff    Expected: 0.0f +/- 0.001f  But was: 0.5f
--- BuildsTheAlphamapWhenTextureGenerationIsOn                    System.NullReferenceException
--- BuildsThePrototypesAndTheDensityMapsTogether...IsOn           System.NullReferenceException
--- DropsThePrototypesAndTheDensityMapsTogether...IsOff           System.NullReferenceException
--- LeavesTheAlphamapUnbuiltWhenTextureGenerationIsOff Expected: null           But was: <8x8x3の実体>
--- ReusesTheCachedVisualOnASecondResolve...IsOn                  System.NullReferenceException
```

NRE は「テクスチャONなのに alphamap が null のまま `TerrainVisualCacheWriter` が `Alphamap.GetLength(0)` を読む」ため。逆向きゲートは即座に落ちる。
唯一通った `NeitherReadsNorWritesTheCacheWhenTextureGenerationIsOff` は `Resolve` 冒頭のキャッシュ早期脱出を逆にしていないため（この1本は MUT-C で落ちる）。

### MUT-C: ゲートを1本も足さない（＝本タスク着手前の挙動・フラグを完全に無視する）

```
total=10 passed=5 failed=5
--- LeavesTheDefaultAlphamapWhenTheTextureFlagIsOff    Expected: <empty>  But was: <2 TerrainLayer>
--- LeavesTheTerrainFlatWhenTheHeightmapFlagIsOff      Expected: 0.0f     But was: 0.5f
--- DropsThePrototypesAndTheDensityMapsTogether...IsOff Expected: 0       But was: 1
--- LeavesTheAlphamapUnbuiltWhenTextureGenerationIsOff Expected: null     But was: <8x8x3の実体>
--- LeavesTheDefaultAlphamap...（上記）
--- NeitherReadsNorWritesTheCacheWhenTextureGener...IsOff Expected: False  But was: True
```

「ゲートを足す前の実装」で **OFF系5本が全部落ちる**。ONの5本は通る（ゲートを足しても壊していないことの確認）。

## 4. 移植元（MM）との対応

- `generateHeightmap`: MM は `result.Heights = null` にして `TerrainApplier` 側で適用を止める。moorestech では TerrainData を組むのが `TerrainDataAssembler` 1箇所しか無いので、そこで `SetHeights` だけを飛ばす形にした。**`heightmapResolution` と `size` は落とさない**（MM も `TerrainGenerationResult.Resolution`/`TerrainSize` は常に埋めている）
- `generateTexture`: MM は `SplatmapJob`（:792）と `ConvertSplatWeights`（:216）の2箇所で切っている。moorestech でも「生成」と「適用」の2箇所で切った（provider と assembler）
- `generateDetail`: MM は Stage 5 全体を `wantDetail` で囲み、`DetailPlacementGenerator.GenerateForBiome` が prototypes と maps を**同時に**返すため、片方だけ欠ける形が原理的に無い。moorestech は2つの型に分かれているので、**同じ型に所有させる**ことで同じ性質を作った
- MM の `PlateauDebugOverlayJob`（:825）は `config.generateTexture && ...` で始まっている。moorestech でも `Generate` の内側なので自然にスキップされる（追記3）

## 5. ブリーフ／移植元からの逸脱と理由

### 5-1. `TerrainDataAssembler` が config のフラグを直接読む（ブリーフは「呼び出し側がスキップ」）

ブリーフは「`ApplySplatmapAsync` をスキップ」と書いているが、**適用側でフラグを読む**形にした。

- 移植元も適用側（`TerrainApplier`）が `result.Heights == null` / `result.Splatmap == null` を見て止めている。決定は上流、判定は適用側という構造は同じ
- 「null を渡したら飛ばす」という暗黙の合図にすると、**呼び出し忘れ・渡し間違いが静かに平坦な地形として焼き付く**。config の明示フラグなら単体テストでゲートの向きまで固定できる（§3 MUT-B/MUT-C）
- `TerrainDataAssembler` は地形生成専用の型で、`TerrainGenerationConfig` を既に受け取っている。汎用基盤にドメイン語彙を持ち込む話ではない

### 5-2. `generateTexture=false` のとき見た目キャッシュを一切使わない（ブリーフに指示なし・追記3の宿題）

- `TerrainVisualCacheFormat.TryCalculatePayloadByteLength` は `alphamapResolution > 0 && layerCount > 0` を要求し、`TerrainVisualCacheWriter` は `tileVisual.Alphamap.GetLength(0/2)` を無条件に読む。alphamap 不在を表現するには **FormatVersion を上げてファイル形式に「alphamap無し」の形を足す**必要がある
- 実データが常に `true` の（そして移植元でも開発用トグルだった）フラグのためにキャッシュ形式を複雑にするのは割に合わない。キャッシュは真実源ではないので、使わなければ毎起動 detail を作り直すだけで**正しさは1つも落ちない**
- この判断はテストで固定してある（`NeitherReadsNorWritesTheCacheWhenTextureGenerationIsOff`）

### 5-3. `SplatLayerTable.Build` と `TerrainLayerAssetLoader.LoadAsync` は**ゲートしない**（追記3の宿題）

- 移植元も `terrainLayers` と `layerCount` は生成フラグに関係なく確定させており、`if (config.generateTexture)` が掛かるのは `SplatmapJob`（:792）と `ConvertSplatWeights`（:216）だけ
- 列の確保は「マスタから決まること」でタイルごとの生成ではない。加えて `_terrainLayers.Length` はキャッシュのレイヤー数照合に使われるので、フラグで長さが動くとキー以外の理由でキャッシュ寸法が変わってしまう

### 5-4. `TerrainClassificationContext.Initialize` は `generateTexture=false` でも回す（追記3の宿題）

- detail の勝者マスク（`WinnerMasks`）が分類の産物なので、texture だけ落とした設定では必要
- ただし **texture も detail も落ちている**ときだけは誰も読まないので、`Resolve` 冒頭で分類ごと省いた。移植元の `needPlacement`（`TerrainGenerator.cs:228` = `generateObject || generateDetail || generateOre` で配置ステージ全体を囲う）と同形

### 5-5. `TerrainVisualCacheFormat.FormatVersion` は **7 のまま据え置き**（追記3の宿題）

上げる必要はないと判断した。根拠は2つ:

1. 3フラグの出所は generation マスタ JSON だけであり、`TerrainVisualCacheKey.Compute` の第1引数 `MasterHolder.GenerationMaster.SourceJsonText`（`GenerationMaster.cs:34` = `jToken.ToString(Formatting.None)` ＝ generation.json 全文）に畳み込まれている。**フラグを動かせばキーが変わる**
2. 万一キーが同じでも、`TerrainVisualCacheReader.cs:86-90` が expected 寸法（`layerCount` / `detailMapCount` / `detailResolution`）と食い違うファイルを「壊れた取り逃し」として捨てる。prototypes 0 に対して detailMapCount N のキャッシュがヒットして落ちる、という経路は無い

なお本タスクは master 側 `generateDetail` を false→true にしているので、pin 更新の時点で**既存キャッシュは全ワールドでキーが変わる**（作り直しになる）。

### 5-6. `GeneratedTerrainSource` の分割の切り口（追記2で分割自体は要求済み）

「ファクトリを切り出す」案と「タイル見た目の供給を切り出す」案を比べ、後者を採った。detail のプロトタイプと密度マップを**同じ型に所有させる**ことで、ブリーフが警告している片側スキップを構造的に作りにくくできるため。ファクトリ切り出しでは行数は減るがこの性質は得られない。

### 5-7. `TerrainDataAssembler.AssembleAsync` の引数型変更

第4引数 `List<DetailPrototype>` → `IReadOnlyList<DetailPrototype>`（provider が公開する型に合わせる）。呼び出し側は本番1箇所＋新規テスト1箇所のみ。

## 6. 実行したコマンドと出力

macOS のログイン画面がロック中（`ioreg -n Root -d1 -a | grep -A1 CGSSessionScreenIsLocked` → `<true/>`）だったため、Task 12 報告 §3 の手順どおり **`uloop` は使わず Unity batchmode** で回した。全実行をバックグラウンド起動＋ポーリングで待った。

```bash
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath .../map-autogen-5x5/moorestech_client \
  -runTests -testPlatform EditMode \
  -testFilter "<フィルタ>" -testResults /tmp/xxx.xml -logFile /tmp/xxx.log
```

| 段階 | フィルタ | 結果 |
|---|---|---|
| 抽出後コンパイル確認 | 本文フィルタ | `error CS0246: BiomePlacementHelper`（using 落ち）→ 修正 |
| RED | 本文フィルタ + `TerrainDataAssembler|TerrainTileVisualProvider` | total=68 passed=62 **failed=6（全てアサーション失敗）** |
| GREEN | 同上 | total=68 **passed=68 failed=0** |
| MUT-A | `TerrainDataAssembler|TerrainTileVisualProvider` | total=10 failed=1（§3） |
| MUT-B | 同上 | total=10 failed=9（§3） |
| MUT-C | 同上 | total=10 failed=5（§3） |
| 最終 | 本文フィルタ + `TerrainDataAssembler|TerrainTileVisualProvider|TerrainAlphamapApplier|TerrainFileLoader` | **total=75 passed=75 failed=0** |

結果XMLは `encoding="utf-8"` 宣言が実体とずれていて `ElementTree.parse` が落ちるため、宣言を落としてから `fromstring` に食わせている（Task 12 報告 §3 と同じ回避）。

master 側:

```bash
cd /Users/katsumi/moorestech-master-worktrees/mapobject-scale-cluster-keys
git diff --stat   # 1 file changed, 1 insertion(+), 1 deletion(-)
git add server_v8/mods/moorestechAlphaMod_8/master/generation.json
git commit        # -> b3d543fb28f91369a94381d337e7530aca106462
```

## 7. 懸念・後続への申し送り

- **【要対応】master pin が上がった。** `.moorestech-external-revisions.json` は `b3d543fb28...` を指す。Task 15 の5x5録画を含め、以降は共有checkout `/Users/katsumi/moorestech_master` をこのコミットへ移す必要がある（`feat/mapobject-scale-cluster-keys` ブランチ上）
- **既存の visual キャッシュは全部作り直しになる。** generation マスタ原文が変わったのでキーが変わる。初回起動が遅くなるのは想定内
- **`generateTexture=false` は実運用で一度も通ったことがない経路。** 本タスクで単体テストは付けたが、`GeneratedTerrainSource` 経由（PlayMode 起動）では未検証。レイヤー0本の TerrainData が URP マテリアルでどう見えるかは未確認（真っ黒/ピンクの可能性）。実データは `true` なので実害は無いが、デバッグでフラグを落とすときは注意
- **`generateHeightmap=false` でも `postHeights` は detail の傾斜計算に使われ続ける**（移植元と同じ）。サーバー側は heights を常に生成・保存したままで、サーバーには一切手を入れていない
- **EditModeInPlayingTest（`TerrainVisualCacheReuseTest` / `PlayerStartsOnBuiltTerrainTest`）は実行していない。** タスク指定のフィルタが PlayMode 遷移テストを避ける形だったため。`GeneratedTerrainSource` を分割したので、Task 15 の前に一度は通しておく価値がある（`EditModeInPlayingTestMod/master/generation.json` は3フラグとも `true` なのでゲートは no-op になるはず）
- テストフィクスチャで踏んだ Unity の下限（`alphamapResolution` は16未満不可 / `heightmapResolution` は33未満不可）は、後続が同種のテストを書くときに再度踏む。定数の脇に2行コメントで残してある
