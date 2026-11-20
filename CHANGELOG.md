# Changelog

**English** | [繁體中文](CHANGELOG.zh-TW.md)

Notable changes to the Polhem.Markdown packages. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- `Polhem.Markdown` parses Markdown with Markdig into the model the viewers render: headings, paragraphs, emphasis,
  inline code, links, lists, code blocks, quotes, tables and rules. Raw HTML shows as text, images as their alternative
  text, and only links with an allowed scheme stay clickable (`MarkdownOptions.AllowedLinkSchemes`, by default `http`,
  `https` and `mailto`).
