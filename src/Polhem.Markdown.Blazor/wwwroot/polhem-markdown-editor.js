// The browser side of Polhem.Markdown.Blazor's MarkdownEditor, which loads it. A native textarea takes the input; its
// text is transparent, and this module draws the same text with colors on the layer behind it, from the spans the
// component's scan found. The textarea belongs to the user: the module reads each edit from it and sends the change to
// the component, and only writes to it for a command (a format, continuing a list, indenting), an undo, a redo, or a
// text the host sets.

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

// Native input held back while a command waits for its answer, and typed after it.
const replayable = { insertText: 1, insertLineBreak: 1, insertParagraph: 1, deleteContentBackward: 1, deleteContentForward: 1, insertFromPaste: 1 };

// The format shortcuts, with Ctrl, or Command on a Mac. The names are those of MarkdownFormat.
const shortcuts = { b: "Bold", i: "Italic", k: "Link", e: "InlineCode" };

// The start of a line that is a list item, up to the caret: a bullet or a number, then a space.
const listItem = /^\s*(?:[-*+]|\d+[.)])\s/;

export function init(root, component, text, kindClasses, reportScroll = false) {
  const textarea = root.querySelector("textarea"), layer = root.querySelector("pre");
  const editor = {
    root, textarea, layer, component, kindClasses,
    local: text, version: 0, pending: [], unsent: [], base: [], spans: [], lines: [],
    composing: false, compositionStart: null, selectionBefore: [0, 0], undo: [], redo: [],
    busy: false, replaying: false, queue: [], generation: 0,
    reportScroll, frame: 0, firstVisible: -1,
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
    scheduleReport(editor);
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
  scheduleReport(editor);
}

export function dispose(root) {
  const editor = editors.get(root);
  if (!editor) return;
  editor.resize.disconnect();
  editors.delete(root);
}

// Whether the module tells the component which text is at the top as the editor scrolls.
export function watchScroll(root, report) {
  const editor = editors.get(root);
  if (!editor) return;
  editor.reportScroll = report;
  editor.firstVisible = -1;
  scheduleReport(editor);
}

// Scrolls the editor so the line holding `offset` is at its top, moved down as far into a wrapped line as the offset is.
export function scrollToSource(root, offset) {
  const editor = editors.get(root);
  if (!editor) return;
  const text = editor.local, at = Math.min(Math.max(offset, 0), text.length);
  const index = count(text, "\n", at), line = editor.layer.children[index];
  if (!line) return;
  const lineStart = text.lastIndexOf("\n", at - 1) + 1, lineEnd = text.indexOf("\n", at);
  const lineLength = (lineEnd < 0 ? text.length : lineEnd) - lineStart;
  const { top, height } = lineBox(editor, line);
  const fraction = lineLength ? (at - lineStart) / lineLength : 0;
  editor.textarea.scrollTop = top + fraction * height - paddingTop(editor);
}

// Moves the focus to the textarea without scrolling it, so the focus does not undo a scroll to the source.
export function focus(root) {
  editors.get(root)?.textarea.focus({ preventScroll: true });
}

// Puts the caret at `offset`, with nothing selected.
export function setCaret(root, offset) {
  const editor = editors.get(root);
  if (!editor) return;
  const at = Math.min(Math.max(offset, 0), editor.local.length);
  editor.textarea.setSelectionRange(at, at);
}

// Replaces the text with one the host set, which also clears the undo history.
export function setText(root, text) {
  const editor = editors.get(root);
  if (!editor || editor.local === text) return;
  editor.textarea.value = text;
  editor.local = text;
  editor.generation++;
  editor.queue.length = 0;
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

// Applies a format to the selection, as its shortcut does. `format` is a name of MarkdownFormat.
export function command(root, format) {
  const editor = editors.get(root);
  if (editor) act(editor, { command: "format", argument: format });
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

function count(text, character, end) {
  let found = 0;
  for (let at = text.indexOf(character); at >= 0 && at < end; at = text.indexOf(character, at + 1)) found++;
  return found;
}

// The top and height of a line of the layer, in the coordinates the textarea scrolls in.
function lineBox(editor, line) {
  const box = line.getBoundingClientRect(), layer = editor.layer.getBoundingClientRect();
  return { top: box.top - layer.top + editor.layer.scrollTop, height: box.height };
}

function paddingTop(editor) {
  return parseFloat(getComputedStyle(editor.textarea).paddingTop) || 0;
}

// The offset of the text at the top of the editor: the line there, as far into it as the top is down a wrapped line.
function firstVisible(editor) {
  const lines = editor.layer.children, top = editor.textarea.scrollTop + paddingTop(editor);
  let low = 0, high = lines.length - 1;
  while (low < high) {
    const middle = (low + high + 1) >> 1;
    if (lineBox(editor, lines[middle]).top <= top) low = middle;
    else high = middle - 1;
  }
  const text = editor.local;
  let lineStart = 0;
  for (let index = 0; index < low; index++) lineStart = text.indexOf("\n", lineStart) + 1;
  const lineEnd = text.indexOf("\n", lineStart), length = (lineEnd < 0 ? text.length : lineEnd) - lineStart;
  const box = lineBox(editor, lines[low]);
  const fraction = box.height > 0 ? Math.min(Math.max((top - box.top) / box.height, 0), 1) : 0;
  return lineStart + Math.round(fraction * length);
}

function scheduleReport(editor) {
  if (!editor.reportScroll || editor.frame) return;
  editor.frame = requestAnimationFrame(() => {
    editor.frame = 0;
    if (!editors.has(editor.root)) return;
    const offset = firstVisible(editor);
    if (offset === editor.firstVisible) return;
    editor.firstVisible = offset;
    editor.component.invokeMethodAsync("FirstVisibleChanged", offset).catch(() => { /* The component is gone. */ });
  });
}

function selection(textarea) {
  return [textarea.selectionStart, textarea.selectionEnd];
}

function difference(before, after) {
  const max = Math.min(before.length, after.length);
  // Compared by code point, so an edit next to an emoji does not start between the two halves of its surrogate pair.
  let head = 0;
  while (head < max && before.codePointAt(head) === after.codePointAt(head)) head++;
  let tail = 0;
  while (tail < max - head && before.codePointAt(before.length - 1 - tail) === after.codePointAt(after.length - 1 - tail)) tail++;
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

// Makes an edit from code (a command, a replayed key, an undo or a redo), which the browser's own undo never sees. With
// a `kind`, the edit is recorded as a step to undo; with a `selection`, that is what is selected after it.
function apply(editor, start, end, text, kind = null, selection = null) {
  const textarea = editor.textarea, before = [textarea.selectionStart, textarea.selectionEnd];
  const edit = { start, removed: editor.local.slice(start, end), inserted: text };
  textarea.setRangeText(text, start, end, "end");
  commit(editor, edit);
  send(editor);
  if (selection) textarea.setSelectionRange(selection[0], selection[1]);
  if (kind) record(editor, edit, kind, before, [textarea.selectionStart, textarea.selectionEnd]);
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
  if (editor.busy) {
    editor.queue.push({ history: which });
    return true;
  }
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
  if (editor.busy && !editor.replaying && e.cancelable && !e.isComposing && replayable[e.inputType]) {
    // NOTE: typing on while a command waits would put the keys before the command's edit on a slow connection, so
    // they are held back and replayed after it.
    e.preventDefault();
    const data = e.inputType === "insertFromPaste" ? e.dataTransfer?.getData("text/plain") : e.data;
    editor.queue.push({ input: e.inputType, data });
    return;
  }
  if (!editor.composing) editor.selectionBefore = selection(editor.textarea);
}

// What a key does in the editor, or null for what the browser does with it.
function keyAction(e) {
  const command = isMac ? e.metaKey : e.ctrlKey, key = e.key.toLowerCase();
  if (command && key === "z") return { history: e.shiftKey ? "redo" : "undo" };
  if (!isMac && e.ctrlKey && !e.shiftKey && key === "y") return { history: "redo" };
  if (command && !e.shiftKey && shortcuts[key]) return { command: "format", argument: shortcuts[key] };
  if (e.ctrlKey || e.metaKey) return null;
  if (e.key === "Enter" && !e.shiftKey) return { key: "Enter" };
  if (e.key === "Tab") return { key: "Tab", outdent: e.shiftKey };
  return null;
}

// Whether the caret is on a list item, past its marker, with nothing selected across lines.
function onListItem(editor) {
  const { selectionStart: start, selectionEnd: end } = editor.textarea, text = editor.local;
  const lineStart = text.lastIndexOf("\n", start - 1) + 1;
  return !text.slice(start, end).includes("\n") && listItem.test(text.slice(lineStart, start));
}

function onKeyDown(editor, e) {
  if (e.isComposing || editor.composing || e.altKey) return;
  const action = keyAction(e);
  if (!action) return;
  if (action.history) {
    e.preventDefault();
    history(editor, action.history);
  } else if (editor.textarea.readOnly) {
    // A read-only editor takes no command; Enter and Tab do what they do anywhere.
  } else if (editor.busy) {
    e.preventDefault();
    editor.queue.push(action);
  } else if (action.command || onListItem(editor)) {
    // NOTE: Tab is taken only on a list item. Elsewhere it moves the focus on, so a keyboard user can leave the editor.
    e.preventDefault();
    act(editor, action);
  }
}

// Carries out a key or a command: Enter and Tab become commands on a list item, and type as usual elsewhere.
function act(editor, action) {
  if (editor.textarea.readOnly) return;
  if (editor.busy) {
    editor.queue.push(action);
    return;
  }
  const { selectionStart: start, selectionEnd: end } = editor.textarea;
  if (action.command) run(editor, action.command, action.argument, null);
  else if (action.key === "Enter") onListItem(editor) ? run(editor, "continueList", null, "\n") : apply(editor, start, end, "\n", "other");
  else if (action.key === "Tab" && onListItem(editor)) run(editor, action.outdent ? "outdent" : "indent", null, null);
}

// Asks the component for the edit of a command and applies it as one step to undo; `fallback` is typed when there is
// no edit. Until the answer comes, what the user types is queued.
function run(editor, command, argument, fallback) {
  const textarea = editor.textarea, start = textarea.selectionStart, length = textarea.selectionEnd - start;
  const version = editor.version, generation = editor.generation;
  editor.busy = true;
  const done = answer => {
    editor.busy = false;
    editor.replaying = true;
    try {
      // An answer for a text the host has since replaced is dropped, with the keys queued for it.
      if (generation === editor.generation && version === editor.version) {
        if (answer) {
          const [at, removed, text, selectionStart, selectionLength] = answer;
          apply(editor, at, at + removed, text, "command", [selectionStart, selectionStart + selectionLength]);
        } else if (fallback) {
          apply(editor, textarea.selectionStart, textarea.selectionEnd, fallback, "other");
        }
      }
      replay(editor);
    } finally {
      editor.replaying = false;
    }
  };
  editor.component.invokeMethodAsync("Command", command, version, start, length, argument).then(done, () => done(null));
}

function replay(editor) {
  while (editor.queue.length && !editor.busy) {
    const item = editor.queue.shift(), { selectionStart: start, selectionEnd: end } = editor.textarea;
    if (item.history) history(editor, item.history);
    else if (item.command || item.key) act(editor, item);
    else if (item.input === "deleteContentBackward") {
      if (start < end) apply(editor, start, end, "", "typing");
      else if (start > 0) apply(editor, start - 1, start, "", "typing");
    } else if (item.input === "deleteContentForward") {
      apply(editor, start, start < end ? end : Math.min(end + 1, editor.local.length), "", "typing");
    } else if (item.input === "insertLineBreak" || item.input === "insertParagraph") {
      apply(editor, start, end, "\n", "other");
    } else if (item.data) {
      apply(editor, start, end, item.data, item.input === "insertFromPaste" ? "other" : "typing");
    }
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
