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
      if (e.button !== 0 || isClose(e)) return;
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
