# Design system — Fidelizar (F1-01, revised in F1-01b)

What a screen looks like and why, before any screen exists. This document explains the tokens
in [`../src/Fidelizar.Client/wwwroot/css/tokens.css`](../src/Fidelizar.Client/wwwroot/css/tokens.css);
that file is the thing to load, this document is why it looks the way it does. Moved there by
`F1-04c` once the client shell existed to load it from `wwwroot/css/` (§2, §3, §14) — there is
only the one copy.

Built for [ARCHITECTURE.md](ARCHITECTURE.md) §2 (Blazor WebAssembly, no CSS framework, no
downloaded fonts) and for the constraint [FUNCTIONAL-SPEC.md](FUNCTIONAL-SPEC.md) §1 states
first: **a cashier, with a customer standing in front of them, on the computer the shop already
has.** Under 15 seconds, under 4 steps, a mouse in one hand, glare on the screen, someone in a
hurry. The same page has to stay usable on a phone. Nothing here is decorative — every value
below exists because of that sentence or because of a numbered rule.

> **F1-01b, following the platform decision of 2026-08-18.** F1-01 built this system for "a cheap
> tablet at counter distance". The device premise was wrong, so this revision re-derives what
> depended on it: hover, keyboard, focus order, and what a wide screen does with the extra room
> (§8, §9, §10). The sizes and colours mostly survived — a 48px row is no worse with a mouse and
> is still needed on a phone — but two contrast pairs did not, and one double-submit hole only
> exists for keyboard users. §15 lists every change.

`F1-02` (flow design for S2–S5) and every frontend task from `F1-05` on build screens from these
tokens. This document does not build a screen, and it does not touch `Fidelizar.Client` — that
project does not exist as a real shell yet (`F1-04c`).

---

## 1. Two budgets, one system

[FUNCTIONAL-SPEC.md](FUNCTIONAL-SPEC.md) §1 draws the line explicitly: the counter screens
(S1–S5) are optimised for **speed** — a cashier standing at the counter, glancing between the
screen and the customer. The manager's and owner's screens (S6–S10 and on) are optimised for
**density** — used sitting down, more information per screen, less urgency per action.

One token set serves both. What changes between them is *usage*, not the palette: the counter
screens use the larger end of the type scale and the `--control-size-primary` size almost
everywhere; the back-office screens use the smaller end and `--control-size-comfortable`. Two
design systems would drift; one system used at two different densities does not.

Both budgets assume the same input devices, in the same order: **mouse and keyboard first, touch
second.** Not two interfaces — one, that answers to both (§8, §9).

## 2. What is out of scope here

- **No screen.** S1–S10 belong to `F1-02` (flow) and `F1-05` onward (build).
- **No client shell.** `Fidelizar.Client` is still the unmodified WebAssembly template from
  `F0-01` (`F1-04c` replaces it). `tokens.css` lives under `docs/` until that shell exists to
  load it from `wwwroot/css/`.
  **Resolved by `F1-04c`**: the shell now exists and `tokens.css` moved to
  `src/Fidelizar.Client/wwwroot/css/tokens.css` — one file, loaded from there, no copy left
  under `docs/` (§3, §14).
- **No dependency.** No CSS framework, no icon font, no downloaded webfont. The type scale uses
  the system font stack; anything that looks like an icon is an emoji or an inline SVG drawn by
  whoever builds the screen — both render at zero network cost, which matters on the counter's
  first load ([ARCHITECTURE.md](ARCHITECTURE.md) §14).
- **No consent wording.** Anything a `Consentimiento` checkbox says is a legal text and belongs
  to the business owner ([FUNCTIONAL-SPEC.md](FUNCTIONAL-SPEC.md) §12).
- **No keyboard shortcuts of its own.** §9 fixes the *contract* — focus order, what Enter and
  Escape mean, how the result list is driven. Inventing `Ctrl`-combinations for actions is a flow
  decision and belongs to `F1-02`.

## 3. A pre-existing issue, flagged, not fixed (resolved by `F1-04c`)

`src/Fidelizar.Client/wwwroot/index.html` used to load `lib/bootstrap/dist/css/bootstrap.min.css`
and `wwwroot/css/app.css` carried the template's hardcoded colours (`#1b6ec2`, `#258cfb`,
plain `red`). That was the stock Blazor WebAssembly template from `F0-01`, untouched since. It
was not `F1-01`'s task to remove — `F1-01` does not touch `Client` — but `F1-04c` deleted the
Bootstrap reference, the `lib/bootstrap/` folder and the template's `app.css` rules rather than
layering this system on top of them. Two colour systems in the same page is how a cashier ends
up looking at a blue that isn't `--color-primary`.

---

## 4. Colour

Two things drove every choice: **AA contrast is the floor, not the target** (several pairs below
clear it by a wide margin because glare erodes contrast further than a lab measurement shows),
and **colour never carries meaning alone** — every semantic state pairs its colour with an icon
or a word, per WCAG 1.4.1. A cashier who cannot distinguish amber from grey (~8% of men) must
still be able to tell a stale balance from a fresh one by reading, not by colour-matching.

Ratios below were computed with the WCAG 2.1 relative-luminance formula
(`(L1 + 0.05) / (L2 + 0.05)`), not eyeballed. §13 has the method.

### 4.1 Light (default)

| Token | Value | Role |
| --- | --- | --- |
| `--color-bg` | `#f3f4f6` | Page canvas |
| `--color-surface` | `#ffffff` | Cards, inputs, the counter card itself |
| `--color-surface-sunken` | `#e7e9ec` | Disabled fill, hover fill, pressed state, secondary panel |
| `--color-border` | `#c9cdd2` | Decorative dividers only |
| `--color-border-strong` | `#6e747b` | Input outlines, anything a boundary must be *seen*, not just implied |
| `--color-text` | `#16181b` | Primary text, the balance figure |
| `--color-text-muted` | `#4a4f55` | Secondary text — the *fresh* cutoff date, helper text |
| `--color-text-disabled` | `#9aa0a6` | Disabled labels |
| `--color-primary` | `#14509e` | Primary action: buscar, registrar canje, confirmar |
| `--color-success` | `#1e7a3d` | Positive confirmation (redemption registered) |
| `--color-warning` | `#8a5a00` | Stale data past 7 days, balance under review, allergy alert |
| `--color-danger` | `#b3261e` | Rejected input, destructive action (anular movimiento) |
| `--color-focus-ring` | `#0b63ce` | The one focus indicator, everywhere |

| Pair checked | Ratio | Requirement | Result |
| --- | --- | --- | --- |
| `--color-text` on `--color-bg` | 16.16:1 | 4.5:1 | pass, well over |
| `--color-text` on `--color-surface` | 17.79:1 | 4.5:1 | pass |
| `--color-text` on `--color-surface-sunken` | 14.62:1 | 4.5:1 | pass — the hovered secondary button |
| `--color-text-muted` on `--color-surface` | 8.27:1 | 4.5:1 | pass |
| `--color-text-muted` on `--color-bg` | 7.51:1 | 4.5:1 | pass |
| `--color-text-muted` on `--color-surface-sunken` | 6.80:1 | 4.5:1 | pass — `.badge--neutral` |
| `--color-text-disabled` on `--color-surface` | 2.64:1 | — | exempt (WCAG 1.4.3 excludes disabled UI text) |
| white on `--color-primary` (button label) | 7.86:1 | 4.5:1 | pass |
| white on `--color-primary-hover` (`#103f7d`) | 10.35:1 | 4.5:1 | pass |
| white on `--color-primary-active` (`#0d3567`) | 12.19:1 | 4.5:1 | pass |
| `--color-success` on `--color-success-bg` (`#e4f3e9`) | 4.68:1 | 4.5:1 | pass |
| `--color-success` on `--color-surface` | 5.38:1 | 4.5:1 | pass |
| `--color-warning` on `--color-warning-bg` (`#fcedd1`) | 5.13:1 | 4.5:1 | pass |
| `--color-warning` on `--color-surface` | 5.93:1 | 4.5:1 | pass |
| `--color-danger` on `--color-danger-bg` (`#fbe7e5`) | 5.50:1 | 4.5:1 | pass |
| `--color-danger` on `--color-surface` | 6.54:1 | 4.5:1 | pass |
| `--color-border` on `--color-surface` | 1.60:1 | — | exempt — decorative, never the only signal |
| `--color-border-strong` on `--color-surface` | 4.72:1 | 3:1 (non-text) | pass |
| `--color-border-strong` on `--color-bg` | 4.29:1 | 3:1 (non-text) | pass |
| `--color-border-strong` on `--color-surface-sunken` | 3.88:1 | 3:1 (non-text) | pass |
| `--color-primary` on `--color-surface` | 7.86:1 | 3:1 (non-text) | pass — active-option bar |
| `--color-primary` on `--color-surface-sunken` | 6.46:1 | 3:1 (non-text) | pass — active-option bar |
| `--color-focus-ring` on `--color-surface` | 5.69:1 | 3:1 (non-text) | pass |
| `--color-focus-ring` on `--color-bg` | 5.17:1 | 3:1 (non-text) | pass |
| `--color-focus-ring` on `--color-surface-sunken` | 4.68:1 | 3:1 (non-text) | pass — focus over a hovered row |

### 4.2 Dark

Automatic via `prefers-color-scheme: dark`, with a `data-theme="dark"` attribute hook for a
manual toggle if a later task adds one (none is built here).

| Token | Value | Role |
| --- | --- | --- |
| `--color-bg` | `#14171a` | Page canvas |
| `--color-surface` | `#1e2226` | Cards, inputs |
| `--color-surface-sunken` | `#262b30` | Disabled fill, hover fill, pressed state |
| `--color-border` | `#3a4046` | Decorative dividers |
| `--color-border-strong` | `#868d95` | Seen boundaries |
| `--color-text` | `#f2f3f5` | Primary text |
| `--color-text-muted` | `#b8bec4` | Secondary text |
| `--color-text-disabled` | `#6b7178` | Disabled labels |
| `--color-primary` | `#5b9bf0` | Primary action |
| `--color-success` | `#4caf6d` | Positive confirmation |
| `--color-warning` | `#e8a93b` | Stale data, balance under review, allergy alert |
| `--color-danger` | `#f2635c` | Rejected input, destructive action |
| `--color-focus-ring` | `#7cb0ff` | Focus indicator |

| Pair checked | Ratio | Requirement | Result |
| --- | --- | --- | --- |
| `--color-text` on `--color-bg` | 16.20:1 | 4.5:1 | pass |
| `--color-text` on `--color-surface` | 14.42:1 | 4.5:1 | pass |
| `--color-text` on `--color-surface-sunken` | 12.86:1 | 4.5:1 | pass — the hovered secondary button |
| `--color-text-muted` on `--color-surface` | 8.54:1 | 4.5:1 | pass |
| `--color-text-muted` on `--color-bg` | 9.60:1 | 4.5:1 | pass |
| `--color-text-muted` on `--color-surface-sunken` | 7.62:1 | 4.5:1 | pass — `.badge--neutral` |
| `--color-text-disabled` on `--color-surface` | 3.25:1 | — | exempt, and still comfortably legible |
| white on `--color-primary` (`#5b9bf0`) | 2.84:1 | 4.5:1 | **fails** — see decision below |
| `#0b1220` (dark navy) on `--color-primary` | 6.59:1 | 4.5:1 | pass — this is the button label colour used |
| `#0b1220` on `--color-primary-hover` (`#71a8f5`) | 7.69:1 | 4.5:1 | pass |
| `#0b1220` on `--color-primary-active` (`#8ab6f7`) | 9.02:1 | 4.5:1 | pass |
| `--color-success` on `--color-success-bg` (`#16301f`) | 5.19:1 | 4.5:1 | pass |
| `--color-success` on `--color-surface` | 5.84:1 | 4.5:1 | pass |
| `--color-warning` on `--color-warning-bg` (`#3a2e12`) | 6.44:1 | 4.5:1 | pass |
| `--color-warning` on `--color-surface` | 7.75:1 | 4.5:1 | pass |
| `--color-danger` on `--color-danger-bg` (`#3a1613`) | 5.15:1 | 4.5:1 | pass |
| `--color-danger` on `--color-surface` | 5.12:1 | 4.5:1 | pass |
| `--color-border` on `--color-surface` | 1.53:1 | — | exempt — decorative |
| `--color-border-strong` on `--color-surface` | 4.77:1 | 3:1 (non-text) | pass |
| `--color-border-strong` on `--color-bg` | 5.36:1 | 3:1 (non-text) | pass |
| `--color-border-strong` on `--color-surface-sunken` | 4.26:1 | 3:1 (non-text) | pass |
| `--color-primary` on `--color-surface` | 5.63:1 | 3:1 (non-text) | pass — active-option bar |
| `--color-primary` on `--color-surface-sunken` | 5.03:1 | 3:1 (non-text) | pass — active-option bar |
| `--color-focus-ring` on `--color-surface` | 7.25:1 | 3:1 (non-text) | pass |
| `--color-focus-ring` on `--color-bg` | 8.15:1 | 3:1 (non-text) | pass |
| `--color-focus-ring` on `--color-surface-sunken` | 6.47:1 | 3:1 (non-text) | pass — focus over a hovered row |

**The one real decision here:** white text on the dark-mode primary blue (`#5b9bf0`) fails AA at
2.84:1 — a lighter accent colour was chosen for dark mode (so it still reads as "blue" against a
near-black background) but it is too light for white text on top of it. `--color-text-on-primary`
in dark mode is therefore a dark navy (`#0b1220`), not white. The button is still unmistakably a
button; only the label colour flips. This is the standard resolution for light-accent-on-dark
buttons and is what the ratio table above verifies, pair by pair, hover and active included.

### 4.3 The border fix F1-01b had to make

`--color-border-strong` was `#8a9099` (light) and `#6b7178` (dark). F1-01 verified it against
`--color-surface` only, where it scraped past at 3.22:1 and 3.25:1. Measured against the other
two surfaces it can actually sit on, it failed:

| Pair, with the old value | Ratio | Requirement | |
| --- | --- | --- | --- |
| light `#8a9099` on `--color-surface-sunken` | 2.64:1 | 3:1 | fail |
| light `#8a9099` on `--color-bg` | 2.92:1 | 3:1 | fail |
| dark `#6b7178` on `--color-surface-sunken` | 2.90:1 | 3:1 | fail |

That was harmless-looking on a tablet and is not harmless on a computer, because
`.btn--secondary:hover` fills the button with `--color-surface-sunken` and keeps
`--color-border-strong` as its outline: **the secondary button lost its visible boundary exactly
while the pointer was on it**, which with a mouse is most of the time. The `--color-bg` failure is
the same defect one step out: an input placed directly on the page canvas rather than inside a
card.

Raised to `#6e747b` (light) and `#868d95` (dark), the value now clears 3:1 against all three
surfaces with margin (§4.1, §4.2). Nothing else moved — the fix is one token per theme, not a
palette change, and no text pair shifted.

### 4.4 Why warning, not danger, for "balance under review" (RN-25)

A negative balance under I6/RN-25 is never the member's fault and never shown as a debt they
owe. Danger red reads as "you did something wrong." The screen that disables the redeem button
and shows *"Saldo en revisión — consultá con la encargada"* uses `--color-warning`, not
`--color-danger` — consistent with treating this as an operational note, not an error.

### 4.5 Why the allergy alert is warning-tinted and birthday/purchase alerts are not

The alert strip (FUNCTIONAL-SPEC §5) lists four alert kinds. Three are informational (balance,
birthday, usual purchases); one is a safety fact affecting what the cashier is about to hand the
customer. Rendering the allergy line with `.badge--warning` — not just the same neutral row as
the others — means a cashier scanning quickly is more likely to catch it before ringing up a
product with gluten for a celiac member. Allergy/diet lines render only with `DatosSensibles`
consent on record (I10) regardless of styling.

---

## 5. Type scale

Base size is the browser/OS default — nothing in `tokens.css` sets `html { font-size: … }` — so
a cashier's own text-size accessibility setting is respected (WCAG 1.4.4, "Resize Text"). Every
size below is `rem`, scaling with that base.

| Token | Size (at 16px root) | Used for |
| --- | --- | --- |
| `--text-xs` | 14px | Captions. Never primary content, never a balance, never an error |
| `--text-sm` | 16px | Helper text, secondary labels, field errors |
| `--text-base` | 18px | Default body text, inputs, buttons, list rows |
| `--text-lg` | 22px | List-row primary line — the member's name in a search result |
| `--text-xl` | 28px | Screen titles |
| `--text-2xl` | 36px | Secondary emphasis figures |
| `--text-display` | 56px | **The balance figure.** The one number a customer reads over the cashier's shoulder |

18px as the working default, not the web-conventional 16px, because this screen is read at
counter distance and sometimes by a second pair of eyes that is not the one driving the mouse
(FUNCTIONAL-SPEC §5, "the screen the customer can see over the cashier's shoulder"). 56px for
the balance is deliberate: RN-12 depends on the customer actually reading that number as a hook,
which fails if it is sized like a heading instead of like a hero. **The balance keeps
`--text-display` at every screen width** — it is the last thing a narrow layout is allowed to
shrink (§10).

Weight: `--font-weight-bold` (700) on the balance figure and on anything that must survive a
glance under glare — screen titles, primary button labels. Regular (400) everywhere else;
medium (500) reserved for secondary emphasis (list-row labels, badges) so bold stays meaningful.

## 6. Spacing, radii, shadows

- **Spacing** is a 4px-based scale (`--space-1` = 4px through `--space-8` = 64px), in `rem` so
  it also follows the root font size. Generous spacing on counter screens is not aesthetic —
  it is what keeps `--control-gap-min` (8px) real between two homonym rows that must never
  be mis-clicked into each other.
- **Radii** (`--radius-sm` 6px through `--radius-full`) are modest, not the rounded-everything
  look. The reason F1-01 gave was a cheap tablet's low-DPI panel; the real reason that survives
  the platform change is that a shop computer's monitor is frequently an old 1366×768 panel at
  100% scaling, where a heavy radius eats the corner of a dense table cell.
- **Shadows** exist for elevation only (cards, the confirm-redemption sheet), never as the only
  signal that something is a button. In dark mode `--shadow-sm` is `none`: a soft shadow tuned
  for a white background is close to invisible on `#1e2226`, so dark mode leans on
  `--color-border-strong` for the same job instead of shipping a shadow nobody sees.

## 7. Control sizes

Renamed from `--touch-target-*` in F1-01b. The old name said these numbers were a concession to
fingers; they are not — they are the minimum size of anything a person activates, whatever they
are pointing with.

| Token | Size | Used for |
| --- | --- | --- |
| `--control-size-min` | 44px | Absolute floor. Small icon-only buttons (e.g. dismiss) only |
| `--control-size-comfortable` | 48px | Default: inputs, list rows, secondary buttons |
| `--control-size-primary` | 56px | The button that matters: buscar, registrar canje, confirmar |
| `--control-gap-min` | 8px | Minimum gap between adjacent activatable rows |

44px is the accessibility floor this system will not go under (WCAG 2.5.8 / the platform HIGs
agree on it). **The values did not change when the device did**, for three reasons that hold with
a mouse:

1. Pointing at a bigger target is faster, mouse included — that is Fitts's law, not a touch
   quirk. On a counter that is measured in seconds (FUNCTIONAL-SPEC §1), a 56px primary button is
   the cheapest speed there is.
2. The cost of a miss is money. Per I7 and the homonym flow (FUNCTIONAL-SPEC §4), **a mis-click
   between two similar names credits money to the wrong person.** `--control-gap-min` exists for
   exactly that case: two homonym rows sit close together and must not be close enough to hit by
   accident with a shaky hand on a worn mouse.
3. The same page still has to work on a phone, where they are a hard requirement rather than a
   nicety.

What *did* change is that a large target is no longer an excuse for a sloppy pointer state: a
48px row that gives no hover or focus feedback is worse with a mouse than with a finger, because
the pointer is on screen continuously and expects the screen to answer it (§8).

## 8. Pointer and hover

### 8.1 Hover is guarded, not global

```css
@media (hover: hover) and (pointer: fine) { … }
```

Every `:hover` rule in `tokens.css` sits inside that query. Without it, a tap on a touch screen
leaves the hover style stuck on the tapped element until something else is tapped — so a member
row keeps looking selected after the cashier already moved on, which on a screen that decides who
gets credited is a misleading state, not a cosmetic one.

### 8.2 Hover never carries information

Hover may only *emphasise* something already visible: a background shift, a border shift. It may
never reveal content — no tooltip holding the cutoff date, the reason for an alert, or why a
button is disabled. Anything the cashier needs to decide is on the screen, at rest, readable
without moving the pointer (WCAG 1.4.13, and FUNCTIONAL-SPEC §1's "nothing that requires reading
a paragraph"). A customer is standing there; the cashier is not going to explore.

The same rule from the other side: **hover is never the only signal that something is
interactive.** A row is a row because of its surface, border and cursor at rest; a button is a
button because of its fill and label. Someone navigating by keyboard never triggers hover at all.

### 8.3 Hover and the keyboard's active option must look different

They can be true at the same time — the pointer resting over row 3 while the keyboard's active
option is row 1 — so they cannot share a style. Hover shifts the background only. The active
option shifts the background *and* shows a 4px `--color-primary` bar on its leading edge (§9.3).
Both were measured against `--color-surface-sunken` in §4.

### 8.4 Cursor and selection

- `cursor: pointer` only on things that actually activate. Never on plain text — a cursor that
  lies about what is clickable costs a click, and a click at this counter costs seconds.
- `cursor: not-allowed` on disabled controls, together with `aria-disabled` (§11).
- **Never `user-select: none` on data.** The manager copying a balance or a member's name out of
  a screen is a legitimate, frequent act on a computer. Text selection stays available on every
  figure and name; only chrome (button labels, badges) may opt out.
- Right-click is never intercepted. There is nothing on these screens worth protecting from a
  context menu, and breaking it breaks paste into a search field.

### 8.5 Transitions

Hover and active transitions run at `--duration-fast` (100ms). Long enough to feel like a
response, short enough that sweeping a mouse down a list of homonyms does not leave a trail of
half-lit rows. `prefers-reduced-motion` collapses them, as it does every animation in the file.

## 9. Keyboard

A cashier who has both hands on the counter has one on the keyboard. The fastest path through
S1→S2→S3 is typed, and the fastest typed path must not require reaching for the mouse. This
section is the contract every screen from `F1-05` on has to honour.

### 9.1 Focus order is DOM order

- Visual order equals DOM order. If they disagree, **the DOM changes, not `tabindex`.**
- **No positive `tabindex`, ever.** `tabindex="0"` to make a non-native control focusable and
  `tabindex="-1"` for a programmatic focus target are the only allowed values. A positive value
  jumps the element ahead of the whole document and is unmaintainable the moment a field is
  added.
- Every interactive element is reachable by `Tab` and operable by `Enter`/`Space` per its native
  role. If it needs a custom role to be reachable, prefer the native element instead.
- The focus ring is `outline`, not `box-shadow` — deliberately, so it survives Windows
  high-contrast / `forced-colors` mode, where `box-shadow` is dropped and a cashier on a
  high-contrast desktop would lose focus entirely.

### 9.2 Where focus starts, and where it goes

| Moment | Where focus lands |
| --- | --- |
| Page load, S1 | The search field, autofocused. It is the **only** autofocus in the product (FUNCTIONAL-SPEC §3) — anywhere else it steals focus from someone mid-task |
| First `Tab` from load | `.skip-link`, visible only while focused, jumping past the header straight to the search field |
| Navigation to another screen | The new screen's `<h1>` (`tabindex="-1"`), and the document title changes — a screen reader announces where you are instead of staying silent |
| A dialog opens | Into the dialog: its first field, or its heading when there is no field. Focus is trapped while it is open |
| A dialog closes | Back to the element that opened it, never to the top of the page |
| A destructive confirm opens (S8 anular) | **On the cancel control, never on the destructive one.** `Enter` on reflex must not void a movement |

Use the native `<dialog>` element for the confirm sheet: it gives the focus trap, `Escape`, and
the inert background without a script that has to be maintained.

### 9.3 The search result list is a combobox, not a list of buttons

S2's results (FUNCTIONAL-SPEC §3) follow the ARIA combobox pattern: the text field keeps focus
and owns the interaction, with `role="combobox"`, `aria-expanded`, `aria-controls` and
`aria-activedescendant` pointing at the active option; the results are a `role="listbox"` of
`role="option"` rows.

| Key | What it does |
| --- | --- |
| `↓` / `↑` | Move the active option. From the field, `↓` activates the first result |
| `Home` / `End` | First / last result |
| `Enter` | Open the active option — the member's record |
| `Esc` | Close the list, keep what was typed. A second `Esc` clears the field |

The reason this pattern and not focusable rows: it makes the whole flow **type → `↓` → `Enter`**,
with no `Tab` at all, and it keeps an unbounded list of results out of the `Tab` sequence — with
plain buttons, tabbing past a search that matched thirty members means thirty stops before
reaching anything else on the page. The cost is that focus stays in the input, which is why the
active option needs a style that does not depend on owning focus (§8.3).

Rows are still ordinary click targets for a mouse and tap targets on a phone. One markup, three
input methods.

### 9.4 Enter and Escape mean one thing each

- `Enter` in a single-purpose form submits it (S4 registrar canje, S5 alta). A cashier who typed
  an amount should not have to find the button.
- `Esc` cancels the thing in front of you and never more than that: it closes a dialog, or closes
  the result list — it never navigates back a screen and never discards a half-filled form. A
  request failing mid-flow must not lose a form (ARCHITECTURE §14); neither may a stray `Esc`.

### 9.5 The double-submit hole `pointer-events` left open

`.btn[aria-busy="true"]` sets `pointer-events: none`, which stops a second click. **It does not
stop a second `Enter`** — a focused button still activates from the keyboard, `pointer-events` is
a pointer rule. On the redemption button that is a duplicated `Canje`: the exact failure
ARCHITECTURE §14 names, reachable only by keyboard, and invisible to any amount of clicking
during review.

CSS cannot close it. So it is a markup rule, and it is not optional: **a control in its loading
state carries `disabled` (or `aria-disabled="true"` with the handler refusing to run) in addition
to the busy styling.** The styling shows the state; the attribute is what actually prevents the
second submit. `F1-07` is where this gets tested, from the keyboard, not from the mouse.

## 10. What a wide screen shows that a narrow one does not

Three breakpoints, and no more. They are literals in the CSS because media queries cannot read
custom properties; `tokens.css` documents them where the layout tokens are defined.

| Name | Range | What it is |
| --- | --- | --- |
| Narrow | `< 640px` | A phone. Everything works, single column |
| Medium | `640px – 1023px` | A tablet, a small laptop |
| Wide | `≥ 1024px` | **The shop's computer — the primary target** |

### 10.1 The rule: nothing is hidden on narrow

A wide screen gains **no feature and no data**. Same functionality, same fields, same order —
what changes is how much of it is visible at once without scrolling. A "desktop-only" action
would be a second interface, which the platform decision of 2026-08-18 rules out.

The corollary matters for the back office: a wide table that becomes a horizontally scrolling
table on a phone hides columns behind a gesture nobody discovers. At narrow, a table row becomes
a stacked block with every field labelled. Linearised, never truncated — least of all a money
column.

### 10.2 What wide actually buys

| Wide | Narrow | Why |
| --- | --- | --- |
| Search results beside the member's record (`.split`), the list staying put | Results, then the record, in sequence | Resolving a homonym (FUNCTIONAL-SPEC §4) means comparing candidates. Side by side, a wrong guess costs one click instead of a trip back |
| Alert strip beside the balance | Alert strip under the balance | Balance and allergy alert land in one glance instead of two |
| Back-office lists as tables | Same rows as stacked blocks | Density is the back office's budget (§1) |
| Content stops at `--layout-max-width` (1200px); forms at `--layout-form-max-width` (544px) | Full width minus the gutter | A 27" monitor should give whitespace, not a 200-character line or a form field a metre wide |
| Gutter grows to `--space-6` | `--space-4` | Room to breathe once there is room |

`.split` and `.page` in `tokens.css` are the two primitives that carry this. They are containers,
not screens — `F1-02` decides what goes in them.

### 10.3 What never changes with width

- The balance stays `--text-display` (§5). RN-12 does not get smaller on a phone.
- Control sizes stay as they are (§7). No breakpoint shrinks a target below 44px.
- No phone number and no DNI on the counter screen, at any width (FUNCTIONAL-SPEC §4).
- At 200% browser zoom on a 1280px window — the WCAG 1.4.10 reflow condition, and a realistic
  setting for a cashier who cannot read small text — the page reflows to the narrow layout with
  no horizontal scrolling and nothing clipped. This is the practical payoff of the whole "narrow
  hides nothing" rule: zoom and a small screen are the same problem.

---

## 11. States

Every interactive element defines all seven; none may be implied by colour alone.

| State | How it reads | Notes |
| --- | --- | --- |
| **Default** | Base surface, border, text tokens | Enough on its own to identify what is interactive (§8.2) |
| **Hover** | `--color-*-hover` background shift | The primary pointer state on the shop's computer. Guarded by `@media (hover: hover)` so it does not stick after a tap (§8.1) |
| **Focus** | `--color-focus-ring`, 3px, 2px offset, on `:focus-visible` | Never removed. `:focus-visible` (not `:focus`) so a click or tap draws no ring but keyboard navigation always does. `outline`, not `box-shadow`, so forced-colors mode keeps it (§9.1) |
| **Active/pressed** | `--color-*-active` background shift | Not hover-guarded: a click and a tap both produce it, and it is the confirmation the press registered |
| **Disabled** | `--color-*-disabled-*` tokens, `cursor: not-allowed`, `aria-disabled` | Exempt from AA text contrast by spec (§4.1); still checked and still legible |
| **Error** | `--color-danger` border + icon + Spanish message under the field | Never colour alone — the border width also changes (1px → 2px) |
| **Loading** | `aria-busy="true"`, label replaced by a spinner, same footprint, `pointer-events: none` — **plus `disabled` in the markup** | Same footprint is the point: a resized button during submit is how a redemption gets submitted twice. And `pointer-events` alone leaves the keyboard path open (§9.5) |

### 11.1 Stale data (R3, FUNCTIONAL-SPEC §5)

Exactly two tiers, matching the functional spec — no third tier is introduced here:

| Age of the cutoff date | Style |
| --- | --- |
| ≤ 7 days | `.badge--neutral` — `--color-text-muted` on `--color-surface-sunken`, regular weight |
| > 7 days | `.badge--warning` — `--color-warning` on `--color-warning-bg`, **bold**, with a warning icon |

The cutoff date is never rendered without one of these two badges next to the balance, on any
screen, in any report, in any export (ARCHITECTURE §13, R3). If a later flow needs finer-grained
staleness bands, that is a decision for `F1-02`, not one this system pre-empts. Neither badge is
ever a hover tooltip (§8.2).

### 11.2 Empty search result

No matches is not an error — it is the normal shape of "keep typing" or "this member does not
exist yet." Neutral tone, no `--color-danger`: *"No encontramos a nadie con ese nombre. Probá
con otro nombre o con menos letras."* An empty-state message is content owned by `F1-02`; the
style contract is: neutral badge/text tokens, never danger tokens, for a state that is not a
failure.

---

## 12. Dark mode mechanism

```css
:root { /* light tokens, the default */ }

@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) { /* dark tokens */ }
}

:root[data-theme="dark"] { /* dark tokens again, wins over OS light */ }
```

No toggle UI is built here — that is a future settings screen's job, if the product ever wants
one. The `data-theme` attribute hook exists so that screen can flip one attribute on `<html>`
instead of maintaining a second stylesheet.

## 13. How this gets verified again

Contrast ratios in §4 were computed with the WCAG 2.1 relative-luminance formula, not read off a
colour picker: `L = 0.2126·R + 0.7152·G + 0.0722·B` (each channel linearised), then
`(L_lighter + 0.05) / (L_darker + 0.05)`. Any later colour change should be re-run through the
same formula before it ships — a value that "looks fine" at a monitor's default brightness can
still fail at counter distance under fluorescent glare, which is exactly the condition this
system is built for.

Every pair is measured against **each surface the element can land on**, not just the most
common one. §4.3 is what happens when that step is skipped.

The rest of this system is verified by doing, and the checks belong to the tasks that build
screens (`F1-05`+, and `F1-17` under real counter conditions):

- **Unplug the mouse.** Complete buscar → ficha → registrar canje from the keyboard alone. If it
  cannot be finished, it is a defect, not a limitation.
- **One `Tab` from a cold load** must land on the skip link, and the ring must be visible.
- **Submit twice with `Enter`** on a slow connection. One `Canje`, not two (§9.5).
- **Tap a row on a phone**, then tap elsewhere. No row is left looking hovered (§8.1).
- **200% zoom at 1280px.** No horizontal scrollbar, nothing clipped (§10.3).
- **Windows high-contrast mode.** The focus ring survives; nothing becomes invisible.
- **Both themes**, every check above. Dark mode is not a skin — it has its own ratio table.

## 14. Consuming this in `F1-04c` and later

1. Load `tokens.css` before any page-specific CSS.
2. Build components from the primitives in it (`.btn`, `.input`, `.list-row`, `.badge`, `.card`,
   `.page`, `.split`, `.skip-link`) or from scratch — they are a starting point, not a mandate,
   but every colour, size and radius used must come from a `var(--…)` in this file. A literal hex
   code or pixel value in a Razor component is the same defect a literal `0.03` is in `Domain`
   (ARCHITECTURE §6): the number stops being something this document can be asked to defend.
3. Remove the template Bootstrap reference and `app.css` rules first (§3) — do not layer this
   system on top of them.
4. The markup rules in §9 are part of the contract, not suggestions: no positive `tabindex`, one
   autofocus in the whole product, `disabled` on a busy control, focus returned when a dialog
   closes.

## 15. What F1-01b changed, and why

| Change | Reason |
| --- | --- |
| Premise rewritten: the shop's computer first, phone second | The device premise F1-01 was given was wrong (owner, 2026-08-18) |
| `--color-border-strong` raised: `#8a9099` → `#6e747b`, `#6b7178` → `#868d95` | It failed 3:1 on `--color-surface-sunken` and `--color-bg`, so a hovered secondary button had no visible boundary (§4.3) |
| Ratio tables extended with every `--color-surface-sunken` pair, and with the active-option bar | Hover and the active option both paint that surface; nothing had been measured on it |
| `--touch-target-*` → `--control-size-*`, `--control-gap-min`; values unchanged | The sizes were never a touch concession (§7). Renamed now, while nothing consumes them yet |
| Every `:hover` moved inside `@media (hover: hover) and (pointer: fine)` | Sticky hover after a tap leaves a member row looking selected when it is not (§8.1) |
| `.btn--secondary:active` added; disabled states excluded from hover and active | A disabled button that lights up under the pointer is a lie about what will happen |
| New: `--focus-ring-width` / `--focus-ring-offset`, `.skip-link`, `.list-row[aria-selected]` | Keyboard navigation had tokens for the ring's colour but not its geometry, no way in, and no style for an active option that does not own focus |
| New: `.page`, `.form-column`, `.split`, `--layout-*`, the three breakpoints | "What a wide screen shows that a narrow one does not" had no answer at all (§10) |
| New §8, §9, §10; §7 and §11 rewritten | The three subjects the platform decision opened |
| Documented: a busy control must also be `disabled` | `pointer-events: none` blocks the mouse and not `Enter` — a keyboard-only double `Canje` (§9.5) |

What did **not** change: the palette (beyond the one border token), the type scale, the spacing
and radius scales, the control sizes, the two staleness tiers, the dark-mode mechanism, and every
decision in §4.4 and §4.5. F1-01's reasoning held; it was the device sentence at the top that
did not.
