// Markdown の表の行をセルへ分け、見出し直下の表を読む（依存なし）
// Splits Markdown table rows into cells and reads the table under a heading (no dependencies)

export function splitRow(line) {
  // 行頭・行末の | は readSectionTable が確かめ済み。`\|` はセル内の文字 | として残し、それ以外の | で区切る
  // Leading/trailing pipes are checked by readSectionTable; keep `\|` as a literal pipe and split on every other pipe
  const body = line.trim().slice(1, -1);
  const cells = [];
  let current = "";
  for (let index = 0; index < body.length; index++) {
    const char = body[index];
    if (char === "\\" && body[index + 1] === "|") {
      current += "|";
      index++;
      continue;
    }
    if (char === "|") {
      cells.push(current.trim());
      current = "";
      continue;
    }
    current += char;
  }
  cells.push(current.trim());
  return cells;
}

export function readSectionTable(text, heading, header) {
  // 見出しは 1 回だけ。次の「## 」までの | 始まりの行を表として集める
  // The heading must appear exactly once; collect pipe-led lines up to the next "## " heading
  const lines = text.split(/\r?\n/);
  const start = lines.indexOf(heading);
  if (start < 0 || lines.indexOf(heading, start + 1) >= 0) {
    return { rows: [], errors: [`見出し「${heading}」が無いか重複している`] };
  }
  const tableLines = [];
  const edgeErrors = [];
  for (let index = start + 1; index < lines.length; index++) {
    const line = lines[index].trim();
    if (line.startsWith("## ")) break;
    if (!line.includes("|")) continue; // silent-ok: 表の外の説明文と空行 / 表の構造は下の見出し行・区切り行・列数で検査し誤りとして返す

    // G1（ledger.py の _table）と同じく、行頭・行末の | が欠けた行は誤りにする（末尾のセルを黙って欠かないため）
    // Like G1 (_table in ledger.py), a row missing its leading or trailing pipe is an error (so the last cell is never silently cut)
    if (!line.startsWith("|") || !line.endsWith("|") || line.endsWith("\\|")) {
      edgeErrors.push(`「${heading}」${index + 1} 行目: 行頭か行末に | が無い`);
      continue;
    }
    tableLines.push({ cells: splitRow(line), lineNumber: index + 1 });
  }

  // 見出し行と区切り行を確かめ、列数の合わない行は誤りにする
  // Validate the header and separator rows; rows with the wrong column count are errors
  if (tableLines.length < 2 || tableLines[0].cells.join("|") !== header.join("|")) {
    return { rows: [], errors: [`「${heading}」の表の見出しが「${header.join(" | ")}」ではない`] };
  }
  const separator = tableLines[1].cells;
  if (separator.length !== header.length || separator.some((cell) => !/^:?-{3,}:?$/.test(cell))) {
    return { rows: [], errors: [`「${heading}」の表の区切り行が不正`] };
  }
  const rows = [];
  const errors = [...edgeErrors];
  for (const { cells, lineNumber } of tableLines.slice(2)) {
    if (cells.length !== header.length) {
      errors.push(`「${heading}」${lineNumber} 行目: 列数が ${header.length} ではない`);
      continue;
    }
    rows.push({ cells, lineNumber });
  }
  return { rows, errors };
}
