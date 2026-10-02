# Monetization and launch — decisions, research, checklist

Decided 2026-10-02 (PLAN.md → D12). Two parts: how Wordwright is funded, and how people find it.

## Part 1 — Monetization decision (D12)

**Wordwright v1.0 launches free, supported by donations: GitHub Sponsors (primary) + Ko-fi (secondary). No ads. A paid model is reconsidered only when v1.1+ features exist.**

### Why not ads (the rejected option, kept for the record)

- **No inventory.** Ad revenue is view-time × impressions × eCPM. Wordwright is designed to be invisible — the window opens rarely, the product lives in the tray. Optimistic maths (10,000 installs, 20% monthly actives, 4 window-opens each): ~8,000 impressions/month → **$4–20/month**. Realistically less. Banner eCPMs on desktop run $0.10–$2.80 with ~70% fill.
- **The platform is gone.** Microsoft's own Ad Monetization platform for Store apps shut down on 2020-06-01. What remains for .NET desktop is thin, and the bandwidth-selling SDKs in that space would not survive Store review or a user's trust.
- **It voids the differentiator.** The niche study's opening is private-and-offline ("no telemetry, sends nothing anywhere"). Ads are network + tracking. An app with a keyboard hook that phones an ad network home is the worst possible trust combination — and contradicts AGENTS.md hard rule 1.
- **Churn.** Banners in desktop utilities measurably raise churn (~18% vs ~11% 90-day churn in cited industry data).

### The donation setup

| Platform | Fee | Why / why not |
|---|---|---|
| **GitHub Sponsors** (primary) | **0%** from personal sponsors (orgs pay ≤6%) | Repo-native: `FUNDING.yml` shows a Sponsor button; audiences here already have GitHub accounts; USD via Stripe Connect, monthly payout on the 22nd (60-day probation after the first sponsorship); needs 2FA + tax form (W-8BEN for non-US). Supported in ~100 regions including India. |
| **Ko-fi** (secondary) | **0%** on one-time donations; 5% on memberships/shop unless Gold ($12/mo) | For supporters without a GitHub account; pays directly into your Stripe/PayPal, no platform balance or threshold; donors need no account. |
| Buy Me a Coffee | 5% flat | Redundant next to two 0% options — skip. |
| Patreon | 10% for pages created after 2025-08-04 (+ processing + 2.5% currency conversion) | Built for recurring content creators with posts and tiers; a utility app has no content cadence — skip. |

**Setup steps (maintainer, manual):** enable GitHub Sponsors on the personal account (Stripe Connect, W-8BEN); create a Ko-fi page; then P10.5 wires `.github/FUNDING.yml`, the About-page link (`About.Support`, opens the browser — the app stays offline) and the README section. Expect donation revenue near zero for months; it is a goodwill channel, not income. Donations are taxable income in most jurisdictions — worth one line with an accountant if they ever become material.

**Later, if real money is wanted:** a one-time **Pro unlock** of future on-device features (fill-in forms, per-app snippets — both already named v1.1 candidates). The Store takes 15% via its own commerce (non-game apps may use their own commerce and keep 100%, but that needs network code — the Store path keeps the app offline). Never gate the free tier's snippet count: "no cap" is the called-out differentiator.

## Part 2 — Launch and discovery

Sequenced so the one-shot moments happen after the app is proven stable. All platforms listed are free.

### Phase 0 — assets (with P10.0/P10.1)
The listing kit, made once, reused everywhere: tagline (≤60 chars, e.g. "Private, offline text expander for Windows"), 500-char description, square icon 512 px+, ≥2 screenshots at 1270×760, the hero illustration, and the README expansion GIF (extends to a 30–60 s screen recording for Product Hunt — the playground and the connected animation are the money shots).

### Phase 1 — release (P10.3, P10.4)
GitHub Release v1.0.0 and the Store publish. Store listing keywords: "text expander", "snippets", "typing"; README gets the "Get it from the Microsoft Store" badge and a privacy section (which doubles as the Store privacy-policy URL).

### Week 1 — directories (permanent, high-intent, do once)
- **AlternativeTo** — list Wordwright, then suggest it as an alternative *on the Text Blaze, TextExpander and PhraseExpress pages*. This is where people search "X alternative Windows offline".
- **SourceForge, Softpedia, SaaSHub, Slant** — catalogue listings.
- PR to **Awesome-Windows/Awesome** — permanent high-authority inbound link.
- UTM-tag each link so the sources are comparable later.

### Week 2–3 — community
- **Show HN** (Tue–Thu, morning ET): "Show HN: Wordwright – private, offline text expander for Windows". The open-source + no-telemetry angle fits HN; stay in the comments the whole day. Windows-only dampens HN somewhat — expect modest, not viral.
- **Reddit**: r/software, r/Windows11, r/productivity, r/opensource. Read each sub's self-promotion rules, disclose authorship, and prefer genuinely answering existing "which text expander?" threads over cold posts.
- **dev.to / personal blog**: the build story — including the honest "we built on-device AI rewriting, then parked it" arc. The most persuasive content this project has is its restraint.

### Week 4 — Product Hunt (one shot — do it last, once stable)
- Self-launch (no paid "hunter" needed; PH says so explicitly). Create a personal account, Submit → New Product, schedule up to a month ahead.
- Go live **12:01 AM PST, Tuesday–Thursday**; the leaderboard runs on a 24-hour PST cycle. Check what else launches that day.
- Fields: name only; tagline ≤60 chars; description ≤500 chars; up to 3 topics; square thumbnail; ≥2 gallery images 1270×760; optional YouTube video (~half of Product-of-the-Day winners have one); pricing "Free"; **maker's first comment** (~70% of top products have one).
- On the day: respond to every comment; comments outweigh upvotes in the ranking. Never ask for upvotes — PH flags voting rings.
- Expectation-setting: PH skews SaaS/Mac, so a Windows utility rarely cracks the top 5. The real yield is the permanent listing, backlinks, and a few hundred downloads — still worth the day.
- Same assets, free, smaller: **Uneed, MicroLaunch, DevHunt, Fazier, BetaList**.

### Ongoing
Slant and Reddit "best text expander?" threads are what AI assistants cite — being present there is the new SEO. Every genuine answer is a permanent listing.
