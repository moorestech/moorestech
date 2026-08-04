# PR #1104 Unity Test CI Repair Design

## Goal

PR #1104で同一SHAに対して再現している3件のUnity EditMode失敗を、検証範囲を狭めずに解消する。

## Failure boundaries

`BlockIconImagePhotographerLifetimeTest`は、EditModeでも撮影後に`PlayerLoopTiming.Update`を待つため処理が完了せず、180秒のテストタイムアウト後も継続処理が残る。さらに対象とRenderTextureへ`Destroy`を使い、EditModeではエラーログを正常系としている。

`PlayerStartsOnBuiltTerrainTest`は地形・プレイヤーのassert自体には到達して成功しているが、Unity TestジョブにNode/pnpmとCefUnityのネイティブ資産が無いため、起動中のWeb UIエラーを捕捉して失敗する。別ジョブのWeb UIセットアップはファイルシステムを共有しない。

## Design

`BlockIconImagePhotographer`は、PlayModeでは遅延破棄後に次フレームを待ち、EditModeでは`DestroyImmediate`で同期的に破棄して次の撮影へ進む。テストはエラーログを期待せず、タスクが短いフレーム上限内に完了し、撮影Cameraを残さないことを検証する。

Unity TestジョブはWeb UIテストとは独立して、`packages-lock.json`の解決済みリビジョンからCefUnityをGit LFS込みで事前取得し、ローカルパッケージへ差し替える。続けて`moorestech_web/setup.sh`を実行し、pnpmのfrozen lockfile installまで完了させてからGameCIを起動する。これにより`PlayerStartsOnBuiltTerrainTest`が意図する起動全体のエラー検知を維持する。

## Verification

対象2クラスをuloopで限定実行し、C#変更後は必ずUnity compileを実行する。push後はPR #1104のGitHub Actionsを監視し、EditMode Testを含む全必須チェックが成功したことを完了条件とする。
