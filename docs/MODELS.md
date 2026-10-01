# Models: catalog, tiers, recommendation and estimates

## Principles
- Only models whose weights download **without an account or licence gate** qualify. Gated repos (e.g. Gemma, Llama on Hugging Face) would force users to sign in, which breaks the "no account" promise. They can still be used through **Import model file**.
- Only permissive licences in the catalog (Apache-2.0, MIT). The licence is shown in the consent dialogue.
- Downloads come straight from the model publisher's official Hugging Face repository (or a well-known GGUF publisher if the official one has no GGUF). Wordwright never hosts weights.
- A model enters the catalog as `candidate`, and becomes `approved` only after the evaluation in `docs/EVAL.md`. Release builds show only `approved` entries; debug builds can show candidates.
- Quantisation: `Q4_K_M` by default (best size/quality balance for CPU).

## Hardware tiers (`Wordwright.Core.Hardware.TierClassifier`)

| Tier | Rule (first match wins) | What we recommend |
|---|---|---|
| `gpu` | A GPU with ≥ 6 GB dedicated memory | 4B-class model on the GPU |
| `cpu16` | Total RAM ≥ 16 GB and AVX2 | 4B-class model on the CPU |
| `cpu8` | Total RAM ≥ 8 GB and AVX2 | 2B-class model |
| `minimal` | Anything else | Tiny model, with a clear "basic grammar only, may be slow" warning; the user may also skip AI |

Also required at recommendation time: **available** RAM ≥ model's `ramRequiredGB` + 1 GB, and free disk ≥ size × 1.2. If not met, step down one tier and say why (copy in `UX_COPY.md`).

## Recommendation (`Recommender`)
1. Filter to `approved` entries listing the user's tier in `tiers` that pass the RAM and disk checks.
2. Pick the highest `evalScores.overall`; on a tie, the smaller file.
3. Offer the others under "Show other options", sorted by fit, each with its own estimate and a plain note if it is larger than recommended ("May be slow on this PC").

## Speed estimate (`SpeedEstimator`)
Two reference jobs, used both before download (catalog ranges) and after calibration (measured values):

| Job | Prompt tokens (incl. system + instruction) | Output tokens |
|---|---|---|
| One line (~20 words) | 140 | 30 |
| Short paragraph (~60 words) | 190 | 90 |

`seconds = promptTokens / promptTokensPerSec + outputTokens / genTokensPerSec`

Before download, compute with both ends of the catalog's `speed[tier]` ranges and show a range rounded to whole seconds ("2–4 seconds"). Show model loading separately ("The first rewrite after starting your PC takes about N seconds longer while the model loads") — that N is the tier's `loadSeconds` beside the two rates, provisional until calibration measures it. After calibration, show single numbers rounded to the nearest half second.

Worked example (2B model, `cpu8`, prompt 60–150 tok/s, generation 15–30 tok/s): one line ≈ 2–4 s, short paragraph ≈ 4–9 s.

## Catalog file: `models/models.json`
- `schemaVersion` changes only on breaking changes; the app ignores catalogs with a newer major schema and keeps its embedded copy.
- `catalogVersion` is a date string, bumped on every edit.
- Speed ranges start as **provisional** estimates and are replaced with numbers measured on real machines (see `EVAL.md`, part B).
- `sha256`, `sizeBytes`, `url` must be filled from the actual file before an entry can become `approved`. The downloader refuses entries with empty hashes.

### Better-model check
A catalog entry counts as "better" for the user when it is `approved`, fits their tier/RAM/disk, and either scores ≥ 0.3 higher on `evalScores.overall` (1–5 scale) than the active model, or scores the same and is ≥ 30% faster for the user's tier. Only then does the banner appear.

## Updating the catalog (maintainer workflow)
1. A weekly GitHub Action (`.github/workflows/model-watch.yml`, added in phase P9) lists recent GGUF releases from a watch-list of publishers and opens an issue titled "Candidate model: …". It never edits the catalog.
2. Maintainer adds the model as `candidate`, runs the eval, fills scores, hashes and measured speeds.
3. If it wins for at least one tier, set `approved`, bump `catalogVersion`, commit. Installed apps see it on their next check.
Expected cadence: every few weeks to a few months, not daily.
