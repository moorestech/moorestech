# 画質レビュー自動修正後の検証

製品コード対象 `6a243cc706..d1faca2cb7`。専用worktreeでcompile、対象Unity、Web、最終録画を再実行した。製品の新機能・D1〜D4は変更していない。

- Unity: `GraphicsQuality|GearRotationCulling|BeltItemObjectPresenter|MapObject.*Visibility`、80/80（追加入口全件を含む）。最終XMLは `PlaytestResults/post-review/unity-tests-restoration-final.xml`。要求外の全Client.Testsは未実行。
- Web: tsc/lint成功、設定・bridge等Vitest252/252、PauseMenu系Playwright6/6。
- 最終compile Error0/Warning0は増分。途中に18/46/60警告を観測、全新旧差分は未実測。
- 試験の容量・landmark fixture・EditModeでの破棄監視・購読隔離・LOD復元を修正。commit `c141ec8370` と `5fdb817cec`。製品コードへの変更なし。

## 最終録画

全5本Record=true、80/80、各録画ErrorLogs0。全動画1280x720、ffprobe成功、ffmpeg全体デコードexit0。画像でアバター・地面・HUD、UI・水位・列車・SSAO・蒸気機関を目視した。

全パスの前置きは `moorestech_client/PlaytestResults/`。各行の `result.json` と同じディレクトリに `recording.mp4` がある。

| 対象 | result | 成功数 |
|---|---|---:|
| quality-fps-measure | 20261021_030444/quality-fps-measure/result.json | 25 |
| quality-ui-switching | 20261021_031205/quality-ui-switching/result.json | 29 |
| quality-effects-train | 20261021_030900/quality-effects-train/result.json | 12 |
| quality-ssao-visual | 20261021_031412/quality-ssao-visual/result.json | 2 |
| quality-reviewed-lifecycle | 20261021_031957/quality-reviewed-lifecycle/result.json | 12 |

中・60秒・録画あり・通常カメラ・UI設置・全計測フレーム前面で平均25.834fps。p50=33.485ms、p95=59.492ms、1563frames、実時間60.520s、照合25.826fps。13852ブロック、10215歯車、Terrain1、移動275.999m。20fps基準成功、30fps目標未達。動画時間でFPSを算出していない。密集帯から通常カメラで移動する既存シナリオを維持した。

UI切替の前後で同じパイプに100→250→450/500を受信し、水位0.2→0.5→0.9を確認。SSAOのon/on差0、on/off差0.333562。実蒸気機関の距離停止/再開と実破棄時Effects保持13865→13864を録画。実機関のRPMは0、非ゼロ120RPMへの復帰はNUnitで検証した。

## 限界と引継ぎ

- 起動時State null9件と、補助録画のStop後にServerStarter破棄参照・socket等5件を観測。原因/画質変更との因果は未確定。後者は `moorestech-aosm1.3` に記録。録画ErrorLogs0を起動からエラー皆無へ拡張しない。
- D1〜D4は裁定待ちを維持。C06は元handoffの単一bool/`invalid_graphics_quality`から、parse statusと`invalid_graphics_quality:<reason>:<field>`へ変更済み。理由4種、対象10キー、localization3キー追加・削除0。勝手に戻さず、採否は親へ報告した。
- 初期の補助lifecycle映像は遠距離地点が床外だったため、床上へ直した031957を採用。製品コードとFPSシナリオは変更していない。
- GraphicsQuality/Environmentのキー不存在、LOD1.0、pipeline、bootstrap flagを復元。原本3SHA一致、通常saves不使用。自分のEditor89267/33595をPID指定で終了。偽receipt/reaper改変なし。
- 全生証拠と絶対媒体パスは `PlaytestResults/post-review/media-manifest.json`、詳細報告は `.superpowers/sdd/post-review-verify-report.md`。これらはgit addしていない。媒体はPRへ退避するまで専用worktreeで保持する。
