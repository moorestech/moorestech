# 画質設定の操作込み録画検証

## 完了基準の計測（2026-10-21 Mac mini）

particle再適用修正後の最終コードで、中プリセット・録画ありで平均 **24.916fps**。20fps以上の完了基準を満たした。目標30fpsには未達。

| 指標 | 実測値 |
|---|---:|
| 平均fps（フレーム数 / unscaledDeltaTime合計） | 24.916 |
| p50 フレーム時間 / fps換算 | 33.735ms / 29.643fps |
| p95 フレーム時間 / fps換算 | 64.349ms / 15.540fps |
| 計測フレーム数 | 1,508 |
| フレーム時間合計 / 実経過時間 | 60.523秒 / 60.518秒 |
| 実時間による平均fps（照合） | 24.918 |
| 歩行距離 | 264.993m |
| ブロック / 歯車Processor数（非active含む） | 13,852 / 10,215 |
| 解像度 / active Terrain数 | 1280×720 / 1 |
| targetFrameRate / vSyncCount | 60 / 1 |
| Recorder captureDeltaTime | 0.03333334秒 |
| フォーカスが有効だった計測フレーム | 1,508 / 1,508 |

- Record=true、Unity Recorder + PlaytestOverlayあり。Record=falseの補助値ではない。Recorderは30fps設定。unscaledDeltaTime集計と実時間の平均が一致することも照合した。
- 元worldはshareのworld_1のworld.json/map.json/save.jsonを専用worktreeのPlaytestResultsへ複製し、固定world入口（template、seed=0、AutoSave=false）でロードした。原本・通常savesは変更せず、退避・復元は不要。
- masterはコミット923367a2のpin worktree。マスタロードのドライランとコンパイル成功。
- 固定world入口が指定するOther環境を、このマシンの通常再生で保存されているDebug（0）へ合わせた。人工足場を追加していない。開始地点(659.5,96.6,586.5)は既存の機械・搬送ラインの間。全13,852ブロックのView受信後、10秒ウォームアップ。
- CinemachineBrainは有効のまま。通常カメラ追従でW/Sを3秒ずつ交互に入力。計測開始24秒以降、ビルドメニュー→木のチェスト選択→前方セルへのクリック→本番サーバー反映→設置アニメ完了→通常画面復帰を実行。歩行・設置・全フレーム前面をアサートした。
- 設置はUI経由で通った。事前scoutの「足場y=32でなければUI設置できない」という否定断定は採用しなかった。最初の試行は固定セルが歩行後の背後となりoff-screenで失敗したため、歩行後の前方セルへ変更した。負荷条件を軽くして再計測したものではない。
- PlayerPrefsのGraphicsQualityは作業前に存在せず。シナリオfinallyでDeleteKey→Saveし、PlayMode終了後にHasKey=false/value空/environment=0を確認した。

実行: `PLAYTEST_WORLD_DIRECTORY=<worktree>/moorestech_client/PlaytestResults/worlds/graphics-quality-world_1 PLAYTEST_MAP_MODE=template PLAYTEST_SEED=0 .agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client .agents/skills/unity-playmode-recorded-playtest/scenarios/graphics-quality/quality-fps-measure.cs <pin-923367a2>/server_v8`

結果: `moorestech_client/PlaytestResults/20261021_012642/quality-fps-measure/result.json`（Success=true、ErrorLogs空）。録画・開始/完了スクリーンショットが存在し、アバター・地面・HUD・機械/搬送ライン・設置チェストをゲーム視点で目視した。
`uloop get-logs --project-path ./moorestech_client --search-text '[PERF]' --max-count 2` で同値を回収した。

比較元（引継ぎ資料、今回の実測ではない）: MacBook・Editor・カメラ固定で、対策なし7〜9fps、常時軽量化3つ14.1fps、さらに描画距離300mで35.5fps。マシン・録画・カメラ・操作条件が違うため直接の速度比には使わない。

## UI録画

`20261021_010009/quality-ui-switching` は13/13アサート成功、ErrorLogs空。Esc→トップ4ボタン→設定→低→高→影だけオフ→カスタム→閉じるを実クリックで通した。トップボタン矩形は全て1280×720内。低far=300/影off/scale=0.5、高far=5000/影on/scale=1を実測。動画10,904,231byte、画像6枚。低/高のゲーム画面とカスタム設定表示を目視済み。

## 状態受信と画面の追加証拠

- 水位: `20261021_012448/quality-water-level`、10/10成功、ErrorLogs空。実パイプへサーバー流体ノード経由で100→250→450を投入し、受信値100/500→250/500→450/500、描画マテリアル0.200→0.500→0.900を実測。ProcessorにUpdateがないことも確認。東向きにした窓がアバターと重ならず、水面の変化をゲーム画面で目視した。通常環境Debugのルート有効、environment=0、active Terrain=1を確認。
- SSAO: `20261021_012304/quality-ssao-visual`、2/2成功、ErrorLogs空。同設定on/onの画像差0.000000に対しon/offは0.333562（0–255のRGB絶対差の平均）。オーバーレイを除くx=700..1199、画像下端基準y=120..399の領域で比較。チェスト周辺の接触部の差をon/off画像で確認した。
- エフェクト/列車: `20261021_012120/quality-effects-train`、12/12成功、ErrorLogs空。既存VFX off、新設置した蒸気機関の設置アニメ完了後VFX1件off、新規機関車へEffectsQualitySwitch付与/VFX1件off、onで復帰を実測。機関車は本番設置プロトコルを使用。橋脚・蒸気機関の準備はサーバー直であり、建築UIの検証はfpsシナリオに担わせた。
- 実オブジェクトにあるParticleSystem/Lightは、ロード済みブロック・既存列車・新設置蒸気機関を束ねたSwitch配下で各0件だった。全未使用prefabを網羅したという意味ではない。
- 合成particle（実prefabとは区別）: 修正前は非activeのplayOnAwake子を再有効化するとisPlaying=true、手動Play後にoffを再適用してもtrueをEDCで実測。設置完了イベントから現在画質を再適用し、既にoffでも保存済み状態を書き換えず停止する修正を実施。修正後録画で再有効化/手動Play後の再適用による停止をアサートし、対象NUnit 2/2成功。追加の手動Playが再適用より後に行われる任意の経路を毎フレーム監視する仕組みは追加していない。現行Client.GameのParticleSystem.Play呼出箇所はSwitchの復元経路のみ（検索実測）。

各最終実行の動画・画像は0byteではなく、result.jsonの全アサートが成功。アバター・地面・HUDを含むゲーム視点で目視した。動画の保存時刻はRecorder固定30fpsによるもので、fps集計の実時間窓とは別。

最小修正前の初回録画（20261021_010920）は24.830fps。上表は修正後の再計測（20261021_012642）であり、同じ原本複製・設定・操作条件を用いた。両方Record=true。
