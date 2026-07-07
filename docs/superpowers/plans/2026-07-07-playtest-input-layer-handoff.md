# キーマウス操作による恒常構築プレイテスト（Phase 3）申し送り

作成: 2026-07-07 / ブランチ: `feature/playtest-stabilization`（worktree: `/Users/katsumi/moorestech-worktrees/playtest`）

## ゴール

実プレイヤーと同じキーマウス操作経路（ホットバー選択 → B → 設置プレビュー → クリック設置 等）で
シナリオを**恒常的に**構築・実行できるようにする。現状の `PlaceBlockDirect`（サーバーデータストア直叩き）では
捕まえられないUI・操作系・設置プレビュー系のバグをE2Eで検証可能にするのが目的。

## 現状（すべて動作確認済み・コミット済み）

- **DSL Phase 1+2 完成**: `Client.Playtest` asmdef + `tools/playtest/run-scenario.sh` の1コマンド一発実行
  （preflight 4項目 → PlayMode起動 → ready.marker（実測18〜26秒）→ EDC 1回でシナリオ投入 → result.json回収）
- **実証済みシナリオ**: `belt-line.cs` — S字ベルト9本＋コンベアチェストへ鉄インゴット10個搬送、
  録画mp4・スクショ4枚・assert4件全通過（コミット c6afe8c5f）
- **masterデータ**: ブランチ互換コミットへピン留めした `/Users/katsumi/moorestech-worktrees/playtest-master`
  (server_v8, 584a14e)。共有 `moorestech_master` のHEADはplan4スキーマに移行済みで**使うと初期化が無言死する**
- **ノウハウの正**: `unity-playmode-recorded-playtest` スキル（メインチェックアウト側に未コミット差分が残存、要コミット）
- Unity Editor(6000.3.8f1)はこのworktreeで起動済み・他worktreeのEditorと並行動作可

## 最重要ブロッカー（今回実測で判明・調査レポートの「legacy残存3箇所」は過小評価）

legacy `UnityEngine.Input` 直読みは**21ファイル**あり、QueueStateEvent注入（InputSystem専用）では一切駆動できない。
恒常構築に直結する箇所:

1. **設置プレビューのレイキャスト**が `UnityEngine.Input.mousePosition` 直読み
   - `Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs:35,56,73`
   - `Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/ElectricWireEditMode.cs:65`
   - → **マウスを注入しても設置プレビューが動かない。Phase 3の本丸**
2. **UIState遷移キー**（B/G/T/R/Tab/F3）が `UnityEngine.Input.GetKeyDown(KeyCode.*)` 直読み
   - `Client.Game/InGame/UI/UIState/State/` 配下: GameScreenState / PlaceBlockState / BuildMenuState /
     DeleteObjectState / PlayerInventoryState / ChallengeListState / DebugBlockInfoState、および `UIRoot.cs`
3. **右クリックカメラ**: `UI/UIState/Input/ScreenClickableCameraController.cs:42,49` の `GetMouseButton(1)`

## 移行の正解パターン（コードベース内に前例あり・これに揃える）

- **マウス座標**: `Client.Game/InGame/Control/BlockClickDetectUtil.cs:48` が既にハイブリッド対応済み:
  ```csharp
  var mousePosition = Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : UnityEngine.Input.mousePosition;
  ```
  この形へ揃えれば QueueStateEvent 注入が効き、実プレイ互換も維持される（PlaceSystemUtil等へ横展開）
- **キー**: `InputManager`（`Client.Input`、InputSystemベース）併用が各Stateに既にある
  （例: `InputManager.UI.CloseUI.GetKeyDown || UnityEngine.Input.GetKeyDown(KeyCode.B)`）。
  `Keyboard.current[Key.B].wasPressedThisFrame` とのORフォールバックが最小差分
- **注入の既存資産**: `Client.Tests/EditModeInPlayingTest/OsInput/OsInputSpoof.cs`（QueueStateEvent抽象化層）
  - 注意1: `Client.Tests` は `UNITY_INCLUDE_TESTS` 制約付きで **Client.Playtestから参照不可** →
    `Client.Playtest/Input/SemanticInput.cs` として移植する（DebugKeyにB/G/T/R/1〜9を追加、マウス絶対座標移動を追加）
  - 注意2: OsInputSpoofの `FlushInputEvents` は `InputSystem.Update()` を呼ぶ設計で、スキルの
    「InputSystem.Update()はスニペットで呼ばない」ガイドと衝突。移植時はQueueStateEventのみ発行し
    通常のUpdateループに処理させる形へ変える（要挙動検証）

## 推奨実装ステップ

1. **legacy Input移行（プロダクトコード修正・最小差分）**: 上記ブロッカー3群をフォールバック形式へ。
   操作感は変えない。`uloop run-tests --filter-type regex` で既存EditModeInPlayingTestの回帰確認
2. **SemanticInput.cs**（Client.Playtest/Input/）: KeyDown/KeyUp/Click/MouseMoveTo(screenPos) を
   QueueStateEventで注入。`Mouse.current`/`Keyboard.current` が無ければ `InputSystem.AddDevice` で生成
3. **Driver拡張（via UI系API）**:
   - `SelectHotbar(int slot)` … 1〜9キー注入
   - `PressKey(Key key)` … UIState遷移（B=建設メニュー等）
   - `AimAt(Vector3 worldPos)` … `Camera.main.WorldToScreenPoint` → マウス絶対座標注入
   - `ClickPlace()` / `PlaceBlockViaUi(string blockName, Vector3Int pos, BlockDirection dir)` … 統合操作
4. **等価性検証シナリオ**: `belt-line.cs` と同一のS字ライン＋同一assertを **UI経路のみ**で構築する
   `belt-line-via-ui.cs` を作り、direct経路との結果一致を恒常テスト化
5. スキル（unity-playmode-recorded-playtest）と `docs/playtest-dsl.md` へ反映

## 落とし穴・注意（実測済みのもの）

- **カメラとマウスの結合**: マウス移動はカメラ回転に食われる。`PlaceBlockState.cs:40` に
  `_isChangeCameraAngle = !UnityEngine.Input.GetKey(KeyCode.LeftShift)` があり、**LeftShift押下中は
  カメラ回転が止まる**仕様 → AimAtは「LeftShift押下＋スクリーン座標直接指定」から始めるのが安全
- クリック座標は `collider.bounds.center` → `WorldToScreenPoint`。`uloop screenshot --capture-mode rendering`
  の座標系と一致。UI要素の座標は `--annotate-elements` で取得可能
- uGUIボタン操作は `ExecuteEvents`（EventSystem直叩き）も選択肢（OSカーソル非依存・カメラ非干渉）
- **run-scenario.shの一発実行を維持すること**。固定sleepやEDC多重往復に戻らない（Untilと結果JSONで待つ）
- 搬送ライン系は投入前に `BlockConnectorComponent<IBlockInventory>.ConnectedTargets.Count` をassert。
  接続0は座標ミスのほか**受け側マスタの`inputConnects`が空**（例: 素の「木のチェスト」はベルト搬入不可、
  コンベアチェストを使う）
- worktree初回起動時: PlayMode前に `uloop compile` を一度通す（スキーマ再コンパイルがPlayMode中に
  発火するとドメインリロードで初期化が破壊される）

## 参照

- 調査レポート: `docs/playtest-stabilization-investigation.md`（問題ランキング・既存資産の棚卸し）
- DSL使い方: `docs/playtest-dsl.md`
- 実装: `moorestech_client/Assets/Scripts/Client.Playtest/`、`tools/playtest/`
- 成果物例: `moorestech_client/PlaytestResults/20260707_030121/belt-line/`（result.json・録画）
