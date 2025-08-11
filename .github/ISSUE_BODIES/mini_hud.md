Frameless, always-on-top window with Card style container.

Header:
- "Network Health" title
- Status pill bound to StatusBrush
- Last-updated text (↻ HH:mm:ss)

Body (2x2 tiles):
- Latency p50 with p95 subtext
- Jitter with loss subtext
- DNS median with p90 subtext
- Bufferbloat delta (+ms) with a small Run test button

Footer:
- Link badge (e.g., Ethernet · 1 Gbps or Wi‑Fi details)
- Buttons: [Details], [✕]

Behavior:
- Always-on-top
- Smooth ~150 ms status color crossfade
- Uses shared ProbeViewModel

