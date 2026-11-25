// Loaded by MarkdownViewer the first time a link within the text is clicked, or when the host keeps an editor in step
// with it; the host page does not link it.

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

export function watchSource(root, component, ranges, reportScroll, takeClicks) {
  let viewer = viewers.get(root);
  if (!viewer) {
    viewer = { root, frame: 0, last: -1 };
    viewer.scroller = scroller(root);
    viewer.onScroll = () => schedule(viewer);
    viewer.onClick = e => onClick(viewer, e);
    eventTarget(viewer.scroller).addEventListener("scroll", viewer.onScroll, { passive: true });
    // Captured, so a click with the command key held reaches no link in the text.
    root.addEventListener("click", viewer.onClick, true);
    viewers.set(root, viewer);
  }
  Object.assign(viewer, { component, ranges, reportScroll, takeClicks });
  if (reportScroll) schedule(viewer);
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
  if (!viewer.reportScroll || viewer.frame) return;
  viewer.frame = requestAnimationFrame(() => {
    viewer.frame = 0;
    const blocks = sourceBlocks(viewer);
    if (!blocks) return;
    const offset = offsetAt(blocks, viewTop(viewer.scroller));
    if (offset === viewer.last) return;
    viewer.last = offset;
    viewer.component.invokeMethodAsync("FirstVisibleChanged", offset).catch(() => { /* The viewer is gone. */ });
  });
}

function onClick(viewer, e) {
  if (!viewer.takeClicks || e.button !== 0 || !(isMac ? e.metaKey : e.ctrlKey)) return;
  const blocks = sourceBlocks(viewer);
  if (!blocks) return;
  e.preventDefault();
  e.stopPropagation();
  viewer.component.invokeMethodAsync("SourceRequested", offsetAt(blocks, e.clientY)).catch(() => { /* The viewer is gone. */ });
}
