// Tests of polhem-markdown-editor.js, the browser side of the Blazor MarkdownEditor, run by `node --test tests/js`.
import { test, beforeEach, mock } from "node:test";
import assert from "node:assert/strict";
import { FakeRoot, FakeComponent, FakeResizeObserver, FakeMutationObserver, patchBytes } from "./fake-dom.mjs";

// The module reads the platform once, when it loads: a platform other than a Mac uses Ctrl for its shortcuts.
Object.defineProperty(globalThis, "navigator", { value: { platform: "Linux x86_64" }, configurable: true });
globalThis.ResizeObserver = FakeResizeObserver;
globalThis.MutationObserver = FakeMutationObserver;
// Frames run when a test asks, and the textarea has the editor's padding.
const frames = [];
globalThis.requestAnimationFrame = callback => frames.push(callback);
globalThis.getComputedStyle = () => ({ paddingTop: "8px" });
const runFrames = () => { while (frames.length) frames.shift()(); };
const editor = await import("../../src/Polhem.Markdown.Blazor/wwwroot/polhem-markdown-editor.js");

// Classes by kind, as SyntaxSpanPatch.KindClasses gives them: 1 marker, 2 heading, 3 inline code.
const kinds = ["", "pmd-sx-marker", "pmd-sx-heading", "pmd-sx-code"];
const Marker = 1, Heading = 2, Code = 3, Bold = 1 << 8, Italic = 2 << 8, Strike = 4 << 8;

let now = 0;
beforeEach(() => {
  now = 1_000_000;
  mock.method(Date, "now", () => now);
});

function start(text) {
  const root = new FakeRoot(text), component = new FakeComponent();
  editor.init(root, component, text, kinds);
  return { root, component, textarea: root.textarea, layer: root.layer };
}

function lines(layer) {
  return layer.children.map(line => line.html);
}

test("the layer shows the text line by line, and the editor is marked ready", () => {
  const { root, layer, component } = start("# Title\n\nSome <b> & text");

  assert.equal(layer.textContent, "# Title\n\nSome <b> & text\n");
  assert.deepEqual(lines(layer), ["# Title\n", "\n", "Some &lt;b&gt; &amp; text\n"]);
  assert.ok(root.classes.has("pmd-editor-ready"));
  assert.equal(component.edits.length, 0);
});

test("text typed before the module loaded is kept and sent as an edit", () => {
  const root = new FakeRoot("abc!"), component = new FakeComponent();

  editor.init(root, component, "abc", kinds);

  assert.deepEqual(component.edits.map(call => call.args), [[1, 3, 0, "!", false]]);
  assert.equal(root.layer.textContent, "abc!\n");
});

test("typing sends each edit and keeps the layer in step", () => {
  const { textarea, layer, component } = start("Hello");

  textarea.type(" world");
  textarea.setSelectionRange(0, 5);
  textarea.type("Hi");

  assert.equal(component.textFrom("Hello"), "Hi world");
  assert.deepEqual(component.edits.map(call => call.args[0]), [1, 2]);
  assert.equal(layer.textContent, "Hi world\n");
});

test("an edit next to an emoji does not split its surrogate pair", () => {
  const { textarea, component } = start("a😀b");

  textarea.value = "a😁b";
  textarea.dispatch("input", { inputType: "insertText" });

  assert.deepEqual(component.edits[0].args.slice(1, 4), [1, 2, "😁"]);
});

test("a large edit goes to the component in parts", () => {
  const { textarea, component } = start("");

  textarea.type("x".repeat(20_000), "insertFromPaste");

  assert.deepEqual(component.edits.map(call => [call.args[1], call.args[3].length, call.args[4]]),
    [[0, 8000, true], [8000, 8000, true], [16000, 4000, false]]);
  assert.equal(component.textFrom(""), "x".repeat(20_000));
});

test("spans color the layer, cut at line breaks, with their styles", () => {
  const { root, layer } = start("# Title\n**b** *i* ~~s~~\n```\ncode\n```");

  editor.applySpans(root, 0, patchBytes(0, 0, 0, [
    [0, 1, Marker], [2, 5, Heading],
    [8, 5, Bold], [14, 3, Italic], [18, 5, Strike],
    [24, 12, Code],
  ]));

  assert.deepEqual(lines(layer), [
    '<span class="pmd-sx-marker">#</span> <span class="pmd-sx-heading">Title</span>\n',
    '<span class="pmd-sx-bold">**b**</span> <span class="pmd-sx-italic">*i*</span> <span class="pmd-sx-strike">~~s~~</span>\n',
    '<span class="pmd-sx-code">```</span>\n',
    '<span class="pmd-sx-code">code</span>\n',
    '<span class="pmd-sx-code">```</span>\n',
  ]);
});

test("a patch replaces the spans that changed and moves those after them", () => {
  const { root, layer } = start("`a` b `c`");
  editor.applySpans(root, 0, patchBytes(0, 0, 0, [[0, 3, Code], [6, 3, Code]]));
  root.textarea.value = "`a` bb `c`";
  root.textarea.dispatch("input", { inputType: "insertText" });

  // The scan of the new text: the first span is the same, the last moved by one.
  editor.applySpans(root, 1, patchBytes(1, 0, 1, []));

  assert.equal(layer.children[0].html,
    '<span class="pmd-sx-code">`a`</span> bb <span class="pmd-sx-code">`c`</span>\n');
});

test("spans of an older text move with the edits made since", () => {
  const { root, textarea, layer } = start("x `a`");
  textarea.setSelectionRange(0, 0);
  textarea.type("yy");

  // The scan was of the text before the edit.
  editor.applySpans(root, 0, patchBytes(0, 0, 0, [[2, 3, Code]]));

  assert.equal(layer.children[0].html, 'yyx <span class="pmd-sx-code">`a`</span>\n');
});

test("an edit cutting through a span keeps the part of the span outside it", () => {
  const { root, textarea, layer } = start("ab **cd** ef");
  editor.applySpans(root, 0, patchBytes(0, 0, 0, [[3, 6, Bold]]));

  // The edit takes the start of the span, then one takes its end.
  textarea.setSelectionRange(1, 5);
  textarea.type("");
  assert.equal(layer.children[0].html, 'a<span class="pmd-sx-bold">cd**</span> ef\n');
  textarea.setSelectionRange(3, 8);
  textarea.type("");
  assert.equal(layer.children[0].html, 'a<span class="pmd-sx-bold">cd</span>\n');
});

test("only the lines whose markup changed are replaced", () => {
  const { textarea, layer } = start("one\ntwo\nthree");
  const [first, , third] = layer.children;

  textarea.setSelectionRange(5, 5);
  textarea.type("w\nt");

  assert.equal(layer.textContent, "one\ntw\ntwo\nthree\n");
  assert.equal(layer.children[0], first);
  assert.equal(layer.children[3], third);
});

test("typing in a row is one step to undo, and a pause starts another", () => {
  const { root, textarea } = start("");

  textarea.type("a");
  textarea.type("b");
  now += 2000;
  textarea.type("c");

  assert.ok(editor.undo(root));
  assert.equal(textarea.value, "ab");
  assert.ok(editor.undo(root));
  assert.equal(textarea.value, "");
  assert.equal(editor.undo(root), false);
  assert.ok(editor.redo(root));
  assert.equal(textarea.value, "ab");
  assert.ok(editor.redo(root));
  assert.equal(textarea.value, "abc");
  assert.equal(editor.redo(root), false);
});

test("deletions in a row are one step, apart from the typing before them", () => {
  const { root, textarea, component } = start("");

  textarea.type("abcd");
  textarea.backspace();
  textarea.backspace();

  editor.undo(root);
  assert.equal(textarea.value, "abcd");
  assert.deepEqual([textarea.selectionStart, textarea.selectionEnd], [4, 4]);
  editor.undo(root);
  assert.equal(textarea.value, "");
  assert.equal(component.textFrom(""), "");
});

test("a line break, a paste or a change of place starts a new step", () => {
  const { root, textarea } = start("");

  textarea.type("a");
  textarea.type("\n", "insertLineBreak");
  textarea.type("b");
  textarea.type("PASTE", "insertFromPaste");
  textarea.setSelectionRange(0, 0);
  textarea.type("z");

  const values = [];
  while (editor.undo(root)) values.push(textarea.value);
  assert.deepEqual(values, ["a\nbPASTE", "a\nb", "a\n", "a", ""]);
});

test("Ctrl+Z, Ctrl+Shift+Z and Ctrl+Y undo and redo, and the browser's own undo is held off", () => {
  const { textarea } = start("");
  textarea.type("hi");

  assert.ok(textarea.key("z", { ctrlKey: true }).defaultPrevented);
  assert.equal(textarea.value, "");
  textarea.key("Z", { ctrlKey: true, shiftKey: true });
  assert.equal(textarea.value, "hi");
  textarea.key("z", { ctrlKey: true });
  textarea.key("y", { ctrlKey: true });
  assert.equal(textarea.value, "hi");
  assert.equal(textarea.key("z", { metaKey: true }).defaultPrevented, false);
  assert.equal(textarea.key("z", { ctrlKey: true, altKey: true }).defaultPrevented, false);
});

test("undo and redo from the browser's Edit menu go to the editor's history", () => {
  const { textarea } = start("");
  textarea.type("hi");

  assert.ok(textarea.dispatch("beforeinput", { inputType: "historyUndo" }).defaultPrevented);
  assert.equal(textarea.value, "");
  textarea.dispatch("beforeinput", { inputType: "historyRedo" });
  assert.equal(textarea.value, "hi");
  assert.equal(textarea.dispatch("beforeinput", { inputType: "historyUndo", cancelable: false }).defaultPrevented, false);
  assert.equal(textarea.value, "hi");
});

test("a read-only editor does not undo", () => {
  const { root, textarea } = start("");
  textarea.type("hi");
  textarea.readOnly = true;

  assert.equal(editor.undo(root), false);
  assert.equal(textarea.value, "hi");
});

test("what an input method composed is one step, sent when it ends", () => {
  const { root, textarea, component } = start("x");
  textarea.setSelectionRange(1, 1);

  textarea.dispatch("compositionstart");
  textarea.type("ㄑ");
  textarea.setSelectionRange(1, 2);
  textarea.type("請");
  assert.equal(component.edits.length, 0);
  assert.equal(textarea.key("z", { ctrlKey: true }).defaultPrevented, false);
  textarea.dispatch("compositionend", { data: "請" });

  assert.equal(component.textFrom("x"), "x請");
  assert.ok(editor.undo(root));
  assert.equal(textarea.value, "x");
  assert.ok(editor.redo(root));
  assert.equal(textarea.value, "x請");
});

test("a text the host sets replaces the text and the history", () => {
  const { root, textarea, layer, component } = start("old");
  textarea.type("!");

  editor.setText(root, "new\ntext");
  editor.setText(root, "new\ntext");

  assert.equal(textarea.value, "new\ntext");
  assert.equal(layer.textContent, "new\ntext\n");
  assert.equal(editor.undo(root), false);
  assert.equal(component.edits.length, 1);
});

test("scrolling and resizing the textarea move and size the layer", () => {
  const { textarea, layer } = start("a");

  textarea.scrollTop = 120;
  textarea.scrollLeft = 7;
  textarea.dispatch("scroll");
  FakeResizeObserver.last.callback();

  assert.equal(layer.scrollTop, 120);
  assert.equal(layer.scrollLeft, 0);
  assert.deepEqual(layer.style, { "--pmd-scroll-left": "-7px", width: "calc(100% - 0px)", height: "calc(100% - 0px)" });
});

test("the layer leaves out the textarea's scroll bars", () => {
  const { textarea, layer } = start("a");

  textarea.clientWidth = 385;
  textarea.clientHeight = 290;
  FakeResizeObserver.last.callback();

  assert.equal(layer.style.width, "calc(100% - 15px)");
  assert.equal(layer.style.height, "calc(100% - 10px)");
});

test("the gutter is as wide as the number of the last line, and at least two digits", () => {
  const { root, textarea } = start("a");
  assert.equal(root.style["--pmd-gutter-digits"], "2");

  textarea.type("\n".repeat(99));
  assert.equal(root.style["--pmd-gutter-digits"], "3");

  textarea.setSelectionRange(0, textarea.value.length);
  textarea.type("b");
  assert.equal(root.style["--pmd-gutter-digits"], "2");
});

test("focus and dispose act on the editor, and calls after dispose do nothing", () => {
  const { root, textarea } = start("a");

  editor.focus(root);
  assert.deepEqual(textarea.focusOptions, { preventScroll: true });
  editor.dispose(root);
  editor.dispose(root);

  assert.ok(textarea.focused);
  assert.equal(FakeResizeObserver.last.observing, false);
  assert.equal(editor.undo(root), false);
  assert.equal(editor.redo(root), false);
  editor.setText(root, "b");
  editor.applySpans(root, 0, patchBytes(0, 0, 0, []));
  assert.equal(textarea.value, "a");
});

// The edit the component's formatter would give, as the array the bridge returns: start, length, text, selection.
const edit = (start, length, text, selectionStart, selectionLength) => [start, length, text, selectionStart, selectionLength];

test("a format shortcut asks the component for the edit and applies it as one step", async () => {
  const { root, textarea, component } = start("a word");
  component.answer = (command, version, at, length, argument) =>
    command === "format" && argument === "Bold" ? edit(at, length, "**word**", at + 2, 4) : null;
  textarea.setSelectionRange(2, 6);

  assert.ok(textarea.key("b", { ctrlKey: true }).defaultPrevented);
  await component.release();

  assert.deepEqual(component.commands, [["format", 0, 2, 4, "Bold"]]);
  assert.equal(textarea.value, "a **word**");
  assert.deepEqual([textarea.selectionStart, textarea.selectionEnd], [4, 8]);
  assert.equal(component.textFrom("a word"), "a **word**");
  editor.undo(root);
  assert.equal(textarea.value, "a word");
  assert.deepEqual([textarea.selectionStart, textarea.selectionEnd], [2, 6]);
});

test("each shortcut names its format, and the host's call does the same", async () => {
  const { root, textarea, component } = start("x");

  for (const key of ["i", "k", "e"]) {
    textarea.key(key, { ctrlKey: true });
    await component.release();
  }
  editor.command(root, "Heading2");
  await component.release();

  assert.deepEqual(component.commands.map(args => args[4]), ["Italic", "Link", "InlineCode", "Heading2"]);
  assert.equal(textarea.key("b", { ctrlKey: true, shiftKey: true }).defaultPrevented, false);
});

test("Enter on a list item continues the list, and elsewhere types a line break as usual", async () => {
  const { textarea, component } = start("- one\ntext");
  component.answer = (command, version, at) => command === "continueList" ? edit(at, 0, "\n- ", at + 3, 0) : null;

  textarea.setSelectionRange(5, 5);
  assert.ok(textarea.key("Enter").defaultPrevented);
  await component.release();
  assert.equal(textarea.value, "- one\n- \ntext");

  textarea.setSelectionRange(textarea.value.length, textarea.value.length);
  assert.equal(textarea.key("Enter").defaultPrevented, false);
  assert.equal(textarea.key("Enter", { shiftKey: true }).defaultPrevented, false);
});

test("Enter on a list item the component has no edit for types a line break", async () => {
  const { textarea, component } = start("1. one");
  textarea.setSelectionRange(6, 6);

  textarea.key("Enter");
  await component.release();

  assert.equal(textarea.value, "1. one\n");
});

test("Tab and Shift+Tab move a list item in and out, and Tab elsewhere moves the focus on", async () => {
  const { textarea, component } = start("- a\n- b\nplain");
  component.answer = (command, version, at) => command === "indent" ? edit(at - 3, 0, "  ", at + 2, 0) : edit(at - 5, 2, "", at - 2, 0);

  textarea.setSelectionRange(7, 7);
  textarea.key("Tab");
  await component.release();
  assert.equal(textarea.value, "- a\n  - b\nplain");
  textarea.key("Tab", { shiftKey: true });
  await component.release();
  assert.equal(textarea.value, "- a\n- b\nplain");

  assert.deepEqual(component.commands.map(args => args[0]), ["indent", "outdent"]);
  textarea.setSelectionRange(textarea.value.length, textarea.value.length);
  assert.equal(textarea.key("Tab").defaultPrevented, false);
  textarea.setSelectionRange(2, 9);
  assert.equal(textarea.key("Tab").defaultPrevented, false);
});

test("keys typed while a command waits are held back and come after its edit, in order", async () => {
  const { root, textarea, component } = start("- one");
  component.answer = (command, version, at) => command === "continueList" ? edit(at, 0, "\n- ", at + 3, 0) : null;
  textarea.setSelectionRange(5, 5);

  textarea.key("Enter");
  for (const data of ["a", "b", "c"]) {
    assert.ok(textarea.dispatch("beforeinput", { inputType: "insertText", data }).defaultPrevented);
  }
  textarea.dispatch("beforeinput", { inputType: "deleteContentBackward" });
  textarea.dispatch("beforeinput", { inputType: "insertFromPaste", dataTransfer: { getData: () => "P" } });
  textarea.key("Enter");
  textarea.dispatch("beforeinput", { inputType: "insertText", data: "x" });
  textarea.key("z", { ctrlKey: true });
  await component.release();

  assert.equal(textarea.value, "- one\n- abP\n- ");
  assert.equal(component.textFrom("- one"), textarea.value);
  editor.undo(root);
  assert.equal(textarea.value, "- one\n- abP");
});

test("deletions and line breaks held back come after the command too", async () => {
  const { textarea, component } = start("ab");
  textarea.setSelectionRange(1, 1);

  textarea.key("b", { ctrlKey: true });
  textarea.dispatch("beforeinput", { inputType: "deleteContentBackward" });
  textarea.dispatch("beforeinput", { inputType: "deleteContentForward" });
  textarea.dispatch("beforeinput", { inputType: "insertLineBreak" });
  await component.release();

  assert.equal(textarea.value, "\n");
});

test("an answer for a text the host has replaced since is dropped", async () => {
  const { root, textarea, component } = start("word");
  component.answer = () => edit(0, 4, "**word**", 2, 4);
  textarea.setSelectionRange(0, 4);

  textarea.key("b", { ctrlKey: true });
  textarea.dispatch("beforeinput", { inputType: "insertText", data: "x" });
  editor.setText(root, "other");
  await component.release();

  assert.equal(textarea.value, "other");
});

test("a command whose call fails leaves the text as it was", async () => {
  const { textarea, component } = start("- a");
  textarea.setSelectionRange(3, 3);

  textarea.key("Tab");
  await component.release(true);

  assert.equal(textarea.value, "- a");
  textarea.type("!");
  assert.equal(textarea.value, "- a!");
});

test("a read-only editor takes no command, and lets Enter and Tab be", () => {
  const { root, textarea, component } = start("- a");
  textarea.readOnly = true;
  textarea.setSelectionRange(3, 3);

  assert.equal(textarea.key("b", { ctrlKey: true }).defaultPrevented, false);
  assert.equal(textarea.key("Enter").defaultPrevented, false);
  editor.command(root, "Bold");

  assert.equal(component.commands.length, 0);
});

test("keys and commands during an input method's composition are left to it", () => {
  const { textarea, component } = start("- a");
  textarea.setSelectionRange(3, 3);
  textarea.dispatch("compositionstart");

  assert.equal(textarea.key("Enter").defaultPrevented, false);
  assert.equal(textarea.key("b", { ctrlKey: true, isComposing: true }).defaultPrevented, false);
  assert.equal(component.commands.length, 0);
});

test("a command the host asks for while another waits comes after it", async () => {
  const { root, textarea, component } = start("w");
  component.answer = (command, version, at, length, argument) => edit(0, textarea.value.length, argument + ":" + textarea.value, 0, 0);
  textarea.setSelectionRange(0, 1);

  textarea.key("b", { ctrlKey: true });
  editor.command(root, "Italic");
  await component.release();

  assert.deepEqual(component.commands.map(args => args[4]), ["Bold", "Italic"]);
  assert.equal(textarea.value, "Italic:Bold:w");
});

// Scrolls the textarea, as the user would, and lets a frame pass.
function scrollTo(textarea, top) {
  textarea.scrollTop = top;
  textarea.dispatch("scroll");
  runFrames();
}

test("while the host listens, the offset of the line at the top is reported as the editor scrolls, once per change", () => {
  const root = new FakeRoot("one\ntwo\nthree\nfour"), component = new FakeComponent();
  editor.init(root, component, root.textarea.value, kinds, true);
  const { textarea } = root;
  runFrames();

  scrollTo(textarea, 20);
  scrollTo(textarea, 20);
  scrollTo(textarea, 40);

  const reported = component.calls.filter(call => call.method === "FirstVisibleChanged").map(call => call.args[0]);
  assert.deepEqual(reported, [0, 4, 8]);
});

test("in a wrapped line, the offset is as far into the line as the top is down it", () => {
  const root = new FakeRoot("a\n0123456789\nb"), component = new FakeComponent();
  root.layer.heights[1] = 100;
  editor.init(root, component, root.textarea.value, kinds, true);
  runFrames();

  scrollTo(root.textarea, 20 + 50);

  assert.equal(component.calls.at(-1).args[0], 2 + 5);
});

test("nothing is reported until the host listens, and nothing after it stops", () => {
  const { root, textarea, component } = start("one\ntwo");
  scrollTo(textarea, 20);
  assert.equal(component.calls.length, 0);

  editor.watchScroll(root, true);
  runFrames();
  editor.watchScroll(root, false);
  scrollTo(textarea, 0);

  assert.deepEqual(component.calls.map(call => [call.method, call.args[0]]), [["FirstVisibleChanged", 4]]);
});

test("scrolling to an offset puts its line at the top, moved down as far into a wrapped line as the offset is", () => {
  const { root, textarea, layer } = start("a\n0123456789\nb");
  layer.heights[1] = 100;

  editor.scrollToSource(root, 2);
  assert.equal(textarea.scrollTop, 20);
  editor.scrollToSource(root, 7);
  assert.equal(textarea.scrollTop, 20 + 50);
  editor.scrollToSource(root, 13);
  assert.equal(textarea.scrollTop, 120);
  // Past the end is the end of the last line, its bottom.
  editor.scrollToSource(root, 99);
  assert.equal(textarea.scrollTop, 140);
});

test("the caret goes to an offset, kept within the text", () => {
  const { root, textarea } = start("abc");

  editor.setCaret(root, 2);
  assert.deepEqual([textarea.selectionStart, textarea.selectionEnd], [2, 2]);
  editor.setCaret(root, -4);
  assert.deepEqual([textarea.selectionStart, textarea.selectionEnd], [0, 0]);
});

test("after dispose, scrolling and the caret do nothing, and a pending report is dropped", () => {
  const root = new FakeRoot("a\nb"), component = new FakeComponent();
  editor.init(root, component, "a\nb", kinds, true);

  editor.dispose(root);
  runFrames();
  editor.watchScroll(root, true);
  editor.scrollToSource(root, 2);
  editor.setCaret(root, 1);

  assert.equal(component.calls.length, 0);
  assert.equal(root.textarea.scrollTop, 0);
  assert.equal(root.textarea.selectionStart, 3);
});

function startWithToolbar(text, items) {
  const root = new FakeRoot(text, items), component = new FakeComponent();
  editor.init(root, component, text, kinds);
  return { root, component, textarea: root.textarea };
}

test("a toolbar button keeps the focus and the selection in the text, and applies its format as one step", async () => {
  const { root, textarea, component } = startWithToolbar("a word", ["Bold", "Undo", "Redo"]);
  component.answer = (command, version, at, length, argument) =>
    command === "format" && argument === "Bold" ? edit(at, length, "**word**", at + 2, 4) : null;
  textarea.setSelectionRange(2, 6);

  const { pressPrevented } = root.button("Bold").click();
  await component.release();

  assert.ok(pressPrevented);
  assert.deepEqual(component.commands, [["format", 0, 2, 4, "Bold"]]);
  assert.equal(textarea.value, "a **word**");
  assert.ok(textarea.focused);
  editor.undo(root);
  assert.equal(textarea.value, "a word");
});

test("the heading button and the host's call ask for the heading cycle", async () => {
  const { root, textarea, component } = startWithToolbar("title", ["Heading"]);
  textarea.setSelectionRange(0, 0);

  root.button("Heading").click();
  await component.release();
  editor.cycleHeading(root);
  await component.release();

  assert.deepEqual(component.commands.map(call => call[0]), ["cycleHeading", "cycleHeading"]);
});

test("undo and redo are enabled with a step to take, and take it", () => {
  const { root, textarea } = startWithToolbar("a", ["Undo", "Redo"]);
  const undo = root.button("Undo"), redo = root.button("Redo");
  assert.deepEqual([undo.disabled, redo.disabled], [true, true]);

  textarea.type("b");
  assert.deepEqual([undo.disabled, redo.disabled], [false, true]);

  undo.click();
  assert.equal(textarea.value, "a");
  assert.deepEqual([undo.disabled, redo.disabled], [true, false]);

  redo.click();
  assert.equal(textarea.value, "ab");
  assert.deepEqual([undo.disabled, redo.disabled], [false, true]);
});

test("undo and redo are disabled while the editor is read-only, and a text the host sets clears them", () => {
  const { root, textarea } = startWithToolbar("a", ["Undo", "Redo"]);
  textarea.type("b");

  textarea.readOnly = true;
  FakeMutationObserver.last.callback();
  assert.ok(root.button("Undo").disabled);

  textarea.readOnly = false;
  FakeMutationObserver.last.callback();
  assert.equal(root.button("Undo").disabled, false);

  editor.setText(root, "new");
  assert.ok(root.button("Undo").disabled);
});

test("a disabled button does nothing", () => {
  const { root, component } = startWithToolbar("a", ["Bold", "Undo"]);
  root.button("Bold").disabled = true;

  root.button("Bold").click();
  root.button("Undo").click();

  assert.equal(component.commands.length, 0);
});

test("the arrow keys, Home and End move between the enabled buttons, and Escape goes back to the text", () => {
  const { root, textarea } = startWithToolbar("a", ["Bold", "Italic", "Link", "Undo", "Redo"]);
  const bold = root.button("Bold"), italic = root.button("Italic"), link = root.button("Link");

  assert.ok(bold.key("ArrowRight").defaultPrevented);
  assert.equal(root.focused, italic);
  assert.deepEqual([bold.tabIndex, italic.tabIndex], [-1, 0]);

  // Undo and redo have no step to take, so End stops at the last button that can do something.
  italic.key("End");
  assert.equal(root.focused, link);
  link.key("ArrowRight");
  assert.equal(root.focused, bold);
  bold.key("ArrowLeft");
  assert.equal(root.focused, link);

  link.key("Escape");
  assert.ok(textarea.focused);
});

test("away from a Mac the tooltips keep their Ctrl shortcuts", () => {
  const { root } = startWithToolbar("a", ["Bold"]);

  assert.equal(root.button("Bold").title, "Bold (pc)");
});

test("a press on the gutter keeps the focus in the text", () => {
  const { root, textarea } = start("a");

  const press = root.dispatch("mousedown", { target: root.body });

  assert.ok(press.defaultPrevented);
  assert.ok(textarea.focused);
});
