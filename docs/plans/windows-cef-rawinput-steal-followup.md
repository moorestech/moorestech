# Windows版マウスカメラ回転不能（CEF raw input奪取）の残タスク

2026-07-31 調査・応急処置済み。本ドキュメントは経緯の要約と、恒久対応までのやるべきことリスト。

## 真因（確定・調査済み）

cef-unityのWindows専用スクロールモニタ（`cef-unity-rust/crates/client/src/scroll_monitor_windows.rs`）が、
`RegisterRawInputDevices(usagePage 0x01 / usage 0x02 = generic mouse, RIDEV_INPUTSINK, hwndTarget=自前のメッセージ専用ウィンドウ)`
をUnityプロセス内（cef_unity_rust.dll）で呼ぶ。

- Windowsのraw input登録はプロセス単位・同一デバイスクラスにつき「最後に登録した1ウィンドウ」だけが受領（MSDN明記。ライブラリからの使用は非推奨と公式警告あり）
- これによりUnity Input Systemの登録が上書きされ、`WM_INPUT`がCEFの隠しウィンドウへ配送される
- カメラ回転（`InGameCameraController` → Lookアクション → `<Pointer>/delta`）はWM_INPUT由来の`Mouse.delta`駆動のため回転だけ死ぬ。`RIDEV_NOLEGACY`なしなのでカーソル移動・クリック・WebUI操作は生存
- macOS実装はNSEventローカルモニタ（素通し観測型）なのでWindowsのみ発症
- raw input追加はcef-unity `b8ac2e3`（7/29）、moorestechピン反映は `ff01c09b1`（7/31）。それ以前はwin-x64ランタイム未同梱でWindows CEF自体が動かないため、ピンを戻す回避は不可
- devトグル`cef_scroll_legacy`は`CEF_UNITY_DEV_TOOLS`ガード内でリリースビルドではコンパイルされず使用不可

## 実施済み: 応急処置（手段B）

`moorestech_client/Assets/Scripts/Client.Input/WindowsMouseRawInputReclaimer.cs`（コミット `d4e4a8778`、gamescom-review-demo）

- 起動時（BeforeSceneLoad＝CEF起動前）にUnity本来のマウスraw input登録を記録
- 約1秒周期で配送先を確認し、奪われていたら後勝ちルールで登録し直して取り返す
- 発動時はPlayer.logに `[WindowsMouseRawInputReclaimer]` のログが出る
- **既知の副作用**: CEF側ネイティブスクロールソースが飢餓し、Windows版WebUIのホイールスクロールが効かない（フォールバック経路は`HasNativeSource==true`のため読まれない）

## やるべきこと

### 1. winbuild実機検証（最優先・応急処置の効果確認）

- [ ] gamescom-review-demo（`d4e4a8778`以降）をWindowsビルドしてwinbuildで起動
- [ ] Player.logで以下2行を確認
  - `[WindowsMouseRawInputReclaimer] Unityのマウスraw input登録を記録 target=0x... flags=0x...`
  - CEF起動後に `[WindowsMouseRawInputReclaimer] マウスraw input配送を取り返した stolenTarget=0x... -> unityTarget=0x...`
- [ ] マウスでカメラ回転が動くことを確認
- [ ] WebUIのクリック操作が引き続き動くことを確認（ホイールスクロールは死んでいて正常）
- 取り返しログが出ないのに回転が死んでいる場合は真因が別にある可能性 → 再調査（対抗仮説はほぼ潰し済みだが、旧Windowsビルドでの回転動作ベースラインは未確認）

### 2. cef-unity側の恒久対応

推奨は **ForwardRawInput転送方式**（スムーズスクロール品質を保ったまま両立できる唯一の案）:

- cef-unityのスクロールモニタが受領したWM_INPUTを、Unity公式API `UnityEngine.Windows.Input.ForwardRawInput` でUnityへ転送する（Rewiredが同構造で実績あり。UnityがまさにこのAPIをこの衝突のために提供している）
- Rust側で受けたRAWINPUTをC#へ渡す経路（もしくはC#側でWM_INPUT受領ウィンドウを持つ構成への変更）が必要
- 代替案: 登録型をやめて`WH_MOUSE_LL`等の観測型に変更（macOS実装と対称。ただしフックはレイテンシ・AV誤検知の懸念）
- 修正後、moorestechのcef-unityピン（`Packages/packages-lock.json` の jp.juha.cefunity）を更新

### 3. 応急処置の撤去

- [ ] cef-unity恒久対応のピン更新と同時に `WindowsMouseRawInputReclaimer.cs` を削除
- [ ] 削除後、winbuildでカメラ回転とWebUIホイールスクロールの両立を確認

### 4. 派生確認（任意）

- [ ] gamescom本番ブランチ/masterへ応急処置を持っていくかの判断（gamescom-review-demoにしか入っていない）
- [ ] WebUIホイールスクロール不能が当面のデモ体験に影響するUI（リング状リスト・研究ツリー等のスクロールUI）が無いか確認
