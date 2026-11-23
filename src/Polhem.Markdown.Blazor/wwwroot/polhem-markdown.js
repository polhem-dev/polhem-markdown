// Loaded by MarkdownViewer the first time a link within the text is clicked; the host page does not link it.

// Scrolls a heading, a footnote or a footnote reference to the top of the view and moves focus to it, so a screen
// reader follows. Focus alone does not scroll when the target already has focus, as after a second click on a link.
export function scrollToAnchor(target) {
  target.scrollIntoView({ block: "start" });
  target.focus({ preventScroll: true });
}
