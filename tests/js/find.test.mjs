// Tests of the find of polhem-markdown.js, the Blazor viewer's script module (polhem-dev/polhem-markdown#85).
import { test } from "node:test";
import assert from "node:assert/strict";

globalThis.window = { addEventListener() {}, removeEventListener() {} };
Object.defineProperty(globalThis, "navigator", { value: { platform: "Linux x86_64" }, configurable: true });
const viewer = await import("../../src/Polhem.Markdown.Blazor/wwwroot/polhem-markdown.js");

const segments = (...texts) => texts.map(text => ({ text }));

test("A query is found in each block, not overlapping, ignoring case unless asked", () => {
  assert.deepEqual(viewer.findInSegments(segments("The order and the Order", "orders"), "order", false),
    [[0, 4, 9], [0, 18, 23], [1, 0, 5]]);
  assert.deepEqual(viewer.findInSegments(segments("The order and the Order"), "order", true), [[0, 4, 9]]);
  assert.deepEqual(viewer.findInSegments(segments("aaaa"), "aa", false), [[0, 0, 2], [0, 2, 4]]);
});

test("A match never spans two blocks", () => {
  assert.deepEqual(viewer.findInSegments(segments("or", "der"), "order", false), []);
});

test("An empty query finds nothing", () => {
  assert.deepEqual(viewer.findInSegments(segments("anything"), "", false), []);
});
