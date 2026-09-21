# Editor停止は既存shutdownパイプラインへ接続する

決定: EditorのPlay停止は、既存の`WebUiHostEditorCleanup`の`ExitingPlayMode`から`GameShutdownEvent`へ`UnawaitableExit`を流し、既存のclean-exit購読へ接続する。

棄却案: 専用のclean marker書き込みをEditorフックへ追加する／新しい終了監視コンポーネントを作る。

理由: 既存shutdownパイプラインが終了理由・二重発火防止・参加者通知・clean markerを一元管理しており、Editor停止フックも既にドメインリロード前に同期処理されるため。ユーザー承認 2026-09-21「作業を進めて」。

リンク: `docs/superpowers/plans/2026-09-21-shutdown-pipeline-marks-clean-exit.md`
