// Tests of the image zoom of polhem-markdown.js, the Blazor viewer's script module (polhem-dev/polhem-markdown#72).
import { test } from "node:test";
import assert from "node:assert/strict";

const windowListeners = {};
globalThis.window = {
  addEventListener: (type, handler) => { windowListeners[type] = handler; },
  removeEventListener: type => { delete windowListeners[type]; },
};
Object.defineProperty(globalThis, "navigator", { value: { platform: "Linux x86_64" }, configurable: true });
const viewer = await import("../../src/Polhem.Markdown.Blazor/wwwroot/polhem-markdown.js");

const room = { width: 800, height: 600 };
const natural = { width: 1600, height: 900 };

test("An image opens fitted to the room with a margin, and never larger than its own size", () => {
  assert.equal(viewer.fitScale(natural, room), (800 - 48) / 1600);
  assert.equal(viewer.fitScale({ width: 100, height: 50 }, room), 1);
});

test("Zooming keeps the point under the pointer where it is, where the image overflows the room", () => {
  const view = { scale: 1, x: 0, y: 0, fitted: true };
  viewer.fit(view, natural, room);
  viewer.zoomTo(view, 2, { x: 500, y: 300 }, natural, room);
  const before = { x: (500 - view.x) / view.scale, y: (300 - view.y) / view.scale };
  viewer.zoomTo(view, 3, { x: 500, y: 300 }, natural, room);
  assert.deepEqual({ x: (500 - view.x) / view.scale, y: (300 - view.y) / view.scale }, before);
  assert.equal(view.fitted, false);
});

test("Zooming stops at eight times the image's size, and at the fitted size", () => {
  const view = { scale: 1, x: 0, y: 0, fitted: true };
  viewer.zoomTo(view, 100, { x: 0, y: 0 }, natural, room);
  assert.equal(view.scale, 8);
  viewer.zoomTo(view, 0.01, { x: 0, y: 0 }, natural, room);
  assert.equal(view.scale, viewer.fitScale(natural, room));
});

test("A smaller image stays centered, and a larger one keeps covering the room", () => {
  const view = { scale: 0.25, x: -500, y: 900, fitted: false };
  viewer.keepInView(view, natural, room);
  assert.deepEqual([view.x, view.y], [(800 - 400) / 2, (600 - 225) / 2]);
  Object.assign(view, { scale: 2, x: 100, y: -5000 });
  viewer.keepInView(view, natural, room);
  assert.deepEqual([view.x, view.y], [0, 600 - 1800]);
});

class Element {
  constructor(classes = "") { this.classes = classes; this.listeners = {}; this.style = {}; }
  addEventListener(type, handler) { (this.listeners[type] ||= []).push(handler); }
  removeEventListener(type, handler) { this.listeners[type] = (this.listeners[type] || []).filter(h => h !== handler); }
  closest(selectors) {
    const classes = this.classes.split(" ");
    return selectors.split(",").some(selector => classes.includes(selector.trim().slice(1))) ? this : null;
  }
}

function openDialog() {
  const image = new Element("pmd-zoom-image");
  Object.assign(image, { complete: true, naturalWidth: natural.width, naturalHeight: natural.height });
  const close = new Element("pmd-zoom-close");
  const save = new Element("pmd-zoom-save");
  const dialog = new Element("pmd-zoom");
  Object.assign(dialog, {
    clientWidth: room.width, clientHeight: room.height, open: false,
    querySelector: selector => (selector === ".pmd-zoom-image" ? image : close),
    save,
    getBoundingClientRect: () => ({ left: 0, top: 0 }),
    showModal() { this.open = true; },
    close() { this.open = false; this.fire("close", { target: this }); },
    fire(type, properties = {}) {
      const event = { type, button: 0, pointerId: 1, target: this, preventDefault() {}, ...properties };
      for (const handler of this.listeners[type] || []) handler(event);
    },
  });
  const calls = [];
  const component = { invokeMethodAsync: (name) => { calls.push(name); return Promise.resolve(); } };
  viewer.openZoom(dialog, component);
  return { dialog, image, close, calls };
}

const scaleOf = image => Number(/scale\(([^)]+)\)/.exec(image.style.transform)[1]);

test("The dialog opens with the image fitted, and reports its closing once to the viewer", () => {
  const { dialog, image, calls } = openDialog();
  assert.equal(dialog.open, true);
  assert.equal(scaleOf(image), viewer.fitScale(natural, room));
  assert.equal(image.style.opacity, "1");

  dialog.close();
  assert.deepEqual(calls, ["ZoomClosed"]);
  assert.equal(windowListeners.resize, undefined);
});

test("The wheel zooms in, + and - step, and 0 fits again", () => {
  const { dialog, image } = openDialog();
  const fitted = scaleOf(image);
  dialog.fire("wheel", { deltaY: -100, clientX: 400, clientY: 300 });
  assert.ok(scaleOf(image) > fitted);
  dialog.fire("keydown", { key: "0" });
  assert.equal(scaleOf(image), fitted);
  dialog.fire("keydown", { key: "+" });
  assert.equal(scaleOf(image), fitted * 1.25);
});

test("A click beside the image closes it; a click on it, a drag, or the close button's own press does not, until it is clicked", () => {
  const { dialog } = openDialog();
  dialog.fire("pointerdown", { clientX: 400, clientY: 300 });
  dialog.fire("pointerup", { clientX: 400, clientY: 300 });
  assert.equal(dialog.open, true);

  dialog.fire("pointerdown", { clientX: 5, clientY: 5 });
  dialog.fire("pointermove", { clientX: 60, clientY: 40 });
  dialog.fire("pointerup", { clientX: 60, clientY: 40 });
  assert.equal(dialog.open, true);

  dialog.fire("pointerdown", { clientX: 5, clientY: 5 });
  dialog.fire("pointerup", { clientX: 5, clientY: 5 });
  assert.equal(dialog.open, false);
});

test("The close button closes the dialog", () => {
  const { dialog, close } = openDialog();
  dialog.fire("pointerdown", { target: close, clientX: 780, clientY: 20 });
  dialog.fire("click", { target: close });
  assert.equal(dialog.open, false);
});

test("Two pointers pinch the image around their middle", () => {
  const { dialog, image } = openDialog();
  const fitted = scaleOf(image);
  dialog.fire("pointerdown", { pointerId: 1, clientX: 350, clientY: 300 });
  dialog.fire("pointerdown", { pointerId: 2, clientX: 450, clientY: 300 });
  dialog.fire("pointermove", { pointerId: 2, clientX: 550, clientY: 300 });
  assert.equal(scaleOf(image), fitted * 2);
});

test("An image that cannot be loaded closes the dialog", () => {
  const { dialog, image, calls } = openDialog();
  for (const handler of image.listeners.error || []) handler({ type: "error" });
  assert.equal(dialog.open, false);
  assert.deepEqual(calls, ["ZoomClosed"]);
});

test("A press on the save button is the button's: it neither drags nor closes the image", () => {
  const { dialog } = openDialog();
  dialog.fire("pointerdown", { target: dialog.save, clientX: 5, clientY: 5 });
  dialog.fire("pointerup", { target: dialog.save, clientX: 5, clientY: 5 });
  assert.equal(dialog.open, true);
});

test("A name without an extension gets the one of the image's type", () => {
  assert.equal(viewer.withExtension("build-passing", "image/svg+xml"), "build-passing.svg");
  assert.equal(viewer.withExtension("chart.png", "image/jpeg"), "chart.png");
  assert.equal(viewer.withExtension("image", "application/octet-stream"), "image");
});

test("Saving downloads the image under its name, or opens it in a new tab when its host does not let the page read it", async () => {
  const image = { currentSrc: "https://example.org/chart", src: "https://example.org/chart" };
  const dialog = { querySelector: () => image };
  const clicked = [];
  const opened = [];
  globalThis.document = { createElement: () => ({ click() { clicked.push(this.download); } }) };
  globalThis.URL.createObjectURL = () => "blob:chart";
  globalThis.URL.revokeObjectURL = () => {};
  globalThis.window.open = url => opened.push(url);

  globalThis.fetch = async () => ({ ok: true, blob: async () => ({ type: "image/png" }) });
  await viewer.saveImage(dialog, "chart");
  assert.deepEqual(clicked, ["chart.png"]);

  globalThis.fetch = async () => { throw new TypeError("Failed to fetch"); };
  await viewer.saveImage(dialog, "chart");
  globalThis.fetch = async () => ({ ok: false, status: 403 });
  await viewer.saveImage(dialog, "chart");
  assert.deepEqual(opened, ["https://example.org/chart", "https://example.org/chart"]);
});
