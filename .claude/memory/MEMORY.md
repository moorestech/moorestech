# moorestech Project Memory

## User Preferences
- [一方通行フロー選好](user-prefers-one-way-flow.md) — Contextハブ・コールバック逆流はNG。毎フレーム系は「集める→純関数Decide→出力適用」パイプライン、非同期はTryConsumeポーリングで
- [Core.Master層境界](core-master-layer-boundary.md) — ドメイン固有ロジックをItemMaster等Core層に足すの禁止。Game層にstatic utilを置く。イベントはActionでなくUniRx
- [マスタは導出せず明示設定](master-explicit-over-derived.md) — blockSize等から値を導出する設計はNG。専用フィールドを設け、食い違いはバリデータで検出
- [No blocking wait loops](no-blocking-wait-loops.md) — rely on background-task auto notifications, never foreground until/sleep polls

## Workflow Gotchas
- [Server tests = immutable file: package](server-tests-immutable-package.md) — NEW server .cs files need a Unity RESTART (not Refresh/Resolve) to compile/run via the client project
- [Server .cs.meta = 2行形式が正規](server-meta-two-line-form.md) — ローカルパッケージ参照経由のUnity生成はMonoImporter無し2行。手動作成と誤判定しない

- [EditModeテストのドメインリロード](editmode-test-domain-reload.md) — EnterPlayModeでuloop切断。45秒以上待機→TestResults.xml直読。SessionState/LogAssert/AssetBundle掃除の定型
- [Worktree needs own Unity](worktree-needs-own-unity.md) — uloop targets the running Unity for that worktree's client; launch one per worktree
- [Worktree master symlink](worktree-master-symlink.md) — worktree PlayMode は ../moorestech_master が解決できず master 読込失敗。`ln -s /Users/katsumi/moorestech_master moorestech-worktrees/moorestech_master` で解決
- [RepositorySync rewinds master](repositorysync-rewinds-master.md) — external-revisions.jsonのピンでmoorestech_masterがdetached HEADに巻き戻る。masterコミット後はピン更新＋checkout確認
- [mooreseditor owns master JSON](mooreseditor-owns-master-json.md) — mooreseditor.app 起動中は moorestech_master の blocks.json 等を外部編集しても数秒で書き戻される。正規ルートはmooreseditor上で設定
- [uloop not installed = .bak復元](uloop-not-installed-bak-restore.md) — UnityMcpSettings.jsonが.bakのみになったらコピー復元
- [cfork worktree fix](cfork-worktree-fork-fix.md) — cmux新splitはcwd非継承→cd前置+SESSION_ID resume。stale surfaceは--workspaceリトライ

## Project Knowledge
- [歯車符号変換は冪等でない](gear-sign-conversion-not-idempotent.md) — isReverse反転の再実行は二重反転。AlwaysForward要素は反転禁止。複合プレハブ4件が未変換残
- [Addressables直列プリロード](addressables-sequential-preload.md) — 並列タスク内の同時ロードで無限ハング。InitializeAsync直後に直列プリロード、ChestBlockInventoryはDispose禁止
- [Chest pushes items every tick](chest-pushes-items-every-tick.md) — adjacent IBlockInventory receivers ping-pong unless they reject backflow by source block id
- [UI網羅は状態機械を背骨にするな](ui-completeness-off-state-overlays.md) — UIStateEnum軸の棚卸しは状態外オーバーレイ(BackgroundSkit等)を構造的に見落とす。Manager走査+名前混入を再検証
- [Web UI移行セッションの型](webui-migration-session-pattern.md) — uGUI→React移行を並列subagentで: コントローラが共有契約+C#を直列所有、CEF破損で動画=Playwright録画、mock host overlayはopt-in

## Reference
- [Key files](key-files.md) — 初期化パイプライン・デバッグオーバーレイ・起動統合テスト・EditModeInPlayingTestの場所
- [Subagent session-limit recovery](subagent-session-limit-recovery.md) — 無通知死をSendMessage pingで検出、変更有無で再開/再spawnを分岐
