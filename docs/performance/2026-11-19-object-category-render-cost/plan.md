# カメラ描画（`RenderPlayModeViewCameras` 約10ms）の内訳計測 計画（v7）

## 0. この計測で答える問い
1. カメラ描画の時間は、CPU のメインスレッド・ジョブのワーカースレッド・レンダースレッド・GPU のどれで決まっているか。Editor とプレイヤーで同じか。
2. その時間は、どの処理（カリング・影・深度・不透明・半透明・コピー・SSAO・アウトライン・ポストプロセス・最後のブリット・UI）に何 ms ずつか（スレッド別・GPU 別）。
3. どの種類のオブジェクト（ブロック種別・MapObject・Terrain の本体／木／草・海・列車・プレイヤー・エフェクト・プレビュー）に何 ms ずつか。
4. どの設定（影の各要素・深度プリパス・SSAO・MSAA・HDR・ポスト・各コピー・中間テクスチャ・描画距離・解像度・LOD）に何 ms ずつか。

「もう測り直さなくていい」とは：1〜4 の全部に区間つきの数字があり、最適化の候補を「律速しているスレッドまたは GPU で効く見込み ms（プレイヤー換算）」の順に並べられ、その数字が Debug・Editor の上乗せ・計測の副作用による見かけの値でないと言える状態。

## 1. 既に分かっている前提（2026-11-14 確認）
- URP 17.3 は **Compatibility Mode**（`m_EnableRenderCompatibilityMode: 1`）。RenderGraph は使っていない。Forward、GPU Resident Drawer オフ。
- グラフィックス API は Editor で **D3D12**。Editor のグラフィックスジョブはオフ、Windows プレイヤーはオン。プレイヤーの `enableFrameTimingStats` は 0。スクリプトのバックエンドは Mono。Windows の API 設定は Automatic。
- 直近のベンチマーク（2026-11-14 after-aim-traversal）：**codeOptimization=Debug・vSyncCount=1・targetFrameRate=60**。フレーム約37ms、`PlayerLoop` 31ms、`RenderPlayModeViewCameras` 10.2ms、`WaitForJobGroupID` 7.6ms、`EditorLoop` 8.7ms、`UpdateAllSkinnedMeshes` 2.6ms。過去の結果に、ゲーム内の画質プリセットは記録されていない。
- URP アセット（High）：MSAA 2x、HDR、影 120m・2カスケード・2048、ソフトシャドウ、追加ライトの影あり、LOD クロスフェード、Depth/Opaque テクスチャを要求。
- レンダラー（`URP Data`）：`m_CopyDepthMode=AfterTransparents`、`m_IntermediateTextureMode=Always`、`m_DepthPrimingMode=Disabled`。Feature は SSAO（Source=Depth、AfterOpaque）、ScreenSpaceOutlines（`outlinesLayerMask=0`、実装は private 側にあり未確認）、OutlinePass（RenderObjects、パス名で絞る）。
- 深度プリパスの有無：`UniversalRenderer` は、深度テクスチャを要求するパスがあると深度コピーを前倒しし、`m_CopyDepthMode` を AfterOpaques に書き換えたまま残す。プリパスは Gizmos がオン・法線を要求するパスがある・Scene ビューのカメラ、などで強制される。今どちらかは未実測（アセットの値は実効値ではない）。
- URP の `ProfilingSampler` は `DEVELOPMENT_BUILD || UNITY_EDITOR` のときだけ中身が入る（Release のプレイヤーでは全部0）。`RenderPlayModeViewCameras` は Editor にしかないマーカー。
- Compatibility Mode では描画命令の実行は全部 `context.Submit()` の中で起きる。パスの `Inl_` は `Execute()` で命令を積む時間だけで、実行の重さは Submit の下に出る（Editor ではメインスレッドかレンダースレッド、プレイヤーではグラフィックスジョブのワーカーの見込み。`renderingThreadingMode` を記録し、試作で確かめる）。`cpuElapsedTime` は全スレッドを合計する見込み（推測、試作で確かめる）。
- 実際に積まれた URP パスの一覧（`m_ActiveRenderPassQueue`）は、カメラを描き終えるたびに `Clear()` される。PlayerLoop からも `endCameraRendering` からも空にしか見えない。
- `m_IntermediateTextureMode`・`m_CopyDepthMode` は renderer のコンストラクタで写されるので、renderer data を書き換えても `SetDirty()` で renderer を作り直さないと効かない。作り直すとパスのサンプラーも作り直され、`enableRecording` が外れる。
- SSAO（AfterOpaque）があると深度コピーが前倒しされ、renderer の `m_CopyDepthMode` は AfterOpaques に書き換わったまま残る。後から SSAO を切っても戻らない。
- `meshDeformation: 2`（GPU スキニング・バッチあり）。
- URP の `ProfilingSampler`：`inlineCpuElapsedTime`（`Inl_<名前>`）がメインスレッドの即時の時間、`cpuElapsedTime` はコマンドバッファ実行側の時間、`gpuElapsedTime` は GPU。コマンドバッファ無しで包んでいるもの（`UniversalRenderTotal`・`RenderCameraStack`・`ExecuteRenderPass` の外側）は `Inl_` しか動かない。値は前のフレーム（GPU はさらに数フレーム遅れ）のもの。パスのサンプラーの多くは `URPProfileId` の列挙に無い（`new ProfilingSampler("Draw Depth Only")` など）。
- 画質の項目は裏で別の設定も変える（`RenderPipelineQualityApplier`・`CameraQualityApplier`）：RenderScale=50% → MSAA=1・HDR オフ／ShadowsEnabled → ソフト・追加ライト影も／EffectsEnabled → 追加ライトの描画モードも／MapObjectsVisible → `lodBias` 1→0.4・クロスフェードも／OceanVisible → カメラの `requiresColorOption=Off` も／TerrainDetail=Coarse → pixelError 5→10 と `drawTreesAndFoliage=false`（木と草が消える）。RenderScale=75% は MSAA・HDR を変えない。プリセット「中」と「高」の違いは描画距離（300m／5000m）だけ。
- 描画距離の選択肢は 300/1000/5000m。Cinemachine の lens が毎フレーム farClip を上書きする。MapObject の非表示距離は 340/350m 固定。
- `MapObjectDistanceVisibilityController` は internal の普通のクラス。止める手段は戻せない `Shutdown()` だけ。距離の帯が変わった時にしか `Renderer.enabled` を書かない。
- `InGameCameraController` は `SetControllable(false)` で Lerp も止まる。飛行モード（`PlayerFlyModeController`）がある。
- `EffectsQualitySwitch` は VFX・ParticleSystem の Renderer・Light をまとめて切る。ブロックの状態の処理や `ReapplyAfterPlacement` が再生し直すことがある。
- `Terrain.drawTreesAndFoliage` は木と草を一緒に切る。木は `treeDistance`、草は `detailObjectDistance` で別々に切れる。
- 環境の prefab に Realtime の反射プローブ（OnAwake）がある。
- UI は CEF（`jp.juha.cefunity`）。
- `GraphicsQualityPreference.SetSettings` は PlayerPrefs に保存する。Windows では Editor とビルドしたプレイヤーで PlayerPrefs の保存場所が違う。
- `RenderPipelineQualityApplier.Start` は、起動時に URP アセット・renderer data・各 Feature を `Instantiate` で複製し、`QualitySettings.renderPipeline` を複製に差し替える。SSAO の切り替えや preference の書き込み先はこの複製。preference を変えると `Apply` が MSAA・HDR・ソフトシャドウ・追加ライトの影・追加ライトの描画モード・クロスフェード・LOD bias を元アセットの値か最低値で上書きする。
- D3D12 では `SystemInfo.graphicsUVStartsAtTop` が真で、深度テクスチャを作るときは中間カラーテクスチャも作る（`UniversalRenderer.cs` 1036行）。`m_IntermediateTextureMode=Always` の判定は有効な Renderer Feature があるかを見る。
- Feature の一覧は renderer のコンストラクタで読まれるので、Feature を足したら `SetDirty` が要る。`EditorSettings.asyncShaderCompilation` は ProjectSettings に保存され得る。
- スニペットの実行中は Editor が止まり、溜まった受信が直後のフレームで処理される（ベンチマーク README）。
- 昼夜サイクルは無い。

## 2. 固定・記録する条件
| 条件 | 内容 |
|---|---|
| マシン | windows-desktop（i7-12700／RTX 3060 Ti）。電源プランは高パフォーマンス。GPU のクロックは、通常プレイの持続ブーストに近い値で `nvidia-smi -lgc` 固定（できない場合は「パフォーマンス最大化を優先」にして、窓ごとにクロック・pstate・温度を記録）。P コア／E コアの構成と、ローカルサーバーのスレッド数を記録 |
| ワールド | `2026-10-22-world_1` を毎回複製 |
| コード | 計測用ブランチの1コミット。プレイヤー用の補助コードは `MOORES_PERF_PROBE` 定義シンボルの中だけ |
| コード最適化 | **Release に固定**。過去との橋渡しに、V0 の素の窓だけ Debug でも取る |
| ジョブの安全装置 | Jobs Debugger・リーク検出・Burst の安全チェックを、プレイヤーと同じくオフにして記録（`JobsUtility.JobDebuggerEnabled`・`NativeLeakDetection.Mode`・Burst）。オンとオフの差を F で1項目測る。`JobWorkerCount` を記録 |
| vSync・上限 | `vSyncCount=0`・`targetFrameRate=-1`・Game ビューの vSync オフ。開始時の値を退避して終了時に戻す |
| 画質プリセット | 開始前に windows-desktop の PlayerPrefs の画質設定を記録する。計測は「中」。過去との橋渡しの窓は、過去と同じ（記録した）設定で取る。終了時に戻す。`environment.cs` にもこの値を足す |
| URP の実効値 | 実際に積まれたパスの一覧は、`RenderPipelineManager.beginCameraRendering` で `scriptableRenderer.EnqueuePass` する「読むだけのパス」（`AfterRendering` の後に積まれ、`Execute` の中でキューを読む。ただのクラスなので動的なアセンブリでも作れ、Feature の数も変えず SetDirty も要らない）で取る。予備として Profiler 窓の `Inl_ExecuteRenderPass` の子の名前からも作る。深度プリパスの有無は、この一覧と `Inl_Draw Depth Only`／`DepthNormals` の呼び出し回数で判定。renderer の private フィールドから実効の `m_CopyDepthMode`・`m_CopyDepthPass.renderPassEvent`・中間テクスチャを作ったか、を読む。MSAA・HDR・影の各値・追加ライト・LOD bias・クロスフェードも記録。**窓ごとに**記録する |
| 窓ごとの表示の実効値 | `Display.main.requiresSrgbBlitToBackbuffer`・`RTHandles.rtHandleScale`・中間テクスチャを作ったか |
| 書き換える先 | 単独の方法で URP の設定を変えるときは、必ず `QualitySettings.renderPipeline` が**今指している、ゲームが作った複製**とその renderer data に書く。スニペット側で新しく `Instantiate` しない（preference 経由の条件が効かなくなる）。元のアセットに書くと効かず、Editor ではアセットが保存される危険がある。戻した後は、preference の `Apply` に上書きされる値（MSAA・HDR・ソフトシャドウ・追加ライト・クロスフェード・LOD bias）の実効値が切る前と一致するかを確かめる。終了後に `Assets/Asset/Common/URPSettings` 以下に差分が無いことを `git status` で確かめる |
| 世界の動き | ローカルサーバーの列車・機械は止めない（実プレイ相当）。代わりに 4.2 の「場面の変化で窓を捨てる」規則を使う |
| CPU の割り当て | affinity は固定しない（起動後に絞るとジョブのワーカー数がコア数を超え、プレイヤーとも揃わない）。`JobWorkerCount` を記録する |
| 解像度 | Game ビュー 1920×1080 固定 |
| Editor の表示 | Game ビューと Editor を前面。Scene ビュー・Hierarchy・Inspector・Profiler ウィンドウを閉じる（Profiler ウィンドウは Profiler 窓の間も開かず、`ProfilerDriver` で記録する）。Game ビューの Gizmos と Stats をオフ（Gizmos は深度プリパスを強制するため必須）。Selection を空に |
| シェーダー | 計測中は `asyncShaderCompilation=false`。切り替えや視点変更の後は `ShaderUtil.anythingCompiling==false` を待つ。終了後に ProjectSettings に差分が出ていないかを `git status` で確かめる |
| 計測の仕組みの上乗せ | 「読むだけのパスを外す」「パスのサンプラーの `enableRecording` を全部オフ」を X 条件として F に入れ（F27・F28）、A1〜A4 での上乗せを出す |
| 記録 | `graphicsDeviceType`・`renderingThreadingMode`・`SystemInfo.supportsGpuRecorder`・`meshDeformation` |

## 3. 視点
| 視点 | 内容 | 用途 |
|---|---|---|
| V0 スポーン | セーブのプレイヤー位置・向き | 主 |
| V1 工場の密集帯 | 飛行モードで、ブロックが最も多い領域を斜め上から見下ろす | 主 |
| V2 地平線 | 工場を背に地形と MapObject を見る | 内訳の切り分け |
| V3 足元 | 地面だけが映る向き | 視点に依らない固定費 |

- 作り方：`SetControllable(false)` → プレイヤーの位置（V1 は飛行モード）→ Cinemachine の vcam の位置・向き（Brain が毎フレームカメラの transform を上書きするので、カメラではなく vcam 側に書く）。座標は試作で決めて書き残す。
- 窓の前後で、カメラの位置・向き・FOV・カメラ距離・farClip を記録し、ずれた窓は捨てる。
- 視点を変えた後の待ち：MapObject の内部の待ち行列が空 → シェーダーのコンパイル完了 → 「描画対象になった Renderer 数」（`isVisible`。影だけのものも含む）の変化が2秒で0.1%以内 → 5秒。

## 4. 計測のやり方
### 4.1 記録係（PlayerLoop の中で動く）
スニペットの呼び出しで Editor を止めないように、計測は PlayerLoop に差し込んだ記録係が行う（`buildmode-probe-start.cs` と同じやり方。状態は AppDomain に置く）。外からは「予定表を渡して開始」と「回収」だけを呼ぶ。

記録係が毎フレーム取るもの：
| 値 | 取り方 |
|---|---|
| フレーム時間 | `Time.unscaledDeltaTime` |
| CPU メイン・レンダー・GPU のフレーム時間 | `ProfilerRecorder` の `CPU Main Thread Frame Time`・`CPU Render Thread Frame Time`・`GPU Frame Time`（取れなければ記録係から `FrameTimingManager.CaptureFrameTimings`） |
| カメラ描画（メイン） | `ProfilerRecorder` のマーカー `RenderPlayModeViewCameras`・`Inl_UniversalRenderTotal`・`Inl_UniversalRenderPipeline.RenderSingleCameraInternal: <名前>`（スタックがある場合は `Inl_RenderCameraStack`）・`ScriptableRenderContext.Submit`（カテゴリは試作で確定） |
| ジョブ待ち | マーカー `WaitForJobGroupID` |
| 処理ごとの時間 | 「読むだけのパス」が集めた各パスの `profilingSampler` と、`URPProfileId` のサンプラー（SSAO・影・コピー・ポストなど）に `enableRecording=true` を付け、**命令を積む時間（メイン）＝`inlineCpuElapsedTime`、命令を実行する時間（スレッド混在）＝`cpuElapsedTime`、GPU＝`gpuElapsedTime`** を毎フレーム読む。renderer を作り直す条件（`SetDirty`）の後は、サンプラーを集め直して `enableRecording` を付け直す |
| 描画の量 | Render カテゴリのカウンター（`Batches Count`・`SetPass Calls Count`・`Draw Calls Count`・`Triangles Count`・`Vertices Count`・`Shadow Casters Count`・`Instanced Batches Count`・`Dynamic Batches Count`・`Static Batches Count`・`Render Textures Count`・`Render Textures Bytes`。名前は試作で `ProfilerRecorderHandle.GetAvailable` から確定） |
| GC | `GC Allocated In Frame` |
| 表示の状態 | Editor ウィンドウが前面か、表示待ちのマーカーの値（二峰の外れを見つけるため） |

### 4.2 予定表と窓
- 予定表は「窓の並び（条件名・フレーム数）」と「条件ごとの切り替え関数・戻し関数」。切り替えと窓の区切りは記録係がフレームの境目で行う。
- 1窓は**10秒**（フレーム数ではなく秒で揃える。プレイヤーが速いと400フレームでは約2秒になり、世界の周期的な動きを平均で消せないため）。
- **切り替えた直後と、戻した直後の両方**で、「その状態になったことの確認（Editor と Dev プレイヤーではサンプラーの値。例：SSAO を切ったら SSAO のサンプラーが0。サンプラーが空の Release プレイヤーでは、読むだけのパスで取ったパスの一覧・中間テクスチャ・`m_CopyDepthMode` などの実効値）ができ、かつフレーム時間の移動中央値が落ち着き、MapObject の待ち行列が空になるまで」捨てる（最低30フレーム、最大300フレーム。超えたら、その窓を A でも X でも不良として記録し、差の計算から外す）。Profiler 窓の直後の素の窓も同じ規則で捨てる。
- 窓の値は**平均**を主とする（加算性・ms の配分・「効く見込み ms」は、トリムしない平均で行う）。5%トリム平均・中央値・p95 は副。
- **場面の変化で窓を捨てる**：窓ごとに、スクリプトで数える量（描画対象になった列車の Renderer 数・ベルト上のアイテムの数）と、取れる環境では描画の量のカウンター（三角形・バッチ・影を落とす数）の中央値を出し、切り替えで変わるはずの無い量が両隣の A から ±5% を超えて外れた窓は捨てる（F・G4 で量そのものが変わる条件は、その条件で変わらないはずの量だけで判定）。
- **時間の周期で動く処理の偏り**：通信・サーバーとの同期・CEF のテクスチャ更新のように時間の周期で動く処理は、描画が減って fps が上がると1フレームあたりの ms が見かけ上減る。Profiler 窓で、それらの ms／秒を A と X で比べ、描画の差は「パスのサンプラーとレンダースレッド・GPU の差」でも併記する。結論の ms には「vSync オフ・fps 上限なしでの差」と注記する。
- **Editor の表示の状態が飛んだ窓を捨てる**：表示待ちのマーカーと `EditorLoop` の窓の中央値が、両隣の A から ±20% を超えて外れた窓は捨てる（README にある 6.4fps／11fps の二峰のような段差を A X A では打ち消せないため）。
- 1回の読み込みは1.5時間以内に収める（Editor の PlayMode を長く続けると状態がずれていくため）。

### 4.3 並べ方と差の取り方
- 基準 A を共有して `A X1 A X2 A X3 … Xn A` と並べ、条件 X の差は「X の窓 − 前後の A の窓の平均」とする（時間とともに一方向に変わる分を打ち消す）。
- 条件の順番は**周ごとに**ランダムにし、乱数の種を記録する。
- 1回の読み込みの中で、この並びを k 周回す。k は試作で決める（4.4 の検出力の計算）。仮に k=3。

### 4.4 統計
- **雑音の推定**：隣り合う A の窓の二階差 `A_i − (A_{i−1}+A_{i+1})/2` の分散を 1.5 で割って、窓1個あたりの雑音の分散 σ² を、視点ごと・指標ごとに推定する。隣り合う二階差は相関するので、実効の自由度は1つおきの値の個数で数える。
- **試作で k と Δ を決める**：試作の段階で、V0 で A・X0・効きの違う条件3〜4個（F1 影・F10 ポスト・F11 MSAA・F7 SSAO）を混ぜた予定表を、**2回の読み込みにまたがって** Editor と Release プレイヤーの両方で回し、σ と Var(u) の目安を測る。差の平均の95%区間の半幅 `t·√(Var(u)/L + 1.5σ²/(kL))`（L は読み込みの数）が Δ/2 以下になる k と L を逆算し、計画書に書き戻す（Var(u)/L が大きければ、周ではなく読み込みを増やす）。所要時間（8章）もこの k で見積もり直す。
- **事前に決める許容幅 Δ**：律速のスレッドまたは GPU の指標で **Δ=0.15ms**（フレーム約25msの0.6%）、パスのサンプラーの指標で Δ=0.05ms。試作で求めた k が大きすぎる（1回の読み込みに収まらない）場合は、Δ を広げるのではなく、「差なし」の判定をノイズの少ないパスのサンプラーで行い（順位を決める Release プレイヤーにはサンプラーが無いので **Dev プレイヤーの列**で行う。使う列は I1 で決めた律速先に合わせる：GPU 律速なら GPU 列、CPU 律速なら命令を積む時間・命令を実行する時間の列。Dev と Release の比を I1 で出しておく）、律速の指標では区間の上限だけを示す。
- **判定**：
  - 「差あり」：差の95%区間が全部 +Δ より上か −Δ より下
  - 「差なし」：差の90%区間が全部 −Δ〜+Δ の中（TOST、片側5%ずつ）
  - それ以外は「未確定」
  - 加算性・効き合いの判定も、この Δ と区間で行う（A/A の幅は使わない）
- **区間の作り方（読み込みをまたいでも自由度を確保する）**：差を `d(条件c, 読み込みl, 周j) = μ_c + u_(c,l) + e_(c,l,j)` の混合モデルで扱い、読み込み×条件の効き合いの分散 Var(u) と周の中の分散 Var(e) は、**全条件で共有して推定**する（条件ごとには推定しない）。Var(e) は A の窓の σ からも推定でき、両者が食い違わないかを確かめる。X の窓の分散が A より大きい条件（切った状態の方が揺れる）は、F 系・G 系それぞれの全条件をまとめて「X の分散 / A の分散」の比を推定し、Levene 検定で外れた条件だけ別扱いにする。
- **読み込みの間の分散**：全条件を最初から**2回の読み込みに分けて**回す（例：k=3 なら2周＋1周）。Var(u) は X0 を除いた全条件をまとめて、効きの大きさに比例する分を含む `Var(u)=τ²+ρ²μ_c²` のモデルで REML 推定する（読み込みで全体が数%速く／遅くなる効果は差に掛け算で効くため）。u は前検定で落とさず、常にモデルに残す。
- **逐次判定の止める規則**：2回の読み込みの後で判定し（Var(u) の推定値を入れた区間で）、未確定の条件だけを3回目・4回目で追加し、4回目で打ち切る。判定を最大3回見るので、各回の α を Pocock 型に分ける（1回あたり約2.2%。「差なし」の TOST も片側2.2%ずつ）。打ち切ったものは「未確定（区間 [a, b]）」と書き、区間の上限で「効く見込み ms」の上限を示す。
- **A/A（X0）**：「切り替え関数も戻し関数も何もしない」条件 X0 を、各周に入れる。役割は、切り替えの操作そのものが偏りを生まないことの確認に限る。判定に使う指標は A1 と律速の指標の2つだけにし、両方で区間が0を含まない（Bonferroni で2本）ときだけ、その予定表を調べ直す。
- **加算性の区間**：各差は窓の一次結合なので、加算性の差（全部切った差 − 単独の差の和）の分散は、予定表から決まる共分散（隣り合う差は σ²/4）を使って `c'Σc`（＋Var(u) の項）で解析的に出す。周ごとの散らばりは突き合わせの確認に使う。未確定なら、組を2つずつ測るか、上限だけ示す。
- **土台が既定の A ではない条件**（F15・F7 の作り直し版・F14・効き合いの組）：土台ごとに `A' X A'` の小さな予定表を作り、その中で差を取る（共有の A と交互に並べると renderer の作り直しが毎回2回起きるため）。
- **持ち越しの確認**：A の二階差を「直前の条件」で回帰し、前の条件の効果が後の A に残っていないかを確かめる（G4b・F29・作り直しの後など）。
- **捨てた窓の割合**：条件・視点ごとに記録し、条件と関係があるか（カイ二乗）を確かめる。
- **多重比較**：「差あり」の判定に Benjamini–Hochberg（FDR 5%）を併用し、最適化の候補に挙げるのは両方を満たすものに限る。

### 4.5 Profiler を記録する窓
処理の内訳を全スレッドで見るため、Profiler を記録する窓を素の窓とは別に、各視点で**周ごとに1回（k=3 周で3回）・5秒**取る（区間を付けるため）。Profiler を記録している間の上乗せは、その窓と前後の素の窓の A1 の差として記録する。F・G4 では記録しない。

## 5. 測る項目

### A. どこで時間がかかっているか
| # | 項目 | 取り方 |
|---|---|---|
| A1 | フレーム時間 | 4.1 |
| A2 | CPU メインのフレーム時間と、表示の待ち | 4.1。`FrameTiming.cpuMainThreadPresentWaitTime` も記録し、A2 から引いた値も出す（GPU 律速だと表示の待ちが A2 に入って CPU 律速に見えるため。Release ではマーカーが無いので律速の判定はこの値で行う）。Editor では EditorLoop と Editor ウィンドウの描画を含む（だから Hierarchy などを閉じる） |
| A3 | CPU レンダースレッドのフレーム時間 | 4.1 |
| A4 | GPU のフレーム時間 | 4.1。CPU 律速のときは GPU のタイムスタンプの差に「命令待ちで GPU が空いている時間」が入るので、GPU 列（A4・C/E の GPU 列）は「上限」と表記する。GPU の仕事量として読むのは、F17（解像度75%）で A4 が画素数に比例して減る場合に限る。I1 で F17 の A4 の差と画素数の比を必ず出す。Editor の値は Editor UI の描画を含む |
| A5 | 待ち時間 | Profiler 窓で、名前に `Wait` を含むマーカーを全スレッドから全部列挙 |
| A6 | `RenderPlayModeViewCameras`・`Inl_UniversalRenderTotal` の total と self | 4.1・Profiler 窓 |
| A7 | 律速の判定 | A2・A3・A4 と GPU 待ちのマーカー、F17（解像度 75%）での A4 の差。**判定の正はプレイヤーの値（I）** |

### B. カメラ・ライト・その他の描画元
| # | 項目 | 取り方 |
|---|---|---|
| B1 | 有効なカメラの一覧（Base/Overlay・cullingMask・farClip・targetTexture・depth・スタック・renderShadows・renderPostProcessing・requiresColor/Depth の実効値） | `Camera.allCameras`・`UniversalAdditionalCameraData` |
| B2 | カメラごとの CPU 時間 | Profiler 窓の `Inl_UniversalRenderPipeline.RenderSingleCameraInternal: <名前>` |
| B3 | 定常時に RenderTexture へ描いているカメラ | B1・B2 |
| B4 | ライト：有効な Light の数（種類別・影あり/なし）、追加ライトの影のスライス数、追加ライトの1オブジェクトあたりの上限 | `FindObjectsByType<Light>` と URP の実効値。可視のライトの数は G3 で補う |
| B5 | Volume：効いているポストエフェクトの一覧と active か | `VolumeManager.instance.stack` |
| B8 | カメラの `useOcclusionCulling` と、ベイク済みのオクルージョンデータがあるか | `StaticOcclusionCulling` |
| B6 | 反射プローブ：数・モード（Realtime か）・更新のタイミング・解像度。Realtime の更新が定常時に起きていないか | `FindObjectsByType<ReflectionProbe>`、Profiler 窓の `ReflectionProbes.*` |
| B7 | 実際に積まれた URP パスの一覧（順番つき） | 2章の「読むだけのパス」 |

### C. 処理別の内訳（スレッド別・GPU）
次の行を、4.1 の**命令を積む時間（メイン）・命令を実行する時間（スレッド混在）・GPU** の3列と、Profiler 窓の**スレッド別（メイン・ワーカー合計・レンダースレッド）**の3列で表にする。**どのスレッドで何 ms かは Profiler 窓のスレッド別を正とする**。Compatibility Mode では実行の重さは Submit の下に出るので、Profiler 窓では Submit の下の `DrawOpaqueObjects` などの名前（`Inl_` なし）を、どのスレッドに出るかとあわせて集計する。試作で、Editor と Dev プレイヤーの両方で、`Inl_` なしのパス名がどのスレッドに出るかを確かめて書き戻す。パス名は B7 で確定させる。

| # | 処理 |
|---|---|
| C1 | カリング（`CullScriptable`・`SceneCulling`・`CullSendEvents`（CullingGroup の判定）・ワーカーのカリングジョブ） |
| C2 | 影のカリング（`CullShadowCasters` とそのジョブ） |
| C3 | メインライトの影（外側 `Draw Main Light Shadowmap` と内側 `MainLightShadow`） |
| C4 | 追加ライトの影 |
| C5 | 深度プリパス（あれば）・深度コピー。どちらが起きているかと、要求したパス |
| C6 | 不透明（`DrawOpaqueObjects`。外側と内側で同じサンプラーを使うので、入れ子の二重計上がないかを試作で確かめる） |
| C7 | スカイボックス |
| C8 | 不透明コピー（`CopyColor`） |
| C9 | 半透明 |
| C10 | SSAO |
| C11 | ScreenSpaceOutlines（`outlinesLayerMask=0` で何をしているか、`ConfigureInput` で法線を要求しているか）・OutlinePass |
| C12 | ポストプロセス（`UberPostProcess`・`Bloom` など） |
| C13 | 最後のブリット（中間テクスチャから画面へ） |
| C14 | UI（`UGUI.Rendering.*`・`Canvas.*`） |
| C15 | 描画命令の発行と提出（`ScriptableRenderContext.Submit`・`ExecuteRenderQueueJob`・`SRPBatcher.Flush`・`Batch.DrawInstanced`） |
| C16 | ジョブ待ちの中身（`WaitForJobGroupID` と、同じ時間に動いていたワーカーのジョブを時間の重なりで突き合わせる。flow event が取れればそれも使う） |
| C17 | カメラの外で描画のためにかかるもの（`UpdateAllRenderers`・`MeshSkinning.*`・`UpdateRendererBoundingVolumes`・`TransformChangeSystem`・`VFX.*`・`ParticleSystem.*`） |
| C18 | Terrain（`Terrain.*`・描画パッチ数） |

### D. 描画の量
4.1 のカウンターを、視点ごとに平均で。SRP Batcher の内訳は G3 で補う。

### E. GPU の内訳
| # | 項目 | 取り方 |
|---|---|---|
| E1 | パス別の GPU 時間 | 4.1（C の GPU 列） |
| E2 | ドローコール単位の GPU 時間・オーバードロー | A7 で GPU が律速か、プレイヤーの A4 が A2 の半分以上の場合だけ。**プレイヤー**を PIX で V0・V1 を1フレームずつキャプチャ（Editor は PIX から起動し直すのが難しく、Editor UI も混ざるため）。パス別時間、上位20ドローコール、シェーダー別合計、オーバードロー |

### F. 設定ごとのコスト
4.3 のやり方で、V0・V1（※印は V3 も）。Editor では処理別の内訳（どのパス・どのスレッドが減ったか）を取り、順位に使う差は I2 の Release プレイヤーで取る。ゲームの画質項目は、preference 経由（ユーザーが切ったときの効果）と単独の効果を分ける。

| # | 切るもの | 単独の方法 | preference 経由 |
|---|---|---|---|
| F1 | 影すべて | カメラ `renderShadows=false` | `ShadowsEnabled=false` |
| F2 | ソフトシャドウ | アセット `m_SoftShadowsSupported` | — |
| F3 | 追加ライトの影 | `m_AdditionalLightShadowsSupported` | — |
| F4 | 影距離 120→60m | `shadowDistance` | — |
| F5 | カスケード 2→1 | `shadowCascadeCount` | — |
| F6 | 影解像度 2048→1024 | `mainLightShadowmapResolution` | — |
| F7 ※ | SSAO | Feature の SetActive(false)。加えて「SSAO オフで renderer を作り直した」状態（深度コピーが AfterTransparents に戻る） | 同じ（プレイ中に切った場合、と注記） |
| F8 ※ | ScreenSpaceOutlines | Feature の SetActive(false) | — |
| F9 | OutlinePass | Feature の SetActive(false) | — |
| F10 ※ | ポストプロセス | カメラ `renderPostProcessing=false` | 同じ |
| F11 ※ | MSAA 2x→なし | `msaaSampleCount=1` | — |
| F12 ※ | HDR | `supportsHDR=false` | — |
| F13 ※ | 不透明コピー | カメラ `requiresColorOption=Off` | — |
| F14 | 深度テクスチャ・深度プリパス・深度コピーの位置 | 深度を要求しているもの（SSAO・海・プリパスを強制しているパス）を B7 で特定し、それらを全部切った状態と、要求したまま `requiresDepthOption=Off` を試した状態を比べる。プリパスが起きている場合は、プリパスを強制しているものだけを切った条件を必ず入れる。深度コピーの位置は切り替えで戻らないので、「SSAO オフで renderer を作り直した（`SetDirty`）状態」の行を足す | — |
| F15 | 中間テクスチャ | 土台は「F25 ＋ 深度を誰も要求しない」。その上で A＝「Always ＋ パスを積まない空の Feature を1つ有効」（中間テクスチャあり）、X＝「Auto ＋ 同じ空の Feature」（なし）を比べる（Feature が全部無効だと Always でも作られず、差が作りの上で0になるため）。合格条件は「**A では作っていて X では作っていない**」ことを窓ごとに確かめること。実プレイの設定（HDR・ポスト・MSAA あり）では中間テクスチャは常に必要なので、この差は「それらを全部やめたときにしか得られない」と注記する。常にかかっている最後のブリットは C13 のサンプラー（Dev プレイヤーの GPU 列）を主な値にする | — |
| F16 | 海 | 海の Renderer だけ `enabled=false` | `OceanVisible=false` |
| F17 | 解像度 100→75% | preference の `RenderScale=75%`（MSAA・HDR は変わらないので単独と同じ） | 同じ。加えて `RenderScale=50%` |
| F18 | 描画距離 300→1000m | — | `DrawDistance=1000m`（MapObject は350mで消えるので増えない、と注記） |
| F19 | 追加ライトの描画 | `m_AdditionalLightsRenderingMode=Disabled` | — |
| F20 | エフェクト | `EffectsQualitySwitch` と同じ対象（VFX・ParticleSystem の Renderer・Light）を止める。窓の最後に有効な数を数え直し、ゲーム側が戻していたら窓を捨てる | `EffectsEnabled=false` |
| F21 | LOD bias 1→0.4 | `QualitySettings.lodBias=0.4`（F22 の preference 経由の差が Δ の中なら省く） | — |
| F22 | MapObject と LOD | — | `MapObjectsVisible=false`（MapObject 単独は G4） |
| F23 | Terrain の pixelError と木・草 | pixelError 5→10 だけ（木・草は G4t） | `TerrainDetail=Coarse`（木と草も消える） |
| F24 | 低プリセット丸ごと | — | 低プリセット |
| F25 ※ | 固定費の下限 | F1・F7・F8・F9・F10・F11・F12・F13 を全部同時に単独の方法で切る。「読むだけのパスを外した版」も取る（F27 と合わせて上乗せの確認） | — |
| F26 | LOD クロスフェードだけ | アセット `m_EnableLODCrossFade=false`（F21 と同じく、F22 の差が Δ の中なら省く） | — |
| （注） | F15 の「空の Feature」は PerfProbe 側のコンパイル済みの型で作る（動的なアセンブリの型を `CreateInstance` できない可能性があるため） | | |
| F27 | 計測の仕組み：読むだけのパス | beginCameraRendering の登録を外す | — |
| F28 | 計測の仕組み：サンプラー | `enableRecording` を全部オフ | — |
| F29 | SRP Batcher | アセット `useSRPBatcher=false`（約21万個の Renderer で、効いているかの確認） | — |
| F31 | テクスチャ（I1 で GPU 律速と分かったときだけ） | `globalTextureMipmapLimit=1`・`anisotropicFiltering` の切り替え | — |
| F30 | オクルージョンカリング | カメラ `useOcclusionCulling` を反転（ベイク済みのオクルージョンデータがあるかも B に記録。無ければ省く） | — |

- 加算性：F25 の差と、F25 に含まれる単独の差の合計を比べる（平均で）。差が Δ と区間の判定で「差あり」なら、効き合いの疑いがある組（影×SSAO、MSAA×コピー、HDR×ポスト、**最後の Feature を切る（F7×F8×F9。有効な Feature が0になると Always でも中間テクスチャと最後のブリットが消える）**、**中間テクスチャが要らなくなる組（HDR×MSAA×ポスト×コピー×深度）**）を測る。F25 の窓で中間テクスチャを作ったかを、加算性の表の横に並べる。
- F の表の「アセット」は、すべてゲームが作った複製（2章「書き換える先」）。Feature の付け外しや renderer data の書き換えの後は `SetDirty` が要り、そのとき renderer とパスのサンプラーが作り直される（サンプラーを集め直して `enableRecording` を付け直す）。
- 各窓で URP の実効値・パスの一覧・描画の量を記録し、切ったものが本当に切れたかを確かめる。切れていない窓は捨てる。

### G. オブジェクトの種類ごとのコスト
#### G1. 分類
Renderer をシーン上の親子とコンポーネントで分ける。どこにも当たらないものは「未分類」とし、名前の上位を出す（描画対象の5%を超えたら区分を足す）。
- ブロック（種別ごと）
- ブロックのプレビュー・設置枠・当たり判定
- MapObject（種類ごと）
- Outcrop
- 海
- 列車（車両・レール）
- ベルト上のアイテム（`BeltTransportItems` 親の下。カメラから45/50m以内で1個ずつ生成・破棄される）
- プレイヤー・キャラ
- エフェクト（VFX・ParticleSystem）
- UI
- その他

Terrain は Renderer ではないので別に扱う（本体・木・草）。アウトライン用の複製は数だけ数えて、非アクティブなことを確かめる。

#### G2. 区分ごとの量（各視点）
素の窓とは別の時点で1回だけ走査する（約21万個）。
- Renderer 数：有効／描画対象（`isVisible`。影だけのものを含む、と明記）
- 描画対象のサブメッシュ数・三角形数・頂点数
- マテリアルの種類数・シェーダーの種類数・マテリアルの複製の数
- SRP Batcher に乗らないもの（MaterialPropertyBlock を使っている・非互換のシェーダー。取れる情報は試作で確かめる）
- 影を落とす数・受ける数
- LODGroup の有無と、今出ている LOD
- 半透明の数
- OutlinePass が対象にするパス名を持つマテリアルの数
- 前フレームから Transform が動いた数
- SkinnedMeshRenderer の数（区分ごと）と `updateWhenOffscreen`（`UpdateAllSkinnedMeshes` 2.6ms の持ち主を出す）
- ブロック種別ごとの「1個あたり Renderer 数・三角形数」と「ワールド内の個数」
- Terrain：ハイトマップの解像度・pixelError・木の数・草の密度・描画距離・Draw Instanced の有無

#### G3. ドローコールの持ち主（補助）
Frame Debugger を反射で使う（V0 だけ。型名は Unity 6000.3 で `UnityEditorInternal.FrameDebuggerInternal` 名前空間にある見込み。試作で確かめる）。「有効化（スニペット1）→ 数フレーム待つ → 一覧を取得（スニペット2）→ 上位50件と SRP Batch が切れる箇所の詳細を何回かに分けて取得 → 無効化」の順で、V0・V1 の1フレーム。有効にすると PlayMode が一時停止するので、無効化の後に `EditorApplication.isPaused=false` に戻したことを確かめる。描画が止まって受信が溜まるので、後は3章の待ち条件で落ち着くまで待つ。SRP Batch の1件には多数の Renderer が入るので、区分ごとの数は「代表の Object による目安」とし、区分のコストは G4 を正とする。

#### G4. 区分ごとに消して差分を測る
4.3 のやり方で V0・V1（Editor で内訳、G4b・G4t は I2 で Release プレイヤーでも）。対象は、G2 で描画対象数・三角形・影を落とす数が多い順にブロック種別の上位5、MapObject、海、列車、プレイヤー、エフェクト。

| # | 消し方 | 対象 | 分かること |
|---|---|---|---|
| G4b | `forceRenderingOff=true` | 全対象（基本） | 描画のコスト（カリングの登録は残る） |
| G4a | `shadowCastingMode=Off` | 影を落とす数の上位3区分 | 影に描くコスト |
| G4c | `enabled=false` | ブロックの上位2種と MapObject | 描画＋カリング登録のコスト。G4b との差がカリングのコスト |
| G4t | Terrain：`drawHeightmap=false`／`treeDistance=0`／`detailObjectDistance=0`／`Terrain.shadowCastingMode=Off` | Terrain | 本体・木・草・影 |

- ベルト上のアイテム：窓の途中で生まれたものに `forceRenderingOff` が付かないので、`BeltTransportItems` 親の `SetActive`（`ISkitEntityObjectControl` 経由）で消すか、記録係が毎フレーム付け直す。窓ごとにアイテムの数を記録する。
- MapObject：コントローラーは止めない。窓の前後で MapObject の enabled の数が変わっていないことを確かめる。CullingGroup の球の数と `CullSendEvents` は別の行で出す。
- 加算性：G4b の対象の差の合計と、全対象を同時に G4b した差を比べる（平均で）。
- 結果は「区分 × 視点 × (メイン・コマンド実行・GPU) の ms」と「1個あたりの µs」。

### H. UI（CEF）
4.3 のやり方で、ブラウザの描画とテクスチャの更新を止めた窓と比べる（止め方は試作で確かめる）。Chromium の GPU プロセスの GPU 使用率も記録。

### I. プレイヤーで測る（順位の正）
**最適化の候補の順位は、Release プレイヤーで測った差を正とする。** Editor（グラフィックスジョブなし）とプレイヤー（あり）ではスレッドの分担が違い、条件ごとに効くスレッドが違うので、一つの比では換算できないため。Editor は処理別の内訳（C・E1）、G4a/G4c、試作に使う。

プレイヤーは2本作る。どちらも `MOORES_PERF_PROBE` の補助コード（PerfProbe：記録係と予定表のプレイヤー版。起動引数で視点・予定表を指定し、結果をファイルに書く。起動時に `GraphicsQualityPreference.SetSettings` で「中」を当てて実効値を記録し、終了時に元の値へ戻す）を入れる。API は、まず引数なしで起動したときに選ばれるものを記録し、以後は `-force-<その API>` で固定する。それが D3D12 でなければ、V0 の A1〜A4 と F の主要な行を D3D12 でも取り、Editor との比較に使う。

| ビルド | 取れるもの | 取れないもの |
|---|---|---|
| Release プレイヤー（`enableFrameTimingStats=1`） | A1〜A4（FrameTimingManager）、描画の量のカウンター（取れるかは I1 で確かめる）、F・G4b・G4t・H の差（A1〜A4 で） | パスのサンプラー（`ProfilingScope` が空）、マーカー、`RenderPlayModeViewCameras` |
| Development プレイヤー | 上に加えて、パスのサンプラー（3列）、`Inl_UniversalRenderTotal`、Profiler をつないだスレッド別の階層 | `RenderPlayModeViewCameras`（Editor 専用） |

| # | 項目 | 時期 |
|---|---|---|
| I1 | 2本のビルドで、引数なしの API・V0 の A1〜A4・Dev で C の主要行。**両方が値を返すこと**、Dev と Release の A1〜A4 の差、律速のスレッドまたは GPU を確かめる。あわせて 4.4 の σ（Release プレイヤー） | 試作の直後 |
| I2 | Release プレイヤーで、**F の全行（単独と preference 経由）・G4b の全区分・G4t・H** を V0・V1 で、4.3・4.4 のやり方で回す（指標は A1〜A4） | A〜G の後 |
| I3 | Dev プレイヤーで V0・V1 の 4.1 の全部と Profiler 窓（スレッド別の内訳） | I2 と同時 |
| I4 | Editor 固有のコスト：`EditorLoop` の内訳 | Profiler 窓 |
| I5 | 説明用の比：Editor（Release）と Dev プレイヤーの `Inl_UniversalRenderTotal`・C の主要行の比。順位には使わない | まとめの前 |

プレイヤーを測るときの環境：
- 起動：`-screen-width 1920 -screen-height 1080 -screen-fullscreen 0`（ウィンドウのモードは試作で決めて固定）。PerfProbe は vSync=0・targetFrameRate=-1 を当てる（High の品質レベルは vSyncCount=1）。`Screen.width`・`Screen.height`・`fullScreenMode`・リフレッシュレートを窓ごとに記録する。
- PerfProbe には `enter-world.cs` と同じ処理（画面スリープの抑止 `SetThreadExecutionState`・クラッシュ報告と開始ゲートの通過・ワールドの指定）を入れる。窓ごとに `Application.isFocused`・画面の電源状態・Present が occluded か、を記録し、外れた窓は捨てる（長時間の I2 の途中で画面が消えると、表示の待ちにしか出ず気付きにくいため）。
- ssh からは対話しないセッションで起動されるので、対話のセッションで起動する（`schtasks /it` など。方法は試作で確かめる）。
- I2（Release）では Editor を終了し、CEF の別プロセスも残っていないことを確かめる。I3（Dev）では Profiler をつなぐために Editor を開くが、PlayMode には入らない。
- I1 で Dev プレイヤーの Player.log に、シェーダーのバリアントが無いという警告が出ていないかを確かめる（単独の方法で切る組み合わせのバリアントが削られていると、近いバリアントで黙って描かれるため）。
- API・グラフィックスジョブのモード・スクリプトのバックエンドを記録する。

## 6. 道具
既存の `docs/performance/benchmark/` に乗せる。

| ファイル | 役割 |
|---|---|
| `run-camera-breakdown.sh` | 読み込み→視点→予定表の実行→回収→後始末。途中で止まっても `camera-cleanup.cs` を必ず呼ぶ |
| `snippets/camera-env.cs` | 2章の条件の設定・記録・退避 |
| `snippets/camera-pose.cs` | 視点の作成と記録 |
| `snippets/camera-settle.cs` | 3章の待ち条件 |
| `snippets/frame-schedule-start.cs` / `-collect.cs` | 4.1・4.2 の記録係と予定表（切り替え関数・戻し関数を含む） |
| `snippets/pass-list-probe.cs` | 「読むだけのパス」を beginCameraRendering で積む・外す |
| `snippets/render-profiler-collect.cs` | Profiler 窓：全スレッドの階層と、待ちマーカーの列挙 |
| `snippets/frame-debugger-*.cs` | G3（有効化・取得・詳細・無効化） |
| `snippets/renderer-census.cs` | B・G1・G2 |
| `snippets/camera-cleanup.cs` | vSync・上限・PlayerPrefs の画質・asyncShaderCompilation・ジョブの安全装置・全切り替えを戻す |
| `compare-camera.py` | 4.3・4.4 の差・区間・A/A 幅・逐次判定 |
| `Client.PerfProbe`（`MOORES_PERF_PROBE`） | 記録係・予定表・約55個の切り替えと戻しの関数・読むだけのパス・空の Feature を、**コンパイル済みのコード1本にまとめる**。Editor でもこの定義シンボルを付け、スニペットからはこれを呼ぶだけにする（Editor とプレイヤーで切り替えの中身が食い違わないため） |

試作の段階で筋を確かめる：パスのメイン時間の合計 ≤ `Inl_UniversalRenderTotal`、`Inl_UniversalRenderTotal` ≤ `RenderPlayModeViewCameras`、F7 で SSAO のサンプラーが0、X0 の差の平均 ≈ 0、など。

## 7. まとめ方
`docs/performance/2026-11-14-camera-render-breakdown/README.md`：
1. 結論（0章の1〜4に1段落ずつ）
2. 条件（2章の実効値）と A/A の幅
3. A・B（視点別）、I の比
4. C・E（処理 × メイン・コマンド実行・ワーカー・レンダー・GPU）
5. D・G2（区分別の量）
6. F（単独と preference 経由）
7. G4（区分別の差と1個あたり）
8. H（UI）
9. 最適化の候補：Release プレイヤーで測った、律速しているスレッドまたは GPU での差の順に。フレーム時間の差では並べない。根拠の表の行へのリンク付き
10. 測れなかったもの・推測にとどまるもの
11. 注記：Compatibility Mode での値。RenderGraph へ移ると CPU 側の数字が変わる。GPU Resident Drawer は Compatibility Mode では試せないので「測れなかったもの」に書く

生データは同じフォルダに置く。

## 8. 順番と所要時間の見積もり
| 段階 | 内容 | 時間 |
|---|---|---|
| 1 | 道具：`Client.PerfProbe`・スニペット・`compare-camera.py` を書く（1〜1.5時間）。動くか分からないもの（カウンター名・URP サンプラー・読むだけのパス・空の Feature・Frame Debugger の型・ScreenSpaceOutlines の実装・CEF の止め方・`asyncShaderCompilation` の効き方・GPU クロック固定・視点）をまとめて1回の読み込みで試し、直して3〜4回（約1時間）。約55個の切り替えが切れて戻るかを予定表で自動確認し、2〜3回直す（30〜60分）。比較スクリプトを作り物のデータで確かめる（30分） | 約3〜4時間 |
| 2 | Release・Dev のプレイヤーのビルドと起動の確認（ビルド時間は未計測。1本20〜40分と推測）。I1（律速・σ・Var(u)）と Editor の σ。k・L を決めて計画書と下の時間を見積もり直す | 約2〜3時間 |
| 3 | Editor：A/A（X0）・A・B・C・D・E1・G1・G2・G3 を4視点で、Profiler 窓を含めて2読み込み。V0 は Debug でも1回 | 2時間 |
| 4 | Editor：F の内訳（約40条件 × V0・V1、※は V3 も）。Editor 側の k はパスのサンプラーの内訳に足りる分だけ | 約3時間 |
| 5 | Editor：G4・H（約20条件 × V0・V1） | 約3時間 |
| 6 | Release プレイヤー：I2（F・G4b・G4t・H の全部 × V0・V1） | 約6時間 |
| 7 | Dev プレイヤー：I3 | 1時間 |
| 8 | E2（必要なら） | 1時間 |
| 9 | まとめ | 1〜2時間 |

道具と確認で約5〜7時間、実機の計測で約16〜17時間（k=3 の場合）。**段階2の後で、測った σ と Var(u) から k・L を決めて見積もり直すことを必須にする**。1回の読み込みは1.5時間以内に分ける。I1 で GPU 律速と分かった場合は、F の影・解像度・MSAA・SSAO 系と E2 を前倒しし、Editor の G4c を削る。時間が足りないときに削る順：F31 → G3 の詳細取得 → 各周の Profiler 窓（1回に減らす）→ V2・V3。
