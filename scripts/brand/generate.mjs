import { mkdirSync, readFileSync, writeFileSync, copyFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { Resvg } from "@resvg/resvg-js";

const field = "#06090E";
const icoSizes = [16, 24, 32, 48, 256];

const assets = [
  { kind: "InAppMark", rel: "DataMan/Assets/Brand/Mark.png", width: 128, height: 128, mode: "OpaqueField" },
  { kind: "InAppSvg", rel: "DataMan/Assets/Brand/DataMan_Logo.svg", width: 107, height: 107, mode: "CopySvg" },
  { kind: "StoreLogo", rel: "DataMan (Package)/Images/StoreLogo.png", width: 50, height: 50, mode: "OpaqueField" },
  { kind: "Square44", rel: "DataMan (Package)/Images/Square44x44Logo.scale-200.png", width: 88, height: 88, mode: "OpaqueField" },
  { kind: "Square44Unplated16", rel: "DataMan (Package)/Images/Square44x44Logo.targetsize-16_altform-unplated.png", width: 16, height: 16, mode: "OpaqueField" },
  { kind: "Square44Unplated24", rel: "DataMan (Package)/Images/Square44x44Logo.targetsize-24_altform-unplated.png", width: 24, height: 24, mode: "OpaqueField" },
  { kind: "Square44Unplated32", rel: "DataMan (Package)/Images/Square44x44Logo.targetsize-32_altform-unplated.png", width: 32, height: 32, mode: "OpaqueField" },
  { kind: "Square44Unplated48", rel: "DataMan (Package)/Images/Square44x44Logo.targetsize-48_altform-unplated.png", width: 48, height: 48, mode: "OpaqueField" },
  { kind: "Square44Unplated256", rel: "DataMan (Package)/Images/Square44x44Logo.targetsize-256_altform-unplated.png", width: 256, height: 256, mode: "OpaqueField" },
  { kind: "Square150", rel: "DataMan (Package)/Images/Square150x150Logo.scale-200.png", width: 300, height: 300, mode: "OpaqueField" },
  { kind: "Wide310", rel: "DataMan (Package)/Images/Wide310x150Logo.scale-200.png", width: 620, height: 300, mode: "Letterbox" },
  { kind: "Splash", rel: "DataMan (Package)/Images/SplashScreen.scale-200.png", width: 1240, height: 600, mode: "Letterbox" },
  { kind: "LockScreen", rel: "DataMan (Package)/Images/LockScreenLogo.scale-200.png", width: 48, height: 48, mode: "OpaqueField" },
  { kind: "AppIcon", rel: "DataMan/Assets/Brand/DataMan.ico", width: 256, height: 256, mode: "Ico" },
];

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..");
const svgPath = join(repoRoot, "DataMan (Package)", "Images", "DataMan_Logo.svg");
const svg = readFileSync(svgPath);

function writeOut(rel, bytes) {
  const path = join(repoRoot, rel);
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, bytes);
  console.log(rel, bytes.length);
}

function renderMark(px) {
  return new Resvg(svg, { fitTo: { mode: "width", value: px } }).render().asPng();
}

function letterbox(width, height) {
  const markPx = Math.round(Math.min(width, height) * 0.72);
  const mark = renderMark(markPx);
  const x = Math.round((width - markPx) / 2);
  const y = Math.round((height - markPx) / 2);
  const href = `data:image/png;base64,${Buffer.from(mark).toString("base64")}`;
  const canvas = `<?xml version="1.0"?>
<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">
  <rect width="${width}" height="${height}" fill="${field}"/>
  <image x="${x}" y="${y}" width="${markPx}" height="${markPx}" href="${href}"/>
</svg>`;
  return new Resvg(canvas, { fitTo: { mode: "original" } }).render().asPng();
}

function writeIco(rel) {
  const frames = icoSizes.map((size) => ({ size, data: renderMark(size) }));
  let offset = 6 + 16 * frames.length;
  const entries = [];
  const blobs = [];
  for (const frame of frames) {
    const dim = frame.size === 256 ? 0 : frame.size;
    const entry = Buffer.alloc(16);
    entry.writeUInt8(dim, 0);
    entry.writeUInt8(dim, 1);
    entry.writeUInt16LE(1, 4);
    entry.writeUInt16LE(32, 6);
    entry.writeUInt32LE(frame.data.length, 8);
    entry.writeUInt32LE(offset, 12);
    entries.push(entry);
    blobs.push(frame.data);
    offset += frame.data.length;
  }
  const header = Buffer.alloc(6);
  header.writeUInt16LE(0, 0);
  header.writeUInt16LE(1, 2);
  header.writeUInt16LE(frames.length, 4);
  writeOut(rel, Buffer.concat([header, ...entries, ...blobs]));
}

for (const asset of assets) {
  if (asset.mode === "CopySvg") {
    const dest = join(repoRoot, asset.rel);
    mkdirSync(dirname(dest), { recursive: true });
    copyFileSync(svgPath, dest);
    console.log(asset.rel, "copied");
    continue;
  }
  if (asset.mode === "Ico") {
    writeIco(asset.rel);
    continue;
  }
  if (asset.mode === "Letterbox") {
    writeOut(asset.rel, letterbox(asset.width, asset.height));
    continue;
  }
  writeOut(asset.rel, renderMark(asset.width));
}
