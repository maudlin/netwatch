# Summary
- What does this PR change and why?

# Linked issue(s)
- Closes #<issue-number>
- Related: #

# Changes
- [ ] User-visible changes described in README/CHANGELOG if needed
- [ ] Screenshots/GIFs for UI changes (Mini HUD / Expanded)

# Acceptance checklist
- [ ] Functional: matches issue acceptance criteria
- [ ] Tests: unit or manual steps documented (include steps to validate)
- [ ] Accessibility: contrast ≥ 4.5:1, focus visuals present, DPI 100/125/150% OK
- [ ] Performance: no hot-path allocations; CPU < 1% idle; memory steady
- [ ] Settings: new settings have sane defaults; persisted; migration handled
- [ ] Error handling: exceptions are categorized and de-duplicated
- [ ] Security/Privacy: no secrets committed; no unsolicited network activity

# Netwatch-specific checks
- [ ] Probe loop cadence and staggering preserved (1 Hz overall; 0/333/666 ms)
- [ ] Ping timeouts/loss handling correct (900 ms; loss accounted)
- [ ] DNS timing with cache-busted lookup (12 s; 1.5 s cap)
- [ ] Streaming stats (P² p50/p95, Welford jitter, windowed loss) update correctly
- [ ] Status heuristic thresholds respected; reason text concise
- [ ] Link badge updates (every 5 s; NIC type/speed) and on NIC changes
- [ ] Bufferbloat test UX: start/cancel states; delta computed; no lingering tasks

# Project hygiene
- [ ] Issue(s) labeled appropriately (UI/Probes/Stats/Packaging, P1/P2)
- [ ] Project item moved to In Progress/Done as appropriate
- [ ] Milestone set (v0.1 for P1 items)

# Manual validation steps
1. 
2. 
3. 

