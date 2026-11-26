// Tests of the source features of polhem-markdown.js, the Blazor viewer's script module.
import { test, beforeEach } from "node:test";
import assert from "node:assert/strict";

class Listeners {
  constructor() { this.listeners = {}; }
  addEventListener(type, handler) { (this.listeners[type] ||= []).push(handler); }
  removeEventListener(type, handler) { this.listeners[type] = (this.listeners[type] || []).filter(h => h !== handler); }
  dispatch(type, properties = {}) {
    const event = { type, button: 0, ctrlKey: false, metaKey: false, defaultPrevented: false, stopped: false, ...properties };
    event.preventDefault = () => { event.defaultPrevented = true; };
    event.stopPropagation = () => { event.stopped = true; };
    for (const handler of this.listeners[type] || []) handler(event);
    return event;
  }
}

// An element whose box is at `top`, `height` tall, in window coordinates, moving up as its scroller scrolls.
class Box extends Listeners {
  constructor(top, height, scroller = null, overflowY = "visible") {
    super();
    Object.assign(this, { top, height, scroller, overflowY, scrollTop: 0, parentElement: null, children: [] });
  }
  getBoundingClientRect() {
    const top = this.top - (this.scroller?.scrollTop ?? 0);
    return { top, height: this.height, bottom: top + this.height };
  }
}

const page = new Box(0, 10_000);
page.scrollTop = 0;
const frames = [];
globalThis.requestAnimationFrame = callback => frames.push(callback);
globalThis.getComputedStyle = element => ({ overflowY: element.overflowY });
globalThis.window = { scrolled: 0, scrollBy(x, y) { this.scrolled += y; page.scrollTop += y; }, ...new Listeners() };
window.addEventListener = Listeners.prototype.addEventListener.bind(window);
window.removeEventListener = Listeners.prototype.removeEventListener.bind(window);
window.dispatch = Listeners.prototype.dispatch.bind(window);
window.listeners = {};
globalThis.document = { scrollingElement: page };
Object.defineProperty(globalThis, "navigator", { value: { platform: "Linux x86_64" }, configurable: true });
const viewer = await import("../../src/Polhem.Markdown.Blazor/wwwroot/polhem-markdown.js");

const runFrames = () => { while (frames.length) frames.shift()(); };

class Component {
  constructor() { this.calls = []; }
  invokeMethodAsync(method, ...args) { this.calls.push([method, ...args]); return Promise.resolve(); }
}

// A viewer in a pane that scrolls, its top at 100 in the window, with blocks 50, 100 and 50 tall.
function setup({ inPane = true } = {}) {
  const pane = inPane ? new Box(100, 200, null, "auto") : null;
  const root = new Box(100, 200, pane);
  root.parentElement = inPane ? Object.assign(new Box(100, 200, pane), { parentElement: pane }) : null;
  root.children = [new Box(100, 50, pane ?? page), new Box(150, 100, pane ?? page), new Box(250, 50, pane ?? page)];
  const ranges = [0, 10, 12, 40, 54, 6];
  return { pane, root, ranges, component: new Component() };
}

beforeEach(() => { frames.length = 0; page.scrollTop = 0; window.scrolled = 0; });

test("the offset at the top of the pane is reported as it scrolls, once per change", () => {
  const { pane, root, ranges, component } = setup();
  viewer.watchSource(root, component, ranges, true, false);
  runFrames();

  pane.scrollTop = 100;
  pane.dispatch("scroll");
  pane.dispatch("scroll");
  runFrames();
  pane.dispatch("scroll");
  runFrames();

  assert.deepEqual(component.calls, [["FirstVisibleChanged", 0], ["FirstVisibleChanged", 32]]);
  viewer.unwatchSource(root);
});

test("scrolling to an offset puts its place in its block at the top of the pane", () => {
  const { pane, root, ranges, component } = setup();
  viewer.watchSource(root, component, ranges, false, false);

  viewer.scrollToSource(root, 32);
  assert.equal(pane.scrollTop, 100);
  viewer.scrollToSource(root, 0);
  assert.equal(pane.scrollTop, 0);
  viewer.scrollToSource(root, 11);
  assert.equal(pane.scrollTop, 50);
  viewer.scrollToSource(root, 999);
  assert.equal(pane.scrollTop, 200);
  viewer.unwatchSource(root);
});

test("a viewer in no pane that scrolls follows the page", () => {
  const { root, ranges, component } = setup({ inPane: false });
  viewer.watchSource(root, component, ranges, true, false);

  viewer.scrollToSource(root, 54);
  assert.equal(window.scrolled, 250);
  window.dispatch("scroll");
  runFrames();

  assert.deepEqual(component.calls.at(-1), ["FirstVisibleChanged", 54]);
  viewer.unwatchSource(root);
});

test("a click with the command key goes to the host and no further; other clicks are left alone", () => {
  const { root, ranges, component } = setup();
  viewer.watchSource(root, component, ranges, false, true);

  const taken = root.dispatch("click", { ctrlKey: true, clientY: 200 });
  const plain = root.dispatch("click", { clientY: 200 });
  const right = root.dispatch("click", { ctrlKey: true, button: 2, clientY: 200 });

  assert.ok(taken.defaultPrevented && taken.stopped);
  assert.ok(!plain.defaultPrevented && !right.defaultPrevented);
  assert.deepEqual(component.calls, [["SourceRequested", 32]]);
  viewer.unwatchSource(root);
});

test("clicks are left alone when the host does not take them, and nothing is reported when it does not listen", () => {
  const { pane, root, ranges, component } = setup();
  viewer.watchSource(root, component, ranges, false, false);

  assert.equal(root.dispatch("click", { ctrlKey: true, clientY: 200 }).defaultPrevented, false);
  pane.dispatch("scroll");
  runFrames();

  assert.equal(component.calls.length, 0);
  viewer.unwatchSource(root);
});

test("ranges that do not match the blocks shown do nothing, and a second watch updates the first", () => {
  const { pane, root, ranges, component } = setup();
  viewer.watchSource(root, component, [0, 10], true, true);
  runFrames();
  viewer.scrollToSource(root, 5);
  assert.equal(root.dispatch("click", { ctrlKey: true, clientY: 200 }).defaultPrevented, false);
  assert.equal(pane.scrollTop, 0);
  assert.equal(component.calls.length, 0);

  viewer.watchSource(root, component, ranges, true, true);
  runFrames();
  assert.deepEqual(component.calls, [["FirstVisibleChanged", 0]]);
  viewer.unwatchSource(root);
});

test("after unwatching, scrolling and clicks do nothing, and unwatching again is harmless", () => {
  const { pane, root, ranges, component } = setup();
  viewer.watchSource(root, component, ranges, true, true);
  runFrames();
  viewer.unwatchSource(root);
  viewer.unwatchSource(root);

  pane.dispatch("scroll");
  root.dispatch("click", { ctrlKey: true, clientY: 200 });
  viewer.scrollToSource(root, 32);

  assert.equal(component.calls.length, 1);
  assert.equal(pane.scrollTop, 0);
});

test("an empty block takes no share of its source", () => {
  const { root, component } = setup();
  root.children = [new Box(100, 0, root.parentElement.parentElement)];
  viewer.watchSource(root, component, [0, 0], false, true);

  root.dispatch("click", { ctrlKey: true, clientY: 300 });

  assert.deepEqual(component.calls, [["SourceRequested", 0]]);
  viewer.unwatchSource(root);
});

test("a link to an anchor scrolls the target to the top and focuses it without scrolling again", () => {
  const calls = [];
  const target = { scrollIntoView: options => calls.push(["scroll", options]), focus: options => calls.push(["focus", options]) };

  viewer.scrollToAnchor(target);

  assert.deepEqual(calls, [["scroll", { block: "start" }], ["focus", { preventScroll: true }]]);
});

test("for the table of contents, the section being read is reported: just below the top of the pane, and at its bottom once at the end", () => {
  const pane = Object.assign(new Box(100, 100, null, "auto"), { clientHeight: 100, scrollHeight: 200 });
  const root = new Box(100, 200, pane);
  root.parentElement = pane;
  root.children = [new Box(100, 50, pane), new Box(150, 100, pane), new Box(250, 50, pane)];
  const component = new Component();
  viewer.watchSource(root, component, [0, 10, 12, 40, 54, 6], false, false, true);
  runFrames();

  pane.scrollTop = 100;
  pane.dispatch("scroll");
  runFrames();

  // 24 pixels into the first block, which is 50 tall with 10 characters of source.
  assert.deepEqual(component.calls, [["ReadingChanged", 5], ["ReadingChanged", 60]]);
  viewer.unwatchSource(root);
});
