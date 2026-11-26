// Loaded by MarkdownViewer the first time a link within the text is clicked or an image is opened, or when the host keeps
// an editor in step with it; the host page does not link it.

// Scrolls a heading, a footnote or a footnote reference to the top of the view and moves focus to it, so a screen
// reader follows. Focus alone does not scroll when the target already has focus, as after a second click on a link.
export function scrollToAnchor(target) {
  target.scrollIntoView({ block: "start" });
  target.focus({ preventScroll: true });
}

// Where the text of the source shows. The viewer gives the start and length of the source of each top-level block, in
// the order of the root's children, which are those blocks (MarkdownRenderTree.Document).
const viewers = new WeakMap();
const isMac = /Mac|iPhone|iPad/.test(navigator.platform);

export function watchSource(root, component, ranges, reportScroll, takeClicks, reportReading = false) {
  let viewer = viewers.get(root);
  if (!viewer) {
    viewer = { root, frame: 0, last: -1, lastReading: -1 };
    viewer.scroller = scroller(root);
    viewer.onScroll = () => schedule(viewer);
    viewer.onClick = e => onClick(viewer, e);
    eventTarget(viewer.scroller).addEventListener("scroll", viewer.onScroll, { passive: true });
    // Captured, so a click with the command key held reaches no link in the text.
    root.addEventListener("click", viewer.onClick, true);
    viewers.set(root, viewer);
  }
  Object.assign(viewer, { component, ranges, reportScroll, takeClicks, reportReading });
  if (reportScroll || reportReading) schedule(viewer);
}

export function unwatchSource(root) {
  const viewer = viewers.get(root);
  if (!viewer) return;
  eventTarget(viewer.scroller).removeEventListener("scroll", viewer.onScroll);
  root.removeEventListener("click", viewer.onClick, true);
  viewers.delete(root);
}

// Scrolls the view so the text at `offset` is at its top.
export function scrollToSource(root, offset) {
  const viewer = viewers.get(root), blocks = viewer && sourceBlocks(viewer);
  if (!blocks) return;
  let index = 0;
  while (index + 1 < blocks.length && blocks[index + 1].start <= offset) index++;
  const block = blocks[index], rect = block.element.getBoundingClientRect();
  const fraction = block.length ? Math.min(Math.max((offset - block.start) / block.length, 0), 1) : 0;
  const delta = rect.top + fraction * rect.height - viewTop(viewer.scroller);
  if (viewer.scroller === document.scrollingElement) window.scrollBy(0, delta);
  else viewer.scroller.scrollTop += delta;
}

// The nearest element around the viewer that scrolls, or the page.
function scroller(root) {
  for (let element = root.parentElement; element; element = element.parentElement) {
    if (/(auto|scroll|overlay)/.test(getComputedStyle(element).overflowY)) return element;
  }
  return document.scrollingElement;
}

// The page's scrolling element raises its scroll events on the window.
function eventTarget(scroller) {
  return scroller === document.scrollingElement ? window : scroller;
}

function viewTop(scroller) {
  return scroller === document.scrollingElement ? 0 : scroller.getBoundingClientRect().top;
}

// The blocks with their elements and source, or null when the shown blocks are not those the ranges describe.
function sourceBlocks(viewer) {
  const elements = viewer.root.children, ranges = viewer.ranges;
  if (!ranges.length || elements.length * 2 !== ranges.length) return null;
  return Array.from(elements, (element, index) => ({ element, start: ranges[index * 2], length: ranges[index * 2 + 1] }));
}

// The source offset at `y`, a position in the window: in the block there, or the last block above it, as far into the
// block's source as `y` is down the block.
function offsetAt(blocks, y) {
  let index = 0;
  while (index + 1 < blocks.length && blocks[index + 1].element.getBoundingClientRect().top <= y) index++;
  const block = blocks[index], rect = block.element.getBoundingClientRect();
  const fraction = rect.height > 0 ? Math.min(Math.max((y - rect.top) / rect.height, 0), 1) : 0;
  return block.start + Math.round(fraction * block.length);
}

function schedule(viewer) {
  if ((!viewer.reportScroll && !viewer.reportReading) || viewer.frame) return;
  viewer.frame = requestAnimationFrame(() => {
    viewer.frame = 0;
    const blocks = sourceBlocks(viewer);
    if (!blocks) return;
    const offset = offsetAt(blocks, viewTop(viewer.scroller));
    if (viewer.reportScroll && offset !== viewer.last) {
      viewer.last = offset;
      viewer.component.invokeMethodAsync("FirstVisibleChanged", offset).catch(() => { /* The viewer is gone. */ });
    }
    if (viewer.reportReading) {
      // At the end of the text the last headings cannot reach the top of the view, so there the bottom decides which
      // section is being read, as in the Avalonia viewer.
      // A heading just below the top counts as read, as the text moves a little when it reflows; the Avalonia
      // reader uses the same distance (MarkdownReader.ReadingMargin).
      const reading = offsetAt(blocks, atEnd(viewer.scroller) ? viewBottom(viewer.scroller) : viewTop(viewer.scroller) + ReadingMargin);
      if (reading !== viewer.lastReading) {
        viewer.lastReading = reading;
        viewer.component.invokeMethodAsync("ReadingChanged", reading).catch(() => { /* The viewer is gone. */ });
      }
    }
  });
}

const ReadingMargin = 24;

function atEnd(scroller) {
  return scroller.scrollHeight - scroller.scrollTop - scroller.clientHeight <= 1;
}

function viewBottom(scroller) {
  return scroller === document.scrollingElement ? window.innerHeight : scroller.getBoundingClientRect().bottom;
}

function onClick(viewer, e) {
  if (!viewer.takeClicks || e.button !== 0 || !(isMac ? e.metaKey : e.ctrlKey)) return;
  const blocks = sourceBlocks(viewer);
  if (!blocks) return;
  e.preventDefault();
  e.stopPropagation();
  viewer.component.invokeMethodAsync("SourceRequested", offsetAt(blocks, e.clientY)).catch(() => { /* The viewer is gone. */ });
}

// An image opened at full size: the dialog the viewer writes (MarkdownRenderTree.ZoomDialog), shown over the page. It
// opens fitted to the window, or at the image's own size when that is smaller, as the Avalonia viewer's ImageZoomOverlay
// does, with the same margin, steps and limit.
const ZoomMargin = 24, MaxZoom = 8, KeyStep = 1.25, DragThreshold = 4;

// The scale at which an image of `natural` size fits `room` with a margin, but no more than its own size.
export function fitScale(natural, room) {
  const width = room.width - 2 * ZoomMargin, height = room.height - 2 * ZoomMargin;
  if (natural.width <= 0 || natural.height <= 0 || width <= 0 || height <= 0) return 1;
  return Math.min(1, width / natural.width, height / natural.height);
}

// Keeps an image larger than the room covering it, and a smaller one centered on the axis it is smaller on, so panning
// never loses it.
export function keepInView(view, natural, room) {
  const width = natural.width * view.scale, height = natural.height * view.scale;
  view.x = width <= room.width ? (room.width - width) / 2 : Math.min(0, Math.max(room.width - width, view.x));
  view.y = height <= room.height ? (room.height - height) / 2 : Math.min(0, Math.max(room.height - height, view.y));
}

export function fit(view, natural, room) {
  view.scale = fitScale(natural, room);
  view.fitted = true;
  keepInView(view, natural, room);
}

// Scales the image to `scale`, keeping the point of it under `anchor` where it is.
export function zoomTo(view, scale, anchor, natural, room) {
  const fitted = fitScale(natural, room);
  scale = Math.min(Math.max(scale, Math.min(fitted, 1)), Math.max(MaxZoom, fitted));
  const factor = scale / view.scale;
  view.x = anchor.x - (anchor.x - view.x) * factor;
  view.y = anchor.y - (anchor.y - view.y) * factor;
  view.scale = scale;
  view.fitted = false;
  keepInView(view, natural, room);
}

export function openZoom(dialog, component) {
  const image = dialog.querySelector(".pmd-zoom-image");
  const view = { scale: 1, x: 0, y: 0, fitted: true };
  const room = () => ({ width: dialog.clientWidth, height: dialog.clientHeight });
  const natural = () => ({ width: image.naturalWidth, height: image.naturalHeight });
  // The stylesheet hides the image until it is first placed, so it does not show at the corner first.
  const apply = () => {
    image.style.transform = `translate(${view.x}px, ${view.y}px) scale(${view.scale})`;
    image.style.opacity = "1";
  };
  const refit = () => {
    if (view.fitted) fit(view, natural(), room());
    else keepInView(view, natural(), room());
    apply();
  };
  const position = e => {
    const rect = dialog.getBoundingClientRect();
    return { x: e.clientX - rect.left, y: e.clientY - rect.top };
  };
  const onImage = point => point.x >= view.x && point.y >= view.y
    && point.x <= view.x + natural().width * view.scale && point.y <= view.y + natural().height * view.scale;
  const center = () => ({ x: dialog.clientWidth / 2, y: dialog.clientHeight / 2 });
  const zoom = (scale, anchor) => { zoomTo(view, scale, anchor, natural(), room()); apply(); };
  const isClose = e => e.target.closest?.(".pmd-zoom-close");

  // A press on the overlay's toolbar, on a button or between them, is the toolbar's, never a drag or a click beside the
  // image.
  const isButton = e => e.target.closest?.(".pmd-zoom-toolbar, .pmd-zoom-close, .pmd-zoom-save");

  // Pointers down on the dialog: one drags, two pinch.
  const pointers = new Map();
  let drag = null, pinch = null;
  const spread = () => {
    const [a, b] = [...pointers.values()];
    return { distance: Math.hypot(a.x - b.x, a.y - b.y), middle: { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 } };
  };

  const handlers = {
    wheel: e => {
      e.preventDefault();
      // A pinch on a touchpad arrives as the wheel with Ctrl held, in smaller steps.
      zoom(view.scale * Math.exp(-e.deltaY * (e.ctrlKey ? 0.01 : 0.0018)), position(e));
    },
    pointerdown: e => {
      if (e.button !== 0 || isButton(e)) return;
      dialog.setPointerCapture?.(e.pointerId);
      pointers.set(e.pointerId, position(e));
      if (pointers.size === 2) {
        pinch = { ...spread(), scale: view.scale };
        drag = null;
      } else if (pointers.size === 1) {
        drag = { start: position(e), from: position(e), moved: false };
      }
    },
    pointermove: e => {
      if (!pointers.has(e.pointerId)) return;
      const point = position(e);
      pointers.set(e.pointerId, point);
      if (pinch && pointers.size === 2) {
        const now = spread();
        if (pinch.distance > 0) zoom(pinch.scale * now.distance / pinch.distance, now.middle);
        return;
      }
      if (!drag) return;
      const dx = point.x - drag.from.x, dy = point.y - drag.from.y;
      if (!drag.moved && Math.abs(dx) + Math.abs(dy) < DragThreshold) return;
      drag.moved = true;
      drag.from = point;
      view.x += dx;
      view.y += dy;
      view.fitted = false;
      keepInView(view, natural(), room());
      apply();
    },
    pointerup: e => {
      if (!pointers.delete(e.pointerId)) return;
      // A click beside the image closes it, as a click on the backdrop does.
      if (drag && !drag.moved && !onImage(drag.start)) dialog.close();
      drag = null;
      if (pointers.size < 2) pinch = null;
    },
    pointercancel: e => {
      pointers.delete(e.pointerId);
      drag = null;
      pinch = null;
    },
    dblclick: e => {
      const point = position(e);
      if (!onImage(point)) return;
      if (view.fitted && fitScale(natural(), room()) < 1) zoom(1, point);
      else { fit(view, natural(), room()); apply(); }
    },
    keydown: e => {
      if (e.ctrlKey || e.metaKey || e.altKey) return;
      if (e.key === "+" || e.key === "=") zoom(view.scale * KeyStep, center());
      else if (e.key === "-") zoom(view.scale / KeyStep, center());
      else if (e.key === "0") { fit(view, natural(), room()); apply(); }
      else return;
      e.preventDefault();
    },
    click: e => { if (isClose(e)) dialog.close(); },
    close: () => {
      for (const [type, handler] of Object.entries(handlers)) dialog.removeEventListener(type, handler);
      window.removeEventListener("resize", refit);
      component.invokeMethodAsync("ZoomClosed").catch(() => { /* The viewer is gone. */ });
    },
  };
  for (const [type, handler] of Object.entries(handlers)) {
    dialog.addEventListener(type, handler, type === "wheel" ? { passive: false } : undefined);
  }
  window.addEventListener("resize", refit);
  dialog.showModal();
  if (image.complete && image.naturalWidth) refit();
  else image.addEventListener("load", refit, { once: true });

  // The viewer cannot tell an image that failed in the text from one that loaded, so one that fails here closes.
  image.addEventListener("error", () => dialog.close(), { once: true });
}

// Finding text in the viewer (MarkdownViewer.FindAsync, polhem-dev/polhem-markdown#85). The text is taken block by block,
// as the Avalonia viewer's text blocks are, so a match never spans two blocks but runs across emphasis and links; an
// image counts as its alternative text and an inline formula as its LaTeX, as when they are copied. Text the Avalonia
// viewer does not show as selectable text is left out: an alert's title, a code block's language, and diagrams.
const finds = new WeakMap();
const inlineTags = new Set(["A", "ABBR", "B", "BR", "BUTTON", "CODE", "DEL", "EM", "I", "IMG", "INS", "KBD", "MARK", "MATH", "PICTURE",
  "S", "SMALL", "SPAN", "STRONG", "SUB", "SUP", "U"]);
const skipped = ".pmd-alert-title, .pmd-code-lang, .pmd-copy, svg, math[display=block]";

// The places `query` occurs in each segment's text, from the start and not overlapping, as [segment, start, end].
export function findInSegments(segments, query, matchCase) {
  const matches = [];
  if (!query) return matches;
  const needle = matchCase ? query : query.toLowerCase();
  segments.forEach((segment, index) => {
    const text = matchCase ? segment.text : segment.text.toLowerCase();
    // NOTE: lower-casing may change the length of a few characters; then the case is matched as written.
    const haystack = text.length === segment.text.length ? text : segment.text;
    let at = haystack.indexOf(needle);
    while (at >= 0) {
      matches.push([index, at, at + query.length]);
      at = haystack.indexOf(needle, at + query.length);
    }
  });
  return matches;
}

// The root's text as segments, one for each block, each with the pieces it is made of: a text node, or an image or a
// formula taken whole.
function segmentsOf(root) {
  const segments = [];
  let current = null;
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT, {
    acceptNode: node => {
      if (node.nodeType === Node.ELEMENT_NODE) {
        if (node.matches(skipped)) return NodeFilter.FILTER_REJECT;
        // NOTE: a MathML element's tag name keeps its case, `math`, where an HTML one's is upper case.
        if (node.localName === "img" || node.localName === "math") return NodeFilter.FILTER_ACCEPT;
        return NodeFilter.FILTER_SKIP;
      }
      return node.parentElement?.closest("math") ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT;
    },
  });
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    let block = node.parentElement;
    while (block !== root && block?.localName && inlineTags.has(block.localName.toUpperCase())) block = block.parentElement;
    if (!current || current.block !== block) {
      current = { block, text: "", pieces: [] };
      segments.push(current);
    }
    const text = pieceText(node);
    current.pieces.push({ node, start: current.text.length, end: current.text.length + text.length });
    current.text += text;
    if (node.nodeType === Node.ELEMENT_NODE) walker.currentNode = lastDescendant(node);
  }
  return segments;
}

// What a piece reads as: a text node's text, an image's alternative text, or a formula's LaTeX between dollars.
function pieceText(node) {
  if (node.nodeType === Node.TEXT_NODE) return node.data;
  if (node.localName === "img") return node.alt || "";
  return "$" + (node.querySelector("annotation")?.textContent ?? "") + "$";
}

function lastDescendant(node) {
  while (node.lastChild) node = node.lastChild;
  return node;
}

// A range over a match: from its first piece to its last, an image or a formula taken whole.
function rangeOf(segment, start, end) {
  const range = document.createRange();
  const first = segment.pieces.find(piece => piece.end > start);
  const last = [...segment.pieces].reverse().find(piece => piece.start < end);
  if (first.node.nodeType === Node.TEXT_NODE) range.setStart(first.node, start - first.start);
  else range.setStartBefore(first.node);
  if (last.node.nodeType === Node.TEXT_NODE) range.setEnd(last.node, end - last.start);
  else range.setEndAfter(last.node);
  return range;
}

// Finds `query` in the viewer, highlights every match, and goes to the first one. Returns [count, current].
export function find(root, query, matchCase) {
  const state = { query, matchCase, current: 0 };
  finds.set(root, state);
  return show(root, state);
}

// Goes `delta` matches on, wrapping round, finding again first, as the text may have changed. Returns [count, current].
export function findStep(root, delta) {
  const state = finds.get(root);
  if (!state) return [0, 0];
  state.current += delta;
  return show(root, state);
}

export function clearFind(root) {
  finds.delete(root);
  globalThis.CSS?.highlights?.delete("pmd-find");
  globalThis.CSS?.highlights?.delete("pmd-find-current");
}

function show(root, state) {
  const segments = segmentsOf(root);
  const ranges = findInSegments(segments, state.query, state.matchCase)
    .map(([segment, start, end]) => rangeOf(segments[segment], start, end));
  if (ranges.length === 0) {
    clearHighlights();
    state.current = 0;
    return [0, 0];
  }
  state.current = ((state.current % ranges.length) + ranges.length) % ranges.length;
  const current = ranges[state.current];

  // A match in a closed section opens it, as a browser's own find does.
  for (let element = current.startContainer.parentElement; element && element !== root; element = element.parentElement) {
    if (element.tagName === "DETAILS" && !element.open) element.open = true;
  }
  const highlights = globalThis.CSS?.highlights;
  if (highlights) {
    highlights.set("pmd-find", new Highlight(...ranges));
    highlights.set("pmd-find-current", new Highlight(current));
  }
  const target = current.startContainer.nodeType === Node.ELEMENT_NODE ? current.startContainer : current.startContainer.parentElement;
  target?.scrollIntoView({ block: "center", inline: "nearest" });
  return [ranges.length, state.current + 1];
}

function clearHighlights() {
  globalThis.CSS?.highlights?.delete("pmd-find");
  globalThis.CSS?.highlights?.delete("pmd-find-current");
}

// Saves the image open at full size: the browser fetches its original bytes and downloads them under `name`, with an
// extension for its type when the name has none. Where the image's host does not let the page read it, it opens in a new
// tab instead, for the reader to save from there.
export async function saveImage(dialog, name) {
  const image = dialog.querySelector(".pmd-zoom-image");
  const source = image.currentSrc || image.src;
  try {
    const response = await fetch(source, { referrerPolicy: "no-referrer" });
    if (!response.ok) throw new Error(`The image answered ${response.status}.`);
    const blob = await response.blob();
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = withExtension(name, blob.type);
    link.click();
    setTimeout(() => URL.revokeObjectURL(link.href), 0);
  } catch {
    window.open(source, "_blank", "noopener,noreferrer");
  }
}

const imageExtensions = {
  "image/png": ".png", "image/jpeg": ".jpg", "image/gif": ".gif", "image/webp": ".webp", "image/svg+xml": ".svg",
  "image/bmp": ".bmp", "image/avif": ".avif",
};

// `name` with the extension of `type` added, unless it has an extension already.
export function withExtension(name, type) {
  if (/\.[a-z0-9]{2,5}$/i.test(name)) return name;
  return name + (imageExtensions[type] ?? "");
}
