# Product

<!-- impeccable:product-schema 1 -->

<!-- Written from the team's hackathon brief and the existing frontend, with the user's go-ahead to fill it in without an interview. Facts marked (inferred) are not yet confirmed by the team. -->

## Platform

web

## Users

- **Trade compliance or quality lead at a small Canadian manufacturer** (primary). They have to show where a product's materials come from so it can qualify for CUSMA tariff treatment, and they must answer customer and EU Digital Product Passport requests. Today the proof is scattered across invoices, mill test reports, supplier certificates and spreadsheets, some of them scanned or in French.
- **Buyer or customs reviewer** (secondary). They open a shared passport link or QR code on a phone and need to see what the manufacturer can stand behind, and what it cannot.
- **Hackathon judges** (event audience, Nov 27-29 2026). They see a 3 to 5 minute demo and judge whether the tool is credible, useful and safe with company data.

## Product Purpose

Origin Vault turns a manufacturer's own documents into evidence it can use. It reads the files, pulls out the fields that matter (origin, heat or lot number, supplier, certificate status), asks a person to confirm each one, and then shows, per product, which share of the bill of materials has proof of North American origin and which share does not. The same checked facts feed a shareable product passport.

Success means a compliance lead can see in under a minute which parts are holding back a CUSMA claim and what document would fix each one, and can send a buyer a passport without exposing anything unverified.

## Positioning

The data never leaves the company's control, and nothing becomes a "fact" until a person approves it with the source line in view. The tool shows evidence, never a verdict: it does not decide that a product qualifies under CUSMA, and it does not claim a regional value content figure it cannot support.

## Operating Context

- Desktop browser at work, often next to the source documents and an ERP export.
- Documents: PDF (native and scanned), XLSX and CSV. Some are in French.
- Review happens in batches: many fields are high-confidence and should be approvable in one action, the rest need a careful look at the source.
- Buyers open passports on a phone from a QR code on a label or a link in an email.

## Capabilities and Constraints

- Frontend: React 19 + TypeScript + Vite, plain CSS with tokens in `frontend/src/styles/theme.css`, light and dark themes.
- Backend (teammates): ASP.NET Core API, a worker with a PostgreSQL job queue, and a Python reader with OCR. API contract in `docs/api/openapi.json`. The frontend runs against an in-browser mock until the API is ready.
- Screens built: Products evidence, Review queue, Passports (plus the public passport view). Not built yet: Documents, Buyer access, Settings.
- HS codes and origin rules are suggestions to be checked, never decisions.
- An unknown value is shown as unknown, never as zero.
- Only verified values (or values reported as missing) may be shared on a passport.

## Brand Commitments

- Name: Origin Vault.
- Voice: plain, precise and calm. It states what is proven, what is not, and what to do next, without hype or legal certainty it doesn't have.

## Evidence on Hand

- A fictional event dataset ("Avonbrook Precision Works") provided by the sponsor. It is git-ignored and must never be committed or redistributed. The committed sample data (`src/data/sample.ts`) is synthetic.
- No real customers, testimonials or metrics exist. Do not invent them.

## Product Principles

1. Evidence over verdicts: show the proof and the gap, let the person decide.
2. A person approves every fact, with the source in view.
3. Unknown stays unknown.
4. Share only what is verified.
5. Company data stays under company control.

## Accessibility & Inclusion

WCAG 2.2 AA: text contrast at least 4.5:1 in both themes, full keyboard use, visible focus, reduced-motion support. French source documents are expected; the UI itself is English for now (inferred).
