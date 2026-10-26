# 旧 UITK の SkitUI は削除し、会話 UI の予備を持たない

決定: 旧 UITK 製の会話 UI（`SkitUI.prefab`・`Client.Skit/UI/SkitUI.cs`・`SkitPresentationMode`）を削除する。会話 UI の予備は持たない。UITK の会話 UI は移行計画の段階 2 で作り直す。
棄却案: 予備として残す（`SkitManager.cs:197` が Web 経路に固定したまま、非 Web の分岐を持ち続ける）。
理由: ADR 0052 以降は常に非表示で、非 Web の分岐は使われておらず、予備として働いていない。bd `moorestech-lnsf.8` が削除の条件にした「会話 UI の予備を永久に持たない」の確認にあたる。
出所: ユーザー 2026-10-26「1 削除」（UITK 段階0 実測セッションでの回答。問いは「旧 SkitUI（今は常に非表示の古い会話 UI）を消してよいか。会話 UI の予備は持たなくなり、段階 2 で新しい UITK 版を作る」）
リンク: [[2026-10-18-CEFを撤去しUI-Toolkitへ全面移行する]]、bd `moorestech-lnsf.8`、`docs/uitk-migration/plan.md` §2.1 の 0-12
