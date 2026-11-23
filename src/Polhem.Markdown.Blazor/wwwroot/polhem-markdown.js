// Loaded by MarkdownViewer the first time a link to a heading is clicked; the host page does not link it.

// Scrolls a heading to the top of the view and moves focus to it, so a screen reader follows. Focus alone does not
// scroll when the heading already has focus, as it does after a second click on the same link.
export function scrollToHeading(heading) {
  heading.scrollIntoView({ block: "start" });
  heading.focus({ preventScroll: true });
}
