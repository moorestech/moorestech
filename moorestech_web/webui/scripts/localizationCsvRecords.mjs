// C#共通parserと同じレコード境界規約をNodeビルドへ共有する
// Share the same record boundary contract as the shared C# parser with the Node build
export function parseRecords(text) {
  const records = [];
  let fields = [];
  let field = "";
  let inQuotes = false;
  let closedQuote = false;
  let recordHasSyntax = false;

  // 文字単位の状態遷移でquote内のカンマ・改行・二重quoteを扱う
  // Use character-level transitions for quoted commas, newlines, and doubled quotes
  for (let index = 0; index < text.length; index += 1) {
    const character = text[index];
    if (inQuotes) {
      if (character !== '"') {
        field += character;
      } else if (index + 1 < text.length && text[index + 1] === '"') {
        field += '"';
        index += 1;
      } else {
        inQuotes = false;
        closedQuote = true;
      }
      continue;
    }

    // 閉じquote後はfield境界以外を不正入力として拒否する
    // Reject anything except a field boundary after a closing quote
    if (closedQuote) {
      if (character === ",") {
        addField();
        closedQuote = false;
      } else if (character === "\r" || character === "\n") {
        addRecord();
        if (character === "\r" && text[index + 1] === "\n") index += 1;
        closedQuote = false;
      } else {
        throw new Error("Unexpected character after closing quote");
      }
      continue;
    }

    if (character === '"') {
      recordHasSyntax = true;
      if (field.length !== 0) throw new Error("Quote must begin at the start of a field");
      inQuotes = true;
    } else if (character === ",") {
      addField();
    } else if (character === "\r" || character === "\n") {
      addRecord();
      if (character === "\r" && text[index + 1] === "\n") index += 1;
    } else {
      field += character;
    }
  }

  if (inQuotes) throw new Error("Unterminated quoted field");
  if (closedQuote || field.length > 0 || fields.length > 0) addRecord();
  return records;

  function addField() {
    fields.push(field);
    field = "";
  }

  function addRecord() {
    addField();
    if (fields.length > 1 || fields[0].length > 0 || recordHasSyntax) records.push(fields);
    fields = [];
    recordHasSyntax = false;
  }
}
