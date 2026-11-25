// Just enough of a page for polhem-markdown-editor.js to run under Node's test runner: a textarea, the layer behind it,
// and the element that holds both. The module's behaviour in real browsers is checked by hand and by the scripts the
// pull requests describe; these fakes check its logic.

export class FakeTextarea {
  constructor(value) {
    this.value = value;
    this.selectionStart = value.length;
    this.selectionEnd = value.length;
    this.readOnly = false;
    this.scrollTop = 0;
    this.scrollLeft = 0;
    this.clientWidth = 400;
    this.clientHeight = 300;
    this.focused = false;
    this.listeners = {};
  }

  addEventListener(type, handler) {
    (this.listeners[type] ||= []).push(handler);
  }

  dispatch(type, properties = {}) {
    const event = { type, cancelable: true, defaultPrevented: false, ...properties };
    event.preventDefault = () => { event.defaultPrevented = true; };
    for (const handler of this.listeners[type] || []) handler(event);
    return event;
  }

  setSelectionRange(start, end) {
    this.selectionStart = start;
    this.selectionEnd = end;
  }

  setRangeText(text, start, end) {
    this.value = this.value.slice(0, start) + text + this.value.slice(end);
    this.selectionStart = this.selectionEnd = start + text.length;
  }

  focus(options) {
    this.focused = true;
    this.focusOptions = options;
  }

  // What the browser does when the user types `text` over the selection: the value changes, then `input` fires.
  type(text, inputType = "insertText") {
    this.dispatch("beforeinput", { inputType, data: text });
    this.setRangeText(text, this.selectionStart, this.selectionEnd);
    this.dispatch("input", { inputType, data: text });
  }

  // Backspace with nothing selected.
  backspace() {
    this.dispatch("beforeinput", { inputType: "deleteContentBackward" });
    const at = this.selectionStart;
    this.setRangeText("", at - 1, at);
    this.dispatch("input", { inputType: "deleteContentBackward" });
  }

  key(key, modifiers = {}) {
    return this.dispatch("keydown", { key, ctrlKey: false, metaKey: false, shiftKey: false, altKey: false, isComposing: false, ...modifiers });
  }
}

class FakeLine {
  constructor(parent, html) {
    this.parent = parent;
    this.html = html;
  }

  get textContent() {
    return unescape(this.html.replace(/<[^>]+>/g, ""));
  }

  // Lines are 20 high unless the layer says otherwise, as a wrapped line is, start below the layer's padding of 8, and
  // move up as the layer scrolls.
  getBoundingClientRect() {
    const lines = this.parent.children, index = lines.indexOf(this);
    let top = 8;
    for (let at = 0; at < index; at++) top += this.parent.heightOf(at);
    top -= this.parent.scrollTop;
    return { top, height: this.parent.heightOf(index), bottom: top + this.parent.heightOf(index) };
  }

  remove() {
    this.parent.children.splice(this.parent.children.indexOf(this), 1);
  }

  insertAdjacentHTML(position, html) {
    if (position !== "beforebegin") throw new Error("Not faked: " + position);
    this.parent.children.splice(this.parent.children.indexOf(this), 0, ...this.parent.parse(html));
  }
}

export class FakeLayer {
  constructor() {
    this.children = [];
    this.style = {};
    this.scrollTop = 0;
    this.scrollLeft = 0;
    this.heights = {};
  }

  heightOf(index) {
    return this.heights[index] ?? 20;
  }

  getBoundingClientRect() {
    return { top: 0, height: 300, bottom: 300 };
  }

  // The module writes each line as `<span>…\n</span>`, and a line holds no other line break.
  parse(html) {
    return html.split("\n</span>").slice(0, -1).map(line => new FakeLine(this, line.replace(/^<span>/, "") + "\n"));
  }

  insertAdjacentHTML(position, html) {
    if (position !== "beforeend") throw new Error("Not faked: " + position);
    this.children.push(...this.parse(html));
  }

  get textContent() {
    return this.children.map(line => line.textContent).join("");
  }
}

export class FakeRoot {
  constructor(text) {
    this.textarea = new FakeTextarea(text);
    this.layer = new FakeLayer();
    this.classes = new Set();
    this.classList = { add: name => this.classes.add(name) };
  }

  querySelector(selector) {
    return selector === "textarea" ? this.textarea : this.layer;
  }
}

// The component's side: records what it is sent, and answers commands with `answer`, which a test sets. The answer is
// held until the test calls `release`, as a slow connection would hold it.
export class FakeComponent {
  constructor() {
    this.calls = [];
    this.answer = () => null;
    this.held = [];
  }

  invokeMethodAsync(method, ...args) {
    this.calls.push({ method, args });
    if (method !== "Command") return Promise.resolve();
    return new Promise((resolve, reject) => this.held.push({ args, resolve, reject }));
  }

  // Answers the commands asked so far; `fail` makes their calls fail instead.
  async release(fail = false) {
    while (this.held.length) {
      const { args, resolve, reject } = this.held.shift();
      fail ? reject(new Error("The circuit is gone.")) : resolve(this.answer(...args));
      await new Promise(done => setTimeout(done, 0));
    }
  }

  get edits() {
    return this.calls.filter(call => call.method === "ApplyEdit");
  }

  get commands() {
    return this.calls.filter(call => call.method === "Command").map(call => call.args);
  }

  // The text the component would hold after the edits sent so far.
  textFrom(initial) {
    let text = initial;
    for (const { args: [, start, removed, inserted] } of this.edits) text = text.slice(0, start) + inserted + text.slice(start + removed);
    return text;
  }
}

export class FakeResizeObserver {
  static last = null;

  constructor(callback) {
    this.callback = callback;
    this.observing = false;
    FakeResizeObserver.last = this;
  }

  observe() {
    this.observing = true;
  }

  disconnect() {
    this.observing = false;
  }
}

function unescape(text) {
  return text.replace(/&lt;/g, "<").replace(/&gt;/g, ">").replace(/&amp;/g, "&");
}

// A patch in the format of SyntaxSpanPatch.cs, as the bytes the component sends.
export function patchBytes(at, removed, delta, spans) {
  const numbers = new Int32Array([at, removed, delta, ...spans.flat()]);
  return new Uint8Array(numbers.buffer);
}
