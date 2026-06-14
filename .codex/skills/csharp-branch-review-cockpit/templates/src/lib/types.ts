export type Status = 'A' | 'M' | 'D';

export interface FoldRange {
  kind: 'method';
  name: string;
  sigStart: number; // signature first line (1-based)
  start: number;    // line with the opening '{'
  end: number;      // line with the matching '}'
}

export interface RegionRange {
  kind: 'region';
  name: string;
  start: number; // #region line
  end: number;   // #endregion line
}

export interface DeclType {
  name: string;
  kind: 'class' | 'struct' | 'interface' | 'enum';
  line: number;
}

export interface Parsed {
  usings: string[];
  declTypes: DeclType[];
  regions: RegionRange[];
  folds: FoldRange[];
  lineCount: number;
}

export interface FileRec {
  path: string;
  name: string;
  asmdef: string;
  status: Status;
  add: number;
  del: number;
  text: string;
  parsed: Parsed;
  addedLines: number[];
  depsOut: string[];
  depsIn: string[];
}

export interface DataSet {
  branch: string;
  base: string;
  sourceRoot: string; // 表示時に剥がす共通ディレクトリ接頭辞(extractor が算出)
  files: FileRec[];
  asmdefs: string[];
}
