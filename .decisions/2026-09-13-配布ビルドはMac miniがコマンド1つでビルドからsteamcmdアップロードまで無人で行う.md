# 配布ビルドはMac miniがコマンド1つでビルドからsteamcmdアップロードまで無人で行う

日付: 2026-09-13
出所: ユーザー裁定 質問「Windows配布ビルドの生成とSteamベータブランチへのアップロードはどこで行いますか？」→ 選択「Mac miniがコマンド1つでビルド→steamcmdアップロードまで無人で行う」

## 決定
- masterの指定コミットから使い捨てworktreeを切り、Release固定のWindowsビルド入口（ADR 0036のMac版と同型）をbatchmodeで焼き、BuildInfoを焼き込み、steamcmdでベータブランチへ上げる
- Steamビルド資格情報はMac miniの封じ込めenvに置く。起動は手動コマンド（自動定期ではない）

## 棄却案
- 開発機のMacでGUIメニューからビルドしスクリプトでアップロード: 毎回人が採られ、手元の状態に依存する
- GitHub Actionsに非公開assetsのdeploy keyを入れてCIで作る: 「配布ビルドCIはstrict化しない」「PRビルドは破壊検知限定」の既存裁定と衝突し有料アセットをCIランナーへ出す
