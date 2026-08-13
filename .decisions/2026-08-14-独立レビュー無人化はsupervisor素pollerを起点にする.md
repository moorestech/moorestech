# 独立レビュー無人化はsupervisor素pollerを起点にする

決定: `独立レビュー待ち` ラベルの検知とheadlessレビュー起動は、always-on supervisor の periodic サービス（決定的なシェルスクリプト poller）が担う

棄却案: Hermes内蔵cron（AIプロンプト型検知）／GitHub Actions webhook で自宅サーバーへ通知

理由: 検知は決定的処理で足りAIを挟むのはコストと不確実性の無駄。webhookは外→内の公開経路が増える割に、数分のポーリング遅延で困らない

リンク: [[2026-08-14-裁定サイトはCloudflare-Accessで保護する]]
