# ユースケース: DSLが無いブランチで手動フローで検証する（方式B・フォールバック）

`Client.Playtest/` が存在しないブランチ用。長期作業ならDSLのあるブランチ
（`feature/playtest-stabilization`、コミット ad7535766 以降）の取り込みを先に検討する。
入力注入・masterピン留め・診断は各リファレンス（input-injection / troubleshooting）が共通で適用される。

## 全体フロー

```
1. (前提) Unity起動、CLI Loopサーバ起動済み（uloop listで確認）
2. NoSaveフラグ:  SessionState.SetBool("moorestech_SkipSaveLoadPlayMode", true)
   masterパス:    DebugParameters.SaveString(ServerDirectory.DebugServerDirectorySettingKey, <ピン留めmaster>)
   デバッグ無効化: SessionState.SetBool("DebugObjectsBootstrap_Disabled", true)
3. uloop control-play-mode --action Play
4. readiness polling（下記ガード付きuntil。固定sleepの多段待機はしない）
5. シナリオ準備（世界配置・カメラframing・UI状態遷移）— まだ録画しない
6. de-risk probe: 単体入力を注入して期待状態を1つ確認（録画前の必須ゲート）
7. Recorder起動（AppDomainに保持）← アクション直前で初めてON
8. シナリオ実行（API叩き / QueueStateEvent注入 / リフレクション遷移）
9. 各ビートで uloop screenshot --capture-mode rendering + 内部状態read
10. Recorder停止（アクション直後に即OFF）→ control-play-mode --action Stop
11. 動画確認（0 byteは失敗）。必要なら ffmpeg -ss -t -c copy で切り出し
```

## readiness pollingのガード（無限ループ事故防止・5点全部付ける）

ポーリングを `until <cmd> | grep -q <pattern>` "だけ"で書くと、スニペットのコンパイル失敗
（存在しないAPI・shellエスケープで`&&`が化ける等）で`Result`が空になり**無限ループ化する**。

1. **事前単発検証**: ループ化前に1回叩いて `Success: true` + `Result` の形式を目視
2. **ループ内でエラーを毎回判定して即abort（最重要）**: `error CS` / `CompilationErrors`非空 /
   `"Success": false` / `Result`空 → 待たずに停止。`Domain Reload`だけは一過性なので短く待って再試行。
   判定優先順位は**エラー検知 > 成功検知 > 待機**
3. **timeout必須**（最後の保険。エラー検知の代わりではない）。抜けたら `uloop get-logs --log-type Error`
4. **grepはResult行に限定**（`grep -q "Result.*ready=True"`）
5. **sleepは5秒以上**（短間隔はドメインリロードを煽る）

長いC#は`--code`直書きせず**一時ファイルに書いて`$(cat file)`で渡す**とエスケープ事故を防げる。
検証系スニペットは`$"..."`補間より`"x=" + val`連結が安定。

## 待機の取り方

- PlayModeの時間を進める待機は**shell sleep + 別スニペット**。EDC内`Thread.Sleep`は
  main threadを握ってUpdateごと止めるので禁止
- `InputSystem.Update()`をスニペット内で呼ぶとeditor-update文脈になりInputActionの
  `WasPressedThisFrame`/`IsPressed`が発火しない。**「queue → shell sleep（本物のフレームを進める）→ 別スニペットでread」が唯一機能する**

## Recorderの手動制御（DSLの`Record=true`が使えない場合）

snippet間でRecorderControllerを持ち越すには`AppDomain.SetData/GetData`が唯一の手段
（static/EditorPrefs不可、Domain Reloadで消える）:

```bash
uloop execute-dynamic-code --project-path <proj> --code '
using System;
using UnityEngine;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;

var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
settings.SetRecordModeToManual();    // 忘れるとFrameIntervalで勝手に停止する
settings.FrameRate = 30;
var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
movie.name = "playtest";
movie.Enabled = true;
movie.EncoderSettings = new CoreEncoderSettings {
    EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
    Codec = CoreEncoderSettings.OutputCodec.MP4,
};
movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1280, OutputHeight = 720 };
movie.OutputFile = "/tmp/playtest";   // .mp4自動付与
settings.AddRecorderSettings(movie);
var ctrl = new RecorderController(settings);
ctrl.PrepareRecording();
ctrl.StartRecording();
AppDomain.CurrentDomain.SetData("playtest_recorder", ctrl);
return "recording started isRec=" + ctrl.IsRecording();'
```

停止は`GetData`で取得して`StopRecording()` → `sleep 2`（muxer完了待ち）→ `ffprobe`確認。
`scripts/start-recording.sh` / `scripts/stop-recording.sh` が参考実装。

**録画ウィンドウは最小に**: boot待ち・世界配置・framing・de-risk probeは録画OFFのまま行い、
検証アクションの直前ON/直後OFF。尺の主因は入力ステップ間の必須sleep 0.5なので録画窓を絞るのが効く
（実測: 全部録ると3分/80MB、アクションだけだと1:23/34MB）。1280×720 30fps H264で約25MB/分。

## サーバー設置API（手動時）

- `WorldBlockDatastore.TryAddBlock` は**5引数版**（`blockId, pos, direction, Array.Empty<BlockCreateParam>(), out var b`）が確実
  （拡張メソッド版はCS7036になることがある）
- 設置後はクライアントView生成待ちが必要（sleep 2）

## スクリーンショット

- `--capture-mode rendering`: GameViewレンダリング結果のみ（PlayMode必須・検証用は基本これ）。
  `window`はツールバーが映り込む
- 検証スクショに**期待UI要素が映っているか**まで確認する（内部dumpだけだと「データは正しいが
  UI Prefab配線が違う」を見落とす。SubInventory等を開いて撮る）

## 完了判定（成功条件4つ全部）

1. 動画が想定サイズで生成（`ffprobe`でDuration確認、0 byteは失敗）
2. 期待する内部stateの達成（EDC read）
3. 検証スクショに期待UI要素が映っている
4. **絵が実プレイ視点**（アバター・地面・HUDが映る。俯瞰直置きカメラは不合格）
