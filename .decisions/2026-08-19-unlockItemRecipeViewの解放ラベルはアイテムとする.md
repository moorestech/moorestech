# unlockItemRecipeViewの解放ラベルはアイテムとする

決定: `unlockItemRecipeView` 由来の解放物の表示を「解放: クラフトレシピ」から「解放: アイテム」(Unlocks: Items)へ改める。あわせてC#側 `UnlockItemIds` → `UnlockItemRecipeViewItemIds` の一括リネームを同時に行う（wire名・testId・i18nキーも追随）

棄却案: 「解放: レシピ閲覧」（アクション名に忠実だが、サーバー通知`ui.notification.unlockedItem`「新しいアイテムを解放しました」と語彙が食い違う）／現状維持（`unlockCraftRecipe`は実在する別アクションであり、将来それを表示する日に同名2セクション問題が起きる）

理由: 現状表示はプレイヤーに「研究したのにクラフトできない」と誤認させる虚偽ラベル。通知側の既存語彙に合わせるのがプレイヤー体感として自然で、ラベル空間の将来衝突も避けられる

リンク: moores-code-review run 2026-08-18-2305 の CR-5 / D3
