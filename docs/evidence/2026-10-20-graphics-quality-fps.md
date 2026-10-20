# 画質設定の操作込み録画検証

## 完了基準の計測（2026-10-21 Mac mini）

中プリセット・録画ありで平均 **24.830fps**。20fps以上の完了基準を満たした。目標30fpsには未達。

| 指標 | 実測値 |
|---|---:|
| 平均fps（フレーム数 / unscaledDeltaTime合計） | 24.830 |
| p50 フレーム時間 / fps換算 | 33.600ms / 29.762fps |
| p95 フレーム時間 / fps換算 | 67.957ms / 14.715fps |
| 計測フレーム数 | 1,502 |
| フレーム時間合計 / 実経過時間 | 60.490秒 / 60.524秒 |
| 実時間による平均fps（照合） | 24.817 |
| 歩行距離 | 262.994m |
| ブロック / 歯車Processor数（非active含む） | 13,852 / 10,215 |
| 解像度 / active Terrain数 | 1280×720 / 1 |
| targetFrameRate / vSyncCount | 60 / 1 |
| Recorder captureDeltaTime | 0.03333334秒 |
| フォーカスが有効だった計測フレーム | 1,502 / 1,502 |

- Record=true、Unity Recorder + PlaytestOverlayあり。Record=falseの補助値ではない。Recorderは30fps設定。unscaledDeltaTime集計と実時間の平均が一致することも照合した。
- 元worldはshareのworld_1のworld.json/map.json/save.jsonを専用worktreeのPlaytestResultsへ複製し、固定world入口（template、seed=0、AutoSave=false）でロードした。原本・通常savesは変更せず、退避・復元は不要。
- masterはコミット923367a2のpin worktree。マスタロードのドライランとコンパイル成功。
- 固定world入口が指定するOther環境を、このマシンの通常再生で保存されているDebug（0）へ合わせた。人工足場を追加していない。開始地点(659.5,96.6,586.5)は既存の機械・搬送ラインの間。全13,852ブロックのView受信後、10秒ウォームアップ。
- CinemachineBrainは有効のまま。通常カメラ追従でW/Sを3秒ずつ交互に入力。計測開始24秒以降、ビルドメニュー→木のチェスト選択→前方セルへのクリック→本番サーバー反映→設置アニメ完了→通常画面復帰を実行。歩行・設置・全フレーム前面をアサートした。
- 設置はUI経由で通った。事前scoutの「足場y=32でなければUI設置できない」という否定断定は採用しなかった。最初の試行は固定セルが歩行後の背後となりoff-screenで失敗したため、歩行後の前方セルへ変更した。負荷条件を軽くして再計測したものではない。
- PlayerPrefsのGraphicsQualityは作業前に存在せず。シナリオfinallyでDeleteKey→Saveし、PlayMode終了後にHasKey=false/value空/environment=0を確認した。

実行: `PLAYTEST_WORLD_DIRECTORY=<worktree>/moorestech_client/PlaytestResults/worlds/graphics-quality-world_1 PLAYTEST_MAP_MODE=template PLAYTEST_SEED=0 .agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client .agents/skills/unity-playmode-recorded-playtest/scenarios/graphics-quality/quality-fps-measure.cs <pin-923367a2>/server_v8`

結果: `moorestech_client/PlaytestResults/20261021_010920/quality-fps-measure/result.json`（Success=true、ErrorLogs空）。録画・開始/完了スクリーンショットが存在し、アバター・地面・HUD・機械/搬送ライン・設置チェストをゲーム視点で目視した。
`uloop get-logs --project-path ./moorestech_client --search-text '[PERF]' --max-count 2` で同値を回収した。

比較元（引継ぎ資料、今回の実測ではない）: MacBook・Editor・カメラ固定で、対策なし7〜9fps、常時軽量化3つ14.1fps、さらに描画距離300mで35.5fps。マシン・録画・カメラ・操作条件が違うため直接の速度比には使わない。

## UI録画

`20261021_010009/quality-ui-switching` は13/13アサート成功、ErrorLogs空。Esc→トップ4ボタン→設定→低→高→影だけオフ→カスタム→閉じるを実クリックで通した。トップボタン矩形は全て1280×720内。低far=300/影off/scale=0.5、高far=5000/影on/scale=1を実測。動画10,904,231byte、画像6枚。低/高のゲーム画面とカスタム設定表示を目視済み。
