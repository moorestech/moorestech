# Task7 I1: 画質切替後も同じパイプの水位が追従する

既存UIシナリオへ水位確認を統合し、独立したquality-water-level.csの重複を除去した。統合後156行、プロダクトコードの変更なし。シナリオcommit: 9f23b70664。

## 一本の録画

結果ディレクトリ: moorestech_client/PlaytestResults/20261021_014009/quality-ui-switching/

- result.json: Success=true、29/29 PASS、ErrorLogs=[]。
- recording.mp4: Record=true、12,640,578byte、1280×720、911フレーム、30.366667秒。ffprobe成功、ffmpeg全体デコードもexit 0。動画時間から実測fpsを算出しない。
- 開始: 2026-10-21T01:40:58.0230020+09:00。終了: 01:41:40.0612630+09:00。
- 事前準備: 元の地形を保持し(659,96,630)に鉄パイプを一度だけ設置、100/500投入、水位0.200を受信・撮影。
- UI実クリック: 低(far300/影false/scale0.5) → 高(far5000/影true/scale1) → 影off → カスタム → 閉じる。
- その後、同じパイプへ150、200を投入。受信250/500→450/500、材質_WaterLevel=0.500→0.900。最後まで影offのカスタムを維持。
- 前後ともblock=679440335、View=-6729572、Processor=-6729488、Renderer=-6729562。同じサーバーブロックとViewの参照一致、同じProcessor/Rendererをアサート。
- 中へのリセット・再設置・再起動を切替と水位変化の間に挟んでいない。既存の独立水位録画は補助証拠に留める。
- water-100/450、03-low-game、04-high-game、05-custom-settingsを目視し、同じ窓の水面差、カスタム・影off表示、アバター・地面・HUDを確認。画像9枚。

## 実行と復元

repo rootで前景実行。メインrepo・通常savesはアクセスしていない。

```sh
uloop launch ./moorestech_client
uloop clear-console --project-path ./moorestech_client
uloop compile --project-path ./moorestech_client
uloop focus-window --project-path ./moorestech_client
uloop control-play-mode --project-path ./moorestech_client --action stop
rsync -a /Users/sakastudio/share/2026-10-20-graphics-quality-world/world_1/{world.json,map.json,save.json} moorestech_client/PlaytestResults/worlds/graphics-quality-world_1/
PLAYTEST_WORLD_DIRECTORY="$PWD/moorestech_client/PlaytestResults/worlds/graphics-quality-world_1" PLAYTEST_MAP_MODE=template PLAYTEST_SEED=0 .agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client .agents/skills/unity-playmode-recorded-playtest/scenarios/graphics-quality/quality-ui-switching.cs /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/pin-923367a2/server_v8
```

runログ: PlaytestResults/task7-fix-ui-run.log。preflight PASS、ready約44秒、シナリオ成功、自動Stop完了。今回はrecovery不要。
finallyでGraphicsQualityの元のキー有無と生値を復元。開始前/Stop後ともHasKey=false、文字列空、environment読取=-1、Playing=false、pipeline=UniversalRP-HighQuality。確認出力: task7-fix-initial-state.json / task7-fix-restored-state.json。
原本3ファイルのSHA-256は前後一致（task7-fix-world-before.sha256 / task7-fix-world-after.sha256）。終了時保存先は複製側のみ。
専用Editor PID49106のみprojectPathと開始tokenを再照合してkill 49106で終了。開始token: Wed Oct 21 01:37:54 2026。偽receiptの作成・reaper変更・他プロセス操作なし。

## 既存NUnit・compileの生証拠

- .superpowers/sdd/task-7-cli.log:9753がcompile実コマンド、9893〜10275が応答全文。Success=true / ErrorCount=0 / WarningCount=74、警告配列74件を回収。
- 同log:10304〜10318に対象EffectsQualitySwitchTestのPassed=2 / Failed=0 / TestCount=2、CompletedAt=2026-10-20T16:20:39.0688060Z。直前に実コマンドあり。
- マシン共通TestResults.xmlのstart=16:20:38Z/end=16:20:39Zと2テスト名・Passedを照合し、task7-fix-prior-EffectsQualitySwitchTest.xmlへコピー済み。NUnit再実行なし。
- 回収した生JSON: PlaytestResults/task7-fix-prior-compile.json / task7-fix-prior-nunit.json。警告内訳: CS0436=15、CS0618=34、CS0108=1、CS8509=11、CS0414=8、CS4014=4、CS8524=1。修正前ブランチとの比較は無く、既存由来/今回増加の区別は未確定。
- 今回のcompileもSuccess=true / ErrorCount=0 / WarningCount=0（task7-fix-compile.log）。増分compileの応答であり、過去74警告の解消を意味しない。
- 元log:6420〜6455は起動Error総数9、表示上限5の生出力。今回もシナリオ開始前に同じState is null署名の9件を全スタック付きで保存（task7-fix-boot-errors.json）。原因未調査であり、画質起因とも既存不具合とも断定しない。resultのErrorLogs=[]は録画区間の事実に限定する。

既存最終fps 24.916（20261021_012642）はプロダクトコード不変のため有効。fps再計測と全Client.Testsは実行していない。
