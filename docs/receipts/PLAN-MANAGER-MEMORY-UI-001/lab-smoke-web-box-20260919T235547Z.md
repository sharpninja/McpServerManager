# Lab smoke — Mcp-Web Memory (box reseed)

- UTC: 2026-09-19T23:55:47Z
- Manager HEAD: `e1636b34545c0b3af428f59df202a18ee23ec3e5`
- McpServer: {"status":"Healthy","version":"1.0.0+720c2b49bede41449eda1ef8edab25296f7592e6"}
- Web: http://127.0.0.1:39984

## Five verbs
| Verb | Result |
|------|--------|
| LIST | PASS (seeded MEMORY-FACT-001 visible) |
| ADD | PASS (BOX-WEB-SMOKE-RESEED-20260919) |
| GET | PASS |
| UPDATE | PASS (Version 1→2 display-only) |
| REMOVE | PASS |
| NAV | Memory immediately after Todos |

## Overall
**PASS**
