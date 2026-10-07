# Mac mini での Unity Editor / uloop 運用

Mac mini 自宅サーバー固有の事情。索引は `CLAUDE.local.macmini.md`、環境に依存しない一般則は `docs/development/uloop-troubleshooting.md`。

## Editor が Editor.log にエラーを残さず消える（dev-server-reaper）
- 症状: Editor.log の末尾が `Application is shutting down...` だけで、クラッシュの形跡がない
- 原因: launchd の `com.sakastudio.dev-server-reaper`（`StartInterval 3600`、毎時1回・分は起動時刻次第で固定でない）が「生きた所有者の居ない Unity」を刈る。証跡は `~/Library/Logs/dev-server-reaper.log` の `REAPED pid=... (no live registered launcher for Unity process in <worktree>)`
- 刈られない条件は2つだけ。(1) 起動から 30 分未満（`MIN_AGE_MIN=30`）、(2) `~/Library/Caches/dev-server-reaper-unity-owners.json` の所有 receipt が、生きた所有者エージェントと一致する
- PATH 上の `uloop`（`~/.local/bin/uloop`）は所有ラッパーへの symlink で、`uloop launch`（`moores-wt new` 経由を含む）が receipt を書く。`ls -la ~/.local/bin/uloop` が生バイナリになっていたら、v3 インストーラに上書きされている（下の「uloop CLI の配置」で戻す）。その間に起動した Editor は起動 30 分超で刈られる
- ラッパーを通らずに起動した Editor を守るときだけ、receipt を手で1件追記する。キーは `owner_pid`（自分の claude プロセス。コマンドラインに `--session-id`/`--resume` があるもの）、`owner_session_id`、`owner_start_token`、`project_path`（`.../moorestech_client`）、`unity_pid`、`unity_start_token`、`registered_at`（epoch 秒）の全部。start token は `LC_ALL=C ps -p <PID> -o lstart=` の出力そのもの（ロケールが違うと一致しない）。必須キーが欠けた receipt は reaper が malformed として扱う
- 登録できたかは `~/bin/dev-server-reaper-unity.py` の `protected_unity_from_receipts` を import して保護集合に入るかで確かめる。`--mode list` の dry-run は証明にならない（30 分未満の Editor は receipt と無関係に候補外）
- 刈られるとテスト実行中の EditModeInPlayingTest が他テストの `sceneLoaded` ハンドラ由来の NRE で落ち、プロダクトのバグに見える。長時間の検証の前に receipt を確かめる

## uloop CLI の配置
- `~/.local/bin/uloop`: `~/bin/uloop-owned-wrapper.py` への symlink（PATH 上の入口）
- `~/bin/uloop-owned-wrapper.py`: `uloop launch` で生えた Unity PID を所有者エージェントに紐づけて receipt を書くラッパー。`REAL_ULOOP` は `~/bin/uloop-v3/uloop`
- v3 のインストーラ・更新は既定で `~/.local/bin/uloop` を生バイナリで上書きし、`~/.zshrc` に PATH ブロックを足す。`ULOOP_INSTALL_DIR=$HOME/bin/uloop-v3` で入れる。上書きされたら、生バイナリを `~/bin/uloop-v3/uloop` へコピーし、`ln -sfh ~/bin/uloop-owned-wrapper.py ~/.local/bin/uloop` で張り直す
- v3 dispatcher は package 1.x へ委譲しない（委譲は V2 package 限定、判定は `.uloop/project-runner-pin.json` の有無）。ラッパーには「pin 不在かつ manifest が旧 git URL 参照なら旧 npm CLI へ回す」フォールバックがある

## Editor の前面/非前面
- 前面でない Editor は CPU 約 1% まで絞られ、前面化した瞬間に跳ね上がる。「1フレームあたり N ms の予算で分散処理する」型のコードを PlayMode で検証すると、非前面では実時間が桁で伸びる
- `uloop control-play-mode --action Status` は、PlayMode 中に Editor が非前面だと警告を出す
- 前面化は1回だけ。`osascript` 等で定期的にフォーカスを当てると Auto Refresh が毎回走り、ドメインリロードが連続して `uloop run-tests` が `Domain Reload in progress` を返し続ける
- 入力注入を使う PlayMode 検証は非前面で回す（前面だと実 OS 入力が注入を上書きする）。コンパイルのために前面化したら、PlayMode の前に `open -a Finder` で戻す

## TestResults.xml の置き場
- `uloop run-tests` の NUnit XML は worktree 配下ではなく `~/Library/Application Support/sakastudio/moorestech/TestResults.xml`（マシン共通）に出る。`moorestech_client/.uloop/outputs/TestResults/` は現行では更新されない
- 並列セッションが同じファイルを上書きする。読む前に XML の `start-time` が自分の run の時刻か確かめ、残したい結果はすぐ scratchpad へコピーする
- PlayMode 遷移テストの結果待ちは、このファイルの更新（`start-time` が自分の run）を待つ

## Steam 版 moorestech が動いていると Web UI が出ない
- 症状: PlayMode で `[CefUnity] Init failed ... (code -6)` が出て、Web UI（インベントリ等）が一切描画されない。続く `WebUiCefNavigator` の NRE は付随症状
- 原因: Steam 版と Editor が CEF キャッシュ `$TMPDIR/cef_unity_cache` を共有するとみられる
- 対処: Steam 版を終了するか、Editor を `TMPDIR=/tmp/<短い名前>/ uloop launch ...` で起動し直す。uloop もソケットを TMPDIR に置くので、以降の uloop 呼び出しにも同じ TMPDIR を付ける。TMPDIR を引き継がない子エージェント（Workflow 等）へ渡す前には既定の TMPDIR で起動し直す
- Web UI が出ていない PlayMode で「メニュー中の挙動」を検証しない（実プレイと条件が違う）

## 新規 worktree の Editor が Safe Mode になる
- `moores-wt new` はメイン checkout の `moorestech-client-private` を fetch せず clonefile で複製する。メイン側が古いと、新 worktree でアセンブリ二重定義 → Safe Mode → uloop のソケットが出ない
- 対処は uloop-troubleshooting.md の Safe Mode 節（その worktree の private を ff 更新し、自分の Editor を再起動）。メインの private を更新すれば根治するが、共有状態なので他セッションへの影響を確かめてから行う
