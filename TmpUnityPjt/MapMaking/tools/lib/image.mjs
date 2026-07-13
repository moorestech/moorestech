import sharp from "sharp";
import { stat, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, basename } from "node:path";
import { randomBytes } from "node:crypto";

// sharp パイプラインを段階的に品質を下げて targetSize 以下に収める共通処理
async function compressToTarget(pipeline, { targetSize, qualitySteps, maxDimension }) {
  if (maxDimension) {
    const metadata = await pipeline.metadata();
    if (metadata.width > maxDimension || metadata.height > maxDimension) {
      pipeline = pipeline.resize(maxDimension, maxDimension, {
        fit: "inside",
        withoutEnlargement: true,
      });
    }
  }

  let buf;
  for (const quality of qualitySteps) {
    buf = await pipeline.clone().jpeg({ quality }).toBuffer();
    if (buf.length <= targetSize) return buf;
  }
  return buf;
}

// Gemini API 用: JPEG バッファを返す（長辺800px・500KB以下）
export async function preprocessImage(imgPath) {
  return compressToTarget(sharp(imgPath), {
    targetSize: 500 * 1024,
    qualitySteps: [80, 70, 60, 50],
    maxDimension: 800,
  });
}

// 複数画像を並列で前処理
export async function preprocessImages(imgPaths) {
  return Promise.all(imgPaths.map(preprocessImage));
}

// Codex 用: 1MB超の画像のみ圧縮し、ファイルパスを返す
// sharp 失敗時は元画像パスをそのまま返す（圧縮スキップ）
export async function preprocessImageForCodex(imgPath) {
  const fileStat = await stat(imgPath);
  if (fileStat.size <= 1024 * 1024) return imgPath;

  try {
    const buf = await compressToTarget(sharp(imgPath), {
      targetSize: 1024 * 1024,
      qualitySteps: [85, 75, 65, 55],
      maxDimension: 2048,
    });

    const tmpPath = join(tmpdir(), `codex-audit-${randomBytes(4).toString("hex")}-${basename(imgPath)}.jpg`);
    await writeFile(tmpPath, buf);
    return tmpPath;
  } catch (err) {
    console.error(`[Warning] 画像圧縮失敗（元画像をそのまま使用）: ${err.message}`);
    return imgPath;
  }
}

// 複数画像を並列で前処理（Codex用）
export async function preprocessImagesForCodex(imgPaths) {
  return Promise.all(imgPaths.map(preprocessImageForCodex));
}
