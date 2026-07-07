# ユースケース: 実行が進まない・設置されない・原因不明

上から順に切り分ける。**推測で直さず、必ず観測してから直す。**

## 0. まず1コマンドで全体状態を見る（診断EDC）

```bash
uloop execute-dynamic-code --project-path <client> --code 'return "playing=" + UnityEditor.EditorApplication.isPlaying + " serverCtx=" + (Game.Context.ServerContext.WorldBlockDatastore != null) + " ready=" + Client.Playtest.Core.PlaytestGameReady.IsReady + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;'
```

| 観測 | 意味 | 次の一手 |
|---|---|---|
| `playing=False` | PlayModeに入っていない | bootのEDCレスポンス確認 |
| `serverCtx=False`のまま | 初期化前半で死亡 | →「1. ready.markerが出ない」 |
| `ready=True`なのに進まない | シナリオ側の問題 | →「3. クリック/操作が効かない」 |

## 1. ready.markerが出ない（`NG: game not ready within 300s`）

原因は2大別。ログで判定する:

```bash
uloop get-logs --project-path <client> --log-type Error 2>&1 | grep -o '"Message": "[^"]*"' | sort | uniq -c | sort -rn | head
uloop get-logs --project-path <client> --log-type Exception 2>&1 | grep -o '"Message": "[^"]*"' | sort | uniq -c | sort -rn | head
```

**(a) `SocketException: Address already in use`** — **ゲームサーバーポート11564は固定・全worktree共通**。
他worktreeのUnityがPlayMode中だと起動不能。さらに**PlayMode停止後もソケットがリークして残る**ことがある:
```bash
lsof -nP -iTCP:11564          # ESTABLISHED/LISTENの保持プロセスを特定（ps -p <PID> -o command=）
# 保持EditorのPlayModeを停止 → まだ残るなら当該Editorへドメインリロードを要求（これでソケットが死ぬ）:
uloop execute-dynamic-code --project-path <保持側client> --code 'UnityEditor.EditorUtility.RequestScriptReload(); return "ok";'
```
※他セッションのEditorを止める前にユーザーへ確認する。preflight [5/5]が事前検出する。

**(b) `MooresmasterLoaderException`（例: PropertyPath: data[0].priority）** — masterスキーマ不整合。
ピン留めmaster（`/Users/katsumi/moorestech-worktrees/playtest-master/server_v8`）を使っているか確認。
互換コミットは`.moorestech-external-revisions.json`の`moorestech_master.commitHash`が正。preflight [4/5]が検出する。
なお本番マスタは`IMasterValidator.Validate`が走るため、ロジックテストで出なかったmaster不整合を
このスキルが最初に検出することが多い（それ自体がバグ検出の成果になる）。

## 2. ログ読みの注意（見逃し防止）

- **頻度上位だけ見ない**: NRE/MapObjectスパムに埋もれ、真犯人が「100件上限でカット」されて上位に出ないことがある。
  容疑キーワードで的打ちする: `uloop get-logs --log-type Error --search-text "BlockElement"` 等
- Error型とException型は**別枠。両方見る**
- 進行地点の特定には最新のLog型ログ（`[WebUiHost] ready`等）が有効

## 3. クリック/操作が効かない（Untilタイムアウト）

PlayModeは生きているので**ライブで観測**する。効いた実績のある調査順:

1. **UI状態・手持ち・ホットバー**:
   ```bash
   uloop execute-dynamic-code --project-path <client> --code '
   var ui = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.UI.UIState.UIStateControl>();
   var hb = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.UI.Inventory.HotBarView>();
   var n = hb.CurrentItem.Id == Core.Master.ItemMaster.EmptyItemId ? "(empty)" : Core.Master.MasterHolder.ItemMaster.GetItemMaster(hb.CurrentItem.Id).Name;
   return "state=" + ui.CurrentState + " slot=" + hb.SelectIndex + " holding=" + n + " x" + hb.CurrentItem.Count;'
   ```
2. **マウス位置とUI被り**: `Mouse.current.position.ReadValue()` と `EventSystem.current.IsPointerOverGameObject()`、
   `EventSystem.RaycastAll` のヒット一覧（クリックはUI被り画素で無効化される）
3. **レイの当たり先**: `Camera.main.ScreenPointToRay(マウス座標)` を対象マスクでRaycastし
   `hit.transform.name / hit.point / GroundGameObjectの有無` を見る
4. **アクティブなplace system**（リフレクション）: UIStateControl→`_uiStateDictionary`→PlaceBlockState→
   `_placeSystemStateController`→`_currentPlaceSystem` を辿り型名を確認。応答待ちフラグ等の内部状態も同様に読める
5. **同じ操作を手で1回だけ注入**して差分観測（`SemanticInput.MouseButtonDown(0)`→sleep→`MouseButtonUp(0)`→GetBlock）
6. それでも不明なら**当該ロジックの入力読み取り・分岐条件を実コードでReadする**
   （この手順でPlaceInfo.BlockId未設定によるプレビュー毎フレーム例外＝実プロダクトバグを特定した実績あり）

チェック観点: legacy Input直読み（input-injection.md）/ `IsPointerOverGameObject` /
プレビュー成立条件（GroundGameObject・y=32）/ 応答待ちフラグ / 未解放・コスト不足。

## 4. uloopコマンド自体の不調

| 症状 | 原因と対処 |
|---|---|
| `Unity is reloading (Domain Reload in progress)` | コンパイル/リロード直後・**または別のテスト実行が進行中**。45秒待って再試行。**run-testsを並走させない** |
| `Unity CLI Loop is not installed (UnityMcpSettings.json not found)` | **run-testsが実行中に設定を.bakへ退避**し、中断で戻し損ねた。**bootのPlayMode突入ドメインリロードでも同事象が起きる**（run-tests非並走でも発生・2026-07-07のモデル評価で複数セッションが遭遇）。`cp UserSettings/UnityMcpSettings.json.bak UserSettings/UnityMcpSettings.json`（uloop fixでは直らない）→ 復元後は必ずPlayMode停止からフレッシュ再実行 |
| `uloop list`がエラー | Editor側で Window > Unity CLI Loop > Server をStartしてもらう |
| EDCの`Result`が空 | 「未ready」ではなく**スニペットが壊れている**可能性が高い。待たずにエラー確認へ |
| CS0104 `Object`曖昧 | `UnityEngine.Object`と完全修飾 |
| 存在しないAPIエラー | 書く前に実ファイルをRead/Grepでシグネチャ確認 |
| stdoutに"Multiple Unity projects found"警告 | JSON抽出は `sed -n '/^{/,$p'` を通す（ランナー実装済み） |

## 5. テスト実行のフレーク

EditModeInPlayingTestを一括実行すると、後半のテストが
`GameInitializerSceneLoader was not found within 60 seconds` で落ちることがある
（連続PlayMode遷移による初期化遅延）。**単独実行で通れば変更起因ではない**と判定してよい。
結果XMLは `moorestech_client/.uloop/outputs/TestResults/`（宣言はutf-16だが実体はUTF-8+BOM）。

## 6. その他の既知事象

- PlayMode中のスキーマ再コンパイル発火→ドメインリロードで初期化破壊。worktree初回はPlayMode前に`uloop compile`
- `.moorestech-external-revisions.json` / `_CompileRequester.cs` はUnityが自動書き換え。スキーマ未更新なら`git checkout --`
- worktreeに`moorestech_web/node/`が無くWebUiHostがエラーを出すが**初期化は継続する**（無視可）
- 論理座標とView層`BlockGameObject.OriginalPos`は回転・footprint origin規約でズレうる。View層APIにはView層の座標
- `control-play-mode --action` は Play/Stop/Pause のみ。再生確認はEDCで`isPlaying`を読む
- 撮り損ねた瞬間は二度と撮れない。中間状態は録画（Record=true）で押さえ、スクショは節目の証拠用

## 7. worktree環境の新規構築

- `Library`(28G)はメインから`cp -Rc`（APFS clonefile）で複製すると再インポート不要で数秒。
  **メイン側のUnityを閉じてから行う**
- `Assets/PersonalAssets`（非公開アセット）・`UserSettings` も同様にコピー
- 複数worktreeのUnity Editorは同時起動できる（プロジェクトパスが違えば独立）。
  ただし**PlayModeの同時実行は不可**（ポート11564固定・上記1(a)）
- 初回はPlayMode前に`uloop compile`を一度通す（スキーマ再コンパイルのPlayMode中発火を防ぐ）
- DebugObjectsBootstrap: 無効化しないとIngameDebugConsoleが毎フレームNREを吐く環境がある
  （エラー1.9万件で本当のエラーが埋もれた実績）。DSLの`PlaytestBoot`が
  `DebugObjectsBootstrap_Disabled` SessionStateフラグを自動設定し、Play終了時に復元する
