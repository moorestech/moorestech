// CSS の :root ブロックから `--name: value;` を宣言順に取り出す（コメントは捨てる）
// Extracts `--name: value;` declarations from CSS :root blocks in order (comments are dropped)

export function stripComments(text) {
  return text.replace(/\/\*[\s\S]*?\*\//g, "");
}

export function parseRootTokens(cssText) {
  const tokens = [];
  const source = stripComments(cssText);
  const rootPattern = /:root\s*\{([^}]*)\}/g;
  for (const rootMatch of source.matchAll(rootPattern)) {
    // 値の中の括弧（rgb(...) 等）にセミコロンは現れないため ; で区切ってよい
    // Parentheses inside values (rgb(...) etc.) never contain semicolons, so splitting on ; is safe
    for (const declaration of rootMatch[1].split(";")) {
      const match = declaration.match(/^\s*(--[\w-]+)\s*:\s*([\s\S]+?)\s*$/);
      if (match) tokens.push({ name: match[1], raw: match[2].replace(/\s+/g, " ") });
    }
  }
  return tokens;
}

export function formatNumber(value) {
  // 浮動小数の誤差を落とし、末尾の 0 を出さない
  // Drops floating-point noise and trailing zeros
  return String(Number(value.toFixed(6)));
}
