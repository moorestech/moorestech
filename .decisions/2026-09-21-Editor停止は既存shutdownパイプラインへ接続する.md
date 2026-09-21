# Editor停止は既存shutdownパイプラインへ接続する

決定: 作業継続の委任に基づくagent設計判断として、EditorのPlay停止は既存の`WebUiHostEditorCleanup`の`ExitingPlayMode`から`GameShutdownEvent`へ`UnawaitableExit`を流し、既存のclean-exit購読へ接続する。

棄却案: 専用のclean marker書き込みをEditorフックへ追加する／新しい終了監視コンポーネントを作る。

理由: 既存shutdownパイプラインが終了理由・二重発火防止・参加者通知・clean markerを一元管理しており、Editor停止フックも既にドメインリロード前に同期処理されるため。`a8fdccde2`、`GameShutdownReason.UnawaitableExit`、`CleanExitMarkWriter`は実装上の根拠であり、個別設計のユーザー承認の証拠ではない。

作業委任の出所: 2026-09-21のユーザー発言「別pcで作ったdocだからよしなにやって」「私は寝てるから進められるだけ進めて。後でまとめて意思決定する」「わかりました。作業を進めて」。作業継続と合理的判断の委任に基づくagent設計判断であり、配置・終了理由・callbackのinternal化をユーザーが個別承認したという記録ではない。

制限: `UnawaitableExit`の既存契約を利用するため、flush完了を待たず意思表明時点でcleanを記録し、その後の終了処理中の停止は識別できない。このトレードオフもagentが委任の範囲で選んだものであり、ユーザー承認済みとしてレビューから免責しない。Web UI自体の同期停止待ちを維持する責務とは別である。

リンク: `docs/superpowers/plans/2026-09-21-shutdown-pipeline-marks-clean-exit.md`
