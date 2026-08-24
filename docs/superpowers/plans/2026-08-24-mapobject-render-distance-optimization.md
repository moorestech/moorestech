# mapObject描画距離最適化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 通常mapObjectを現在描画中のカメラから350mで距離カリングし、遠景ランドマークを残しながらprobe・URP設定を含む実測済み描画最適化を1本の本repo PRへまとめる。

**Architecture:** mapObject masterに必須の`distanceVisibilityType`を追加し、表示役割の正本をデータへ置く。クライアントは`CameraManager`のUniRx通知とUnity `CullingGroup`の距離band通知をつなぎ、Rendererだけを340/350mヒステリシス付きで切り替える。probe設定はruntime個体ごとの初期化ではなくwrapper authoringへ焼き込み、既存URP設定コミットと同じPRで検証する。

**Tech Stack:** Unity 6000.3 C# / UniRx / UniTask / Unity CullingGroup / NUnit EditMode / uloop / YAML SourceGenerator / GitHub CLI

## Requirements

- R1. 全mapObject masterは必須enum `distanceVisibilityType`（`cullable` / `landmark`）を持つ — 受け入れ: SourceGenerator生成定数を参照する契約テストが通り、対象全JSONで欠損0
- R2. 出荷v8の`BigMesa_*`・`ThinMesa_*`・`StratMesaSharp_*`・`Boulders_*`・`BigBoulders_*`だけを`landmark`にし、その他は`cullable`にする — 受け入れ: master JSON監査でlandmarkが該当29 master種だけ、実行中シーンでは440個が除外
- R3. `cullable`は現在描画中のカメラから350m超でRendererを休止し、340m以内で再表示する — 受け入れ: 初期距離・band 0/1/2・カメラ切替の単体テストとunityプレイ録画テストが通る
- R4. 距離カリングはGameObject・Collider・破壊/HP同期・最寄り探索登録を無効化しない — 受け入れ: 非表示中もroot active・Collider enabledを保ち、破壊後は距離内へ戻してもRendererが復活しないテストが通る
- R5. `landmark`は距離カリングへ登録せず、遠距離でも既存LODで表示可能なままにする — 受け入れ: 400m配置のlandmark Rendererが有効な単体テストと実機確認
- R6. カメラ切替と距離band変化は通知駆動とし、毎フレームの同値ポーリングを追加しない — 受け入れ: `CameraManager.OnMainCameraChanged`がRegister/UnRegisterの最上位変化だけを通知し、visibility実装に`Update`/`LateUpdate`が無い
- R7. 一度に多数の表示変更が届いた場合は1ms時間予算でフレーム分散する — 受け入れ: controllerが既存`FrameTimeBudget`とUniTask Yieldでqueueを処理し、同一indexの未反映変更を集約する
- R8. 出荷v8の全195 mapObject addressable prefabに含まれる全RendererはLight Probe/Reflection ProbeがOff — 受け入れ: pinned master全mapObjectを横断するEditorテストが通る
- R9. 既存コミット`26d341c5c`のSSAO Source=Depth / AfterOpaque / Downsample、Shadow Distance=120、Cascades=2を維持する — 受け入れ: Unity実行時設定監査とProfiler再計測でDepthNormals prepassが消えている
- R10. 同一ゲーム状態で、mesa/岩山を残した350mカリングが基準15.7msに対して悪化せず、目安14.3ms前後・drawcall 4,496前後を再現する — 受け入れ: 適用後120フレーム中央値とdrawcallをBeads note・PR本文へ記録
- R11. 本repoでは上記を1PRへまとめ、masterデータ変更は`moorestech_master` companion PRへpushし、本repo pinをそのコミットへ更新する — 受け入れ: 両PRが作成され相互リンク済み

**やらないこと:**
- Renderer統合・BatchRendererGroup化
- MeshCollider簡略化・距離無効化
- mesa/岩山のimposter・追加LOD作成
- mapObjectの生成/破棄ストリーミング化
- 露頭・ブロック・キャラクターの描画距離変更

## Global Constraints

- partial・`Func<>`・プロダクションのデバッグ専用public・C# `event Action`は禁止
- 1ファイル200行以下・1ディレクトリ10ファイル以下。既存`MapObject/`と`Client.Tests/Map/`は各10ファイルなので新規ファイルは`Visibility/`へ置く
- 状態変化はUniRxまたは`CullingGroup`通知で受け、`Update()`の同値ポーリングを追加しない
- `distanceVisibilityType`は必須＋YAML default＋全JSON一括更新。`optional: true`・`?? Default`・ローダー補完は禁止
- `Mooresmaster.Model.*`生成物は手動編集しない。`VanillaSchema/map.yml`と`_CompileRequester.cs`を更新して生成する
- Prefab/Scene/ScriptableObjectはテキスト編集せず、Unity Editorまたは`uloop execute-dynamic-code`経由で保存する
- コメントは主要処理ごとに日本語・英語2行セット。複雑メソッドのローカル関数はメソッド末尾`#region Internal`内
- `.cs`変更後は`uloop compile --project-path ./moorestech_client`を必ず実行する
- テストは`uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<regex>"`。domain reload中は45秒待って再試行する
- 実装中に見つけたバグは再現するREDテストを先に追加してから修正する
- 本repoの既存ステージ済み`.moorestech-external-revisions.json`は、master companion commitのpinへ最終更新するまで保持する
- 全変更を通常コミットし、Squash and mergeを選ばない

## 配置と前例（spec-architecture-review対象）

| # | 項目 | 配置先 | 機構 | 役割同型の前例 |
|---|---|---|---|---|
| 1 | `distanceVisibilityType` | `VanillaSchema/map.yml` / 全master JSON | 必須enum | 同ファイルの`miningType`・`soundEffectType`、スキーマ必須化規約 |
| 2 | camera変化通知 | `Client.Common/CameraManager.cs` | private `Subject<IGameCamera>` + public `IObservable<IGameCamera>` | `GameStateController.OnStateChanged`のUniRx公開形。状態所有者自身が通知する |
| 3 | `MapObjectDistanceVisibilityController` | `Client.Game/InGame/Map/MapObject/Visibility/` | `CullingGroup` + queue + UniTask | `MapObjectInstantiationRunner`の時間予算分散、mapObject collaboratorの同一ドメイン配置 |
| 4 | `MapObjectRendererVisibility` | 同`Visibility/` | authored Renderer状態のcapture/restore | `MapObjectGameObject.DestroyMapObject`のRenderer制御を置換せず、距離表示だけを受動追加 |
| 5 | controller生成・camera購読 | `MapObjectGameObjectDatastore` | Construct時生成・UniRx購読 | datastoreがrunner/registry collaboratorを所有する既存構造 |
| 6 | 生成後visibility登録 | `MapObjectLayoutInstantiator` | registry成功直後に明示Register | 既存`MapObjectRegistry.TryRegister`と同じ生成完了境界 |
| 7 | probe authoring | `Editor/MapObjectWrapperGenerator/WrapperPrefabFactory.cs` + 195 prefab | Renderer propertyをUnityで保存 | wrapperのlayer/outline/ray targetをauthoring時に焼く既存責務 |

**データフロー地図:** `CameraManager.Register/UnRegister` → `OnMainCameraChanged` → `MapObjectDistanceVisibilityController.SetCamera` → Unity `CullingGroup`距離band → 時間予算queue → `MapObjectRendererVisibility.SetVisible` → Renderer。新規controllerはカメラ状態の読み手・Renderer表示の書き手で、採掘/同期/registry経路へ分岐を返さない。

**機能パリティ死活表:** 採掘=生きる（350m内でRenderer復帰、Collider常時）／破壊・HP同期=生きる（rootとcomponent常時active）／最寄り探索・ワールドピン=生きる（registry不変）／開幕スキット非表示=生きる（既存datastore root SetActiveが上位で優先）／後着生成=生きる（成功Register直後にvisibility登録）／視点・スキットcamera切替=生きる（CameraManager最上位通知）／outline・HPバー=生きる（authored Renderer状態を復元、親active制御を維持）。

---

### Task 1: master表示区分の必須契約と全JSON更新

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Map/Visibility/MapObjectDistanceVisibilityContractTest.cs`
- Modify: `VanillaSchema/map.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/map.json`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/map.json`
- Modify: `mooresmaster/mooresmaster.SandBox/TestMod/map.json`
- Modify: `mooresmaster/mooresmaster.SandBox/schema/map.json`
- Modify: `../moorestech_master/server_v4/mods/moorestechAlphaMod_4/master/mapObjects.json`
- Modify: `../moorestech_master/server_v5/mods/moorestechAlphaMod_5/master/mapObjects.json`
- Modify: `../moorestech_master/server_v6/mods/moorestechAlphaMod_6/master/mapObjects.json`
- Modify: `../moorestech_master/server_v7/mods/moorestechAlphaMod_7/master/mapObjects.json`
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json`

**Interfaces:**
- Produces: `MapObjectMasterElement.DistanceVisibilityType` / `DistanceVisibilityTypeConst.cullable` / `.landmark`
- Consumes: SourceGenerator入力`VanillaSchema/map.yml`

- [ ] **Step 1: RED契約テストを追加する**

```csharp
using Mooresmaster.Model.MapModule;
using NUnit.Framework;

namespace Client.Tests.Map.Visibility
{
    public class MapObjectDistanceVisibilityContractTest
    {
        [Test]
        public void 距離表示区分は通常と遠景ランドマークの二値を持つ()
        {
            Assert.AreEqual("cullable", MapObjectMasterElement.DistanceVisibilityTypeConst.cullable);
            Assert.AreEqual("landmark", MapObjectMasterElement.DistanceVisibilityTypeConst.landmark);
        }
    }
}
```

- [ ] **Step 2: REDを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `DistanceVisibilityTypeConst`未生成でコンパイル失敗

- [ ] **Step 3: schema末尾へ必須enumを追加しcompile markerを更新する**

```yaml
    - key: distanceVisibilityType
      type: enum
      default: cullable
      options:
      - cullable
      - landmark
```

`_CompileRequester.cs`の`dummyText`を新しいGUID文字列へ変更する。foreignKey追加ではないためValidator変更不要だが、`validate-schema` checklistでforeignKey差分0を確認する。

- [ ] **Step 4: 全JSONを機械更新する**

トップレベル`mapObjects`配列の各要素へ`distanceVisibilityType`を追加する。v8の名前が正規表現`^(BigMesa|ThinMesa|StratMesaSharp|Boulders|BigBoulders)_[0-9]+$`に一致する29 master種だけ`landmark`、その他は`cullable`。旧v4〜v7・test mod・sandboxは全件`cullable`。

更新対象はmaster定義の9ファイルだけとする。`server*/map/map.json`、`EditModeInPlayingTest/ServerData/map/map.json`、`TestMod/{ConfigOnly,ForUnitTest}/map/map.json`の`mapObjects`はmaster要素ではなく配置データであり、同名配列でもschema対象外なので変更しない。`generation.json`の入れ子`algorithmParam.mapObjects`も地形生成設定であり対象外。

- [ ] **Step 5: JSON全数監査とGREENを確認する**

Run: 明示した現行master 5ファイルと旧`mapObjects.json` 4ファイルを列挙し、jqで全要素のfield存在・許容値・v8 landmark 29件を検証する。同名の配置配列を拾う単純な`rg`結果は監査母集団にしない。

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectDistanceVisibilityContractTest"`
Expected: 1件PASS

- [ ] **Step 6: master repoを先行コミットする**

`../moorestech_master`で`perf/mapobject-distance-visibility`ブランチを`origin/master`から作成し、5ファイルをコミットする。

```bash
git commit -m "perf(map-object): 遠景ランドマーク表示区分を追加"
```

- [ ] **Step 7: 本repoのschema契約をコミットする**

既存のステージ済みpinを含めず、schema・compile marker・test mod/sandbox JSON・契約テストだけを明示stageする。

```bash
git commit -m "feat(master): mapObject距離表示区分を追加"
```

---

### Task 2: CameraManagerの最上位camera変化通知

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Common/CameraManagerTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Common/CameraManager.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Common/Client.Common.asmdef`

**Interfaces:**
- Produces: `public static IObservable<IGameCamera> OnMainCameraChanged`
- Consumes: `RegisterCamera(IGameCamera)` / `UnRegisterCamera(IGameCamera)`の既存stack変化

- [ ] **Step 1: REDテストを追加する**

テスト用`RecordingGameCamera`を同ファイルprivate classに置き、`Initialize→Subscribe→Register A→Register B→UnRegister B`で通知列がA/B/A、enabled状態が既存stack規則どおりになることを検証する。stack途中のAを外しても最上位Bの通知が増えないテストも分ける。

- [ ] **Step 2: REDを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `OnMainCameraChanged`が存在せず失敗

- [ ] **Step 3: UniRx通知を実装する**

`Client.Common.asmdef`へ`"UniRx"`を追加する。`CameraManager`はprivate `Subject<IGameCamera>`を所有し、最上位が実際に変わったRegister/UnRegister完了後だけ`OnNext(MainCamera)`する。公開は`IObservable<IGameCamera>`のみで、Subjectや変更窓口は公開しない。

- [ ] **Step 4: GREENを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "CameraManagerTest"`
Expected: 全件PASS

- [ ] **Step 5: コミットする**

```bash
git commit -m "feat(camera): 最上位カメラの変化を通知"
```

---

### Task 3: 通知駆動のmapObject距離カリング

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/Visibility/MapObjectRendererVisibility.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/Visibility/MapObjectDistanceVisibilityController.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Map/Visibility/MapObjectRendererVisibilityTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Map/Visibility/MapObjectDistanceVisibilityControllerTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectLayoutInstantiator.cs`

**Interfaces:**
- Produces: `internal sealed MapObjectRendererVisibility(MapObjectGameObject)` / `SetVisible(bool)`
- Produces: `internal sealed MapObjectDistanceVisibilityController(int capacity, CancellationToken)` / `SetCamera(Camera)` / `Register(MapObjectGameObject, bool isLandmark)` / `Shutdown()`
- Consumes: `CameraManager.OnMainCameraChanged`, `MapObjectMasterElement.DistanceVisibilityType`, `FrameTimeBudget`

- [ ] **Step 1: Renderer状態のREDテストを追加する**

enabled=true/falseの2 Rendererとenabled Colliderを持つrootを作り、hide後はRenderer両方falseでもroot active・Collider enabledを維持し、show後はRendererがtrue/falseへ復元することを検証する。`MapObjectGameObject.DestroyMapObject()`後のshowでは両Rendererがfalseのままを別テストで検証する。

- [ ] **Step 2: controllerのREDテストを追加する**

400mの通常個体はRegister直後に非表示、400mのlandmarkは表示維持、band 2でhide・band 1で維持・band 0でshow、`SetCamera`で基準cameraを切り替えると新camera距離へ揃うことを実GameObject/Cameraで検証する。同一indexへhide→showが同フレームに届いた場合は最新showだけが反映されるテストも置く。`ApplyDistanceBand(int index, int band)`はnative callbackとテストが共有するinternal入口にする。

- [ ] **Step 3: REDを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: visibility型が存在せず失敗

- [ ] **Step 4: Renderer visibilityを最小実装する**

constructorで`GetComponentsInChildren<Renderer>(true)`と各`enabled`初期値をcaptureする。`SetVisible(false)`は全Rendererをfalse、`SetVisible(true)`は`mapObject.IsDestroyed`なら何も復活させず、それ以外はcapture値へ戻す。同じvisible値の再適用は走査しない。

- [ ] **Step 5: CullingGroup controllerを実装する**

距離bandは`340f, 350f`、BoundingSphere半径は0、landmarkは未登録。band 0はshow、band 1は現在値維持、band 2はhide。pending indexごとに最新targetを上書きし、未queueなら1回だけenqueueする。processor開始時に一度`UniTask.Yield(cancellationToken)`して同フレームのnative callbackをbatchし、その後は1ms `FrameTimeBudget`ごとにYieldする。`Update`/`LateUpdate`は作らない。新camera設定時は全登録個体の初期距離を再評価する。

- [ ] **Step 6: datastoreとinstantiatorへ接続する**

`MapObjectGameObjectDatastore.Construct`でlayout総数capacityのcontrollerを生成し、`CameraManager.OnMainCameraChanged`をSubscribe/AddTo(this)する。`MapObjectLayoutInstantiator`はregistry登録成功後、master enumを`isLandmark`へ解釈してcontrollerへRegisterする。`OnDestroy`でcontrollerの`Shutdown()`を呼びCullingGroup native資源を閉じる。

購読開始時点ですでにcamera登録済みの初期化順を許容するため、購読後に`CameraManager.MainCamera`をcontrollerへ明示seedする。REDテストにはcamera先行登録→datastore相当の後続購読でも初期cameraを取得できるケースを含める。

- [ ] **Step 7: GREEN・既存回帰を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObject(RendererVisibility|DistanceVisibilityController|InstantiationCompletion|NearestSearcher|Registry)Test"`
Expected: 全件PASS

- [ ] **Step 8: コミットする**

```bash
git commit -m "perf(map-object): 350m距離カリングを追加"
```

---

### Task 4: mapObject全Rendererのprobe authoring最適化

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Map/Visibility/MapObjectProbeUsageTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Editor/MapObjectWrapperGenerator/WrapperPrefabFactory.cs`
- Modify via Unity Editor: `moorestech_client/Assets/AddressableResources/Environment/**/*.prefab`のうちv8 masterの195 addressable

**Interfaces:**
- Produces: wrapper生成結果の全Renderer `lightProbeUsage=Off` / `reflectionProbeUsage=Off`
- Consumes: pinned v8 `map.json`の全mapObject addressableとAddressables登録表

- [ ] **Step 1: REDテストを追加する**

`PinnedMasterRepository.ReadPinnedFile("server_v8/mods/moorestechAlphaMod_8/master/map.json")`から195 addressを読み、Addressables登録先prefabをロードする。0件素通りを禁止し、各`GetComponentsInChildren<Renderer>(true)`が2種のprobeともOffであることを検証する。gitignoreされたPersonalAssets内の親prefab variantへ依存するため、既存`MapObjectAddressableLoadTest`と同じ`[Category("IgnoreCI")]`を付け、ローカルUnity検証として扱う。

- [ ] **Step 2: REDを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectProbeUsageTest"`
Expected: 現在OnのRendererを列挙してFAIL

- [ ] **Step 3: generatorへauthoring規則を追加する**

`WrapperPrefabFactory.CreateWrapperPrefab`の保存直前で、root配下の全Rendererへ`LightProbeUsage.Off`と`ReflectionProbeUsage.Off`を設定する。outlineを含めて適用するため、outline生成後に行う。

- [ ] **Step 4: Unity Editor経由で195 prefabだけを更新する**

`uloop execute-dynamic-code`でv8 master address→Addressables asset pathを解決し、`PrefabUtility.LoadPrefabContents`で各prefabを開く。全Rendererへ2設定を適用し、`PrefabUtility.SaveAsPrefabAsset`後にUnloadする。対象数195・変更Renderer数をログへ出し、Prefab/Scene YAMLはテキスト編集しない。

- [ ] **Step 5: GREENと差分範囲を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectProbeUsageTest|MapObjectAddressableLoadTest"`
Expected: 全件PASS

Run: `git diff --stat`と`git diff --name-only`で、195 prefab・generator・test以外のUnity asset巻き込みが無いことを確認する。

- [ ] **Step 6: コミットする**

```bash
git commit -m "perf(map-object): probeサンプリングを無効化"
```

---

### Task 5: 統合QAと実測バグ狩り

**Files:**
- Modify when a bug is found: corresponding production/test files after adding a RED regression test
- Record: `bd note moorestech-ara "<metrics and findings>"`

- [ ] **Step 1: schema最終監査を実行する**

`validate-schema` checklistでforeignKey差分0、optional追加0、全JSON欠損0、許容値外0、v8 landmark 29件を確認する。

- [ ] **Step 2: Unity compileを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0。warningは新規差分由来0

- [ ] **Step 3: 限定テストを実行する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "CameraManagerTest|MapObject.*(Visibility|Probe|Addressable|Instantiation|Nearest|Registry).*Test"`
Expected: 全件PASS

- [ ] **Step 4: EditModeInPlayingTestを実行する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectNearFieldStartupTest|MapObjectNearestSearchTest|MapObjectRotationTest"`
Expected: 全件PASS。domain reload中エラーなら45秒後に再実行

- [ ] **Step 5: unityプレイ録画テストを実行する**

`unity-playmode-recorded-playtest`スキルでMainGameを録画し、通常mapObjectの350m越え非表示・340m以内再表示・遠景mesa継続表示・スキットcamera切替後の表示追従・採掘/破壊後に再表示されないことを通し確認する。

- [ ] **Step 6: Profiler再計測を行う**

同一save・同一camera位置・vsync offで120フレーム中央値、drawcall、tris、shadow caster、`DrawDepthNormalPrepass`、`UpdateRendererBoundingVolumes`を記録する。計測後はvsyncと一時状態を戻す。問題が0件なら対象を境界往復・camera切替・破壊個体へ拡張して再探索する。

- [ ] **Step 7: 発見バグをTDD修正し最終コミットする**

各バグは再現RED→最小修正→限定GREEN→compileの順。全QA結果を`bd note moorestech-ara`へ保存する。

---

### Task 6: 必須全ブランチレビュー

**Files:**
- Review scope: `origin/master...HEAD`と`../moorestech_master origin/master...HEAD`

- [ ] **Step 1: 必ずmoores-code-reviewスキルで全ブランチレビューを実行する**

自動実行・ゴール文言による省略は禁止。決定論チェックと全レンズを実行し、Critical/Warningを実コードへ照合する。

- [ ] **Step 2: 真の指摘をTDDで修正する**

挙動バグはREDテスト先行。修正後にcompile・限定テスト・必要なunityプレイ録画テスト区間を再実行する。

- [ ] **Step 3: review後の全変更をコミットする**

未コミット差分を本repo・master repoとも確認し、生成物・metaを含む全作業を通常コミットする。

---

### Task 7: 両repoのPRを作成しセッション終了可能状態にする

**Files:**
- Modify: `.moorestech-external-revisions.json`
- PR: `../moorestech_master` companion PR
- PR: 本repo optimization PR

- [ ] **Step 1: master repoをpushしてcompanion PRを作る**

`pr-create`スキルで`perf/mapobject-distance-visibility`をpushし、`master`向けPRを作成する。通常マージ前提を本文へ記載し、本repo PR予定をリンクする。

- [ ] **Step 2: 本repo pinをmaster PRのpush済みHEADへ更新する**

`.moorestech-external-revisions.json`の`moorestech_master.commitHash`をcompanion branch HEADへ更新し、pinだけを最終依存更新としてコミットする。既存ステージ済みの3f0c09b更新はこの最終値で置き換える。

- [ ] **Step 3: masterとの競合と最終compileを確認する**

`origin/master`を通常mergeし、競合があれば`pr-create`手順で解消する。解消後に`uloop compile`と最重要限定テストを再実行する。

- [ ] **Step 4: 本repoをpushして1本のoptimization PRを作る**

`pr-create`スキルでpush・PR作成し、SSAO/Shadow・350m culling・landmark master・probeの4点、before/after実測、テスト、companion PRを本文へ記載する。

- [ ] **Step 5: close protocolを完了する**

`bd close moorestech-ara --reason="描画最適化を実装・検証し両repo PRを作成"`、`git status`を両repoで確認し、全作業がコミット・push済みでPRがマージ可能な状態を確認する。

## 判断記録（ADR）

- 設計正本: `docs/adr/0032-mapobject-distance-culling.md`
- ユーザー裁定正本: `.decisions/2026-08-24-mapObject遠景ランドマークはmaster区分で350mカリングから除外する.md`
- ユーザー裁定 2026-08-24: 本repoの1PRへSSAO/Shadow・350m culling・probe無効化をまとめ、Renderer統合・Collider・imposterは除外
- ユーザー裁定 2026-08-24: landmarkは名前ハードコードでなく全mapObject master必須区分
- agent前提: Renderer限定カリング、340/350mヒステリシス、CameraManager UniRx通知、CullingGroup、1ms時間予算分散
- agent前提: probeはruntime設定でなくauthoring正本へ焼き込み、195 prefabをUnity Editor経由で更新する
- agent前提: ランタイム表示とcamera切替に触れるため、軽量EditModeInPlayingTestだけでなくunityプレイ録画テストを含める
- agent前提: master schemaは本repo所有のため本repo PRは1本だが、外部JSON repoはAGENTS.md規約に従いcompanion PRを別途作る
