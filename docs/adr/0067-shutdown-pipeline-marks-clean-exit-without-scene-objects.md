# 0067. 正常終了の印は終了パイプライン自身が保険として書く（シーン上のオブジェクトに依存しない）

日付: 2026-09-21
状態: 採択。review r2のcontroller設計判断（D1案A・D2案B・D3案B）で更新。

## Context

エディタでPlayを停止しただけなのに、次のPlayで「前回異常終了の確認」ゲートが応答待ちで起動を止める、という報告があった。

実測（Editor.log 2026-09-21 17:23:39）した経路はこうなっている。Playを停止すると `SaveAndQuitPresenter.OnApplicationQuit`／`OnDestroy` から `Disconnect()` が走るが、その1行目 `ClientContext.VanillaApi.Disconnect()` が NullReferenceException を投げ、次の行 `GameShutdownEvent.FireGameShutdown(GameShutdownReason.UnawaitableExit)` に到達しない。終了の意思表明が飛ばないので `CleanExitMarkWriter` は正常終了の印（`marks/pid_<PID>/session_<utcTicks>/clean`）を書かず、次の起動が「前回異常終了」と判定してゲートを出す。

さらにこの状態は自己増殖する。ゲート応答待ちの起動は初期化が完了しておらず `VanillaApi` が未設定なので、そこでPlayを止めると再び同じ NRE が起き、次の起動でもゲートが出る。実ログでも clean:True が4回続いた後、1回 clean:False になり、その次の起動でゲートが出ていた。

根の問題は NRE 単体ではない。**終了の意思表明が、MainGameシーンに置かれた1個の MonoBehaviour の生存と、その中の1行が例外を投げないことに依存している。** MainGameシーンが読み込まれる前（Vite起動待ち等）に停止すれば、NREを直してもそもそも印を書く主体が居ない。

## 出所の訂正（review r2 D2案B）

元台帳に残るユーザー原文は「プレイを停止しただけなのにこれが出るのうざい」。これは通常停止で偽の異常終了確認を出さない要求を支持するが、特定APIや検証方式の個別承認ではない。旧版の質問＋選択ラベルは一次記録を再確認できず、ユーザー逐語としては再掲しない。以下の具体選択・棄却は、明示された自律実行委任と正本Requirementsに基づくagent設計判断。

## Decision（agent設計判断）

- **エディタのPlay停止は、初期化の途中だろうがゲート応答待ちだろうが、未宣言の通常停止なら「正常終了」として印を書く。既にIntentionalExitのflush中の場合とInitializationFailed通知済みの場合は、その終了状態を上書きしない。** ゲートの仕組み自体は据え置き、実クラッシュ・既に失敗表明した起動の確認経路を保つ。配布ビルドの終了要求保留と正規終了口は変えないが、終了印の開始時点・対象は配布版を含め拡大する。
  出所: agent設計判断（正本要件と既存実装の照合。具体選択へのユーザー個別承認は未確認）
  棄却案: エディタでは異常終了ゲートを丸ごと出さない（エディタで踏んだクラッシュの報告をゲート経由で拾う道も同時に消える）／ゲートで起動を止めるのをやめ後から聞く（テスターが通知を無視して報告が取れなくなる）

- **「停止したら必ず印を書く」責任は終了パイプライン自身が持つ。`GameShutdownEvent` が `Application.quitting` を購読し、まだ誰も終了の意思を表明していなければ自分で `UnawaitableExit` を発火する。** シーン上のオブジェクトの生存に依らないので、初期化途中・ゲート応答待ちの停止でも印が残る。同じ保険が配布ビルドにもかかることを受け入れる。
  出所: agent設計判断（正本要件と既存実装の照合。具体選択へのユーザー個別承認は未確認）
  棄却案: エディタ専用フック（`EditorApplication.playModeStateChanged` の `ExitingPlayMode`）で書く（配布ビルド側に同じ穴が残る）／`SaveAndQuitPresenter` の NRE を直すだけ（MainGameシーン読込前の停止は Presenter 自体が居ないので直らない）

- **正規の終了口（`QuitApplicationAsync`）を通った終了では保険は何もしない。** 既に `_fired` が立っているため、終了処理中に停止した場合は従来どおり「意思表明はあるが完了の印が無い＝異常終了」として数えられ、終了処理中のフリーズ検知（F03）は保たれる。
  出所: agent前提（`GameShutdownEvent._fired` の既存セマンティクスと `CleanExitMarker.ShutdownStalled` の判定）

- **保険が発火したことは開発者が読めるログへ必ず出す。** 「誰も意思表明しないまま終了要求が来たので、待てない終了として記録した」旨を出す。
  出所: agent前提（AGENTS.md「fail-closedで縮退する経路は理由を必ずログへ。無音の縮退は禁止」）

- **`SaveAndQuitPresenter.Disconnect()` の NRE は同じPRで直す。** 停止のたびに赤い例外が出るノイズを消す。`UIStateControl.Update` など他の未初期化NRE（`moorestech-johg.2`）は別タスクのまま残す。
  出所: agent設計判断（正本要件と既存実装の照合。具体選択へのユーザー個別承認は未確認）
  棄却案: 未初期化NREをまとめて直す（今回のゲート問題と無関係な範囲までPRが広がる）／直さず印の保険だけ入れる（停止のたびに赤い例外が出続ける）

- **既存の未応答資料は、実機確認前に絶対パスを検証して一時退避する。** 削除の個別承認は再確認できないため、破棄せず復元可能に保つ。
  出所: agent設計判断（正本要件と既存実装の照合。具体選択へのユーザー個別承認は未確認）
  不採用案（agent判断）: 検証開始時に資料を不可逆に削除する。退避で同じ初期条件を作れるため削除は不要。

- **検証は、保険の分岐を引数で叩ける形に切り出した EditMode テストと、実機Playの目視の2本立て。** `Application.quitting` はテストから発火できないため、分岐だけを切り出して覆う。実機では初期化途中で停止し、再Playでゲートが出ないことと clean の印ができることを確かめる。
  出所: agent設計判断（正本要件と既存実装の照合。具体選択へのユーザー個別承認は未確認）
  棄却案: 実機Play確認だけ（後で壊れても自動では気づけない）

## 最早期停止の補完（review r2 D1案A）

- PreviousSessionStartupTasks.BeginCurrentSessionMarksで今回sessionとwriterを最初のawait前に同期設置する。WebUi未ready・起動失敗・remoteでも終了印を持つ。GameShutdownEventからmarkerへ直接依存させない。
- RunAtStartupはホスト起動後のsalvage/recoveryだけを担当し、session再開始やwriter再設置をしない。既存scannerのcurrent pid/session除外で今回の印を保護する。
- ログ・録画・進行記録・報告確保の収集同意条件と終了印は分離する。ADR 0060裁定5の同時設置は時点のみ部分更新し、同じ所有クラスへの集約を維持する。
- writerを待たないPlay→即Stopでstarted/cleanと保険ログを確認し、再Playでclean:True・異常終了ゲートなしを検証する。初回実測で判明した「WebUi待機中は印が無い」窓を残さない。
- D3案Bによりshutdown初期化の既存3 APIを維持する。

## Consequences

### 資料の所有契約（review r2 refix、agent設計判断）

- 無条件のsession/writer設置は維持する。終了印があることとsnapshot記録を開始したことを同一視しない。起動後のsalvage APIから今回のremote/world設定を除き、落ちたsession自身の記録だけで保存元を選ぶ。
- `origin.json` と退避後の `previous-origin.json` に `snapshotCapture` を追加する。`version: 1`、`state: notStarted | started | unknown`、`directory`、`owner`、`missingReason` のJSONオブジェクト。開始時はnotStarted、実際の内蔵サーバーのringがactiveになった後だけstartedへ更新する。startedのdirectoryは実際のWorldDataDirectoryから得た絶対パス、ownerはpid/sessionの組である。
- 同じoriginを保存元の `capture-owner.json` にも書く。回収時はsession側と保存元側のowner/directory一致を必要条件とする。同じワールドを別sessionが再利用した場合、古いoriginだけを根拠に資料を回収しない。ring開始時は所有印を最初に削除し、旧snapshot/packetの削除失敗もログ後に起動を中断する。旧資料へ新所有印を付けないためであり、既存IO境界のcatchを使用する。
- remote・WebUi未ready/起動失敗・記録無効はnotStartedのまま。資料不在は理由ログと既存manifestのMissingへ残す。未知version・所有情報のない旧origin・必須値不正はunknownとして扱い、ビルド出所は保持するがsnapshot/packetは回収しない。今回の設定や既定パスへの補完はしない。
- 未応答資料の再提示にも退避先の所有印照合を適用する。新たな未記録sessionのクラッシュへ古い退避snapshotを混ぜない。既存manifestの項目・Missing形式は変更しない。旧版の未応答snapshotには所有を証明できないものがあるため、推測で添付せずMissingを明記する。
- world save JSON、WorldSaveAllInfo.CurrentVersion、アイテム/液体/ブロックのGUID解決、マスタ由来値は変更しない。これは診断資料の出所契約であり、world save migrationは不要。

- `GameShutdownEvent` の「Editorでの停止は UnawaitableExit で記録する」というコメントは、実際の記録主体が Presenter から終了パイプライン自身へ移るので書き換える。
- 配布ビルドで、正規の終了口を通らずプロセスが終了処理に入った場合（OSからの終了要求など）も、待てない終了として正常終了に記録されるようになる。ハードクラッシュ・強制終了では `Application.quitting` が飛ばないので、従来どおり異常終了として検知される。

- 終了印のキーは既存started/origin.json/exit_intent/cleanのままで、ワールドセーブ形式・GUID解決は変えない。
