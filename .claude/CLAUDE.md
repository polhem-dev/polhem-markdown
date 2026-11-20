# Polhem.Markdown — guidance for coding agents

## Language

- Everything maintained together is written in **English**: source code, XML documentation, comments, test method
  names and their `DisplayName` text, commit messages, and the files under `.claude/`.
- This overrides any personal or user-level setting that asks for another language for prose. Replies in a
  conversation may still follow the user's language.
- User documents are bilingual (`name.md` and `name.zh-TW.md`); change both files together.

## Project overview

Markdown viewers for .NET that render the same text the same way on different front ends:

- `Polhem.Markdown` parses with Markdig and turns the syntax tree into a small model of its own, applying one safety
  policy (raw HTML shown as text, link schemes checked).
- `Polhem.Markdown.Avalonia` and `Polhem.Markdown.Blazor` render that model.

The design decisions are recorded in polhem-dev/polhem#529.

- **Version**: `src/Directory.Build.props`, the only place it is declared. The three packages always ship together
  under that version.
- **Dependencies**: each package takes only its own external package (Markdig, Avalonia, the Blazor component
  library). `src/Directory.Build.targets` (MARKDOWN9001) and `DependencyGateTests` enforce it. Do not add a package
  reference without asking.
- **No Markdig type in the public API.** `PublicApiGateTests` fails when a public member refers to the `Markdig`
  namespace. The model is `internal`, exposed to the viewers through `InternalsVisibleTo`.
- **Both viewers render every node of the model.** A node one viewer does not handle is a difference between the
  front ends, which is what these packages exist to prevent.
- **Avalonia version**: the viewer references the lowest Avalonia version it supports, kept on the line Semi.Avalonia
  supports, because that version becomes the minimum for every consumer.
- **Downstream**: the Polhem framework (`polhem-dev/polhem`) uses the viewers in DefineEditor and maps the Blazor
  viewer's `--pmd-*` CSS variables to its MudBlazor and Fluent UI themes. Renaming a variable or changing what the
  viewers expose changes what it sees; say so in the pull request.

## Build and test

```bash
dotnet build Polhem.Markdown.slnx --configuration Release
dotnet test Polhem.Markdown.slnx --configuration Release --no-build
./check-md-links.sh
```

## Workflow

- How changes reach `main`, and who merges them, is the polhem-dev organization's contributing guide,
  <https://github.com/polhem-dev/.github/blob/main/CONTRIBUTING.md>: contributors work from a fork and open a pull
  request, and only the maintainer merges. `main` is protected, so nothing is pushed to it directly.
- Every change reaches `main` through a pull request. Agents name their branches `claude/<topic>`.
- Build and test locally before pushing when the environment allows it.
- **Never push a `v*` tag without the user's explicit consent.** Once publishing is set up it publishes the packages
  to nuget.org, and a published package cannot be withdrawn.
- Never change a test or the source code just to make a check pass.

## Local working documents

- `local/` at the repository root is ignored by git. Keep plans, drafts and personal notes there.
- Never commit anything under `local/`, never add it with `git add -f`, and never link to it or name a file in it from a
  committed file.
- Work other maintainers need to see belongs in a GitHub issue or pull request.
