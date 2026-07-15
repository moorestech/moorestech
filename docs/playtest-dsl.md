# プレイテストDSL 使い方ガイド

`docs/playtest-stabilization-investigation.md` の提案を実装したもの。
録画付き実プレイ検証を「Bash呼び出し約170〜190回」から「シェル1コマンド」に圧縮する。

Implementation of the proposal in the stabilization investigation: compresses a recorded
play-verification session from ~170-190 Bash calls down to a single shell command.

## 構成 / Layout

- `moorestech_client/Assets/Scripts/Client.Playtest/` — 事前コンパイル済みDSL（Editor専用asmdef、Test Runner非依存）
- `.claude/skills/unity-playmode-recorded-playtest/scripts/preflight.sh` — 実行前チェック（疎通/コンパイル/master実在/マスタロードドライラン）
- `.claude/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh` — 一発実行ランナー
- `.claude/skills/unity-playmode-recorded-playtest/scenarios/*.cs` — シナリオ置き場（execute-dynamic-codeに渡すC#スニペット。コンパイル資産にはならない）

シェルスクリプトとシナリオは `unity-playmode-recorded-playtest` スキルに同梱されており、リポジトリ側の `tools/` には置かない。
- 成果物は `moorestech_client/PlaytestResults/<セッション>/<ラン名>/`（result.json・スクショ・mp4、git管理外）

## 一発実行 / One-shot run

```bash
cd <worktree-root>
SKILL=.claude/skills/unity-playmode-recorded-playtest
"$SKILL/scripts/run-scenario.sh" \
    /path/to/moorestech_client \
    "$SKILL/scenarios/sample-chest.cs" \
    [master-server-dir]   # 省略時: /Users/katsumi/moorestech-worktrees/playtest-master/server_v8
```

流れ: preflight → `PlaytestBoot.PrepareAndEnterPlayMode`（NoSave・masterパス設定・PlayMode起動）→
ready.marker出現待ち（初期化完了イベント駆動、実測~26秒）→ シナリオをEDC 1回で投入 →
result.json出現待ち → 表示（Successでexit 0）。

## シナリオの書き方 / Writing scenarios

execute-dynamic-code のスニペット形式（using可・文の羅列・returnで値を返す）:

```csharp
using Client.Playtest;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };   // Record=trueでmp4録画
return PlaytestRunner.Run("my-scenario", options, async p =>
{
    await p.SetupFlatGround();                            // 足場(50x3x50 @ y30)生成+ワープ
    p.PlaceBlockDirect("木のチェスト", new Vector3Int(3, 32, 3), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(3, 32, 3)); // クライアント側出現待ち
    await p.GiveItem("鉄インゴット", 16);                   // 本番giveコマンド経路+反映待ち
    p.GiveItemDirect("鉄板", 8);                           // サーバー直挿入（即時）
    await p.Until(() => p.CountItem("鉄インゴット") >= 16, 10f, "在庫反映");
    p.Assert(p.GetBlock(new Vector3Int(3, 32, 3)) != null, "チェスト存在");
    await p.Screenshot("final");                           // UI込みGameViewスクショ
});
```

主なDriver API: `SetupFlatGround` / `WarpPlayer` / `PlayerPosition` /
`GiveItem`(コマンド経路) / `GiveItemDirect` / `CountItem` /
`PlaceBlockDirect` / `RemoveBlock` / `GetBlock` / `WaitBlockGameObject` /
`Until(条件, timeout, ラベル)` / `WaitSeconds` / `Assert` / `Screenshot` /
`SendCommand` / `ServerService<T>()`。

- Direct系＝サーバーデータストア直叩き（状態を素早く作る用・インベントリ非消費）
- 非Direct系＝本番プロトコル経路
- 例外・タイムアウト・Assert失敗はすべて result.json に落ちる（実行は`Error`に記録して終了）

## UI経路操作（Phase 3） / UI-route operations (Phase 3)

実プレイヤーと同じキーマウス経路（B→ビルドメニュー→設置プレビュー→クリック/ドラッグ設置）で
構築するAPI群。入力は`Client.Playtest/Input/SemanticInput.cs`がQueueStateEventで注入する
（InputSystem.Update()は呼ばず通常フレーム更新に委ねる。フォーカス不要）。

UI-route APIs that build through the same key/mouse path as a real player. Input is injected by
SemanticInput via QueueStateEvent (no InputSystem.Update() calls; focus not required).

```csharp
// UI設置の前提を1行で: アンロック＋マスタのRequiredItems×個数分の建設コスト付与（クライアント在庫反映待ち込み）
await p.PrepareBlockForUiPlacement("ベルトコンベア", 15);
// ドラッグ設置: ベルトの向きは経路から自動解決（(2,2)→(2,6)なら北向き5本）
await p.DragPlaceViaUi("ベルトコンベア", new Vector3Int(2, 32, 2), new Vector3Int(2, 32, 6));
// 単クリック設置: 向きはデフォルトNorth（回転キー注入は未実装）
await p.PlaceBlockViaUi("木のコンベアチェスト", new Vector3Int(4, 32, 8), BlockDirection.North);
await p.ExitToGameScreen();
```

分解して使う場合: `UnlockBlock(name)`（サーバー側アンロック→イベントでクライアント同期）と
`GiveConstructionCost(name, blockCount)`（RequiredItems解決＋give経路＋クライアント同期待ち）。

低レベルAPI: `PressKey(Key)` / `SelectHotbar(slot)`(0始まり=キー1) / `AimAt(worldPos)` /
`ClickPlace()` / `WaitUiState(UIStateEnum, timeout)` / `CurrentUiState` /
`PlaytestUiOps.OpenBuildMenuAndSelectBlock(blockName)`（ビルドメニューのスロットはEventSystem直叩きでクリック）。

- 設置原点の照準は`PlaytestUiOps.PlaceAimPoint`がCalcPlacePointを逆算（接地面上のフットプリント中心）
- 足場は上面がy=32ちょうど（`SetupFlatGround`が保証）。プレビューの`Floor(hit.y)`がブロックグリッドと一致する条件
- 等価性シナリオ: スキル同梱の `scenarios/belt-line-via-ui.cs`（belt-line.csと同一ライン・同一assertをUI経路のみで構築）

## ハマりどころ（今回実際に踏んだもの） / Pitfalls actually hit

1. **masterデータとスキーマの不整合**: moorestech_masterリポジトリHEADが別ブランチ（plan4）用に
   移行していると `MooresmasterLoaderException` でPlayMode初期化が無言死する。
   → ブランチ互換コミットにピン留めしたmasterのgit worktree（`playtest-master`、現在584a14e）を使う。
   preflight [4/4] のマスタロードドライランがPlayMode前に検出する。
2. **コンパイル直後のEDC失敗**: ドメインリロード中のexecute-dynamic-codeは失敗する。
   スクリプト内の`edc_retry`/`edc`がリトライで吸収。手で叩くときも1回の失敗で諦めない。
3. **worktree初回起動のスキーマ再コンパイル**: 「Schemaフォルダに変更がありました」がPlayMode中に
   発火するとドメインリロードで初期化が破壊される。初回はPlayMode前に一度コンパイルを通して落ち着かせる。
4. **DebugObjectsBootstrap**: 無効化しないとIngameDebugConsoleが毎フレームNREを吐く環境がある。
   `PlaytestBoot`がテストと同じ`DebugObjectsBootstrap_Disabled`フラグで自動無効化する。
5. **worktreeのLibrary**: メインの`Library`(28G)を`cp -Rc`（APFS clonefile）で複製すれば再インポート不要。
   メイン側のUnityが閉じている時に行うこと。

## UI経路のハマりどころ（Phase 3で実際に踏んだもの） / UI-route pitfalls actually hit

1. **legacy Input直読みは注入で駆動できない**: `UnityEngine.Input.mousePosition`/`GetKeyDown`直読み（21ファイル）は
   QueueStateEvent注入が効かない。設置プレビュー・UIState遷移キー・右クリックカメラ等の主要経路は
   `Client.Input.HybridInput`（InputSystem優先＋legacyフォールバック）へ移行済み。新規コードもHybridInputか
   InputManagerを使うこと。
2. **PlaceBlock中のメニュー再オープンはTab**: BはGameScreenへ抜ける。さらにTabはOpenInventoryと同キーのため、
   遷移判定の順序次第でインベントリに食われる（実プレイでも死んでいたバグをこのテストが検出し、修正済み）。
3. **単クリック設置の向きはNorth固定**: `_currentBlockDirection`は place system 内部状態で外から読めない。
   回転が必要なラインはドラッグ設置（向き自動解決）で構成するか、Northで成立する配置を選ぶ。
4. **足場の上面高さ**: 素のCubeは`GroundGameObject`が無く設置プレビューのレイキャスト対象にならない。
   また上面がy=32ちょうどでないと`Floor(hit.y)`がずれて1段下に埋まる。`SetupFlatGround`が両方を保証する。
5. **サーバーポート11564は固定**: 他worktreeのPlayModeが占有していると起動が「Address already in use」で
   無言死する（ready 300秒タイムアウト）。さらにPlayMode停止後もソケットがリークして残ることがあり、その場合は
   当該Editorへ`UnityEditor.EditorUtility.RequestScriptReload()`を打つと解放される。preflight [5/5]が事前検出する。
6. **HoldingItemId駆動の設置（歯車チェーンポール等）**: ビルドメニュー選択でなく「ホットバーの手持ちアイテム」で
   place systemが切り替わる。`GiveItemToHotbar(slot,...)`→`SelectHotbar(slot)`で手持ちにし、UI stateは
   ビルドメニュー経由でPlaceBlockに入れておく（例: `gear-chain-pole-via-ui.cs`。連続延長の起点は
   `ExitToGameScreen()`でリセットでき、セグメントを分離できる）。

## 今後 / Next

- 回転キー（R）注入による設置方向指定（place system内部状態の追跡が必要）
- ホットバー選択→設置のplaceModeスイッチ経路（HoldingItemId駆動）の検証
- Enter Play Mode Optionsのドメインリロード無効化調査（Play開始の大幅短縮）
