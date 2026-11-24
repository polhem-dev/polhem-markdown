// The browser side of Polhem.Markdown.Blazor's MarkdownEditor, which loads it. A native textarea takes the input; its
// text is transparent, and this module draws the same text with colors on the layer behind it, from the spans the
// component's scan found. The textarea belongs to the user: the module reads each edit from it and sends the change to
// the component, and only writes to it for an undo, a redo, or a text the host sets.

const editors = new WeakMap();
const isMac = /Mac|iPhone|iPad/.test(navigator.platform);

// A message from the browser to a server may not exceed 32 KB by default (SignalR's MaximumReceiveMessageSize), so a
// large edit, such as a paste, goes in parts well below it.
const part = 8000;

// Typing in a row is one step to undo, until it pauses this long.
const joinWithin = 1000;
const historyLimit = 500;

// Native input that joins the step before it when typed in a row.
const typing = { insertText: 1, deleteContentBackward: 1, deleteContentForward: 1, deleteWordBackward: 1, deleteWordForward: 1 };

export function init(root, component, text, kindClasses) {
  const textarea = root.querySelector("textarea"), layer = root.querySelector("pre");
  const editor = {
    root, textarea, layer, component, kindClasses,
    local: text, version: 0, pending: [], unsent: [], base: [], spans: [], lines: [],
    composing: false, compositionStart: null, selectionBefore: [0, 0], undo: [], redo: [],
  };
  const on = (type, handler) => textarea.addEventListener(type, handler);
  on("input", e => onInput(editor, e));
  on("beforeinput", e => onBeforeInput(editor, e));
  on("keydown", e => onKeyDown(editor, e));
  on("compositionstart", () => {
    editor.composing = true;
    editor.compositionStart = { text: editor.local, selection: selection(textarea) };
  });
  on("compositionend", e => {
    editor.composing = false;
    onInput(editor, e);
    endComposition(editor);
  });
  on("scroll", () => {
    layer.scrollTop = textarea.scrollTop;
    layer.scrollLeft = textarea.scrollLeft;
  });
  // The layer takes the size of the textarea's text area, which leaves out its scroll bar.
  editor.resize = new ResizeObserver(() => {
    layer.style.width = textarea.clientWidth + "px";
    layer.style.height = textarea.clientHeight + "px";
  });
  editor.resize.observe(textarea);
  editors.set(root, editor);

  // Text typed before the module loaded is kept, and sent as an edit.
  if (textarea.value !== text) onInput(editor, null);
  render(editor);
  root.classList.add("pmd-editor-ready");
}

export function dispose(root) {
  const editor = editors.get(root);
  if (!editor) return;
  editor.resize.disconnect();
  editors.delete(root);
}

export function focus(root) {
  editors.get(root)?.textarea.focus();
}

// Replaces the text with one the host set, which also clears the undo history.
export function setText(root, text) {
  const editor = editors.get(root);
  if (!editor || editor.local === text) return;
  editor.textarea.value = text;
  editor.local = text;
  editor.pending.length = 0;
  editor.unsent.length = 0;
  editor.spans = [];
  editor.undo.length = 0;
  editor.redo.length = 0;
  render(editor);
}

export function undo(root) {
  const editor = editors.get(root);
  return !!editor && history(editor, "undo");
}

export function redo(root) {
  const editor = editors.get(root);
  return !!editor && history(editor, "redo");
}

// Takes the spans of a scan of the text as it was after edit `version`, as a patch on those sent before (the format is
// described in SyntaxSpanPatch.cs). The spans are then moved by the edits made since.
export function applySpans(root, version, bytes) {
  const editor = editors.get(root);
  if (!editor) return;
  const numbers = new Int32Array(bytes.slice().buffer);
  const [at, removed, delta] = numbers;
  const inserted = [];
  for (let index = 3; index < numbers.length; index += 3) inserted.push([numbers[index], numbers[index + 1], numbers[index + 2]]);
  const base = editor.base;
  for (let index = at + removed; index < base.length; index++) base[index] = [base[index][0] + delta, base[index][1], base[index][2]];
  base.splice(at, removed, ...inserted);

  editor.pending = editor.pending.filter(edit => edit.version > version);
  const spans = base.map(span => span.slice());
  for (const edit of editor.pending) shift(spans, edit);
  editor.spans = spans;
  render(editor);
}

function selection(textarea) {
  return [textarea.selectionStart, textarea.selectionEnd];
}

function difference(before, after) {
  const max = Math.min(before.length, after.length);
  let head = 0;
  while (head < max && before.charCodeAt(head) === after.charCodeAt(head)) head++;
  let tail = 0;
  while (tail < max - head && before.charCodeAt(before.length - 1 - tail) === after.charCodeAt(after.length - 1 - tail)) tail++;
  return { start: head, removed: before.slice(head, before.length - tail), inserted: after.slice(head, after.length - tail) };
}

function onInput(editor, e) {
  const now = editor.textarea.value;
  if (now !== editor.local) {
    const edit = difference(editor.local, now);
    commit(editor, edit);
    if (!editor.composing && e?.type === "input") {
      record(editor, edit, typing[e.inputType] ? "typing" : "other", editor.selectionBefore, selection(editor.textarea));
    }
  }
  if (!editor.composing) send(editor);
}

// What an input method composed is one step, from the text before it started to the text it left.
function endComposition(editor) {
  const start = editor.compositionStart;
  editor.compositionStart = null;
  if (start && start.text !== editor.local) {
    record(editor, difference(start.text, editor.local), "composition", start.selection, selection(editor.textarea));
  }
}

// Takes an edit already made in the textarea into the module's copy and its colors, and queues it for the component.
function commit(editor, edit) {
  editor.local = editor.textarea.value;
  const sent = { version: ++editor.version, start: edit.start, removed: edit.removed.length, text: edit.inserted };
  editor.pending.push(sent);
  editor.unsent.push(sent);
  shift(editor.spans, sent);
  render(editor);
}

function send(editor) {
  for (const edit of editor.unsent) {
    for (let at = 0; at === 0 || at < edit.text.length; at += part) {
      const text = edit.text.slice(at, at + part), more = at + part < edit.text.length;
      editor.component.invokeMethodAsync("ApplyEdit", edit.version, edit.start + at, at === 0 ? edit.removed : 0, text, more)
        .catch(() => { /* The component is gone, or refused the edit; the next scan, if any, puts the colors right. */ });
    }
  }
  editor.unsent.length = 0;
}

// Moves spans with an edit: those after it move by the change in length, a span the edit falls inside grows or shrinks
// with it, and one the edit cuts through keeps the part outside it until the next scan.
function shift(spans, edit) {
  const delta = edit.text.length - edit.removed, end = edit.start + edit.removed;
  let write = 0;
  for (const span of spans) {
    const spanEnd = span[0] + span[1];
    if (spanEnd <= edit.start) { /* Before the edit. */ }
    else if (span[0] >= end) span[0] += delta;
    else if (span[0] <= edit.start && spanEnd >= end) span[1] += delta;
    else if (span[0] < edit.start) span[1] = edit.start - span[0];
    else { span[1] = spanEnd - end; span[0] = edit.start + edit.text.length; }
    if (span[1] > 0) spans[write++] = span;
  }
  spans.length = write;
}

// Makes an edit from code (an undo or a redo), which the browser's own undo never sees.
function apply(editor, start, end, text) {
  const edit = { start, removed: editor.local.slice(start, end), inserted: text };
  editor.textarea.setRangeText(text, start, end, "end");
  commit(editor, edit);
  send(editor);
}

function record(editor, edit, kind, selectionBefore, selectionAfter) {
  editor.redo.length = 0;
  const last = editor.undo[editor.undo.length - 1], time = Date.now();
  if (kind === "typing" && last?.kind === "typing" && time - last.time < joinWithin && !edit.inserted.includes("\n")) {
    const previous = last.edit;
    if (!edit.removed && !previous.removed && edit.start === previous.start + previous.inserted.length) {
      previous.inserted += edit.inserted;
      last.selectionAfter = selectionAfter;
      last.time = time;
      return;
    }
    if (!edit.inserted && !previous.inserted && edit.start + edit.removed.length === previous.start) {
      previous.start = edit.start;
      previous.removed = edit.removed + previous.removed;
      last.selectionAfter = selectionAfter;
      last.time = time;
      return;
    }
  }
  editor.undo.push({ kind, edit, selectionBefore, selectionAfter, time });
  if (editor.undo.length > historyLimit) editor.undo.shift();
}

function history(editor, which) {
  const textarea = editor.textarea;
  if (textarea.readOnly || editor.composing) return false;
  const step = (which === "undo" ? editor.undo : editor.redo).pop();
  if (!step) return false;
  const edit = step.edit;
  if (which === "undo") {
    apply(editor, edit.start, edit.start + edit.inserted.length, edit.removed);
    textarea.setSelectionRange(step.selectionBefore[0], step.selectionBefore[1]);
    editor.redo.push(step);
  } else {
    apply(editor, edit.start, edit.start + edit.removed.length, edit.inserted);
    textarea.setSelectionRange(step.selectionAfter[0], step.selectionAfter[1]);
    step.time = 0;
    editor.undo.push(step);
  }
  return true;
}

function onBeforeInput(editor, e) {
  if (e.inputType === "historyUndo" || e.inputType === "historyRedo") {
    // Undo and redo from the browser's Edit menu.
    if (e.cancelable) {
      e.preventDefault();
      history(editor, e.inputType === "historyUndo" ? "undo" : "redo");
    }
    return;
  }
  if (!editor.composing) editor.selectionBefore = selection(editor.textarea);
}

function onKeyDown(editor, e) {
  if (e.isComposing || editor.composing || e.altKey) return;
  const command = isMac ? e.metaKey : e.ctrlKey, key = e.key.toLowerCase();
  let which = null;
  if (command && key === "z") which = e.shiftKey ? "redo" : "undo";
  else if (!isMac && e.ctrlKey && !e.shiftKey && key === "y") which = "redo";
  if (which) {
    e.preventDefault();
    history(editor, which);
  }
}

const escapes = { "&": "&amp;", "<": "&lt;", ">": "&gt;" };
const escape = text => text.replace(/[&<>]/g, c => escapes[c]);

// The markup of each line, from the text and the spans. A span that crosses lines is cut at each line break.
function lineMarkup(editor) {
  const text = editor.local, spans = editor.spans, classes = editor.kindClasses, lines = [];
  let line = "", position = 0, index = 0;
  const plain = (from, to) => {
    for (let at = from; at < to;) {
      const lineEnd = text.indexOf("\n", at);
      if (lineEnd < 0 || lineEnd >= to) { line += escape(text.slice(at, to)); return; }
      lines.push(line + escape(text.slice(at, lineEnd)));
      line = "";
      at = lineEnd + 1;
    }
  };
  const styled = (from, to, code) => {
    const style = code >> 8, names = [classes[code & 255] || ""];
    if (style & 1) names.push("pmd-sx-bold");
    if (style & 2) names.push("pmd-sx-italic");
    if (style & 4) names.push("pmd-sx-strike");
    const open = '<span class="' + names.join(" ").trim() + '">';
    for (let at = from; at < to;) {
      const lineEnd = text.indexOf("\n", at), end = lineEnd < 0 || lineEnd >= to ? to : lineEnd;
      if (end > at) line += open + escape(text.slice(at, end)) + "</span>";
      if (end === to) return;
      lines.push(line);
      line = "";
      at = end + 1;
    }
  };
  for (; index < spans.length; index++) {
    const [start, length, code] = spans[index];
    if (start < position) continue;
    if (start >= text.length) break;
    const end = Math.min(start + length, text.length);
    plain(position, start);
    styled(start, end, code);
    position = end;
  }
  plain(position, text.length);
  lines.push(line);
  return lines;
}

// Draws the colored text, replacing only the lines whose markup changed, so typing in a long text stays quick.
function render(editor) {
  const lines = lineMarkup(editor), old = editor.lines, layer = editor.layer;
  let head = 0;
  while (head < lines.length && head < old.length && lines[head] === old[head]) head++;
  let tail = 0;
  while (tail < lines.length - head && tail < old.length - head
    && lines[lines.length - 1 - tail] === old[old.length - 1 - tail]) tail++;

  const removed = old.length - head - tail;
  for (let index = 0; index < removed; index++) layer.children[head].remove();
  const html = [];
  for (let index = head; index < lines.length - tail; index++) html.push("<span>" + lines[index] + "\n</span>");
  if (html.length) {
    const next = layer.children[head];
    next ? next.insertAdjacentHTML("beforebegin", html.join("")) : layer.insertAdjacentHTML("beforeend", html.join(""));
  }
  editor.lines = lines;
  // NOTE: the scroll position is not copied here. Reading the textarea's makes the browser lay out the whole text at
  // once, which took about 40 ms a keystroke in a text of 130,000 characters; the scroll listener copies it instead.
}
