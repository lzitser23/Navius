// Test runner for the C#-vs-JS spring evaluator agreement (JsAgreementTests).
// Usage: node eval-spring.mjs <path-to-navius-motion.js> <path-to-cases.json>
// Input JSON: { "evals": [{ "spring": {...}, "t": 0.1 }, ...], "bake": {...} }
// Output JSON: { "evals": [{ "position": n, "velocity": n }, ...],
//   "bake": { "easing": "...", "durationMs": n, "points": [...] } }
import { pathToFileURL } from 'node:url';
import { readFileSync } from 'node:fs';

const [modulePath, casesPath] = process.argv.slice(2);
const engine = await import(pathToFileURL(modulePath).href);
const input = JSON.parse(readFileSync(casesPath, 'utf8'));

const output = {
  evals: input.evals.map((c) => ({
    position: engine.springPosition(c.spring, c.t),
    velocity: engine.springVelocity(c.spring, c.t),
  })),
  bake: input.bake ? engine.bakeSpringEasing(input.bake) : null,
};

process.stdout.write(JSON.stringify(output));
