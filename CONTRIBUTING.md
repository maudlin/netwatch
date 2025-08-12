# Contributing to Netwatch

Thanks for your interest in contributing! This guide explains how we work and what we expect in pull requests.


## Project goals (quick recap)
- Lightweight Windows network HUD (WPF, .NET 8) with minimal CPU/RAM and calm UI
- Simple probe loop (1 Hz) with staggered pings and infrequent DNS timing
- Clear, deterministic status heuristic and actionable visuals


## Getting started
Prerequisites:
- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 (recommended) or VS Code with C# Dev Kit

Build and run:
- dotnet build
- dotnet run
- Or open networkhud.sln in Visual Studio and F5


## Issues, project, and milestones
- Issues are the source of truth for work items.
- The Netwatch Project board organizes issues (Status: Todo/In Progress/Done).
- Milestones (v0.1, v0.2, …) group releases.
- Labels indicate Area (UI, Probes, Stats, Packaging) and Priority (P1, P2).

When filing an issue:
- Provide a clear title and description.
- For features: acceptance criteria and any mockups.
- For bugs: repro steps, expected vs actual, logs/snapshots if possible.


## Branching and pull requests
- Create a feature branch from main: feature/<short-desc> or fix/<short-desc>
- Keep changes focused; small PRs are easier to review and ship.
- Link the issue in the PR description using “Closes #<issue>”.
- Use the PR checklist (pre-filled from the template) and check relevant items.
- Set the appropriate labels (Area, Priority) and milestone.

Suggested commit message convention:
- type(scope): short description
- Examples: feat(ui): tray toggle for Mini HUD; fix(probes): handle DNS timeout


## Code style and structure
- C# 12 / .NET 8
- Nullable enabled, implicit usings enabled (see Netwatch.csproj)
- WPF (XAML) for UI, MVVM-lite with simple INotifyPropertyChanged
- Prefer preformatted strings in the ViewModel to avoid heavy converters
- Keep UI resource tokens in Themes/Theme.xaml (color, spacing, typography)

Naming and layout:
- Controls/: lightweight custom controls (e.g., SparklineControl)
- Models/: data structures and streaming estimators (P², Welford, RingCounter)
- Services/: ProbeService, commands, utilities
- Windows: MiniHudWindow, ExpandedWindow


## Performance and networking guidelines
- Probe loop: PeriodicTimer at 1 Hz; stagger operations (0/333/666 ms)
- Timeouts on all external ops; no nested retries
- Streaming stats only; avoid per-tick allocations
- Backoff when offline; light mode when hidden/on battery (lower cadence)
- Keep charts lightweight (DrawingVisual-style renderers)

Bufferbloat test:
- Short (5–10 s) on-demand; increase ping cadence during load
- Configurable endpoint; consider bandwidth caps and cancellation UX


## Testing and validation
- Manual validation steps in PR description (include screenshots/GIFs for UI)
- Verify acceptance criteria in the linked issue
- Accessibility: contrast ≥ 4.5:1, visible focus, DPI 100/125/150%
- Performance: idle CPU < 1%, steady memory; no hot-path allocations


## Security and privacy
- Do not commit secrets; keep local config out of Git (.gitignore)
- No background uploads by default; bloat test runs only on explicit action


## Releases and packaging
- dotnet publish -p:PublishSingleFile=true -r win-x64 for artifacts
- CI workflow (GitHub Actions) attaches artifacts to tagged releases
- Optional: winget/MSIX in later milestones


## Development tips
- Use PerfView to inspect allocations on the probe loop and bindings
- Freeze WPF brushes/pens used frequently
- Debounce NIC change handling and rediscover targets safely


## Questions
Open a discussion or issue if you’re unsure about approach or scope. Thanks for helping improve Netwatch!
