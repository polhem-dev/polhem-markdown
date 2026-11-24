# ADR-001: The Avalonia viewer draws SVG images only from trusted hosts

## Status

**Accepted (2026-11-24)**

Part of showing images ([#12](https://github.com/polhem-dev/polhem-markdown/issues/12), pull request
[#30](https://github.com/polhem-dev/polhem-markdown/pull/30)), which aligns the viewers with how GitHub renders `.md`
files ([#9](https://github.com/polhem-dev/polhem-markdown/issues/9)).

## Context

Images load by default from `https` sources. In Markdown read on GitHub, SVG is common: most badges in a README are SVG
served by a badge service. The text the viewers show often comes from an AI model's reply, which a third party can
steer, so an image's address, and what its server sends back, are untrusted input.

The two viewers draw images in different places:

- **Blazor** writes an `<img>`. The browser draws the SVG in its hardened engine, where an image's scripts do not run
  and it cannot reach the page, and the browser's own limits apply.
- **Avalonia** has no browser. It decodes the bytes in the application's own process. Raster images go through Skia
  after their declared size is checked. SVG needs an SVG renderer, and the one that fits Avalonia 12 is Svg.Skia, used
  at version 5.1.1, the last on SkiaSharp 3, which Avalonia 12 uses.

Two adversarial reviews of the pull request showed what Svg.Skia does with hostile SVG:

| Input | Size | Effect |
|---|---|---|
| `<g>` nested 2500 deep | 17.6 KB | Stack overflow: the process ends. No exception can be caught. |
| 300 `pattern` elements, each filled from the next | 38 KB | Stack overflow, within a limit of 64 levels of nesting |
| `clipPath`, 20 levels, each referring to the next twice | 2.9 KB | 7 s and 1.2 GB |
| `use`, 20 levels, each using the one below twice | 1.2 KB | 27 s and 1.8 GB |
| 150 000 CSS rules, 500 000 dashed path segments | under 10 MB | tens of seconds |
| `<image href>`, `<use>`, `filter`, `@import` to other servers | 1 KB | 32 requests, unless external resources are disabled |

Each fix to a pre-scan of the SVG was followed by a new way around it: references through `url(#…)` instead of
nesting, a `use` reference padded with spaces. The surface is the renderer's whole feature set.

Research into how other tools handle this found:

- Every widely used Markdown viewer that shows SVG hands it to a browser engine: VS Code's preview, GitHub (behind its
  camo proxy), GitLab, JetBrains IDEs, Obsidian and Typora.
- VS Code's extension Marketplace, which renders README files written by third parties, refuses SVG images unless they
  come from a list of trusted badge services (`TrustedSVGSources` in vscode-vsce's `src/package.ts`), "due to security
  concerns", and asks for PNG otherwise.
- The native .NET Markdown viewers for Avalonia and MAUI that draw SVG all use Svg.Skia without limits.
- No SVG renderer usable from .NET bounds processor time or memory. resvg and librsvg limit recursion and references,
  but say they do not limit time or memory either. Svg.Skia merged recursion limits on 2026-11-06 (its issue #568
  reports the same crash), not yet released at the time of writing, and later Svg.Skia versions require SkiaSharp 4.
- Avalonia 12's WebView (MIT) can show HTML, but one native browser view per image is heavy, interferes with scrolling
  and clipping, needs a runtime on Linux, and would be a new dependency for every consumer.

## Decision

### 1. Avalonia draws an SVG only from a trusted host

An SVG image is drawn when its address is `https` and its host is in `MarkdownViewer.TrustedSvgHosts`, or it is a
GitHub workflow badge (`https://github.com/{owner}/{repository}/(actions/)workflows/{file}/badge.svg`). Any other SVG
shows its alternative text, as an image that fails to load does. Raster images are not affected.

The default list is VS Code's `TrustedSVGSources` as it stood on 2026-11-24: `img.shields.io`, `badgen.net`,
`codecov.io`, `coveralls.io`, `dev.azure.com`, `gitlab.com` and the other badge services in it. Those services send SVG
built from their own templates, with only text filled in. A host adds or removes entries; the remarks of
`TrustedSvgHosts` say to add only hosts whose SVG is trusted as much as code.

- The list is the one an established tool already uses for the same reason, which keeps the choice from being ours
  alone and makes it easy to compare when that list changes.
- Badges, which are most SVG in a README, keep working.

### 2. A redirect does not carry trust

With the viewer's own loader, the address an image finally came from, after any redirect, must be trusted too.
Otherwise a trusted host that redirects (`github.com` to `raw.githubusercontent.com`, for one) would pass any file
through. A loader the host supplies (`MarkdownViewer.ImageLoader`) is judged by the address it was asked for: the host
answers for where its loader fetches.

### 3. Defences stay behind the list

The list is the boundary; these limit the damage if a trusted service is compromised or the list is extended carelessly:

- DTD processing is prohibited and external resources are disabled per document, so an SVG reaches nothing outside
  itself.
- A pre-scan refuses SVG past 64 levels of nesting, 2000 elements or 64 `use` elements, or with a `use` of an element
  that holds a `use`.
- Filters are not drawn, and the bitmap is at most 2048 pixels a side.
- Svg.Skia runs on a thread of its own with a 64 MB stack, so recursion that would overflow a thread pool thread does
  not end the process. A test reproduces the 300-pattern chain and fails, with the test process crashing, when the
  drawing runs on a thread pool thread instead.
- The budget for decoded images, the limit of four loads at a time and the 10 MB limit on downloaded bytes apply to
  SVG as to any image.

### 4. The viewers differ on SVG from untrusted hosts

Blazor shows every allowed image, SVG included, through `<img>`; Avalonia shows SVG only from trusted hosts. Both
viewers render the image node; what differs is whether an untrusted SVG's pixels appear or its alternative text does.
The difference is accepted because the browser is a sandbox the Avalonia viewer does not have.

## Consequences

- An SVG logo or diagram from a project's own site or from `raw.githubusercontent.com` shows as its alternative text in
  Avalonia until the host trusts that host. The Blazor viewer shows it.
- `TrustedSvgHosts` is public API. Removing it later, for example once SVG can be drawn safely, is a breaking change;
  emptying its default is not.
- The default list is a copy. When VS Code's list changes, ours does not follow by itself; the date above says which
  version it is.
- Svg.Skia remains a dependency of the Avalonia viewer, for the trusted SVG.
- Revisit when a released Svg.Skia on SkiaSharp 3 carries the recursion limits, or when Avalonia moves to SkiaSharp 4:
  the large-stack thread may then be dropped, but not the list, since no renderer bounds time or memory.

## Alternatives considered

- **Draw any SVG, with a stricter pre-scan.** Rejected: each review found a new way around the scan, and no scan bounds
  time or memory, because the renderer itself does not.
- **Draw no SVG in Avalonia.** Safest and one dependency fewer, but badges, the most common SVG in a README, would show
  as text on the desktop.
- **A browser view per image (Avalonia WebView).** Gives a browser's sandbox, at the cost of a heavy native view per
  image, layout problems in scrolling text, a runtime on Linux and a new dependency.
- **resvg through native bindings.** Better recursion limits, but the existing .NET binding has no iOS or Android build,
  and it does not bound time or memory either.
- **Rewrite badge addresses to PNG** (`raster.shields.io`). Works for one service only, and changes what the author
  wrote.
