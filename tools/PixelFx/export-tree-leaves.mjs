import { readFileSync, writeFileSync, copyFileSync, mkdirSync } from "node:fs";
import { resolve, dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
const here = dirname(fileURLToPath(import.meta.url));
const checkout = resolve(process.argv[2] ?? join(here, "../../Library/PixelFxAuthoring"));
const output = resolve(here, "../../Assets/Resources");
copyFileSync(join(here, "tree-leaf-fall.js"), join(checkout, "src/fx/nature/tree-leaf-fall.js"));
const { loadEffect, renderFrames, spriteSheet, contactSheet } = await import(pathToFileURL(join(checkout, "tools/lib/fxlib.mjs")));
const fx = await loadEffect("nature/tree-leaf-fall");
const four = renderFrames(fx, { actor: false, options: { count: 4, seed: 39 } });
const five = renderFrames(fx, { actor: false, options: { count: 5, seed: 17 } });
const combined = { ...four, n: four.n + five.n, frames: [...four.frames, ...five.frames] };
const sheet = spriteSheet(fx, combined, "TreeLeafFall");
sheet.json.meta.frameTags = [
  { name: "four", from: 0, to: four.n - 1, direction: "forward" },
  { name: "five", from: four.n, to: four.n + five.n - 1, direction: "forward" }
];
sheet.json.meta.pixelfx.sourceCommit = "5294518ce7c72e48b6100a744596e0acf57f2826";
sheet.json.meta.pixelfx.leafCounts = [4, 5];
mkdirSync(output, { recursive: true });
writeFileSync(join(output, "TreeLeafFall.png"), sheet.png);
writeFileSync(join(output, "TreeLeafFallFrames.json"), JSON.stringify(sheet.json, null, 2) + "\n");
const preview = join(checkout, "records/nature/tree-leaf-fall");
mkdirSync(preview, { recursive: true });
writeFileSync(join(preview, "four-contact.png"), contactSheet(fx, four, { max: 10, zoom: 3 }));
writeFileSync(join(preview, "five-contact.png"), contactSheet(fx, five, { max: 10, zoom: 3 }));
console.log(JSON.stringify({ output, frames: combined.n, clips: sheet.json.meta.frameTags, size: sheet.json.meta.size }));
