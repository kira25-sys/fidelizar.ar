# Business rules — RN catalog

Ported from Octaviano's `Plan-vipbot.md`. Every rule the product implements traces back to a
number here, and every test that covers a rule names it.

**Two things changed when this became a product** (Plan §6):

1. The numbers below are the *default program* — the one running in the owner's own shop. In the
   product they are **per-business configuration**. No amount or percentage from this file may
   appear as a literal in code.
2. `status = 1` meaning "paid" is a fact about one specific POS. It belongs to the **POS
   adapter**, never to the engine.

---

## Active — accrual and redemption

| # | Rule | Notes for the product |
| --- | --- | --- |
| **RN-01** | 3% of every purchase is credited, **always, with no threshold condition** | Percentage → configuration. Confirmed with the owner 2026-07-25: the monthly target does **not** gate the accrual |
| **RN-02** | Calculation base: total invoiced | |
| **RN-03** | Only **paid** purchases count. Pending and voided do not | What counts as "paid" → POS adapter |
| **RN-04** | Credit does not expire with time. It is lost only through inactivity | Inactivity handling → RN-16..RN-23, configurable and **off by default** |
| **RN-05** | Free redemption: no minimum | Corrected by **RN-24** |
| **RN-24** | **A redemption never exceeds the balance.** No minimum still, but any amount above the available balance is rejected | Invariant I6. Decided 2026-08-06 |
| **RN-25** | A balance may go negative **only** through a system-generated `Ajuste`: a sale that was credited, then redeemed by the member, then voided. While negative, **new redemptions are blocked** and the manager is notified. No human action ever produces a negative balance | New in this product. Decided 2026-08-12 — see the note below |
| **RN-15** | Credit is treated as payable only when the member claims it | |

> **Why RN-25 exists.** RN-24 said flatly that negative balances do not exist, but one real
> sequence produces one anyway: a sale is credited, the member redeems that credit, and only then
> the sale is voided. The correcting `Ajuste` has nowhere to go. Refusing the `Ajuste` would mean
> the ledger no longer matches reality, and clamping it at zero would mean silently absorbing the
> difference. Both break the product's core claim — that the number is defensible.
>
> So the ledger records what actually happened, and the *consequence* is contained instead: no
> new redemptions until it recovers, and a person is told. The member is never shown a negative
> number as a debt they owe; they are shown that their balance is under review.

## Active — monthly target

| # | Rule | Notes for the product |
| --- | --- | --- |
| **RN-06** | Monthly target of $120.000, per **calendar month**, accumulated — not per ticket | Amount → configuration. Some programs have no target at all: it must be optional |
| **RN-07** | The target is **global across all branches** | Never filter totals by branch. A member spending $70.000 in one branch and $60.000 in another has met the target |
| **RN-22** | The 3% is credited **per paid sale, when the import runs** — the balance is current as of the last import. The manager may still **veto** a specific member's month | Decided 2026-08-12: accrual is written at import time, not at month end. The veto is implemented as an `Ajuste` reversing that member's month, recorded with author and reason (RN-21) |

## Active — member lifecycle

| # | Rule | Notes for the product |
| --- | --- | --- |
| **RN-10** | Re-entry starts from zero | |
| **RN-11** | Birthday: notice **2 days before**. Gift only to non-inactive members; the greeting goes to everyone | Only day and month matter — the year is ignored even when present |
| **RN-12** | Proactively surface the balance as a purchase hook | In this product that means the cashier's screen first, not messaging (Plan §8) |
| **RN-13** | Discretionary exception: the manager may "gift the month". **It is recorded** | Generalised by RN-21 |
| **RN-14** | Bank: name only, optional | |

## Active — grace streak (v4, 2026-08-06)

Replaces the old automatic expiry. **Configurable and off by default in the product**
(Plan §6): most businesses will not want points expiring at all.

| # | Rule |
| --- | --- |
| **RN-16** | A month below target does **not** lose the points: it starts a grace streak of up to **3 months**, with continuous notices to the member |
| **RN-17** | The bad-month counter advances **only** on months under $50.000 (including $0) |
| **RN-18** | A month between $50.000 and $119.999 **freezes** the streak: neither advances nor resets it |
| **RN-19** | **Only reaching $120.000 resets** the counter to zero |
| **RN-20** | On the third bad month the system **proposes** expiry. The manager executes it — **never the system on its own** |
| **RN-21** | End-of-month decisions belong to the manager: reset the counter, expire the points, deactivate the member, or skip the month's accrual. **All of them are recorded in the ledger** with who and why |
| **RN-23** | Freezing has a cap: past **3** consecutive frozen months, a frozen month counts as bad. Decided 2026-08-12 |

> Why the cap: without it, a member buying $60.000 every month freezes forever and never faces
> a decision. The threshold that moves the streak is $50.000 (RN-17/RN-18) and the last word is
> always the manager's (RN-20/RN-21).

## Replaced — do not implement

| # | Former rule | Replaced by |
| --- | --- | --- |
| **RN-08** | 1 month below target → AT RISK. 2 consecutive → INACTIVE and loses everything | RN-16..RN-23 |
| **RN-09** | Deactivation after 2 consecutive months with no purchase at all | RN-16..RN-23 |

> The rule that killed RN-08: applied literally, a real member would have gone INACTIVE in
> February and lost her whole balance while still being a regular customer. Automatic
> confiscation of something the member perceives as money destroys the program's credibility.
> Hence RN-20: the system proposes, a person decides.

---

## Resolved questions

Both former open questions were decided 2026-08-12:

1. **RN-23** — a frozen month counts as bad after **3** consecutive frozen months.
2. The default configuration for a **new** business ships with the grace streak **off** and
   **no monthly target** — the simplest possible program, only the accrual percentage. Target
   and grace are enabled per business if its owner wants them.
