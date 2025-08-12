This PR fixes a binding typo and stabilizes chart rendering, plus improved bufferbloat UI state.

Changes
- XAML: Correct IsBloatTestRunning DataTrigger binding (extra brace) so the test button disables and shows "Testing…" correctly.
- Charts: Keep a single left Y axis (Latency) and hide Jitter axis labels to prevent ghosting; maintain evenly spaced gridlines clipped to plot.
- Axis: Implement sticky auto-ranging max values with hysteresis and caps for Latency, Jitter, and Loss; expose as bindable properties and bind in ExpandedWindow.
- ProbeService: Fully qualify System.Windows.Media Brush/Color types to avoid System.Drawing ambiguity.
- Build: Verified Release build succeeds.

UX
- Bufferbloat card shows a status pill (TESTING/WARMING UP/GOOD/MODERATE/POOR) and disables the button while running.

Notes
- This targets the fix/charts-alignment-debounce branch.
- After merge, we can cut a v0.1 prerelease.

