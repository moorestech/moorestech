決定: Mac 版は Apple Silicon（arm64）専用とし、Mac 向け ffmpeg（arm64）を非公開アセットへ追加して FfmpegRuntimeBundler で同梱する。
棄却案: Universal（Intel＋Apple Silicon）／今回は ffmpeg を同梱せず録画欠損を許容する
理由: CEF の Mac ランタイムが osx-arm64 のみで、Intel Mac では Web UI が動かない。報告の録画は Windows と揃える。
リンク: docs/adr/0071-playtest-distribution-adds-apple-silicon-mac-depot.md
出所: ユーザー裁定 2026-09-25 質問「Mac版のバグ報告録画（ffmpeg）をどうしますか？」→ 選択「B：Mac向けffmpegを追加し同梱」／質問「Mac版はどのCPUを対象にしますか？」→ 選択「A：Apple Silicon専用」
