# Task 1（P3・マップ自動生成）: Layout応答へterrainメタ追加（サーバー）

Status: **DONE_WITH_CONCERNS**（実装・テストは完了。懸念は環境側＝画面ロックでUnity Editorが停止しuloop経由の検証が不能になった点のみ）

コミット: `5a7ef02e6` — `va:mapData Layout応答にterrainメタ5項目を追加する`

## 実装したもの

### 1. `Game.MapGeneration/Transfer/TerrainTransferMeta.cs`（新規・37行）
Layout応答へ載せる地形メタの値オブジェクト。`MapMode` / `WorldId` / `TerrainResolution` /
`TerrainTileCount` / `TerrainChunkTotal` を readonly フィールドで保持。
Task 2 と共有するチャンク単位を `public const int ChunkByteSize = 256 * 1024;` としてここ1箇所に置いた
（Task 2 の論理ストリーム切り出しは同じ定数を参照すること）。
ワールドディレクトリを持たない構成向けに `CreateWithoutWorldDirectory()` を用意。

### 2. `Game.MapGeneration/Transfer/TerrainTransferMetaReader.cs`（新規・57行）
`WorldDataDirectory` から world.json と terrain 実ファイルを読んで `TerrainTransferMeta` を作る。
- `Root == null`（`FromServerDataMap` のレガシー形）は **明示的な分岐** で `CreateWithoutWorldDirectory()` を返す（`?? Default` 的な穴埋めはしていない）
- `MapMode` は world.json の値をそのまま使い、`WorldProvisioner.GeneratedMapMode` / `TemplateMapMode` 以外は例外（フォールバック禁止・プロトコル本体のMode switchと同じ流儀）
- `TerrainChunkTotal` は `terrain/` 配下の実ファイル総バイト数を `ChunkByteSize` で切り上げ除算（templateは0）
- `WorldId` は `"{seed}:{createdAt}"` の SHA256 先頭16桁（小文字hex）

### 3. `Server.Protocol/PacketResponse/GetMapDataProtocol.cs`（変更・170行）
- DIから `WorldDataDirectory` を取得し、`CreateLayoutResponse()` で `TerrainTransferMetaReader.Read(...)` を呼ぶ
- `ResponseMapDataMessagePack` に `[Key(5)] MapMode` / `[Key(6)] WorldId` / `[Key(7)] TerrainResolution` /
  `[Key(8)] TerrainTileCount` / `[Key(9)] TerrainChunkTotal` を後方追加（既存 Key 2,3,4 は不変。`[Key(10)]` は Task 2 の `TerrainHash` 用に空けてある）
- **170行に収まったため、DTOの `MapData/` 配下への切り出しは行っていない**（200行規約内。切り出しは「超える場合」の条件付き指示だったため）

### 4. `Server.Protocol.asmdef`
`Game.Paths`（`WorldDataDirectory`）と `Game.MapGeneration`（`TerrainTransferMeta*`）への参照を追加。

### 5. `Client.DebugSystem/CharacterTestDebug.cs`
`ResponseMapDataMessagePack` のコンストラクタ引数追加に追従。デバッグシーンはサーバーワールドを持たないので
`TerrainTransferMeta.CreateWithoutWorldDirectory()` を渡す。

### DI登録について（ブリーフとの差分）
ブリーフは `MoorestechServerDIContainerGenerator` へ `WorldDataDirectory` のSingleton登録を足す指示だったが、
**P1時点で既に `services.AddSingleton(options.worldDataDirectory);`（225行目）が存在**しており、
`PacketResponseCreator` が使う `serviceProvider` から解決できることを確認したため、DI生成器は変更していない。

## テストと結果

テストファイル: `Tests/CombinedTest/Server/PacketTest/GetMapDataProtocolTest.cs`（87行 → 187行）

1. `GetMapDataLayoutTest`（既存を拡張）: `FromServerDataMap` のレガシー形。既存のspawn/mapObjects/mapVeins検証に加え、
   MapMode=template / WorldId=空 / 解像度・タイル数・チャンク数=0 を検証。
2. `Generatedワールドのterrainメタがworld_jsonとterrainファイル実体に整合する`（新規）: 一時ディレクトリへ
   generated ワールドをプロビジョニングし、`world.json` をテスト側で読み直して `TerrainResolution` /
   `TerrainTileCount` の一致を検証。`TerrainChunkTotal` は `terrain/` 配下の実ファイル総バイト数から
   テスト側で独立に切り上げ算して突き合わせ（かつ >0 であることを保証）。WorldIdは16桁hexを検証。
3. `Templateワールドはterrainメタが0でWorldIdはワールドごとに異なる`（新規）: template ワールドで
   3項目が0かつ WorldId が16桁hexで埋まること、別ワールド（別seed・別createdAt）では WorldId が異なることを検証。

### RED（実装前）

Unity Editor経由の実行（`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GetMapDataProtocolTest"`）は
アセンブリがコンパイルできないためテストが検出されず、`uloop compile` が失敗を報告した：

```
$ uloop compile --project-path ./moorestech_client
Success False Errors 22 Warnings 8
.../GetMapDataProtocolTest.cs(111,72): error CS1061: 'GetMapDataProtocol.ResponseMapDataMessagePack' does not contain a definition for 'MapMode' ...
.../GetMapDataProtocolTest.cs(112,52): error CS1061: ... does not contain a definition for 'WorldId' ...
.../GetMapDataProtocolTest.cs(113,41): error CS1061: ... does not contain a definition for 'TerrainResolution' ...
.../GetMapDataProtocolTest.cs(114,41): error CS1061: ... does not contain a definition for 'TerrainTileCount' ...
.../GetMapDataProtocolTest.cs(115,41): error CS1061: ... does not contain a definition for 'TerrainChunkTotal' ...
（以下同種、計22件。全て GetMapDataProtocolTest.cs のterrainメタ参照）
```

想定通りの失敗である理由: 応答DTOにterrainメタ5項目がまだ無いため、テストが要求するAPIが存在しない。
C#では「未実装APIを参照するテスト」はコンパイルエラーとして落ちるのが正しいRED。エラーは全て新規アサーション行に限定されており、
既存のspawn/mapObjects/mapVeins検証は無傷。

（補足: RED取得前に、Unityが `TerrainTransferMeta.cs` を取り込み損ねて
`CS0246: The type or namespace name 'TerrainTransferMeta' could not be found` が出続ける取り込み不具合に遭遇した。
force-recompile・Assets/Refresh では解消せず、ファイルを一度削除→Refresh→再作成→Refresh で解消。
`CompilationPipeline.GetAssemblies` で当該ファイルだけがソース一覧から欠落していることを確認済み。コード側の問題ではない。）

### GREEN（実装後）

画面ロックによりUnity Editorが応答しなくなったため（下記「懸念」）、同一プロジェクト・同一フィルタを
Unity のバッチモードで実行した:

```
$ /Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -projectPath .../moorestech_client -runTests -testPlatform EditMode \
    -testFilter "Tests.CombinedTest.Server.PacketTest.GetMapDataProtocolTest" \
    -testResults .../batch-results.xml -logFile .../batch.log
EXIT=0

# NUnit XML
{'result': 'Passed', 'total': '3', 'passed': '3', 'failed': '0', 'skipped': '0', 'duration': '0.4301551'}
Passed  Generatedワールドのterrainメタがworld_jsonとterrainファイル実体に整合する
Passed  GetMapDataLayoutTest
Passed  Templateワールドはterrainメタが0でWorldIdはワールドごとに異なる
```

軽微な整形（末尾改行）の修正後に再実行しても同結果（3/3 Passed、`grep -c "error CS" batch2.log` → 0）。
バッチ実行はテスト実行前に全アセンブリをコンパイルするため、**コンパイル検証（エラー0）もこの実行で担保されている**。
なお実装直前のGREEN前コンパイルでは Editor経由 `uloop compile` が `Success True / Errors 0 / Warnings 198`（警告は全て既存コード由来）を返している。

## 変更ファイル

- 新規: `moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainTransferMeta.cs`（+ Unity生成 .meta）
- 新規: `moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer/TerrainTransferMetaReader.cs`（+ Unity生成 .meta）
- 新規: `moorestech_server/Assets/Scripts/Game.MapGeneration/Transfer.meta`（Unity生成）
- 変更: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetMapDataProtocol.cs`
- 変更: `moorestech_server/Assets/Scripts/Server.Protocol/Server.Protocol.asmdef`
- 変更: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataProtocolTest.cs`
- 変更: `moorestech_client/Assets/Scripts/Client.DebugSystem/CharacterTestDebug.cs`

`_CompileRequester.cs` がUnity実行の副作用でタイムスタンプ更新されていたが、スキーマ変更を伴わないため
`git checkout` で戻し、コミットには含めていない。

## 自己レビュー所見

- **完全性**: ブリーフのKey番号補正（5〜9）、templateの0値、レガシー形（Root==null）の明示分岐、
  256KB定数の1箇所集約、既存Key不変——指示された論点は全て満たしている。Task 2の範囲（TerrainChunkモード・TerrainHash）には手を付けていない。
- **設計レンズ**: ①前例一致=world.jsonの読み書きは `WorldProvisioner`/`WorldMetaJson` の既存形に合わせ、パス連結は `WorldDataDirectory` のみ。
  ⑥データ防御禁止=`?? Default` もローダー補完も無し、未知mapModeは例外。⑨配置規約=`PacketResponse/` 直下は
  `IPacketResponse` 実装のまま（DTOは従来通りネスト、170行で規約内）。`Func<>`・partial・try-catch・デフォルト引数は不使用。
- **YAGNI**: `TerrainTransferMeta` を struct/enum 判別子などに凝らず、5値＋定数の素直な値オブジェクトに留めた。
  DTOの `MapData/` 切り出しも行数超過が起きなかったので実施していない（不要な波及を作らない）。
- **テストが実挙動を検証しているか**: チャンク数は「実装と同じ式のコピー」ではなく、**terrain実ファイルをテスト側で列挙して総バイト数から独立算出**し突き合わせている。
  解像度・タイル数は world.json を読み直して比較。WorldIdは形式（16桁hex）＋「別ワールドで別ID」という性質で検証しており、
  ハッシュ式そのものの写経はしていない（式を変えても性質が保たれれば通る＝仕様レベルの検証）。
- **弱点の自認**: WorldIdが「seed+createdAtのSHA256先頭16桁」であること自体はテストで固定していない。
  仕様の同一性はブリーフに依拠している。Task 2でクライアントのキャッシュキーに使う際に前提が変わるなら、そこで固定テストを足すべき。

## 懸念・引き継ぎ事項

1. **環境**: 作業中にmacOSの画面がロックされ、GUIのUnity EditorがEditorループごと停止した（CPU 0%・uloopが全コマンド180sタイムアウト）。
   `uloop` による検証はこの状態では一切不能。やむを得ずEditorをSIGTERMで終了し、`-batchmode -runTests` で検証した。
   **後続タスクも画面ロック中はuloop不可**。バッチモード（本レポートのコマンド）が代替手段になる。
   なお私が起動したEditorは終了済みで、開始時点の状態（tree1のEditorは未起動）に戻っている。
2. **Unityの取り込み不具合**: 新規.csがコンパイル対象に入らないことがある（上記CS0246）。
   `削除→Refresh→再作成→Refresh` で回復する。後続タスクで新規ファイルを足す際に再発しうる。
3. **Task 2への申し送り**: チャンク単位は `TerrainTransferMeta.ChunkByteSize`（256KB）を必ず参照すること。
   `TerrainChunkTotal` は現状 `terrain/` 直下の全ファイル総バイト数からの切り上げで算出しているため、
   Task 2の論理ストリーム（タイル順に height→biome を連結）の総バイト数と一致していなければならない。
   タイルごとのファイル以外を `terrain/` に置くとこの前提が崩れる。
