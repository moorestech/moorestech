# 走りのトグル／ホールド：unityプレイ録画テスト

## 結果

対象製品HEADは`3469ceeac0`（同値方式確認の状態保持修正込み）。最終録画は40/40項目成功、ErrorLogs 0。設定ページのホールド選択、アバター・地面・HUD、動画から抽出した2秒／22秒フレームを目視確認した。

| 操作 | 水平速度 | 走り状態 |
|---|---:|---|
| 保存値なしで起動、Wだけ押す | 7.500 m/s | true |
| Shift単独タップ後、W | 3.500 m/s | false |
| 配置中Shift+R後、W | 3.500 m/s | false |
| インベントリ中Shiftタップ後、W | 3.500 m/s | false |
| 設定でホールドへ変更、W | 3.500 m/s | false |
| Wを保持したままShift押下 | 7.500 m/s | true |
| Wを保持したままShift解放 | 3.500 m/s | false |

Shift+Rのベルト方向は `North → UpNorth`（期待 `VerticalRotation()`）だった。設定変更は `pause-menu-open-settings → sprint-mode-option-hold` のDOM座標取得・マウス注入経路を通り、PlayerPrefsへの`Hold`保存を検証した。速度測定は実アバターの期待値（MoveSpeed 3.5 / SprintSpeed 7.5）と比較し、全区間でGrounded・高さ不変を確認した。

## 再起動確認・後片付け

最終録画で保存されたHoldを変更せず、Editor PID36529を専用projectPathで照合してkillし、ps終了確認後にuloop launchで新PID31795を起動した。新プロセスでHold保存値を確認してから新規PlayModeを起動し、15/15項目成功、ErrorLogs 0。歩き3.503 → Shift押下7.463 → 解放3.504 m/sを実測し、設定読込・保存値・接地を確認した。OSプロセス再起動の未確認事項W01は解消。原本は `moorestech_client/PlaytestResults/20261016_203823/sprint-hold-restart/result.json`、画像は同runの `01-restart-hold.png`。再起動確認はRecord=false。

新Editor PID31795を親担当へ引継ぎ。試験終了時EDC実測: `prefExists=False, background=ResetAndDisableNonBackgroundDevices, editor=PointersAndKeyboardsRespectGameViewFocus, webMode=, playing=False`。試験前の設定へ復元済み。

共有ページ（動画・画像・生JSON）:
https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-10-16-sprint-toggle-mode-20261016/

## 不正保存値の復旧通知

別の新規PlayMode起動前にPlayerPrefsへ不正値`2`を保存した。`20261016_203928/sprint-invalid-recovery`はRecord=false、13/13項目成功、ErrorLogs 0。起動時の実保存値がToggleへ修復され、設定ページに復旧文が出た。Shiftで歩きへ切替後、同じToggleをクリックすると通知だけ消え、ゲーム復帰後も歩き3.508 m/s・sprint=falseを維持した。

原本は`moorestech_client/PlaytestResults/20261016_203928/sprint-invalid-recovery/`。`01-recovery-notice.png`と`02-recovery-acknowledged.png`で通知の表示・消去を目視した。`03-recovery-walking.png`とresult.jsonに歩き維持を記録。result.json SHA256は`1e50a6483aea3f1f0b1b6a781ed132e3609e3c24f6b166fb9f4c793e53d08310`。

## 成果物原本

リポジトリルートからの相対パス:

- 最終成功run: `moorestech_client/PlaytestResults/20261016_203553/sprint-toggle-mode/`
- 生結果: `result.json`（SHA256 `4d936be42dce6564928efb2c077f01eeab0081765471a35cfd4ad76e463dcc3d`）
- 録画: `recording.mp4`（29.333333秒、11,737,322 bytes、SHA256 `6d20bd834f3a196d75c36433ad73f66bb302aa5965e9c0dd1867c23da541b003`）
- スクリーンショット: `01-default-running.png`、`02-shift-r-rotation.png`、`03-inventory-shift.png`、`04-settings-hold.png`、`05-hold-running.png`、`06-complete.png`
- 動画抽出画像: `video-02s.png`、`video-22s-settings.png`、`video-24s-game.png`
- 詳細な試行履歴: `.superpowers/sdd/task-4-rerun-report.md`（作業用、git管理外）

## 試験環境と範囲

- Unity 6000.3.8f1。互換masterはコミット`a159a4efbc0067ece717d51b54b9e95295adf0b4`。
- 入力はすべてSemanticInput / InputSystem.QueueStateEvent。OSキー・マウス注入なし。
- 各シナリオは録画開始前に背景 `Application.isFocused=False`、keyboard/mouse有効、Tab往復、inventory-sort DOM応答を確認する。
- Editor限定の入力設定を一時的に`IgnoreFocus` / `AllDeviceInputAlwaysGoesToGameView`へ変更。元値`ResetAndDisableNonBackgroundDevices` / `PointersAndKeyboardsRespectGameViewFocus`はシナリオのfinallyで戻す。
- 遅延ロード地形と試験足場が干渉する可能性を除くため、Unity APIで100m四方・上面302mの一時足場を配置した。Prefab/Sceneアセット編集なし。
- 開発用Viteの起動疎通が失敗したため、既存`WebUiProductionArtifactBuilder.StageArtifact`でビルド済みdistを配置し、manifest6ファイル検証後に`MOORESTECH_WEBUI_MODE=prod`で実行した。試験後は元の未設定へ戻す。
- 録画開始時の一時timeScale=0を条件待機し、1へ復帰後に実測する。測定値は水平距離/実際のTime.time差。許容差±0.5 m/s。
- フライ×6はこの録画の対象外。既存`PlayerFlightMotionTest`で別途検証済み（親タスク記録）。

## 再実行

スキル `unity-playmode-recorded-playtest` と `references/input-injection.md` に従う。主シナリオは `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/sprint/sprint-toggle-mode.cs`。録画する版はRecord=true、調査時はPlaytestResults/scratchへコピーしてfalseへ変更する。

1. PlayModeを止め、PlayerPrefsキー`SprintInputMode`の有無と値、環境変数`MOORESTECH_WEBUI_MODE`を退避する。既定値の試験に先立ってPlayerPrefsキーだけを削除・Saveする。
2. 同梱Nodeは`bash moorestech_web/setup.sh`、webui依存は専用worktreeでpnpm install。本番UIを使う場合はpnpm build済みdistをStageArtifact経由で配置し、manifestを検証して環境変数をprodへ設定する。
3. `uloop compile --project-path ./moorestech_client`。全スニペットのAPI参照はEDC投入時にもコンパイルされる。
4. スキルのrun-scenario.shへクライアント・シナリオ・互換masterのserver_v8を渡す。通常のマスタ自動解決が通らないworktree配置では第3引数を明示する。
5. boot応答消失後に`ERROR: already playing`になった場合、bootを重複送信せず`PlaytestPaths.SessionDirectory`とready.markerを確認する。その新規セッションへEDCの`--code-file`でシナリオを投入する。今回の前景再実行補助は`.superpowers/sdd/task-4-run.py`にある。
6. CEF起動が前面を取ったら、ready後にFinderなどへ前面を戻す。実測でfocused=Falseになってから注入する。GameViewは表タブにしておく。
7. result.jsonのSuccessと全Asserts、動画の非ゼロサイズ、設定画像・実プレイ画像・動画抽出画像を確認する。
8. 保存したHoldを変更せずPlayModeを止める。Editor PIDを専用projectPathで検証してkill、psで終了確認し、uloop-launchスキルで新Editorを起動する。新PIDとHold保存値を実測し、prod環境変数を再設定後、`sprint-hold-restart.cs`で新規起動して復元・速度を確認する。
9. 不正値復旧を再確認する場合、PlayMode停止中にPlayerPrefsへ`2`を保存してSaveし、新規起動で`sprint-invalid-recovery.cs`を実行する（日本語UI前提）。
10. PlayMode停止後、PlayerPrefs・環境変数・Editor入力設定を退避値へ戻し、git statusで自動生成差分を確認する。

製品の入力判定経路や条件式がレビュー修正で変わった場合は、同じ主シナリオを新規セッションで録画し直し、成功した原本パス・動画・観測値を更新する。

生EDC投入による3本のシナリオのコンパイル・実行は成功した。全スニペット診断の同期wrapperがトップレベルawaitへ出すCS4032とは区別する。
