// Tests of the find of polhem-markdown.js on a small stand-in for the DOM: walking the text, ranges, highlights, opening a
// closed section, and scrolling (polhem-dev/polhem-markdown#85).
import { test, beforeEach } from "node:test";
import assert from "node:assert/strict";

const ELEMENT = 1, TEXT = 3;
const FILTER_ACCEPT = 1, FILTER_REJECT = 2, FILTER_SKIP = 3;

class FakeNode {
  constructor(nodeType) { this.nodeType = nodeType; this.parentNode = null; this.childNodes = []; }
  get parentElement() { return this.parentNode; }
  get lastChild() { return this.childNodes.at(-1) ?? null; }
}

class FakeText extends FakeNode {
  constructor(data) { super(TEXT); this.data = data; }
  get textContent() { return this.data; }
}

class FakeElement extends FakeNode {
  constructor(localName, attributes = {}, children = []) {
    super(ELEMENT);
    this.localName = localName;
    this.tagName = localName === "math" || localName === "annotation" ? localName : localName.toUpperCase();
    this.attributes = attributes;
    this.className = attributes.class ?? "";
    this.alt = attributes.alt;
    this.open = attributes.open ?? false;
    this.scrolled = 0;
    for (const child of children) this.append(child);
  }
  append(child) { child.parentNode = this; this.childNodes.push(child); }
  get textContent() { return this.childNodes.map(child => child.textContent).join(""); }
  // A selector list of tags, classes, and tags with one attribute value: what the find uses.
  matches(selectors) {
    return selectors.split(",").map(selector => selector.trim()).some(selector => {
      if (selector.startsWith(".")) return this.className.split(" ").includes(selector.slice(1));
      const attribute = /^(\w+)\[(\w+)=(\w+)\]$/.exec(selector);
      if (attribute) return this.localName === attribute[1] && this.attributes[attribute[2]] === attribute[3];
      return this.localName === selector;
    });
  }
  closest(selector) {
    for (let element = this; element; element = element.parentNode) if (element.matches(selector)) return element;
    return null;
  }
  querySelector(selector) {
    for (const child of this.childNodes) {
      if (child.nodeType !== ELEMENT) continue;
      if (child.matches(selector)) return child;
      const found = child.querySelector(selector);
      if (found) return found;
    }
    return null;
  }
  scrollIntoView() { this.scrolled++; }
}

// A tree walker over the stand-in: depth first, with the filter's accept, skip and reject.
class FakeWalker {
  constructor(root, filter) { this.root = root; this.filter = filter; this.currentNode = root; }
  nextNode() {
    let node = this.currentNode;
    let descend = true;
    for (;;) {
      const next = descend && node.childNodes.length ? node.childNodes[0] : this.after(node);
      if (!next) return null;
      const verdict = this.filter.acceptNode(next);
      node = next;
      if (verdict === FILTER_ACCEPT) {
        this.currentNode = next;
        return next;
      }
      descend = verdict === FILTER_SKIP;
    }
  }
  after(node) {
    for (; node && node !== this.root; node = node.parentNode) {
      const siblings = node.parentNode.childNodes;
      const index = siblings.indexOf(node);
      if (index + 1 < siblings.length) return siblings[index + 1];
    }
    return null;
  }
}

class FakeRange {
  setStart(node, offset) { this.startContainer = node; this.startOffset = offset; }
  setEnd(node, offset) { this.endContainer = node; this.endOffset = offset; }
  setStartBefore(node) { this.setStart(node.parentNode, node.parentNode.childNodes.indexOf(node)); }
  setEndAfter(node) { this.setEnd(node.parentNode, node.parentNode.childNodes.indexOf(node) + 1); }
}

globalThis.Node = { ELEMENT_NODE: ELEMENT, TEXT_NODE: TEXT };
globalThis.NodeFilter = { SHOW_ELEMENT: 1, SHOW_TEXT: 4, FILTER_ACCEPT, FILTER_REJECT, FILTER_SKIP };
globalThis.document = {
  createTreeWalker: (root, _, filter) => new FakeWalker(root, filter),
  createRange: () => new FakeRange(),
};
globalThis.window = { addEventListener() {}, removeEventListener() {} };
Object.defineProperty(globalThis, "navigator", { value: { platform: "Linux x86_64" }, configurable: true });
globalThis.Highlight = class { constructor(...ranges) { this.ranges = ranges; } };
const highlights = new Map();
globalThis.CSS = { highlights };
const viewer = await import("../../src/Polhem.Markdown.Blazor/wwwroot/polhem-markdown.js");

const el = (name, attributes, ...children) => new FakeElement(name, attributes, children);
const text = data => new FakeText(data);

// A viewer's root: a paragraph with emphasis, a link and an image, an alert with its title, a code block with its
// language, a closed section, a diagram, and an inline and a display formula.
function viewerRoot() {
  return el("div", { class: "pmd" },
    el("p", {}, text("An "), el("strong", {}, text("or")), text("der form, "), el("a", {}, text("order")), text(" and "),
      el("img", { alt: "an order chart" })),
    el("div", { class: "pmd-alert" }, el("p", { class: "pmd-alert-title" }, text("Order")), el("p", {}, text("Note."))),
    el("div", { class: "pmd-code" }, el("span", { class: "pmd-code-lang" }, text("order")), el("pre", {}, el("code", {}, text("order()")))),
    el("details", {}, el("summary", {}, text("More")), el("p", {}, text("A hidden order."))),
    el("svg", {}, el("text", {}, text("order"))),
    el("p", {}, text("Euler: "), el("math", {}, el("semantics", {}, el("mi", {}, text("e")), el("annotation", {}, text("e^{order}"))))),
    el("math", { display: "block" }, el("annotation", {}, text("order"))));
}

beforeEach(() => highlights.clear());

test("Find takes the text block by block, across emphasis and links, with an image's alt text and a formula's LaTeX, and leaves out titles, languages and diagrams", () => {
  const root = viewerRoot();

  assert.deepEqual(viewer.find(root, "order", false), [6, 1]);
  assert.equal(highlights.get("pmd-find").ranges.length, 6);
  assert.equal(highlights.get("pmd-find-current").ranges.length, 1);
});

test("A match across emphasis starts in one text node and ends in another; one in an image covers the image", () => {
  const root = viewerRoot();
  viewer.find(root, "order", false);

  const [first, , image] = highlights.get("pmd-find").ranges;
  assert.equal(first.startContainer.data, "or");
  assert.equal(first.endContainer.data, "der form, ");
  assert.equal(image.startContainer.localName, "p");
  assert.equal(image.endOffset - image.startOffset, 1);
});

test("Stepping goes through the matches and wraps; a match in a closed section opens it and is scrolled to", () => {
  const root = viewerRoot();
  const details = root.childNodes[3];
  viewer.find(root, "hidden order", false);

  assert.equal(details.open, true);
  assert.equal(details.childNodes[1].scrolled, 1);

  viewer.find(root, "order", false);
  assert.deepEqual(viewer.findStep(root, -1), [6, 6]);
  assert.deepEqual(viewer.findStep(root, 1), [6, 1]);
});

test("Matching case, finding nothing, and clearing take the highlights away", () => {
  const root = viewerRoot();

  assert.deepEqual(viewer.find(root, "Order", true), [0, 0]);
  assert.equal(highlights.size, 0);

  viewer.find(root, "order", false);
  viewer.clearFind(root);
  assert.equal(highlights.size, 0);
  assert.deepEqual(viewer.findStep(root, 1), [0, 0]);
});
