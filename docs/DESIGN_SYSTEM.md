# Design system — "Clinical Navy"

All values live in `web/src/styles/tokens.css`. Components never use raw hex values.

## Colour

| Token | Value | Use |
|---|---|---|
| `--primary-600` | `#1D4E89` | Primary buttons, active nav, links |
| `--primary-700` | `#163B69` | Hover / pressed |
| `--primary-50` | `#EEF4FB` | Active nav background, icon tiles |
| `--accent-500` | `#0E9F9A` | Teal accent, used sparingly (second chart series, highlights) |
| `--bg` | `#F7F8FA` | Page background |
| `--surface` | `#FFFFFF` | Cards, header, sidebar |
| `--border` | `#E4E7EC` | Hairlines |
| `--text` / `--muted` | `#0F172A` / `#64748B` | Body text / secondary text |
| Status | success `#12A26B`, warning `#D98B0B`, danger `#D64545`, info `#2F80ED` | Always paired with an icon or label |

Chart colours `--chart-1` (`#2C64A6`) and `--chart-2` (`#0E9F9A`) passed a colour-blind separation check on white (ΔE 17.4 worst case).

## Shape, depth, spacing

- Radius: 8px for inputs and buttons, 12px for cards, 16px for dialogs.
- Shadows are soft and low (`--shadow-sm` on cards, `--shadow-lg` only on overlays).
- 4px spacing scale. Page gutter 24px, card padding 20px, grid gap 16px.
- Content max width 1440px. Header 64px. Sidebar 256px, or 72px when collapsed.

## Typography

- Inter Variable for English, numbers and codes. Noto Nastaliq Urdu for Urdu, with a larger size and taller line height.
- Numbers, IDs, money and clinical values always render in Inter, left-to-right, with tabular figures (`.num`), so they never reorder inside Urdu text.

## Components (`web/src/components/ui`)

| Component | Notes |
|---|---|
| `Button` | primary / secondary / ghost / danger; sm / md / lg; `loading`, `iconOnly` |
| `Field` | Label, icon, end action, hint, accessible error (`aria-invalid`, `aria-describedby`) |
| `Card` | Title, subtitle, actions, footer, `flush` for tables |
| `Badge` | neutral / primary / success / warning / danger / info / accent, optional dot |
| `StatCard` | KPI tile with icon, delta (good/bad aware) and built-in skeleton |
| `Skeleton` | Shimmer placeholder |
| `EmptyState` | Icon, title, text, action |
| `ConfirmDialog` | Replaces `window.confirm`. Focuses the safe action; Escape closes; returns focus afterwards |
| `ToastProvider` / `useToast` | Success / info / danger feedback, auto-dismiss, screen-reader live region |
| `.table`, `.segmented`, `.banner` | CSS patterns for tables, range toggles and inline notices |

## RTL rules

- Use logical CSS properties only (`margin-inline-start`, `inset-inline-end`, `text-align: start`).
- Directional icons get the class `flip-rtl`.
- Time-series charts keep time running left to right in both languages. Only the surrounding text flips.
