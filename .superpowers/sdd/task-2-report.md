# Task 2 レポート: C2 — 地形モード解釈を単一入口へ寄せる

**ステータス:** DONE
**コミット:**
- `1a03f795a` fix: 地形モード解釈をToTerrainTransferMeta1本へ寄せる(C2) — ブリーフStep 1〜8
- （追補）fix: 地形モード判定の残る消費側2件をIsTemplateへ寄せる(C2) — 親のGO裁定を受けた追加分（下記「追補」節）

> 注: このファイルは 7/30 の別SDDラン（鉱脈範囲表示のShow分離）のレポートを上書きしている。
> 旧内容はコミット `fba93f3e3` に残っており復元可能。親が指定したパスであり、同ランで既に再利用されているスロットのため上書きした。

## 実施したStepと結果

### Step 1: 失敗するテストを書く（完了）

配置先: `~/moorestech-worktrees/tree1/moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/Transfer/TerrainTransferMetaModeTest.cs`

ブリーフの仮パス `Tests.UnitTest/MapGeneration/` は実在しないため、既存の地形転送テスト（`TerrainChunkReaderTest` / `TerrainTransferMetaReaderTest` / `TerrainFileWriterTest`）が並ぶ `Tests/UnitTest/Game/MapGeneration/` の隣に合わせた。ただし同ディレクトリは既に `.cs` が10ファイルで上限に達していたため、`Transfer/` サブディレクトリを新設してそこへ置いた。

これは同ディレクトリの既存前例に一致する: `Spawn/SpawnBoundaryTest.cs` も1ファイルだけのサブディレクトリで、名前空間は親のまま `Tests.UnitTest.Game.MapGeneration` を使っている。本テストも同じく名前空間は `Tests.UnitTest.Game.MapGeneration` のままにした（フォルダに合わせて `.Transfer` を付けると、ファイル先頭の `using Game.MapGeneration.Transfer;` と名前解決が紛らわしくなるため）。

テスト本体の2メソッドはブリーフの逐語コピー。アサーション文言・値・変数名は一切変えていない。

### Step 2: テストを実行して失敗を確認（完了・期待どおり失敗）

`uloop compile --project-path ./moorestech_client`

```
"Success": false,
"ErrorCount": 2,
TerrainTransferMetaModeTest.cs(17,60): error CS1061: 'TerrainTransferMeta' does not contain a definition for 'IsTemplate' ...
TerrainTransferMetaModeTest.cs(21,62): error CS1061: 'TerrainTransferMeta' does not contain a definition for 'IsTemplate' ...
```

ブリーフのExpected（`IsTemplate` 未定義のコンパイルエラー）と完全一致。

### Step 3: TerrainTransferMeta に判別子を持たせる（完了）

`moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainTransferMeta.cs`

`MapMode` の直下に `public readonly bool IsTemplate;` をブリーフ逐語の2行セットコメント付きで追加し、privateコンストラクタ内で `IsTemplate = mapMode == WorldProvisioner.TemplateMapMode;` を設定。`MapMode` と同じ readonly フィールド扱い（申し送りどおり）。

### Step 4: TerrainRuntimeBuilder（完了）

3分岐＋独自throwを、先に `ToTerrainTransferMeta()` へ変換してから `IsTemplate` で2分岐する形へ置換。ブリーフのコード片そのまま。未使用になった `using Game.MapGeneration.Provisioning;` を削除。

なおマテリアル解決（`await AddressableLoader.LoadAsyncDefault<Material>`）は従来どおり分岐の前のまま。未知モードの例外が投げられるタイミングはマテリアル解決の後で、変更前と同じ。

### Step 5: TerrainDataFetcher（完了）

`wireMeta.MapMode == WorldProvisioner.TemplateMapMode` の早期returnを削除し、先に `ToTerrainTransferMeta()` してから `terrainMeta.IsTemplate` で早期returnする形へ。「ここで独自分岐を持たない」という自己矛盾コメントも消えた。未使用になった `using Game.MapGeneration.Provisioning;` を削除。

ブリーフ逐語からの唯一の差分: `if (terrainMeta.IsTemplate) return 0;` の直後に空行を1行入れた（削除前も早期returnの後は空行だったため、体裁維持）。

### Step 6: 二重実装が残っていないことの確認（実行済み・結果は下記）

### Step 7: コンパイルとテスト（完了・全PASS）

### Step 8: コミット（完了）

## Step 6 の grep 結果（全文）

```
$ grep -rn "TemplateMapMode\|GeneratedMapMode" moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts --include="*.cs"

moorestech_client/Assets/Scripts/Client.Tests/Playtest/PlaytestBootLifecycleTest.cs:37:        [TestCase(WorldProvisioner.GeneratedMapMode, 3, 0)]
moorestech_client/Assets/Scripts/Client.Tests/Playtest/PlaytestBootLifecycleTest.cs:38:        [TestCase(WorldProvisioner.TemplateMapMode, 2, 12345)]
moorestech_client/Assets/Scripts/Client.Tests/Playtest/PlaytestBootEnvironmentTest.cs:30:                "/master/server_v8", "/tmp/fixed-world", WorldProvisioner.GeneratedMapMode, 12345);
moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Terrain/TerrainVisualCacheReuseTest.cs:48:                await LoadMainGameWithMapMode(null, worldDirectory, WorldProvisioner.GeneratedMapMode);
moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Terrain/TerrainVisualCacheReuseTest.cs:51:                Assert.AreEqual(WorldProvisioner.GeneratedMapMode, mapLayout.TerrainMeta.MapMode, "generatedモードで起動していない");
moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Util/EditModeInPlayingTestUtil.cs:44:            await LoadMainGameWithMapMode(serverDirectory, worldDirectory, WorldProvisioner.TemplateMapMode);
moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Terrain/TerrainCacheFetchTest.cs:48:                await LoadMainGameWithMapMode(null, worldDirectory, WorldProvisioner.GeneratedMapMode);
moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Terrain/TerrainCacheFetchTest.cs:51:                Assert.AreEqual(WorldProvisioner.GeneratedMapMode, mapLayout.TerrainMeta.MapMode, "generatedモードで起動していない");
moorestech_client/Assets/Scripts/Client.Playtest/PlaytestBootLifecycle.cs:55:                WorldProvisioner.GeneratedMapMode => DebugEnvironmentType.Runtime,
moorestech_client/Assets/Scripts/Client.Playtest/PlaytestBootLifecycle.cs:56:                WorldProvisioner.TemplateMapMode => DebugEnvironmentType.Other,
moorestech_client/Assets/Scripts/Client.Playtest/PlaytestBootLifecycle.cs:154:            if (mapMode == WorldProvisioner.GeneratedMapMode || mapMode == WorldProvisioner.TemplateMapMode) return;
moorestech_client/Assets/Scripts/Client.Starter/StandaloneQa/StandaloneTerrainQaSettings.cs:73:                MapMode = WorldProvisioner.GeneratedMapMode,
moorestech_server/Assets/Scripts/Tests.Module/TerrainTransferTestScope.cs:58:            return Provision(WorldProvisioner.GeneratedMapMode, seed);
moorestech_server/Assets/Scripts/Tests.Module/TerrainTransferTestScope.cs:63:            return Provision(WorldProvisioner.TemplateMapMode, seed);
moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataProtocolTest.cs:98:            Assert.AreEqual(WorldProvisioner.TemplateMapMode, response.TerrainMeta.MapMode);
moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataTerrainMetaTest.cs:46:            var worldDataDirectory = ProvisionWorld(WorldProvisioner.GeneratedMapMode, 12345);
moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataTerrainMetaTest.cs:50:            Assert.AreEqual(WorldProvisioner.GeneratedMapMode, response.TerrainMeta.MapMode);
moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataTerrainMetaTest.cs:85:            var worldDataDirectory = ProvisionWorld(WorldProvisioner.GeneratedMapMode, 12345);
moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataTerrainMetaTest.cs:105:            var firstWorld = ProvisionWorld(WorldProvisioner.TemplateMapMode, 42);
moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataTerrainMetaTest.cs:110:            Assert.AreEqual(WorldProvisioner.TemplateMapMode, firstResponse.TerrainMeta.MapMode);
moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataTerrainMetaTest.cs:118:            var secondWorld = ProvisionWorld(WorldProvisioner.TemplateMapMode, 43);
moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/TerrainChunkReaderTest.cs:116:            Assert.AreEqual(WorldProvisioner.GeneratedMapMode, terrainMeta.MapMode);
moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/TerrainChunkReaderTest.cs:158:                MapMode = WorldProvisioner.GeneratedMapMode,
moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/TerrainTransferMetaReaderTest.cs:100:            Assert.AreEqual(WorldProvisioner.TemplateMapMode, meta.MapMode);
moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainTransferMeta.cs:39:            IsTemplate = mapMode == WorldProvisioner.TemplateMapMode;
moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainTransferMeta.cs:52:                WorldProvisioner.GeneratedMapMode, worldId, terrainResolution, terrainTileCount, terrainChunkTotal, worldSeed, origins);
moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainTransferMeta.cs:58:                WorldProvisioner.TemplateMapMode, worldId, 0, 0, 0, worldSeed, TerrainOrigins.WithoutTerrain());
moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainTransferMetaReader.cs:32:                WorldProvisioner.GeneratedMapMode => TerrainTransferMeta.CreateGenerated(
moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainTransferMetaReader.cs:35:                WorldProvisioner.TemplateMapMode => TerrainTransferMeta.CreateTemplate(CalculateWorldId(), worldMeta.Seed),
moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisioner.cs:19:        public const string TemplateMapMode = "template";
moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisioner.cs:20:        public const string GeneratedMapMode = "generated";
moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisioner.cs:49:                GeneratedMapMode => BuildGenerated(tempDataDirectory, settings),
moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisioner.cs:50:                TemplateMapMode => BuildTemplate(tempDataDirectory, settings),
moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisioner.cs:84:                    MapMode = GeneratedMapMode,
moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisioner.cs:110:                    MapMode = TemplateMapMode,
moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainStreamHasher.cs:21:            if (terrainMeta.MapMode == WorldProvisioner.TemplateMapMode) return string.Empty;
moorestech_server/Assets/Scripts/Server.Boot/ServerInstanceManager.cs:66:            var seed = settings.Seed ?? (settings.MapMode == WorldProvisioner.GeneratedMapMode ? Guid.NewGuid().GetHashCode() : 0);
moorestech_server/Assets/Scripts/Server.Boot/Args/StartServerSettings.cs:18:        public string MapMode { get; set; } = WorldProvisioner.TemplateMapMode;
moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/TerrainChunkReader.cs:20:            if (terrainMeta.MapMode == WorldProvisioner.TemplateMapMode)
moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/TerrainTransferMetaMessagePack.cs:58:            if (MapMode == WorldProvisioner.TemplateMapMode) return TerrainTransferMeta.CreateTemplate(WorldId, WorldSeed);
moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/TerrainTransferMetaMessagePack.cs:59:            if (MapMode == WorldProvisioner.GeneratedMapMode)
```

**判定: 主要受け入れ条件は達成。** `TerrainRuntimeBuilder.cs` / `TerrainDataFetcher.cs`（クライアントの消費側）のヒットは0件になった。

**ただしブリーフのExpectedが列挙した許容ヒット（WorldProvisioner / TerrainTransferMeta / TerrainTransferMetaMessagePack / テスト）に含まれない残存がある。** 分類は下記「懸念1」を参照。

## 実行したテストコマンドと出力

### 1. Step 7 指定のコマンド

```
$ uloop compile --project-path ./moorestech_client
"Success": true, "ErrorCount": 0, "WarningCount": 173   ← error CS のgrepヒット0件

$ uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TerrainTransferMetaModeTest|TerrainCacheFetchTest"
"Success": true
"Message": "Test execution completed with status: Passed"
"TestCount": 3, "PassedCount": 3, "FailedCount": 0, "SkippedCount": 0
```

TestCount 3 の内訳は新規2件（`ワイヤメタからのモード解釈は単一入口で完結する` / `未知モードは変換入口で例外になる`）＋ `TerrainCacheFetchTest.TerrainCacheRestoreReuseAndRefetchTest` 1件で、想定と一致。**申し送りの「編集直後は古いアセンブリでPASSを返す」罠については、先に `uloop compile` を成功させた上でTestCountを照合して確認した**（実装前の単独実行が `TestCount: 2`、`TerrainCacheFetchTest` を足して `3` に増えたことも新アセンブリで走った裏付けになっている）。

### 2. 追加の回帰テスト（ブリーフ外・自主実行）

ブリーフのStep 7は `TerrainCacheFetchTest`（＝`TerrainDataFetcher` 側）しか押さえていない。もう一方の変更対象 `TerrainRuntimeBuilder` を実際に通す唯一のテストは `TerrainVisualCacheReuseTest` なので追加実行した。またサーバー側 `TerrainTransferMeta` にフィールドを足したため、同型を扱う既存テストも回した。

```
$ uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TerrainChunkReaderTest|TerrainTransferMetaReaderTest|GetMapDataTerrainMetaTest|GetMapDataProtocolTest"
"Success": true, "TestCount": 22, "PassedCount": 22, "FailedCount": 0

$ uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TerrainVisualCacheReuseTest"
"Success": true, "TestCount": 1, "PassedCount": 1, "FailedCount": 0
```

**合計 26テスト / 26 PASS / 0 FAIL。**

環境メモ: `run-tests` は「Unity is reloading (Domain Reload in progress)」を3回返した。いずれも60〜75秒待機してリトライで解消。`get-version` 相当のJSONが返る事象は発生しなかった。

## ブリーフからの逸脱

1. **テストファイルの配置先。** ブリーフの `Tests.UnitTest/MapGeneration/TerrainTransferMetaModeTest.cs` は実在パスではなく、実際の `Tests/UnitTest/Game/MapGeneration/` は既に `.cs` 10ファイルで規約上限。`Transfer/` サブディレクトリを新設した（同ディレクトリ内の `Spawn/` に前例あり、名前空間は親のまま）。親からの指示「既存の地形転送テストの隣に合わせる／10ファイル制限を確認する」に沿った判断。

2. **`TerrainDataFetcher` の早期return直後に空行1行を追加。** 体裁のみで、ブリーフのコード片の内容は変えていない。

3. **回帰テストの追加実行**（上記）。コードの変更ではない。

4. **Step 8 のコミット方法を `git commit -am` から明示パス指定へ変更**（懸念3参照）。

上記以外、ブリーフのコード片・コメント文言・アサーション文言は逐語どおり。

## 気づいた懸念

### 懸念1（**解決済み** — 親のGO裁定により追補で対応。経緯として残す）: ドメイン型の消費側に文字列比較が2件残っていた

新設した `IsTemplate` のコメントは「消費側は文字列比較を持たない / consumers never compare the string themselves」と宣言しているが、サーバー側に `TerrainTransferMeta` を受け取って `MapMode` を文字列比較する箇所が2件残っており、宣言と実態が食い違っている。**C2と同じ欠陥クラス**（モード解釈の散逸）で、どちらも1行で解消できる。

- `moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainStreamHasher.cs:21`
  `if (terrainMeta.MapMode == WorldProvisioner.TemplateMapMode) return string.Empty;`
  → `if (terrainMeta.IsTemplate) return string.Empty;`（`using Game.MapGeneration.Provisioning;` が未使用になる）

- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/TerrainChunkReader.cs:20`
  `if (terrainMeta.MapMode == WorldProvisioner.TemplateMapMode)`
  → `if (terrainMeta.IsTemplate)`（21行目の例外メッセージ内の `terrainMeta.MapMode` は表示用なので残す。`using` は他で使っていなければ削除）

初回コミット時点では未変更としていた（ブリーフのFilesが3ファイル限定＋「勝手に解釈を広げない」の指示のため）。**親の裁定はGO** — 「C2の受け入れ条件はStep 6のExpected列挙そのものではなく『`TerrainTransferMeta` を受け取る側に文字列比較が残っていないこと』であり、この2件はC2の対象範囲内。Expectedが許容ヒットを列挙し漏らしていただけ」。追補として対応済み（下記「追補」節）。

なお、以下の残存ヒットは**正当**と判断した（消費側ではなく生成側・入口側のため）:
- `TerrainTransferMetaReader.cs:32,35` — world.json のモード文字列をドメイン型へ変換するサーバー側の入口。`ToTerrainTransferMeta()` の対になる正当な解釈点
- `WorldProvisioner` / `StartServerSettings` / `ServerInstanceManager` / `StandaloneTerrainQaSettings` / `PlaytestBootLifecycle` — モード文字列の定義元・生成元・起動引数の検証。`TerrainTransferMeta` を消費していない
- 各テスト — ワイヤ値のアサーションとして文字列そのものを見るのが妥当

### 懸念2（**最終ブランチレビューへ申し送り** — 親の指示により記録）: `!IsTemplate == generated` が成り立つ根拠は「不変条件」であって型ではない

`TerrainRuntimeBuilder` は `IsTemplate` が false なら generated として扱う。これが安全なのは、`TerrainTransferMeta` のコンストラクタが private で、生成経路が `CreateGenerated` / `CreateTemplate` の2つしかなく、`ToTerrainTransferMeta()` と `TerrainTransferMetaReader` の両方が未知モードを例外にしているため。**現状は正しい**が、3つ目のモードが増えたとき `IsTemplate` は bool なので黙って generated 側へ倒れる（コンパイルエラーにならない）。将来モードを増やすなら、その時点で bool ではなく enum 判別子へ変える必要がある。今回のスコープでは対応不要と判断した。

### 懸念3（情報・コミット衛生）: 自分の変更でない dirty ファイルが2件あった

作業開始時点で `.moorestech-external-revisions.json` と `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` が既に変更済みだった（後者はタイムスタンプが `2026/08/03 12:03:15` で、自分の初回コンパイル前）。**ブリーフのStep 8は `git commit -am` を指定しているが、これだと他タスクの変更を巻き込むため、明示パス指定の `git add` ＋ `git commit` に変えた。** 上記2ファイルは未コミットのまま残してある（親からこの判断で正しいと確認済み）。

---

# 追補: 消費側2件のIsTemplate化（親のGO裁定を受けた追加対応）

## 変更内容

親の裁定「C2の受け入れ条件は『`TerrainTransferMeta` を受け取る側に文字列比較が残っていないこと』」に基づき、懸念1で挙げた2件を対応した。

- `moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainStreamHasher.cs:21`
  `if (terrainMeta.MapMode == WorldProvisioner.TemplateMapMode) return string.Empty;` → `if (terrainMeta.IsTemplate) return string.Empty;`
  `WorldProvisioner` が同ファイル唯一の `Game.MapGeneration.Provisioning` 参照だったため `using` を削除

- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/TerrainChunkReader.cs:20`
  `if (terrainMeta.MapMode == WorldProvisioner.TemplateMapMode)` → `if (terrainMeta.IsTemplate)`
  21行目の例外メッセージ内 `$"World in '{terrainMeta.MapMode}' mode owns no terrain chunk to read."` は**表示用のため残置**（実際のモード名が出ないと診断価値が落ちる）
  同じく `using Game.MapGeneration.Provisioning;` を削除

親の指示どおり `TerrainTransferMetaReader.cs:32,35`（world.jsonの解釈入口）と生成側・起動引数・テスト群は触っていない。

## コンパイル

```
$ uloop compile --project-path ./moorestech_client
"Success": true, "ErrorCount": 0   （error CS のgrepヒット0件）
```

## テスト（親指定の範囲を全て含む）

```
$ uloop run-tests --project-path ./moorestech_client --filter-type regex \
    --filter-value "TerrainTransferMetaModeTest|TerrainCacheFetchTest|TerrainVisualCacheReuseTest|TerrainChunkReaderTest|TerrainTransferMetaReaderTest"
"Success": true
"Message": "Test execution completed with status: Passed"
"TestCount": 22, "PassedCount": 22, "FailedCount": 0, "SkippedCount": 0
```

**TestCount照合**: 各テストファイルの `[Test]`/`[TestCase]`/`[UnityTest]` 属性を数えて事前に期待値を算出し、一致を確認した。

| テストクラス | 属性数 |
|---|---|
| TerrainTransferMetaModeTest | 2 |
| TerrainCacheFetchTest | 1 |
| TerrainVisualCacheReuseTest | 1 |
| TerrainChunkReaderTest | 6 |
| TerrainTransferMetaReaderTest | 12 |
| **合計（期待値）** | **22** |

実測 `TestCount: 22` と完全一致 → 古いアセンブリではなく新アセンブリで走っている。なおこの数え方の妥当性は、初回ラウンドの別セット（ChunkReader 6＋MetaReader 12＋GetMapDataTerrainMeta 3＋GetMapDataProtocol 1 = 22）が実測22と一致したことで裏付け済み。

`TerrainStreamHasher` の回帰は、親の指摘どおり `TerrainCacheFetchTest`（フェッチ後の再ハッシュで転送破損を検出する経路）と `TerrainVisualCacheReuseTest` のPASSが実質の確認になっている。

## Step 6 grep 再実行（消費側0件の確認）

```
$ grep -rn "TemplateMapMode\|GeneratedMapMode" moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts --include="*.cs" \
  | grep -E "TerrainRuntimeBuilder|TerrainDataFetcher|TerrainStreamHasher|TerrainChunkReader\.cs"
（出力なし = 0件）
```

`TerrainTransferMeta` を受け取る消費側4ファイル全てでヒット0件。

テストを除いた全残存ヒットは以下のみで、すべて**生成側・入口側・起動引数**（＝モード文字列を作る側／外部データを解釈してドメイン型へ変換する側）:

```
moorestech_client/Assets/Scripts/Client.Starter/StandaloneQa/StandaloneTerrainQaSettings.cs:73:                MapMode = WorldProvisioner.GeneratedMapMode,
moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainTransferMetaReader.cs:32:                WorldProvisioner.GeneratedMapMode => TerrainTransferMeta.CreateGenerated(
moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainTransferMetaReader.cs:35:                WorldProvisioner.TemplateMapMode => TerrainTransferMeta.CreateTemplate(CalculateWorldId(), worldMeta.Seed),
moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainTransferMeta.cs:39:            IsTemplate = mapMode == WorldProvisioner.TemplateMapMode;
moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainTransferMeta.cs:52:                WorldProvisioner.GeneratedMapMode, worldId, terrainResolution, terrainTileCount, terrainChunkTotal, worldSeed, origins);
moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainTransferMeta.cs:58:                WorldProvisioner.TemplateMapMode, worldId, 0, 0, 0, worldSeed, TerrainOrigins.WithoutTerrain());
moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisioner.cs:19:        public const string TemplateMapMode = "template";
moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisioner.cs:20:        public const string GeneratedMapMode = "generated";
moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisioner.cs:49:                GeneratedMapMode => BuildGenerated(tempDataDirectory, settings),
moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisioner.cs:50:                TemplateMapMode => BuildTemplate(tempDataDirectory, settings),
moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisioner.cs:84:                    MapMode = GeneratedMapMode,
moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisioner.cs:110:                    MapMode = TemplateMapMode,
moorestech_server/Assets/Scripts/Server.Boot/ServerInstanceManager.cs:66:            var seed = settings.Seed ?? (settings.MapMode == WorldProvisioner.GeneratedMapMode ? Guid.NewGuid().GetHashCode() : 0);
moorestech_server/Assets/Scripts/Server.Boot/Args/StartServerSettings.cs:18:        public string MapMode { get; set; } = WorldProvisioner.TemplateMapMode;
moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/TerrainTransferMetaMessagePack.cs:58:            if (MapMode == WorldProvisioner.TemplateMapMode) return TerrainTransferMeta.CreateTemplate(WorldId, WorldSeed);
moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/TerrainTransferMetaMessagePack.cs:59:            if (MapMode == WorldProvisioner.GeneratedMapMode)
```

これで「モード文字列を解釈する場所」は `TerrainTransferMetaMessagePack.ToTerrainTransferMeta()`（ワイヤ入口）と `TerrainTransferMetaReader`（world.json入口）の2つの入口＋`TerrainTransferMeta` 自身の判別子導出だけになり、C2の宣言と実装が一致した。

コミットは追補も明示パス指定で行い、`.moorestech-external-revisions.json` と `_CompileRequester.cs` は未コミットのまま残してある。
