// calc() の中身を四則演算として評価する。値は { value, unit: "px" | "" } で持つ
// Evaluates a calc() body as arithmetic; values are carried as { value, unit: "px" | "" }
// resolveVarNumber(name) は参照トークンを数値へ解決し、できなければ null を返す
// resolveVarNumber(name) resolves a referenced token to a number, or returns null

export function evaluateCalc(expression, resolveVarNumber) {
  const tokens = tokenize(expression);
  if (tokens === null) return null;
  let position = 0;
  const result = parseSum();
  return result !== null && position === tokens.length ? result : null;

  function parseSum() {
    let left = parseProduct();
    while (left !== null && (peek() === "+" || peek() === "-")) {
      const operator = tokens[position++];
      const right = parseProduct();
      if (right === null || right.unit !== left.unit) return null;
      left = { value: operator === "+" ? left.value + right.value : left.value - right.value, unit: left.unit };
    }
    return left;
  }

  function parseProduct() {
    let left = parsePrimary();
    while (left !== null && (peek() === "*" || peek() === "/")) {
      const operator = tokens[position++];
      const right = parsePrimary();
      if (right === null) return null;
      // 長さ×長さ・長さで割る式は CSS でも不正なので評価しない
      // Length×length or division by a length is invalid CSS too, so reject it
      if (operator === "*" && left.unit && right.unit) return null;
      if (operator === "/" && right.unit) return null;
      const unit = left.unit || right.unit;
      left = { value: operator === "*" ? left.value * right.value : left.value / right.value, unit };
    }
    return left;
  }

  function parsePrimary() {
    const token = tokens[position++];
    if (token === undefined) return null;
    if (token === "(") {
      const inner = parseSum();
      return tokens[position++] === ")" ? inner : null;
    }
    if (typeof token === "object" && token.varName) return resolveVarNumber(token.varName);
    if (typeof token === "object") return token;
    return null;
  }

  function peek() {
    return tokens[position];
  }
}

// 数値・単位・var()・演算子・括弧を字句に分ける。未知の単位（% 等）は評価不能として null
// Splits numbers, units, var(), operators and parentheses; unknown units (% etc.) yield null
function tokenize(expression) {
  const tokens = [];
  const pattern = /\s*(?:(calc\()|(var\((--[\w-]+)\))|(-?\d*\.?\d+)([a-z%]*)|([-+*/()]))/gy;
  let match;
  let consumed = 0;
  while ((match = pattern.exec(expression)) !== null && match[0] !== "") {
    consumed = pattern.lastIndex;
    if (match[1]) tokens.push("(");
    else if (match[2]) tokens.push({ varName: match[3] });
    else if (match[4] !== undefined) {
      const number = numberWithUnit(Number(match[4]), match[5]);
      if (number === null) return null;
      tokens.push(number);
    } else tokens.push(match[6]);
  }
  return expression.slice(consumed).trim() === "" ? tokens : null;
}

export function numberWithUnit(value, unit) {
  if (unit === "") return { value, unit: "" };
  if (unit === "px") return { value, unit: "px" };
  if (unit === "rem") return { value: value * 16, unit: "px" };
  return null;
}
