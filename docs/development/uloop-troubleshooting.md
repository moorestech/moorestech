# uloop / Unity Editor のトラブルシュート

`uloop compile` / `uloop run-tests` が繋がらない・結果が合わない・Editor が固まるときに読む。
症状 → 原因 → 対処の順に並べる。Mac mini 固有の事情（reaper・前面/非前面・TestResults.xml の置き場など）は `docs/development/macmini/unity-editor.md`。

## 繋がらない

### `Cannot connect to Unity` / `not running` が全コマンドで出る
- 原因で最も多いのは **そのブランチ自身のコンパイルエラー**。コンパイルエラーがあると Unity は `[InitializeOnLoad]` を実行しない。uloop の Editor 側サーバは `[InitializeOnLoad]`（package 3.x は `UnityCliLoopEditorBootstrap`）から立つので、サーバが立たず `uloop compile` も使えない
- 対処: サーバ不要の唯一の観測点 `~/Library/Logs/Unity/Editor.log` を `grep "error CS"` で見る（`WorkingDir:` 行でどの worktree のログか確認）。エラーを直し、Editor を一度前面化して Asset Refresh を発火させる（`open -a Unity`）。前面化したら PlayMode 検証の前に必ずバックグラウンドへ戻す（入力注入が実 OS 入力に上書きされるため。unity-playmode-recorded-playtest スキルの絶対規則）
- Editor.log は追記式で古いエラーが残る。`uloop compile` が通るなら、残っている `error CS` 行は過去のもの
- モーダル固着・ロックファイル・`isServerRunning` の永続値は、この症状の原因でないことが多い。先に疑わない
- 複数 Editor が同時に動いていると Editor.log は奪い合いになる（後述）。目当ての Editor のログ実体は `lsof -p <pid> | awk '$4=="1u"'` で辿る

### PlayMode 遷移テスト中に `Domain Reload in progress` / `not installed` が返る
- PlayMode 遷移を伴うテスト（EditModeInPlayingTest）や playtest の PlayMode 突入中は、ドメインリロードで一時的に uloop が応答しない。どちらのメッセージも「まだ走っている」の意味
- `uloop run-tests` をループで再 invoke しない（走行中の run と競合し、いつまでも結果が返らない）。1回だけ投げ、結果 XML が新しく出るのを待つ
- uloop package 3.x の設定は `moorestech_client/UserSettings/UnityCliLoopSettings.json`、接続は project ごとの Unix ソケット。`UnityMcpSettings.json` は旧 package（1.x/2.x）の設定ファイルで、旧版は PlayMode 遷移中にこれを `.bak` へ退避して `not installed` を返した。旧 package の worktree で PlayMode 外なのに `not installed` が続くなら、`cp -n UserSettings/UnityMcpSettings.json.bak UserSettings/UnityMcpSettings.json` で戻す。Library 再生成や再 launch はその後

### `UNITY_SERVER_BUSY`
- uloop は single-flight。並行エージェントが同じ Editor を叩いているときに返る。数十秒置いてリトライする

### Editor が Safe Mode で起動し、uloop のソケットが作られない（`compile` が i/o timeout）
- Editor.log に `Safe Mode` と `Assembly with name '...' already exists` があれば、アセンブリの二重定義。よくあるのは `moorestech-client-private` が古く、master が移設したプラグインと二重になるケース
- 対処: その worktree の `moorestech_client/Assets/PersonalAssets/moorestech-client-private` を `git -C <private> fetch && git -C <private> merge --ff-only origin/master` で最新化し、Unity が書き換えた tracked な `.meta` を `git checkout -- <path>` で戻し、残骸ディレクトリ（`.DS_Store` だけのもの）を消して Editor を再起動する
- `TMPDIR` を長いパスにして Editor を起動しても、Unix ソケット長の上限を超えて Bee の IPC が落ち Safe Mode になる。`TMPDIR` を変えるなら `/tmp/<短い名前>/` にする

## テストの結果が合わない

### 直したはずの箇所がまだ落ちる
- client と server のどちらか片側にコンパイルエラーが1件でもあると、Unity は前のアセンブリを保持し、もう片側の変更も反映されない
- 対処: テスト結果を読む前に必ず `uloop compile` の `ErrorCount 0` を確かめる

### localization.csv だけ変えたのに古いキーのまま / 触っていないキーで CS0117 が大量に出る
- `Localization/localization.csv` は Assets 外にあり、`Client.Localization/csc.rsp` の `/additionalfile` 経由で SourceGenerator に渡る。AssetDatabase が監視しないので、CSV 変更だけでは再生成されない。master へ rebase した直後に出やすい
- 対処: コードを疑う前に `uloop compile --project-path ./moorestech_client --force-recompile` を1回挟み、続けて通常の `uloop compile` で結果を読む（force 版はエラー本文を返さず `COMPILE_RESULT_UNKNOWN` になることがある。v3 では値なしのフラグ）

### regex フィルタで除外したはずの重いテストが走る
- `--filter-type regex` の値は Unity TestRunner の groupNames として渡り、テストケースだけでなく親 suite（namespace・fixture）の FullName にも照合される。親が一致すると配下は全件走る。否定先読み（`^(?!.*Foo\.).*Bar.*$`）は namespace ノードで一致してしまい除外にならない
- 対処: 走らせたいクラスを肯定形で列挙する（`ClassA|ClassB`、または `--filter-type class`）。除外は効かない前提でフィルタを組む

### PlayMode テストのつもりが走らない / EditMode テストのつもりが PlayMode に入る
- uloop v3 の `run-tests --test-mode` 既定は **EditMode**（v1 系は PlayMode 既定だった）。PlayMode テストは `--test-mode PlayMode` を明示する
- EditModeInPlayingTest は `EnterPlayMode`/`ExitPlayMode` の yield が EditMode テストでしか効かないので EditMode で走らせる（既定のままでよい）
- PlayMode で固着したら `uloop control-play-mode --project-path ./moorestech_client --action Stop`

### 結果 XML を Python で読むと落ちる
- uloop の NUnit 結果 XML は UTF-8 BOM 付きなのに宣言が `encoding="utf-16"`。`ET.parse` は必ず失敗する
- 対処: `open(path, encoding='utf-8-sig')` で読み、`<?xml ...?>` 宣言を正規表現で除去してから `ET.fromstring` する
- 結果 XML の置き場はマシン共通パスのことがあり、並列 worktree で上書きし合う。読む前に `start-time` が自分の run の時刻か確かめる（Mac mini の置き場は macmini/unity-editor.md）

## Editor が固まる

### PlayMode 遷移テストのフィルタにだけ、2秒で `Domain Reload in progress` が返り続ける
- 見分け方: 同じ Editor・同じ時刻に EditMode 単体テストは完走し、`uloop compile` も通る。PlayMode に入った形跡が出ない
- Editor 再起動・`uloop fix`・PlayMode Stop・放置のいずれでも解けないことがある
- 対処: その worktree での復旧に時間を使わない。新しい worktree を切って別 Editor で PlayMode 検証だけ実行する。実装はコンパイルと EditMode テストで先へ進め、PlayMode 検証は bd に切り出してよい

### `Domain Reload in progress` が延々続く
- AGENTS.md のとおり 45 秒待ってリトライする。それでも続くなら、Editor にフォーカスを繰り返し当てていないか確かめる。前面化のたびに Auto Refresh が走りドメインリロードが連続する。フォーカスを当てるのは1回だけにする

## 計測ログが見つからない
- 全 Editor が `~/Library/Logs/Unity/Editor.log` へ書く。後から起動した Editor が既存を `Editor-prev.log` へ回すので、先行 Editor はリネーム済み inode へ書き続ける。並列運用では `Debug.Log` を計測の観測点にできない
- 対処: 起動時間計測などの計装は `File.AppendAllText("/private/tmp/<name>.log", ...)` のように固定パスへ書く
