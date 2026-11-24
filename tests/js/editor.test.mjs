// Tests of polhem-markdown-editor.js, the browser side of the Blazor MarkdownEditor, run by `node --test tests/js`.
import { test, beforeEach, mock } from "node:test";
import assert from "node:assert/strict";
import { FakeRoot, FakeComponent, FakeResizeObserver, patchBytes } from "./fake-dom.mjs";

// The module reads the platform once, when it loads: a platform other than a Mac uses Ctrl for its shortcuts.
Object.defineProperty(globalThis, "navigator", { value: { platform: "Linux x86_64" }, configurable: true });
globalThis.ResizeObserver = FakeResizeObserver;
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
  assert.equal(component.calls.length, 0);
});

test("text typed before the module loaded is kept and sent as an edit", () => {
  const root = new FakeRoot("abc!"), component = new FakeComponent();

  editor.init(root, component, "abc", kinds);

  assert.deepEqual(component.calls.map(call => call.args), [[1, 3, 0, "!", false]]);
  assert.equal(root.layer.textContent, "abc!\n");
});

test("typing sends each edit and keeps the layer in step", () => {
  const { textarea, layer, component } = start("Hello");

  textarea.type(" world");
  textarea.setSelectionRange(0, 5);
  textarea.type("Hi");

  assert.equal(component.textFrom("Hello"), "Hi world");
  assert.deepEqual(component.calls.map(call => call.args[0]), [1, 2]);
  assert.equal(layer.textContent, "Hi world\n");
});

test("an edit next to an emoji does not split its surrogate pair", () => {
  const { textarea, component } = start("a😀b");

  textarea.value = "a😁b";
  textarea.dispatch("input", { inputType: "insertText" });

  assert.deepEqual(component.calls[0].args.slice(1, 4), [1, 2, "😁"]);
});

test("a large edit goes to the component in parts", () => {
  const { textarea, component } = start("");

  textarea.type("x".repeat(20_000), "insertFromPaste");

  assert.deepEqual(component.calls.map(call => [call.args[1], call.args[3].length, call.args[4]]),
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
  assert.equal(component.calls.length, 0);
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
  assert.equal(component.calls.length, 1);
});

test("scrolling and resizing the textarea move and size the layer", () => {
  const { textarea, layer } = start("a");

  textarea.scrollTop = 120;
  textarea.scrollLeft = 7;
  textarea.dispatch("scroll");
  FakeResizeObserver.last.callback();

  assert.deepEqual([layer.scrollTop, layer.scrollLeft], [120, 7]);
  assert.deepEqual(layer.style, { width: "400px", height: "300px" });
});

test("focus and dispose act on the editor, and calls after dispose do nothing", () => {
  const { root, textarea } = start("a");

  editor.focus(root);
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
