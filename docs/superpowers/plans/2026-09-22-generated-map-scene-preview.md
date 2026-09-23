# Generated Map Scene Preview Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モードは同スキルの規模ゲートで決める。ステップはチェックボックスで記録する。

**Goal:** 現行ゲームの生成マップをPlayせず専用の一時シーンで確認し、閉じて元の編集作業へ戻れるようにする。

**Architecture:** EditorのPreviewSceneStageが一時資源を所有する。DefaultGeneratedWorldProvisionerで一時ワールドを用意し、WorldTerrainSessionと既存Terrain描画部品で全域を表示する。マップ配置は既存MapAuthoringと共有する編集用配置部品を使う。

**Tech Stack:** Unity 6、UnityEditor.SceneManagement、UniTask、Addressables Editor/AssetDatabase、既存Game.MapGeneration.Facade。

**Baseline:** origin/master `06f330346`（2026-09-22 fetch確認）。設計bd: `moorestech-uvrb`。実装は同タスクの子タスクを作成してclaimする。

## Requirements

- R1: メニューから専用の一時シーンを開く。生成開始から表示完了までEditorApplication.isPlayingはfalse。Playへの遷移を前提にしない。
- R2: 現在のゲームと同じ既定seed、マスタ、生成コードで全域を表示する。高さ・テクスチャ・草・木・岩・露頭を含め、配置の位置/回転/スケールを保つ。
- R3: Sceneビューで移動、回転、俯瞰、選択ができる。初回はスポーン周辺へ視点を合わせ、全体を見る操作も用意する。
- R4: 「生成／再生成」「閉じる」を提供する。再生成はマスタを読み直す。同時生成は1回だけとし、操作が拒否された理由をConsoleへ出す。
- R5: 閉じると元の編集コンテキストへ戻る。既存シーンのオブジェクト、dirty状態、アクティブシーン、保存済みセーブ、マスタの内容が変わらない。
- R6: 生成物は保存しない。再生成、閉じる、コンパイルによるドメインリロード、Play移行、エディタ終了で一時シーン/生成TerrainData/専用一時ファイルを片付ける。共有キャッシュや既存アセットを消さない。
- R7: 一部の配置物が欠けたら、欠損数と理由を表示し「完了」としない。地形構築失敗/キャンセルでも閉じる・再生成が可能で、失敗した部分生成物が後の表示に混ざらない。
- R8: 生成設定の編集UIと生成物保存は対象外（ユーザー裁定）。任意seed、既存セーブ再現、試遊導線、自動再生成、新しい地下資源/バイオーム検査表示は追加しない（agent前提）。通常ゲームの起動・MapAuthoring Import/Exportを維持する。

## Global Constraints

- ADR [0068](../../adr/0068-generated-map-scene-preview.md)と[0025](../../adr/0025-generation-system-exposes-results-only.md)を読む。ユーザー裁定とagent前提を混同しない。
- 実装専用worktreeは `moores-wt new feat/generated-map-scene-preview --from design/generated-map-scene-preview` で作る。既存の設計worktreeや他Editorを再利用/停止しない。
- 全.csは200行以下、各新規ディレクトリのコードは10ファイル以下。partial、Func、既定引数、イベント用Actionは禁止。主要セクションは日英2行コメント。
- EditorコードはEditorアセンブリに置き、#if UNITY_EDITORで囲む。Scene/Prefab/TerrainData/.metaは手編集しない。Unity Editor経由でのみ生成する。
- Library削除禁止。コード編集後は自分のworktreeのUnityでcompile必須。レビュー/QAの実測前にチェックを完了にしない。
- Game.MapGenerationのPipeline/Config/CacheへClient/Editorから直接依存しない。WorldTerrainSessionとDefaultGeneratedWorldProvisionerの既存境界を使う。
- キャッシュは既存生成処理に任せる。今回の一時生成はユーザーのセーブディレクトリを渡さない。生成バージョンとセーブ形式は変更しない。
- try/finallyによる所有資源の解放を行う。catchは外部JSON/ディスクIO/アセットロード境界に限定し、理由をLogErrorし、結果に失敗を残す。内部不具合を成功へ変換しない。

## 配置と前例

データフロー: Editor操作 → 既存既定ワールド生成 → 公開生成結果 → 共通Terrain組み立て/編集用配置 → 一時Stage。
新設部分の役割は「生成結果の読み手」。ゲームの状態機械、通信、プレイヤー、採掘状態、フレーム更新を開始しない。

| 対象 | 所属 | 責務/機構 | 前例 |
|---|---|---|---|
| ITerrainAssetLoader / RuntimeTerrainAssetLoader | Client.Game | 地形描画用アセット取得の差し替え点 | TerrainLayerAssetLoader/DetailPrototypeAssetResolver |
| 既存TerrainLayerAssetLoader/DetailPrototypeAssetResolverへのoverload | Client.Game | 同じ順序と同じ設定を別のアセット取得元で組み立てる | 既存の両クラス |
| TerrainMaterialAssetLoader | Client.Game | 現行Terrain用マテリアルのアドレスと取得 | TerrainRuntimeBuilderの既存private定数を移す |
| TerrainDataAssemblerへのAssembleIntoAsync | Client.Game | 所有者が確保したTerrainDataに既存変換を適用 | 既存AssembleAsync |
| EditorTerrainAssetLoader / MapObjectPrefabPlacement | Client.MapScene.Editor | 編集中の実アセット参照、姿勢を保つPrefab配置 | MapAuthoringImporter |
| GeneratedMapPreviewStage/Window | Client.MapScene.Editor | 編集コンテキスト切替と操作 | MapAuthoringWindow、Unity PreviewSceneStage |
| GeneratedMapPreviewRun / World / Terrain / Content | Client.MapScene.Editor | 1走行、一時ワールド、表示、資源所有 | WorldSnapshotBundler、TerrainRuntimeBuilder |
| 新規テスト | Client.Tests | EditModeで資源/配置/副作用を検査 | 既存TerrainDataAssemblerGateTest |

新アセンブリを `Editor/MapScene/Client.MapScene.Editor.asmdef` に置く。includePlatformsはEditorのみ、autoReferenced=true。参照: Client.Game、Client.Common、Core.Master、Game.Map.Interface、Game.MapGeneration.Contract、Game.MapGeneration.Facade、Game.Paths、Server.Boot、Mod.Config、Mod.Loader、UniTask、Unity.Addressables.Editor。Newtonsoft.Jsonは既存の導入形式を使う。predefined Assembly-CSharp-Editor側のMapAuthoringImporterからこのアセンブリは参照可能。逆向きの参照は作らない。

共有部品を利用する案を採用する。ゲームを一瞬Playして停止する案はR1違反。MapMakingの生成器を複製する案はADR0025とR2違反なので実装候補にしない（ユーザーが棄却した案としては記録しない）。

## Task 1: 描画資産の取得と編集用Prefab配置を共有する

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Assets/ITerrainAssetLoader.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Assets/RuntimeTerrainAssetLoader.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Assets/TerrainMaterialAssetLoader.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/TerrainLayerAssetLoader.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/DetailPrototypeAssetResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/TerrainDataAssembler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/TerrainAlphamapApplier.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/TerrainRuntimeBuilder.cs`
- Create: `moorestech_client/Assets/Scripts/Editor/MapScene/Client.MapScene.Editor.asmdef`
- Create: `moorestech_client/Assets/Scripts/Editor/MapScene/EditorTerrainAssetLoader.cs`
- Create: `moorestech_client/Assets/Scripts/Editor/MapScene/MapObjectPrefabPlacement.cs`
- Modify: `moorestech_client/Assets/Scripts/Editor/MapAuthoring/MapAuthoringImporter.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Tests.asmdef`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/MapPreview/MapObjectPrefabPlacementTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/MapPreview/TerrainAssetLoaderTest.cs`

**Interfaces:**

```csharp
// namespace Client.Game.InGame.Environment.Terrain.Assets
public interface ITerrainAssetLoader
{
    UniTask<T> LoadAsync<T>(string address, CancellationToken cancellationToken)
        where T : UnityEngine.Object;
}
// RuntimeTerrainAssetLoader : ITerrainAssetLoader
// TerrainMaterialAssetLoader
public static UniTask<Material> LoadAsync(ITerrainAssetLoader assets, CancellationToken cancellationToken);
// 以下は既存Build名前空間のクラスへの追加overload。元シグネチャもruntime用wrapperとして残す。
public static UniTask<TerrainLayer[]> LoadAsync(
    IReadOnlyList<string> addresses, ITerrainAssetLoader assets, CancellationToken cancellationToken);
public static UniTask<List<DetailPrototype>> ResolveAsync(
    IReadOnlyList<DetailPrototypeSpec> specs, ITerrainAssetLoader assets, CancellationToken cancellationToken);
// TerrainDataAssembler: 呼び出し元がTerrainDataの寿命を所有する。
public static UniTask AssembleIntoAsync(TerrainData terrainData, WorldTerrainLayout layout,
    BakedTerrainTile tile, IReadOnlyList<DetailPrototype> prototypes, TerrainLayer[] layers,
    CancellationToken cancellationToken);
// TerrainAlphamapApplierへのoverload。元APIはCancellationToken.Noneのwrapper。
public static UniTask ApplyAsync(TerrainData data, TerrainLayer[] layers,
    BakedTerrainTile tile, CancellationToken cancellationToken);
// namespace Client.MapScene.Editor
// EditorTerrainAssetLoader : ITerrainAssetLoader（ctorでAddressables設定のaddress→AssetPathを作る）
// MapObjectPrefabPlacement
public static GameObject Instantiate(MapObjectInfoJson info, GameObject prefab, Transform parent);
```

- [ ] 既存のloader本体をoverloadへ移し、ロード箇所だけ `assets.LoadAsync<T>(address, cancellationToken)` にする。元のwrapperは `new RuntimeTerrainAssetLoader()` と `CancellationToken.None` を渡す。列順・全DetailPrototypeパラメータ・失敗検査をそのまま保つ。
- [ ] RuntimeTerrainAssetLoaderはAddressableLoader.LoadAsyncDefaultへ委譲する。Materialの定数 `Vanilla/Environment/Terrain/TerrainLitMaterial` とnull時の例外はTerrainMaterialAssetLoaderへ移し、TerrainRuntimeBuilderもそこを呼ぶ。新しい材質やフォールバックは導入しない。
- [ ] EditorTerrainAssetLoaderはAddressableAssetSettingsDefaultObject.Settings.GetAllAssetsでフォルダ展開込みの一覧を得て、addressの完全一致でAssetPathへ解決する。重複addressは例外、未解決アセットはaddress/typeを含めた例外とする。AssetDatabaseで取得するためAddressablesハンドルを増やさない。返すのは借用アセットで、呼び手はDestroyしない。

```csharp
public UniTask<T> LoadAsync<T>(string address, CancellationToken cancellationToken)
    where T : UnityEngine.Object
{
    cancellationToken.ThrowIfCancellationRequested();
    if (!_paths.TryGetValue(address, out var path))
        throw new InvalidOperationException($"Preview asset address is missing: {address}");
    var asset = AssetDatabase.LoadAssetAtPath<T>(path);
    if (asset == null)
        throw new InvalidOperationException($"Preview asset is missing: {address}, {typeof(T).Name}, {path}");
    return UniTask.FromResult(asset);
}
```

- [ ] TerrainDataAssemblerは現行のValidate/ApplyHeightmap/ApplyAsync/ApplyDetailをAssembleIntoAsyncへ移す。元のAssembleAsyncはnew TerrainDataしてCancellationToken.Noneで呼び、成功した物を返す。例外経路ではtry/finallyでそのnewしたデータだけをDestroy（EditModeはDestroyImmediate）する。新overloadは渡されたデータをDestroyしない。previewは確保直後に所有リストへ加え、途中失敗でも回収する。
- [ ] TerrainAlphamapApplierはtoken付きoverloadへ本体を移し、各planeの先頭・UniTask.Yield直後・DirtyTextureRegion直前にThrowIfCancellationRequestedを入れる。Assemblerも入力検査前とApplyDetail前に確認する。OnCloseStageは必ずCancelしてからTerrainDataを破棄するので、次の継続は破棄済みnative objectへ触る前に止まる。tokenを外側のタイル間だけで検査する実装は禁止。
- [ ] MapObjectPrefabPlacementへMapAuthoringImporterのInstantiate・transform・MapObjectGameObject検査・SetRuntimeIdentityを移す。失敗時はID/GUIDをLogError、生成したinstanceをDestroyImmediateしてnullを返す。Importerは戻り値が非nullのときだけimportedCountを増やす。既存のprefab解決/鉱脈/スポーン/Exportは維持する。

```csharp
var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
instance.transform.SetPositionAndRotation(info.Position, info.Rotation);
instance.transform.localScale = info.Scale;
var component = instance.GetComponent<MapObjectGameObject>();
if (component == null)
{
    Debug.LogError($"Map object prefab root missing: {info.MapObjectGuidStr}");
    UnityEngine.Object.DestroyImmediate(instance);
    return null;
}
component.SetRuntimeIdentity(info.InstanceId, info.MapObjectGuidStr);
return instance;
```

- [ ] Tests.asmdefにClient.MapScene.Editor参照を追加する。テストは任意位置/非単位スケール/非identity回転/既存親transformのfixtureで姿勢復元を検査。アセットはテストが作った一時AssetsフォルダだけをEditor APIで作成/削除する。loaderは材質/木prefab/草テクスチャについて実addressの解決を検査する。欠損と重複は失敗が観測できること、借用アセットが破棄されないことを確認する。
- [ ] compile、既存Terrain関連テストと新規2クラス、MapAuthoring Import/Exportの既存回帰を実行しコミットする。

## Task 2: EditModeの専用一時ステージへ全生成マップを表示する

**Files:**
- Create: `moorestech_client/Assets/Scripts/Editor/MapScene/Preview/GeneratedMapPreviewWindow.cs`
- Create: `moorestech_client/Assets/Scripts/Editor/MapScene/Preview/GeneratedMapPreviewStage.cs`
- Create: `moorestech_client/Assets/Scripts/Editor/MapScene/Preview/GeneratedMapPreviewRun.cs`
- Create: `moorestech_client/Assets/Scripts/Editor/MapScene/Preview/GeneratedMapPreviewWorld.cs`
- Create: `moorestech_client/Assets/Scripts/Editor/MapScene/Preview/GeneratedMapPreviewTerrain.cs`
- Create: `moorestech_client/Assets/Scripts/Editor/MapScene/Preview/GeneratedMapPreviewContent.cs`
- Create: `moorestech_client/Assets/Scripts/Editor/MapScene/Preview/GeneratedMapPreviewState.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/MapPreview/GeneratedMapPreviewLifecycleTest.cs`

**Interfaces (namespace Client.MapScene.Editor):**

```csharp
public enum GeneratedMapPreviewState { Empty, Generating, Ready, Failed, Closing, Closed }
// Window: EditorWindow, menu "moorestech/Generated Map Preview"
// Stage: PreviewSceneStage、StageUtilityがOnOpenStage/OnCloseStageを呼ぶ
public GeneratedMapPreviewState State { get; private set; }
public string StatusText { get; private set; }
public void Regenerate();
public void FrameSpawn();
public void FrameAll();
// Run: 1生成の所有者。生成中の再使用はしない。
public int ExpectedMapObjectCount { get; private set; }
public int CreatedMapObjectCount { get; private set; }
public int MissingMapObjectCount => ExpectedMapObjectCount - CreatedMapObjectCount;
public UniTask ExecuteAsync(Scene scene, CancellationToken cancellationToken);
public void Dispose();
// World: IDisposable、一時ディスク領域の所有者
public static GeneratedMapPreviewWorld Create(string serverDataDirectory);
public WorldTerrainSession TerrainSession { get; }
public MapInfoJson Map { get; }
public void Dispose();
// Terrain: 既存結果→表示を進める
public static UniTask BuildAsync(TiledTerrainSession session, GeneratedMapPreviewContent content,
    ITerrainAssetLoader assets, CancellationToken cancellationToken);
// Content: IDisposable、rootと生成TerrainDataのみ所有、借用アセットは所有しない
public GeneratedMapPreviewContent(Scene scene);
public Transform Root { get; }
public TerrainData CreateTerrainData();
public void Dispose();
```

- [ ] Windowを開くだけでは生成しない。生成ボタンでCreateInstance<GeneratedMapPreviewStage>()→StageUtility.GoToStage(stage,true)→Regenerateを呼ぶ。既存の同型Stageが開いていればそのRegenerateを使い、別Stageを積み重ねない。GUIは状態と失敗内容を表示する。未完了時は生成ボタンを無効、閉じるは有効とする。
- [ ] Stageはbase.OnOpenStageを呼び、作業開始前にMainStageから入る。Prefab編集中はユーザーの編集を勝手に閉じず「Prefab編集を閉じてから生成してください」と理由を表示/ログして拒否する。閉じるはStageUtility.GoToMainStage。PlayMode遷移/assembly reload/editor quitも同じ終了経路へ集約する。
- [ ] Regenerateは前回runをDisposeして新runを作る。Stage内でCancellationTokenSourceを所有し、OnCloseStageでCancel→run.Dispose→base.OnCloseStage。awaitから戻るたびにtokenを確認し、閉じたSceneへObjectを追加しない。終了後のcontinuationはstateをReadyへ書き戻さない。Disposeは複数回呼んでも他資源を壊さない。
- [ ] World.Createはマスタを既存MapAuthoringImporterと同じ順序で読み、専用の一意なtmp rootをWorldDataDirectory.FromWorldRootに渡す。指定rootを先にDirectory.CreateDirectoryしない（既存EnsureWorldはworld.json無しのRootを破損とみなす）。一意性にはGuidを使う。専用領域はプロジェクトのTemp/GeneratedMapPreview配下とし、Libraryやセーブ領域を使わない。

```csharp
var resources = new ModsResource(ServerConst.CreateServerModsDirectory(serverDataDirectory));
MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(resources)));
var root = Path.Combine(Path.GetDirectoryName(Application.dataPath),
    "Temp", "GeneratedMapPreview", Guid.NewGuid().ToString("N"));
var files = WorldDataDirectory.FromWorldRoot(root);
DefaultGeneratedWorldProvisioner.EnsureWorld(files, serverDataDirectory);
var map = JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(files.MapJsonFilePath));
var session = WorldTerrainSession.Open(TerrainTransferMetaReader.Read(files), serverDataDirectory);
```

  Create内のディスク/JSON境界はcatch根拠を明記し、失敗をログして呼び出し元へ伝える。所有権を返す前に失敗した場合も作成済みrootとfiles.ProvisioningTempDirectoryを片付ける。Dispose時も削除対象はこの2パスだけ。共有キャッシュやsnapshotは生成側所有として残す。プロセス強制終了で残る専用tmpは次の起動時にこのツールの専用領域だけ回収し、他Editorの生存中走行を消さないようプロセスごとに区切る。

- [ ] Contentはpreview Scene内にRootを移してから子を作る。アセットのPrefab、Material、TerrainLayerは借用し、所有リストにはnew TerrainDataだけを追加する。DisposeはRoot→生成TerrainDataの順でDestroyImmediateし、リストをclearする。手編集したオブジェクトやMainStageをGameObject.Findで探索/削除しない。
- [ ] Terrain.BuildAsyncはTiledTerrainSession.Layoutの全TileCoordinatesを列挙する。loaderでlayer/detail/materialを1回取得し、タイルごとに次を実行。全件後にTerrainNeighborLinker.Linkを呼ぶ。地形変換、草密度、原点、マテリアル、detailObjectDistance/Densityは既存値を使う。

```csharp
cancellationToken.ThrowIfCancellationRequested();
var tile = session.BakeTile(tileX, tileZ);
var data = content.CreateTerrainData();
await TerrainDataAssembler.AssembleIntoAsync(data, layout, tile, prototypes, layers, cancellationToken);
cancellationToken.ThrowIfCancellationRequested();
var terrain = TerrainObjectFactory.Create(content.Root, $"Terrain_{tileX}_{tileZ}",
    tile.ScenePosition, data, material, layout.DetailObjectDistance, layout.DetailObjectDensity);
terrains.Add(new Vector2Int(tileX, tileZ), terrain);
```

- [ ] Map.MapObjectsをそのまま列挙しMasterHolder.MapObjectMasterからaddressを解決、EditorTerrainAssetLoaderでPrefabを取得、MapObjectPrefabPlacement.Instantiateで配置する。同GUIDのPrefab参照と解決失敗を1走行内でキャッシュする。位置や密度を再計算しない。MapObjectのInitialize(snapshot)、登録簿、距離カリング、採掘/通信は呼ばない。50件ごとにUniTask.Yieldし、直後にtokenを確認する。現在のUniTask PlayerLoopHelperはEditorApplication.updateでEditModeのyieldを進める（Library/PackageCache内を調査済み）。
  外部アセットの解決失敗はGUIDごとの境界でログし、欠損を同GUIDの配置件数として集計する（キャンセルは欠損に数えず伝播）。Instantiateのroot-component欠損も個体数へ加算する。全件の欠損集計後、0ならReady、1以上ならFailedとして部分生成物を破棄し、StatusTextに期待数/作成数/欠損数を残す。
- [ ] Map.DefaultSpawnPointJson.Positionに名前付きSpawnPointを置く。ユーザーが選択して位置を確認でき、FrameSpawnでその周辺をSceneView.LookAtへ渡す。FrameAllは全TerrainのBoundsを合成する。Lightを一時Sceneへ作り、SceneView.sceneLighting=trueで表示する。元のSceneView設定/カメラ状態を取得し、閉じると復元する。RenderSettingsを主シーンへ書き込まない。
- [ ] stage内のstatusはenumを唯一の判別子とする。実行中に閉じた場合はClosing/Closedへ遷移し、通常失敗はFailedと理由、欠損は期待数/表示数/欠損数を保持する。欠損はConsoleへ個別の原因を出し、Readyにはしない。通常失敗後はrun.Disposeして再生成可能に戻す。root/contentのnull有無で同じstateを別管理しない。
  Runの件数はMapの全件数と実際のInstantiate成功数から記録する。StageはExecuteAsync完了後にMissingMapObjectCountを読み、0ならReady、それ以外はFailedと件数のStatusTextにする。キャンセル/内部例外はfinallyでstateがGeneratingのままならFailedへ戻してrunを破棄し、例外は最外側のUniTask.ForgetでConsoleへ伝える。閉じたStageのClosedをfinallyで上書きしない。
- [ ] LifecycleTestは小さなSceneとTerrainDataをContentへ渡し、Disposeの二重呼び出し、再作成、無関係Sceneの保持を実測する。Stageを開閉してdirty/アクティブScene/SceneViewカメラの復元を確認する。生成待ち中のCloseとassembly reload/Play移行はTask3で実機確認する。
- [ ] 自分のEditorでcompileと新規LifecycleTestを実行しコミットする。

## Task 3: 実マスタでEditModeの表示と資源寿命を検証する

**Files:**
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/MapPreview/GeneratedMapPreviewIntegrationTest.cs`
- Create: `docs/development/generated-map-preview.md`

**Interfaces:** Consume Stage.Regenerate/FrameSpawn/FrameAll、World.Create、Content.Dispose。新しいproduction公開口をテスト専用には増やさない。公開Editor機能とUnity標準のStage/Scene APIから観測する。

- [ ] 小fixtureのテストコードでは、開いたSceneの保持と生成TerrainDataの破棄を最低限次の形で検査する。fixtureのStageへ所有されるSceneを渡し、finallyでStageを閉じる。

```csharp
var originalScene = SceneManager.GetActiveScene();
var wasDirty = originalScene.isDirty;
var content = new GeneratedMapPreviewContent(previewScene);
var data = content.CreateTerrainData();
Assert.That(data == null, Is.False);
content.Dispose();
content.Dispose();
Assert.That(data == null, Is.True);
Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(originalScene));
Assert.That(originalScene.isDirty, Is.EqualTo(wasDirty));
```

- [ ] 実マスタのintegrationはpreview公開入口を呼び、ReadyまたはFailedへ至るまでEditModeで待つ（上限600秒）。Readyを要求し、Terrain数をWorldTerrainSession.Layout.TileCoordinates.Countと比較、MapObjectの全GUID/位置/rotation/scaleを同じ生成ワールドのmap.jsonと比較する。1件以上の木/岩/露頭はmasterの区分から存在を確認する。実データで0件なら成功扱いにせずfixture条件を報告する。
- [ ] 全タイル境界のneighborリンク、terrainLayersの順序、各detail layerの密度配列、材質shaderの対応を比較する。高さは生成出力とTerrainData.GetHeightsを同じ添字でサンプル比較する。GameViewのスクリーンショットだけでこの一致を代替しない。
- [ ] 2回再生成後のRoot/TerrainData数が増えないこと、閉じた後に生成物が残らないことを確認する。失敗用アセット参照をfixtureに与え、Failedと欠損理由が観測できること、正常入力に戻した再生成がReadyへ進むことを対で検査する。
- [ ] alphamapの最初のyield時点でキャンセルしてContent.Disposeし、残りのcontinuationを進めてもMissingReferenceExceptionが出ずOperationCanceledで終わることを検査する。0件の配置物はTerrainだけでReady、1件成功はReady、1件欠損はFailed→正常入力でReadyの最小ケースを含める。
- [ ] uloop-screenshotでSceneViewを撮影し、全体俯瞰・スポーン近景・草の地表・木/岩/露頭を確認する。EditorApplication.isPlaying=falseの証拠と、実際にUIから生成/再生成/閉じるした証拠を併記する。
- [ ] 生成途中のClose、生成途中のPlay移行、コンパイルによるdomain reload、Windowだけを閉じた後のStage終了を手動操作で確認する。同期生成中は途中キャンセルできないので、戻った直後の安全境界で止まることを確認する。永久にボタンがdisabledになる状態を残さない。
- [ ] 起動前後のユーザーセーブとマスタのhash、開いていたシーンのdirty/オブジェクト数/アクティブSceneが同じであることを確認する。共有キャッシュの新規生成は許容し、プレビュー用tmpと区別する。
- [ ] 共有Terrain描画部品の変更があるため、editmode-in-playing-testスキルで通常ゲーム初期化を1回通し、既存Terrain描画が壊れていないことを確認する。長いゲームプレイの変更は無いのでunity-playmode-recorded-playtestは今回不要。Playを使うのはこの回帰検証だけで、プレビュー成功の証明はEditModeで取る。
- [ ] 操作マニュアルへメニューの場所、再生成で現在マスタを読むこと、一時表示なので保存されないこと、失敗理由の場所を書く。結果はbd noteとprivate logsへ残し、実測していない項目を完了にしない。変更をコミットする。

Run（タスクごとに対象を絞る）:

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapPreview|TerrainDataAssemblerGateTest|TerrainAlphamap" --timeout-seconds 1500
```

Expected: compile ErrorCount=0、新規/既存対象テストPASS。domain reload中なら45秒待って同コマンドを再試行する。

## Task 4: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること

- [ ] moores-code-reviewの全系統を実行し、指摘を実コードと照合して修正する。記録はprivate logsへ置く。
- [ ] 判定/キャンセル/評価時点に触れた修正後はTask3のEditMode確認とEditModeInPlayingTestを反映後のバイナリで再実行する。
- [ ] 全作業をコミットする。

## Task 5: セッション終了可能状態にすること

- [ ] pr-createでPRを作成する。masterとの競合があれば同スキル経由でmasterをmergeして解消、compile/対象回帰を確認してpushする。
- [ ] 全作業がcommit/push済み、PRがレビュー可能な状態であることを確認する。外部repo変更は想定しないが、必要になった場合は外部repoのPRとpush済みpinが必須。
- [ ] PR作成直後に自分のworktreeをmoores-wt rmで片付ける。設計worktree/他Editorには触らない。

## 既存操作の死活表

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 通常ゲーム起動の生成地形表示 | 維持 | 既存APIはruntime loaderへのwrapperとして残し回帰検証 |
| MapAuthoring Importの姿勢/ID復元 | 維持 | 共通placementへ移設しfixtureで比較 |
| MapAuthoring Export/鉱脈編集 | 維持 | 非変更、Importの結果形は同じ |
| MainStageの編集/シーン保存 | 維持 | PreviewSceneStageで分離、前後状態を検査 |
| Prefab編集中の作業 | 維持 | 勝手にStageを閉じない、プレビュー開始だけ理由付き拒否 |

## 判断記録（ADR）

- ユーザー裁定: [ADR0068](../../adr/0068-generated-map-scene-preview.md)、[蒸留記録](../../../.decisions/2026-09-22-生成マップはPlay不要の一時シーンプレビューで確認する.md)。原文「tmp/MapMaking にあるみたいに、生成マップのプレビューをシーンでチェックできるようにしてほしい」、回答「1」「一時プレビューでok」「1、エディットモードで確認できるように」。専用一時シーン/EditMode/閲覧を外してはいけない。
- 独立質問generate→filter: 14問中5問がユーザー裁定で解決。残9問は追加機能/詳細スコープを問うもので、「自明すぎる質問しないで」に従って再提示せずADRのagent前提として保持した。全問が裁定済みとは記録しない。
- agent前提: PreviewSceneStage、手動Generate、既定生成条件/全域、通常景観の照明、SceneViewのFrame操作を採る。新しいゲームプレイ/マスタ編集/セーブ形式は追加しない。
- agent前提: アセットロード差し替えはITerrainAssetLoaderへ閉じ、既存の草設定と地表レイヤー組み立てを複製しない。AssetDatabase側は既存アセットを借用する。
- agent前提: TerrainDataの所有はContent。AssembleIntoAsyncは変換のみ。これで閉じる/失敗のタイミングでも確保済み資源を列挙できる。
- agent前提: MapObjectPrefabPlacementを既存Importerと共用し、生成配置の姿勢/ID設定を二重実装しない。ランタイム登録/採掘UI初期化は閲覧に不要。
- agent前提: 通常Terrain描画部品にも触るためEditModeInPlayingTestを含める。入力/ゲームプレイ変更ではないためunityプレイ録画テストは省略する。
- Self-Review: R1/R3/R4はTask2、R2はTask1/2/3、R5/R6/R7はTask2/3、R8はTask1/3と死活表でカバー。全新規型の配置、アセット借用と生成物所有、0/1件・失敗後再試行、外部境界の理由ログを照合した。
- 構造レビュー（2026-09-22、fresh-context gpt-6-astra）: 検査5〜7は強0/弱0/第3バケツ0。別枠の実コード照合で、途中Close時にalphamapのyieldが破棄後へ再開する欠陥を指摘。AssembleIntoAsync→TerrainAlphamapApplierへtokenを貫通し、内部yield直後の停止と回帰テストを追加して解消した。
- user-simulator review（2026-09-22、gpt-6-astra）: 上記修正後の残存Critical/Warning/要裁定は0。過大な基盤化、生成境界、既存Prefab配置の前例を確認。実機のStage/描画/Play移行/domain reloadは未実測でありTask3の実施が必要。予測のユーザー的中評価はまだ得ていない。
