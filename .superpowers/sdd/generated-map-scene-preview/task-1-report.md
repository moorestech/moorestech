# Task 1 report: 描画資産と編集用 Prefab 配置の共有

Status: DONE

Worktree: `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/feat-generated-map-scene-preview`

## 実装

- `ITerrainAssetLoader` と runtime 実装を追加し、runtime は従来の `AddressableLoader.LoadAsyncDefault` へ token を渡して委譲した。
- TerrainLayer と DetailPrototype の従来 API を runtime wrapper として残し、新 overload へ本体を移した。層順、全 DetailPrototype 設定、未解決時の例外を維持した。
- Terrain 材質の address と欠損時例外を `TerrainMaterialAssetLoader` に集約し、runtime builder も同じ材質を取得する。
- `EditorTerrainAssetLoader` は `GetAllAssets(entries, false)` でフォルダ展開を含む登録を snapshot し、Ordinal の完全一致で `AssetDatabase.LoadAssetAtPath` へ解決する。返す資産は借用で、Addressables handle は作らない。
- TerrainData 組立に caller 所有の `AssembleIntoAsync` を追加した。従来 API は自身の `new TerrainData` の失敗時だけ finally で破棄する。Into overload は渡されたデータを破棄しない。
- Alphamap の開始時、各 plane の先頭、Yield 直後、DirtyTextureRegion 直前に cancellation を検査する。Assembler は入力検証前と Detail 適用前にも検査する。
- PrefabUtility による編集用配置、世界姿勢・local scale 復元、root component 検証、identity 注入を `MapObjectPrefabPlacement` へ移した。root 不備は ID/GUID を LogError し、作った instance だけを破棄して null を返す。Importer は成功分だけを加算する。
- 新 Editor asmdef と Tests 参照を追加した。既存 Prefab 解決・鉱脈・スポーン・Export の処理は維持した。

## 計画と現データの差異・controller 裁定

1. 全登録を constructor で重複拒否すると、既存の `Vanilla/Skit/Animations/anim_think01` 重複により preview 全体が開けないことを実テストで発見した。
   - `Vanilla Asset Group.asset` の GUID `074122ef0fe264b54bfa5cc04a18dca8` と `69ab4ea6e4ac2471195dc1329cf399c5` が同じ address を持つ。
   - controller は、構築時に重複候補を記録し、`LoadAsync` で要求された address が重複している場合に address/type/候補パスを含む例外を投げる変更を承認した。
   - 曖昧な address を実際に解決することはない。既存登録は変更していない。要求された重複の拒否と、無関係な重複があっても一意の資産を読めることを両方テストした。
2. 計画の「実 address の草テクスチャ」は現登録に存在しない。全 group の GUID と Assets を突合した結果、草 Detail は Prefab で、画像 Texture2D の登録は 0 件だった。
   - controller 承認により実材質、実木 Prefab、実草 Prefab の解決と、fixture 専用 Texture2D の登録・フォルダ展開・解決で検証した。
   - 実検証 address は `Vanilla/Environment/Terrain/TerrainLitMaterial`、`Vanilla/Environment/Tree/Base/BirchTree02`、`Vanilla/Environment/Terrain/Detail/Redwood/Grass1`。
3. 既存 MapAuthoring Import/Export の NUnit 回帰は全 repo の `*Test*.cs` 検索で見つからなかったため、実装済み Import/Export を一時シーン・一時 JSON で実行した。

## 検証結果

最終コードで実行した focused 検証と、変更後に通過済みの既存回帰を合わせて **32/32 passing、skip 0**。

| 検証 | 結果 |
| --- | --- |
| Unity compile | Success=true、ErrorCount=0、WarningCount=16 |
| 新規 MapPreview 2 クラス | 12/12 pass、0 fail、0 skip。完了 2026-09-21T15:56:14Z |
| 既存 Client.Tests.UnitTest.Terrain 全クラス | 20/20 pass。最初の合同 run の同クラス結果を XML で確認 |
| Import→Export 動的回帰 | PASS。一時シーンと JSON を finally で片付け、元の active scene を復元 |
| git diff --cached --check（.meta 除外） | pass。Unity 生成 .meta には既定の空欄末尾スペースがあるため手編集せず保持 |
| 変更 C# の行数 | 全ファイル 200 行以内 |
| Addressables group の git diff | なし |

実行コマンド（project-path は全て指定 worktree の client 絶対パス）:

```text
uloop compile --project-path <worktree>/moorestech_client
uloop run-tests --project-path <worktree>/moorestech_client --filter-type regex --filter-value '^Client\.Tests\.UnitTest\.(Terrain|MapPreview)\.'
uloop run-tests --project-path <worktree>/moorestech_client --filter-type regex --filter-value '^Client\.Tests\.UnitTest\.MapPreview\.'
uloop execute-dynamic-code --project-path <worktree>/moorestech_client --code-file <task-1-evidence>/map-authoring-regression.cs
```

新規・拡充テストの観測点:

- 回転・非単位スケールを持つ親の下でも、world position/rotation、local scale、Prefab リンク、ID/GUID を復元する。
- MapObject component が子にしか無い Prefab を拒否し、ID/GUID を記録し、instance のみ破棄して借用 Prefab を保つ。
- 実資産の解決、フォルダ登録の子 Texture2D 解決、完全一致（大文字小文字）、欠損 address、型不一致、snapshot 後に消えた資産、借用資産の保持。
- 重複した要求 address の全候補を例外へ出し、無関係な重複では正常資産を止めない。
- TerrainLayer の列順と DetailPrototype の全 16 描画設定・mesh/texture 分岐・token の伝播。
- Alphamap の最初/最後の plane で Yield 中に cancel→TerrainData 破棄した後、MissingReference でなく OperationCanceledException で止まる。
- Assembler の既存 API は検証失敗で native TerrainData を漏らさず、Into は失敗でも caller 所有データを保持し、キャンセルを入力/native 操作より前に見る。
- Import→Export は位置、rotation、非単位 scale、Prefab リンク、GUID、Import ID=42、Export の再採番 ID=0、item/fluid 両鉱脈の境界、spawn を保持する。

失敗からの修正:

- 初回 compile は、この repo の NUnit に存在しない NonParallelizable 属性で 2 errors。属性を除去し compile が通ることを実測した。
- 最初の合同 run は 31 件中 22 pass / 9 fail。fail はすべて上記の既存重複 address。修正後、新規 12 件の再実行は 11 pass / 1 fail となった。
- 残った 1 件は、AssetDatabase import が Texture2D の作成時ラッパーを置き換える fixture の想定違い。現在の永続 asset と同一であることを比較するよう直し、12/12 pass を実測した。
- 既存 `ThrowsWhenAPrototypeAssetIsUnresolved` は EditMode で 146.259 秒かかったが自然完了して pass。途中の診断 snippet は非 public API 呼び出しの compile error で実行されておらず、テスト状態へ介入していない。
- 実装前の意図的な RED は実測していない。上記は実装後に観測した compile/test failures と修正結果である。

生の証拠（worktree 内のローカル資料）:

- `.superpowers/sdd/generated-map-scene-preview/task-1-evidence/final-compile.json`
- `.superpowers/sdd/generated-map-scene-preview/task-1-evidence/map-preview-tests.json`
- `.superpowers/sdd/generated-map-scene-preview/task-1-evidence/map-authoring-result.json`
- `.superpowers/sdd/generated-map-scene-preview/task-1-evidence/map-authoring-regression.cs`
- 初回合同 run XML: `moorestech_client/.uloop/outputs/TestResults/20260921_155048_6369880_794adc65e4cb479a98b6f3824f3025f9.xml`

## 変更ファイル

- 新規 `Client.Game/InGame/Environment/Terrain/Assets/`: `ITerrainAssetLoader.cs`、`RuntimeTerrainAssetLoader.cs`、`TerrainMaterialAssetLoader.cs`。
- 変更 `Client.Game/InGame/Environment/Terrain/Build/`: `TerrainLayerAssetLoader.cs`、`DetailPrototypeAssetResolver.cs`、`TerrainDataAssembler.cs`、`TerrainAlphamapApplier.cs`。
- 変更 `Client.Game/InGame/Environment/Terrain/TerrainRuntimeBuilder.cs`。
- 新規 `Editor/MapScene/`: `Client.MapScene.Editor.asmdef`、`EditorTerrainAssetLoader.cs`、`MapObjectPrefabPlacement.cs`。
- 変更 `Editor/MapAuthoring/MapAuthoringImporter.cs` と `Client.Tests/Tests.asmdef`。
- 新規 `Client.Tests/UnitTest/MapPreview/`: `MapObjectPrefabPlacementTest.cs`、`TerrainAssetLoaderTest.cs`。
- 拡充 `Client.Tests/UnitTest/Terrain/`: `Build/TerrainDataAssemblerGateTest.cs`、`TerrainAlphamapApplierTest.cs`、`DetailPrototypeAssetResolverTest.cs`。
- 上記の新規 source / asmdef / folder に対して Unity が生成した `.meta`。手作成はしていない。
- 本 report。plan checkbox、Task 2–5 の実装、PR は変更・作成していない。

## 自己レビュー・懸念

- lens digest と brief を照合した。runtime の役割等価な既存処理を共通化し、生成ファサードより奥への依存を追加していない。CancellationToken は loader と alphamap の非同期境界まで伝わる。borrowed asset と作成した instance/native data の所有権を分離した。
- Save/永続化チェックを再確認した。保存 schema、ID/GUID 表現、JSON 形式、マスタ永続化値に変更はない。save migration は不要。
- 最終 compile warnings 16 件は `UnitGenerator/UnitOfAttribute.cs` の重複型と既存 `EditModeInPlayingTestUtil.cs` の obsolete API。今回変更したファイル由来の warnings はない。
- `agent-runtime-compat/SKILL.md` は main/worktree/global skills に見つからなかった。controller へ報告し、uloop の該当 skill と CLI を直接使用した。
- Beads `moorestech-uvrb.1` は全実装の項目なので note を追記し、close は controller へ委ねる。Task 1 の未完了要件はない。
