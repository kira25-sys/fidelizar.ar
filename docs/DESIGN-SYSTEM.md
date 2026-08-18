# Design system — Fidelizar (F1-01)

What a screen looks like and why, before any screen exists. This document explains the tokens
in [`design-system/tokens.css`](design-system/tokens.css); that file is the thing to `@import`,
this document is why it looks the way it does.

Built for [ARCHITECTURE.md](ARCHITECTURE.md) §2 (Blazor WebAssembly, no CSS framework, no
downloaded fonts) and for the constraint [FUNCTIONAL-SPEC.md](FUNCTIONAL-SPEC.md) §1 states
first: **a cashier, with a customer standing in front of them, on a cheap tablet, in a shop.**
Under 15 seconds, under 4 taps, glare on the screen, a smudged finger, someone in a hurry.
Nothing here is decorative — every value below exists because of that sentence or because of a
numbered rule.

`F1-02` (flow design for S2–S5) and every frontend task from `F1-05` on build screens from these
tokens. This document does not build a screen, and it does not touch `Fidelizar.Client` — that
project does not exist as a real shell yet (`F1-04c`).

---

## 1. Two budgets, one system

[FUNCTIONAL-SPEC.md](FUNCTIONAL-SPEC.md) §1 draws the line explicitly: the counter screens
(S1–S5) are optimised for **speed** — a cashier standing up, glancing between the tablet and the
customer. The manager's and owner's screens (S6–S10 and on) are optimised for **density** — used
sitting down, more information per screen, less urgency per tap.

One token set serves both. What changes between them is *usage*, not the palette: the counter
screens use the larger end of the type scale and the `--touch-target-primary` size almost
everywhere; the back-office screens use the smaller end and `--touch-target-comfortable`. Two
design systems would drift; one system used at two different densities does not.

## 2. What is out of scope here

- **No screen.** S1–S10 belong to `F1-02` (flow) and `F1-05` onward (build).
- **No client shell.** `Fidelizar.Client` is still the unmodified WebAssembly template from
  `F0-01` (`F1-04c` replaces it). `tokens.css` lives under `docs/` until that shell exists to
  load it from `wwwroot/css/`.
- **No dependency.** No CSS framework, no icon font, no downloaded webfont. The type scale uses
  the system font stack; anything that looks like an icon is an emoji or an inline SVG drawn by
  whoever builds the screen — both render at zero network cost, which matters on the tablet's
  first load ([ARCHITECTURE.md](ARCHITECTURE.md) §14).
- **No consent wording.** Anything a `Consentimiento` checkbox says is a legal text and belongs
  to the business owner ([FUNCTIONAL-SPEC.md](FUNCTIONAL-SPEC.md) §12).

## 3. A pre-existing issue, flagged, not fixed

`src/Fidelizar.Client/wwwroot/index.html` still loads `lib/bootstrap/dist/css/bootstrap.min.css`
and `wwwroot/css/app.css` still carries the template's hardcoded colours (`#1b6ec2`, `#258cfb`,
plain `red`). That is the stock Blazor WebAssembly template from `F0-01`, untouched since. It is
not this task's to remove — `F1-01` does not touch `Client` — but whoever builds `F1-04c` should
delete the Bootstrap reference and the template's `app.css` rules rather than layer this system
on top of them. Two colour systems in the same page is how a cashier ends up looking at a blue
that isn't `--color-primary`.

---

## 4. Colour

Two things drove every choice: **AA contrast is the floor, not the target** (several pairs below
clear it by a wide margin because glare erodes contrast further than a lab measurement shows),
and **colour never carries meaning alone** — every semantic state pairs its colour with an icon
or a word, per WCAG 1.4.1. A cashier who cannot distinguish amber from grey (~8% of men) must
still be able to tell a stale balance from a fresh one by reading, not by colour-matching.

Ratios below were computed with the WCAG 2.1 relative-luminance formula
(`(L1 + 0.05) / (L2 + 0.05)`), not eyeballed. §7 has the method.

### 4.1 Light (default)

| Token | Value | Role |
| --- | --- | --- |
| `--color-bg` | `#f3f4f6` | Page canvas |
| `--color-surface` | `#ffffff` | Cards, inputs, the counter card itself |
| `--color-surface-sunken` | `#e7e9ec` | Disabled fill, pressed state, secondary panel |
| `--color-border` | `#c9cdd2` | Decorative dividers only |
| `--color-border-strong` | `#8a9099` | Input outlines, anything a boundary must be *seen*, not just implied |
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
| `--color-text-muted` on `--color-surface` | 8.27:1 | 4.5:1 | pass |
| `--color-text-muted` on `--color-bg` | 7.51:1 | 4.5:1 | pass |
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
| `--color-border-strong` on `--color-surface` | 3.22:1 | 3:1 (non-text) | pass |
| `--color-focus-ring` on `--color-surface` | 5.69:1 | 3:1 (non-text) | pass |
| `--color-focus-ring` on `--color-bg` | 5.17:1 | 3:1 (non-text) | pass |

### 4.2 Dark

Automatic via `prefers-color-scheme: dark`, with a `data-theme="dark"` attribute hook for a
manual toggle if a later task adds one (none is built here).

| Token | Value | Role |
| --- | --- | --- |
| `--color-bg` | `#14171a` | Page canvas |
| `--color-surface` | `#1e2226` | Cards, inputs |
| `--color-surface-sunken` | `#262b30` | Disabled fill, pressed state |
| `--color-border` | `#3a4046` | Decorative dividers |
| `--color-border-strong` | `#6b7178` | Seen boundaries |
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
| `--color-text-muted` on `--color-surface` | 8.54:1 | 4.5:1 | pass |
| `--color-text-muted` on `--color-bg` | 9.60:1 | 4.5:1 | pass |
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
| `--color-border-strong` on `--color-surface` | 3.25:1 | 3:1 (non-text) | pass |
| `--color-focus-ring` on `--color-surface` | 7.25:1 | 3:1 (non-text) | pass |
| `--color-focus-ring` on `--color-bg` | 8.15:1 | 3:1 (non-text) | pass |

**The one real decision here:** white text on the dark-mode primary blue (`#5b9bf0`) fails AA at
2.84:1 — a lighter accent colour was chosen for dark mode (so it still reads as "blue" against a
near-black background) but it is too light for white text on top of it. `--color-text-on-primary`
in dark mode is therefore a dark navy (`#0b1220`), not white. The button is still unmistakably a
button; only the label colour flips. This is the standard resolution for light-accent-on-dark
buttons and is what the ratio table above verifies, pair by pair, hover and active included.

### 4.3 Why warning, not danger, for "balance under review" (RN-25)

A negative balance under I6/RN-25 is never the member's fault and never shown as a debt they
owe. Danger red reads as "you did something wrong." The screen that disables the redeem button
and shows *"Saldo en revisión — consultá con la encargada"* uses `--color-warning`, not
`--color-danger` — consistent with treating this as an operational note, not an error.

### 4.4 Why the allergy alert is warning-tinted and birthday/purchase alerts are not

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
counter distance and sometimes by a second pair of eyes that isn't holding the tablet
(FUNCTIONAL-SPEC §5, "the screen the customer can see over the cashier's shoulder"). 56px for
the balance is deliberate: RN-12 depends on the customer actually reading that number as a hook,
which fails if it is sized like a heading instead of like a hero.

Weight: `--font-weight-bold` (700) on the balance figure and on anything that must survive a
glance under glare — screen titles, primary button labels. Regular (400) everywhere else;
medium (500) reserved for secondary emphasis (list-row labels, badges) so bold stays meaningful.

## 6. Spacing, radii, shadows

- **Spacing** is a 4px-based scale (`--space-1` = 4px through `--space-8` = 64px), in `rem` so
  it also follows the root font size. Generous spacing on counter screens is not aesthetic —
  it is what keeps `--touch-target-gap-min` (8px) real between two homonym rows that must never
  be mis-tapped into each other.
- **Radii** (`--radius-sm` 6px through `--radius-full`) are modest, not the rounded-everything
  look — sharper corners keep small text and numbers crisp on the low-DPI panel a cheap tablet
  usually has.
- **Shadows** exist for elevation only (cards, the confirm-redemption sheet), never as the only
  signal that something is a button. In dark mode `--shadow-sm` is `none`: a soft shadow tuned
  for a white background is close to invisible on `#1e2226`, so dark mode leans on
  `--color-border-strong` for the same job instead of shipping a shadow nobody sees.

## 7. Touch targets

| Token | Size | Used for |
| --- | --- | --- |
| `--touch-target-min` | 44px | Absolute floor. Small icon-only buttons (e.g. dismiss) only |
| `--touch-target-comfortable` | 48px | Default: inputs, list rows, secondary buttons |
| `--touch-target-primary` | 56px | The button that matters: buscar, registrar canje, confirmar |
| `--touch-target-gap-min` | 8px | Minimum gap between adjacent tappable rows |

44px is the accessibility floor this system will not go under (WCAG 2.5.8 / the platform HIGs
agree on it). It is set as the floor, not the default, because the hand at this counter may be
rushed, and — per I7 and the homonym flow (FUNCTIONAL-SPEC §4) — **a mis-tap between two similar
names credits money to the wrong person.** `--touch-target-gap-min` exists for exactly that case:
two homonym rows sit close together on the screen and must not be close enough to fat-finger.

## 8. States

Every interactive element defines all seven; none may be implied by colour alone.

| State | How it reads | Notes |
| --- | --- | --- |
| **Default** | Base surface, border, text tokens | |
| **Hover** | `--color-*-hover` background shift | Mouse-driven; rare on a touch tablet, present for `Encargada`/`Dueño` desktop use |
| **Focus** | `--color-focus-ring`, 3px, 2px offset, on `:focus-visible` | Never removed. `:focus-visible` (not `:focus`) so a tap doesn't draw a ring but Tab-navigation always does |
| **Active/pressed** | `--color-*-active` background shift | Immediate visual confirmation a tap registered — matters more on a screen with input lag |
| **Disabled** | `--color-*-disabled-*` tokens, `cursor: not-allowed`, `aria-disabled` | Exempt from AA text contrast by spec (§4.1); still checked and still legible |
| **Error** | `--color-danger` border + icon + Spanish message under the field | Never colour alone — the border width also changes (1px → 2px) |
| **Loading** | `aria-busy="true"`, label replaced by a spinner, same footprint, `pointer-events: none` | Same footprint is the point: a resized button during submit is how a redemption gets submitted twice (ARCHITECTURE §14) |

### 8.1 Stale data (R3, FUNCTIONAL-SPEC §5)

Exactly two tiers, matching the functional spec — no third tier is introduced here:

| Age of the cutoff date | Style |
| --- | --- |
| ≤ 7 days | `.badge--neutral` — `--color-text-muted` on `--color-surface-sunken`, regular weight |
| > 7 days | `.badge--warning` — `--color-warning` on `--color-warning-bg`, **bold**, with a warning icon |

The cutoff date is never rendered without one of these two badges next to the balance, on any
screen, in any report, in any export (ARCHITECTURE §13, R3). If a later flow needs finer-grained
staleness bands, that is a decision for `F1-02`, not one this system pre-empts.

### 8.2 Empty search result

No matches is not an error — it is the normal shape of "keep typing" or "this member does not
exist yet." Neutral tone, no `--color-danger`: *"No encontramos a nadie con ese nombre. Probá
con otro nombre o con menos letras."* An empty-state message is content owned by `F1-02`; the
style contract is: neutral badge/text tokens, never danger tokens, for a state that is not a
failure.

---

## 9. Dark mode mechanism

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

## 10. How this gets verified again

Contrast ratios in §4 were computed with the WCAG 2.1 relative-luminance formula, not read off a
colour picker: `L = 0.2126·R + 0.7152·G + 0.0722·B` (each channel linearised), then
`(L_lighter + 0.05) / (L_darker + 0.05)`. Any later colour change should be re-run through the
same formula before it ships — a value that "looks fine" at a monitor's default brightness can
still fail at counter distance under fluorescent glare, which is exactly the condition this
system is built for.

## 11. Consuming this in `F1-04c` and later

1. Load `tokens.css` before any page-specific CSS.
2. Build components from the primitives in it (`.btn`, `.input`, `.list-row`, `.badge`, `.card`)
   or from scratch — they are a starting point, not a mandate, but every colour, size and radius
   used must come from a `var(--…)` in this file. A literal hex code or pixel value in a Razor
   component is the same defect a literal `0.03` is in `Domain` (ARCHITECTURE §6): the number
   stops being something this document can be asked to defend.
3. Remove the template Bootstrap reference and `app.css` rules first (§3) — do not layer this
   system on top of them.
