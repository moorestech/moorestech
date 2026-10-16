決定: 削除ツールの箱選択の Q/E 高さは、設置モードと同じ `PlacementHeightOffset`（DI シングルトン・HUD に高さが出る）を BP コピーと同じく共用する。箱を畳む・削除モードを離れるときに `ResetToGround()` で地表へ戻し、設置モードへ持ち越さない。
棄却案: 削除ツール専用のインスタンスを持つ（設置側へ一切漏れないが、削除モードでは HUD に高さが出ずマーカー位置でしか分からない。plan の初案）。
理由: ユーザー裁定 2026-10-16 選択「共用し、削除モード離脱・箱クリアで地表へ戻す」。同役割の前例 BlueprintCopySystem が共用しており、「唯一の正」テスト（PlacementHeightOffsetSingleSourceTest）の意図とも一致。
リンク: PlacementHeightOffset.cs / BlueprintCopySystem.cs / docs/superpowers/plans/2026-10-16-delete-tool-hold-confirm-box-select.md
