---
name: editmode-test-domain-reload
description: EnterPlayModeを含むEditModeテストはドメインリロードでuloop接続が切れる。待機してTestResults.xmlを直接読む
metadata: 
  node_type: memory
  type: project
  originSessionId: 4af4fa5c-5b45-4d86-9cb7-4f7ba0604186
---

`EnterPlayMode` を含む EditModeInPlayingTest はドメインリロードを起こし、uloop/MCP 接続が一度切れる。

**How to apply:**
- テスト実行後は45〜50秒待ってからリトライ。結果が取れなければ `~/Library/Application Support/sakastudio/moorestech/TestResults.xml` を直接読む
- ドメインリロードで static フィールドはリセットされる → フラグ保持は `SessionState.SetBool` を使う
- `LogAssert.ignoreFailingMessages = true` を EnterPlayMode 直後に設定（フレームワーク内部エラー対策）
- `AssetBundle.UnloadAllAssetBundles(true)` を EnterPlayMode 前に実行（残留バンドル掃除、`EnterPlayModeUtil()` が自動実行）
- `DebugObjectsBootstrap` は SessionState フラグで無効化する（テスト終了時に必ずクリア）

詳細な作成手順はプロジェクトスキル `editmode-in-playing-test` を参照。

**uloopで`--filter-value "."`の全件実行は禁物**: EditModeInPlayingTestまで巻き込み、PlayMode遷移＋環境ノイズ（cefunitysampleの不正.metaがUnhandledLogMessageException化）でテストランナーが異常中断→「Domain Reload in progress」が数十分続くループ＋UnityMcpSettings.jsonの.bak化を繰り返す泥沼になる（2026-07-06実測）。広域回帰は `"^(?!.*EditModeInPlaying).*$"` の除外フィルタで回し、EditModeInPlayingTestは個別に実行する。泥沼に入ったら `uloop launch -r` で再起動＋.bak復元。

関連: [[server-tests-immutable-package]] [[key-files]] [[uloop-not-installed-bak-restore]]
