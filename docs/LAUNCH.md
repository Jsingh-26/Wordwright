# Monetization and launch — decisions, research, checklist

Decided 2026-10-02 (PLAN.md → D12, D13). Two parts: how Wordwright is funded, and how people find it.

## Part 1 — Monetization decision (D12)

**Wordwright v1.0 launches free, with one-time "Support Wordwright" payments through one Razorpay Payment Page. No ads, no subscriptions, no monthly anything. A paid model is reconsidered only when v1.1+ features exist.**

The maintainer's constraints: one-time payments only · simplest possible setup · payout via UPI to an Indian account · no foreign tax forms.

| Option | Verdict |
|---|---|
| **Razorpay Payment Page** (chosen) | No code: dashboard → Payment Pages → "Customer Decides Amount". UPI for Indian payers, cards for international ones, payout to your bank in INR. One-time payments by nature. KYC is the standard Indian PAN-based check, done once — no W-8BEN-style tax forms. Fee ≈2% + GST domestic (international cards higher, needs activation). |
| GitHub Sponsors | Rejected: W-8BEN tax form is mandatory, payouts run through Stripe Connect, and the model is subscription-tiers-shaped. Fails three of the four constraints. |
| Ko-fi | Rejected as primary: **no UPI at all**; Indian payout only via PayPal (fees + RBI auto-withdrawal friction). Keep in the back pocket if international PayPal demand ever shows up. |
| Patreon / Buy Me a Coffee | Rejected: 10% / 5% fees, both subscription-shaped. |
| Ads | Rejected (see below). |

**Setup steps (maintainer, ~15 minutes, manual):** Razorpay dashboard → complete KYC → Payment Pages → create one with "Customer Decides Amount", enable UPI + cards → publish → copy the `razorpay.me/...` URL. Then P10.5 puts that URL in `.github/FUNDING.yml` (a Sponsor heart shows on the repo), the About-page link (`About.Support`, opens the browser — the app itself stays offline) and the README. Expect revenue near zero for months; this is a goodwill channel. Gifts this small need no tax ceremony, but if payments ever become material, one line with an accountant.

**Later, if real money is wanted:** a one-time **Pro unlock** of future on-device features (fill-in forms, per-app snippets — both already named v1.1 candidates). The Store takes 15% via its own commerce; using your own commerce keeps 100% but needs network code — the Store path keeps the app offline. Never gate the free tier's snippet count: "no cap" is the called-out differentiator.

### Why not ads (the rejected option, kept for the record)

- **No inventory.** Ad revenue is view-time × impressions × eCPM. Wordwright is designed to be invisible — the window opens rarely, the product lives in the tray. Optimistic maths (10,000 installs, 20% monthly actives, 4 window-opens each): ~8,000 impressions/month → **$4–20/month**. Realistically less. Banner eCPMs on desktop run $0.10–$2.80 with ~70% fill.
- **The platform is gone.** Microsoft's own Ad Monetization platform for Store apps shut down on 2020-06-01. What remains for .NET desktop is thin, and the bandwidth-selling SDKs in that space would not survive Store review or a user's trust.
- **It voids the differentiator.** The niche study's opening is private-and-offline ("no telemetry, sends nothing anywhere"). Ads are network + tracking. An app with a keyboard hook that phones an ad network home is the worst possible trust combination — and contradicts AGENTS.md hard rule 1.
- **Churn.** Banners in desktop utilities measurably raise churn (~18% vs ~11% 90-day churn in cited industry data).

## Part 2 — Launch and discovery

Sequenced so the one-shot moments happen after the app is proven stable. All platforms listed are free.

### Phase 0 — assets (with P10.0/P10.1)
The listing kit, made once, reused everywhere: tagline (≤60 chars, e.g. "Private, offline text expander for Windows"), 500-char description, square icon 512 px+, ≥2 screenshots at 1270×760, the hero illustration, and the README expansion GIF (extends to a 30–60 s screen recording for Product Hunt — the playground and the connected animation are the money shots).

### Phase 1 — release (P10.3, P10.4)
GitHub Release v1.0.0 and the Store publish. Store listing keywords: "text expander", "snippets", "typing"; README gets the "Get it from the Microsoft Store" badge and a privacy section (which doubles as the Store privacy-policy URL).

### Week 1 — directories (permanent, high-intent, do once)

Submit the day v1.0.0 is released (a listing without a working download is wasted); AlternativeTo and Softpedia take days to approve, so queueing them the moment the release is out loses nothing.

**Listing kit — paste-ready** (same voice as UX_COPY: plain, sentence case, no filler):

| Field | Value |
|---|---|
| Name | Wordwright |
| Publisher | Unbound Kite (unboundkite.com; Partner Center publisher name, Razorpay brand name) |
| Tagline (≤60 chars) | A private, offline text expander for Windows. |
| Short description (~120 chars) | Type ;sig anywhere and get your full signature. Free, open-source text expander for Windows — everything stays on your PC. |
| Long description (~480 chars) | Wordwright is a text expander for Windows: type a short trigger like ;sig or ;date and it expands into the full text, in any app — Word, Outlook, your browser, chat. It has built-in variables (date, time, clipboard, cursor position), search, import and export, and a first-run playground where you can try expansion inside the app itself. Wordwright is free and open source (MIT): no accounts, no sync, no telemetry, no AI in the cloud — your snippets never leave this PC. It sits quietly in the tray until you need it. |
| Category | Productivity → Text expansion / Utilities |
| Keywords | text expander, text expansion, snippets, autotext, text replacement, typing, clipboard, offline, privacy, Windows |
| Platforms | Windows 10 (19045+) / Windows 11, x64 |
| Licence / price | Free, open source (MIT); optional one-time support via Razorpay |
| Links | https://github.com/Jsingh-26/Wordwright · https://github.com/Jsingh-26/Wordwright/releases · (Microsoft Store badge after P10.4) |
| Images | square icon from `brand/icon-256.png`; screenshots from `scripts/ui-check-*.png`; hero from P10.0 |
| Author | Unbound Kite (Jsingh-26 on GitHub; the real name if a directory asks) |

**Per site** (each is an account + a form; ≈15 minutes each):
1. **AlternativeTo** — list Wordwright, then also click "suggest an alternative" *on the Text Blaze, TextExpander and PhraseExpress pages*. This is where people search "X alternative Windows offline".
2. **Softpedia** — their "Submit software" form. They check for bundleware; the clean MSIX/installer passes.
3. **SourceForge** — add the project; it powers the Slashdot alternatives pages on top.
4. **SaaSHub** — submit product form.
5. **Slant** — add Wordwright as an option with honest pros/cons on "What are the best text expanders for Windows?".
6. **Awesome-Windows** — a small PR to `github.com/Awesome-Windows/Awesome` adding one line under Productivity.

UTM-tag each link (`?utm_source=alternativeto` etc.) so the sources are comparable later. That is the whole of Week 1 — everything else waits until we see how it goes (D13).

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
