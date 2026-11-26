// Leaf drawing adapted from PixelFX nature/_lib.js and Sprite Fusion's falling leaves.
// MIT License, Copyright (c) 2026 Patch and Hugo Duprez.
import { fit, rng, TAU, clamp, smooth } from "../../core/index.js";
import { drawLeaf } from "./_lib.js";
export const meta = {
  name: "Tree leaf fall",
  desc: "Four or five green leaves shake loose from a canopy, flutter and turn as they fall, then fade near the ground.",
  tags: ["nature", "tree", "leaves", "fall", "one-shot"],
  size: [96, 96], origin: [48, 84], duration: 2.4, loop: false, fps: 15, bg: "ground",
  options: { palette: ["#183b32", "#356346", "#638d50", "#aac56b"], count: 5, seed: 17 }
};
export function draw(p, t, o) {
  if (t >= meta.duration) return;
  const C = fit(o.palette, 4);
  for (let i = 0; i < Math.max(4, Math.min(5, o.count)); i++) {
    const R = rng(o.seed, i);
    const delay = R.range(0, 0.09), age = t - delay;
    if (age < 0) continue;
    const life = R.range(1.7, 2.22), u = clamp(age / life);
    if (u >= 1) continue;
    const x0 = (i - (o.count - 1) * 0.5) * 14 + R.range(-2, 2), y0 = R.range(-64, -39);
    const phase = R.range(0, TAU), frequency = R.range(4, 6);
    const swing = Math.sin(age * frequency + phase) - Math.sin(phase);
    const x = x0 + swing * R.range(2.5, 3.5) + R.range(-3, 3) * u;
    const y = y0 + (-3 - y0) * (u * 0.75 + u * u * 0.25);
    const tilt = Math.cos(age * frequency + phase) * 0.55 + R.range(-0.2, 0.2);
    const alpha = (1 - smooth(0.88, 1, u)) * Math.min(1, age * 24);
    drawLeaf(p, x, y, R.range(7.5, 9.5), R.range(2, 2.8), tilt, phase + age * R.range(4, 7), C, alpha);
  }
}
