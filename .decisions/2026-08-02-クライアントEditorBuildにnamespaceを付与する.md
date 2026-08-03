決定: moorestech_client の Editor/Build 配下4ファイルに namespace Client.Editor.Build を付与し、build.yml のクライアントビルドジョブの buildMethod を完全修飾名へ更新する。
棄却案:
- クラス名も PlayerBuildEntry へ変更する（UnityEditor.BuildPipeline との概念混同も解消できるが、変更点が増える）
- 現状維持でグローバル2本併存を許容し、理由コメントを残す（-executeMethod の解決先が未定義のまま残る）
理由: クライアントプロジェクトには Server.Editor 側の同名グローバル BuildPipeline も取り込まれており、CIの -executeMethod BuildPipeline.WindowsBuildFromGithubAction がどちらを呼ぶか未定義。namespaceで曖昧性を消すのが最小で確実。サーバービルドジョブ側の buildMethod は server プロジェクトの BuildPipeline を指すため変更しない。
リンク: docs/plans/pr-1116-independent-review-fix-plan.md B-4
