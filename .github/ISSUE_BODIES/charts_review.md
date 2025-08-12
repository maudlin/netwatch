Observed issues in sparkline/graph rendering:

1) Ticklines set/placement feels off
- Currently shows 5 horizontal lines: min, ~10th, mid (50%), ~90th, max.
- Proposal: show evenly spaced 0/25/50/75/100% gridlines, with dynamic labeling to actual ms values.
- Ensure tick computation is consistent across Latency/Jitter and Packet Loss views.

2) Gridline rendering artifacts
- The ~10% and ~90% gridlines extend left beyond the y-axis bar.
- Ensure gridlines are clipped within the plot area and align precisely with the y-axis boundary.

3) Packet Loss baseline below zero
- Loss chart y-axis baseline appears at about -0.5% ("packet gain").
- Force zero baseline and clamp to [0, 100]%.

Acceptance criteria
- [ ] Replace current gridlines with evenly spaced 0/25/50/75/100% lines (computed to correct ms/% scale).
- [ ] Gridlines do not protrude left of the y-axis; are clipped to plot bounds.
- [ ] ForceZeroBaseline respected for both latency/jitter and loss charts; no negative baselines.
- [ ] Labels/tooltips (if any) reflect correct dynamic ms/% values.
- [ ] Verified on Windows 10/11, different DPI scales.

Notes
- Likely changes in Controls/SparklineControl: gridline placement, axis range calculation, and clipping.
- Ensure minimal per-tick allocation; re-use pens/geometry and Freeze brushes.

