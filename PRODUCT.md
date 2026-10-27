# Product

<!-- impeccable:product-schema 1 -->

## Platform

android

## Users

Exactly two roles, one Android app, one install — the role comes from login, not from a separate build or a chosen line *(docs/01_PRD.md §2.3, §2.4)*.

- **Coordinador** — one per plant, scope: all 10 production lines. Plans the next day's shift (which lines run, with what SKU), assigns a supervisor to each active line, triggers shift start, can intervene on any line, manages the entire master roster (~160 workers), catalogs, and parameters — from a phone, moving through the plant with no guaranteed desk. There is no backup web console; every piece of master data has to be thumb-usable. Critical moment: the previous day's planning and the shift-start trigger — a mistake there means the whole shift starts wrong.
- **Supervisor** — one per active line, scope: exclusively that line, with total, deliberate isolation from every other line's people and posts. Operates standing, one-handed, walking, under variable industrial lighting. Fills rotating posts, watches fatigue, requests/receives relief, logs stoppages and waste, closes the shift. Critical moment: the shift-start window, when their group has to be placed before it closes.
- **Supervisor of L8 ("the Bolsón" — the floating labor pool)** — same account type as Supervisor, not a third role, but a different home screen (available-personnel list + pending relief-request queue + pending arrivals, not a post grid) and a unique capability: accepting or rejecting relief candidates proposed for other lines' posts. Of another line's post they see only the post, never the person.

## Product Purpose

SmartAssign exists to manage personnel movement across production lines in a way that's controlled and verifiable: keeping track of where every worker physically is, rotating people through posts so nobody stays too long on the same repetitive task, running the relief (hand-off) process end to end, and assigning people to posts under rules that apply themselves. It replaces a verbal/paper process that produced five concrete, named losses *(docs/01_PRD.md §1.1)*: nobody reliably knows where people are; people get put on posts their medical condition forbids; physical wear from repetition goes unmanaged; idle time during line stoppages goes unrecovered; and nothing is measurable until the day is already over.

Success is a full shift run end to end inside the app, with no parallel paper record, zero medical-restriction violations on any path, zero people in an unknown location at shift close, and a closing report the system generates without manual work *(docs/01_PRD.md §3.1, RG-1–RG-5)*.

## Positioning

The mechanism a neighboring tool couldn't casually copy: occupational-safety and fatigue rules (medical restrictions, category compatibility, per-post recovery windows, fatigue thresholds) are enforced automatically, on the server, at the exact instant of the decision, across every path that can produce an assignment — not as a policy someone is trusted to remember.

The principle that governs all of it: **the system never lies about what's happening** *(docs/01_PRD.md §1.3)*. If something can't be done, it says why and what unblocks it, in plant language. Concretely: rejection is a designed feature, not an error case — every user story has its own rejection scenario; "loading" and "empty" never look the same; no live indicator appears without a freshness stamp.

What it deliberately is not: it does not move people on its own — it flags and suggests, a human decides and executes; it is not a time-and-attendance system (no clock-in integration); it is not a MES (it records what the supervisor observes, not what a machine measures); it is not a medical record (it stores which physical capabilities are forbidden, not diagnoses) *(docs/01_PRD.md §1.4)*.

## Operating Context

- A real plant floor: ~160 workers distributed across 10 production lines throughout a shift. One line, L8, has no fixed posts — it's a floating labor pool that absorbs people between assignments.
- The central operational loop this product manages: a person occupies a rotating post, accumulates fatigue specific to that post, gets flagged for relief before or as they cross a threshold, a replacement is proposed and dispatched, the outgoing person is confirmed and rerouted to a new post — with proximity between lines and per-post recovery windows (so nobody repeats the same wearing task without enough rest) governing where people go.
- Both roles work standing, one-handed, walking, under variable industrial lighting — a scratched screen protector at partial brightness under a fluorescent tube, not an office screen in good light.
- No offline queueing by design: when connectivity drops, the app goes defensive — blocks new cross-line movement and new assignments, shows a permanent unambiguous notice, and stamps stale data with its age — rather than queuing actions that might later be rejected *(docs/01_PRD.md §3.3, Épica H)*.
- Lines are prioritized, and that priority is reconfigurable mid-shift, but changes only ever apply forward — nothing already assigned gets revised retroactively.
- Real personnel data is already imported from the client's own spreadsheet: 164 active people, 98 fixed posts, across all 10 lines. Only L1/L2/L4/L6/L8 currently carry real day-to-day operating data; the rest stay "inactive" until the Coordinador plans them *(docs/PROGRESO.md)*.

## Capabilities and Constraints

- Two roles, mutually exclusive scopes, enforced at three independent layers (API role filter, line-scope filter, and SQL Server row-level security) — a Supervisor's isolation from other lines' people is a security guarantee, not just a UI convenience *(docs/00_DECISIONES.md §D1, §E2)*.
- Hard rules that never yield for anyone, including the Coordinador's exception path: medical restrictions, category compatibility, and post-specific recovery windows *(docs/00_DECISIONES.md §B12)*.
- The system only ever flags or suggests fatigue-based relief and reassignment; a human always executes the move — nothing reassigns a person automatically *(docs/01_PRD.md §9.3)*.
- No integration with time clocks or MES/SCADA in this product. The only external service is push-notification transport, which carries an opaque event ID and zero personnel data *(docs/01_PRD.md §3.3)*.
- The server is sole authority for time, concurrency resolution (first transaction to commit wins), and live statistics. The device clock and on-device calculation are never trusted for anything auditable — two panels showing the same shift must show the same number *(docs/00_DECISIONES.md §B1, §C4, §C6)*.
- Existing stack: Android native client (Kotlin, Jetpack Compose) talking to a .NET backend (Clean Architecture: Domain/Application/Infrastructure/Api) over SQL Server. No iOS or web client exists or is planned — explicitly out of scope.
- Undecided, explicitly left open rather than invented: per-post fatigue threshold minutes are being calibrated from real operating data (fields ship empty with a plant-wide default); KPI targets are provisional pending the first two weeks of real operation, flagged `⚠ PENDIENTE-E7` in the source docs.

## Brand Commitments

- Product name: **SmartAssign**. Android package `com.smartassign.app`.
- Voice: plant-floor language, never error codes or generic text — every rejection names its concrete cause and the concrete next step.
- The client/company operating the plant is intentionally left unnamed in this record, matching the existing docs.

## Evidence on Hand

- Real roster data imported from the client's spreadsheet (`Base de Datos.xlsx`): 164 active personnel, 12 marked absent, 98 fixed posts — confirmed real operational data, not samples.
- Medical-restriction records currently in the system are simulated placeholders (an explicit client decision, pending real data from Enfermería/nursing) — future work must not treat any restriction shown today as real, and must not fabricate additional sample restrictions beyond what's already marked `simulado` in the data.
- No case studies, testimonials, press coverage, or pricing exist. None should be assumed or invented.
- A full, pre-existing visual/component system already lives in `docs/03_UIUX_BRIEF.md` (tokens, components, layout, accessibility, motion, micro-copy) — noted here as evidence of visual maturity, not restated or extended by this file.

## Product Principles

1. The system never lies about what's happening — every rejection states its concrete cause and the concrete next unblocking step, in plant language, never a code.
2. Rejection is a feature, not an error case — it's how the hard safety rules stay real instead of decorative.
3. The decision, the clock, and every calculation live on the server, never on the device — two panels showing the same shift always show the same number.
4. The system flags and suggests; a human always decides and executes the movement of a person.
5. Nothing about where a person is or what state they're in is ever left ambiguous — audit trail with zero exceptions, zero people in an unknown location at shift close.

## Accessibility & Inclusion

- WCAG **AAA** contrast (7:1 text, 3:1 UI elements) is the enforced standard, chosen deliberately over AA because the real reading condition is a scratched screen protector at partial brightness under variable industrial lighting, not an office screen *(docs/00_DECISIONES.md §A11, docs/03_UIUX_BRIEF.md §5.2)*.
- 48dp minimum touch target everywhere; 64dp for the single primary action per screen (the most-used, least-forgiving one) — calibrated for standing, walking, one-handed, bare-handed operation (glove-based sizing was explicitly evaluated and retired, see `00_DECISIONES.md §A11`).
- Status is never communicated by color alone; every state pairs text with shape or icon *(docs/01_PRD.md §12.2)*.
