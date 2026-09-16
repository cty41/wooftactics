# Pure Run Artwork Contract Registry

This directory is the versioned, machine-readable state authority for artwork production. ImageGen is an external non-deterministic step between `create-job` and `ingest`; every local transition is deterministic and hash-bound.

The simplified approval branch is `ready -> ingested -> prepared -> annotated -> review_pending -> approved -> promoted`; `rejected` and `technical_failed` are terminal branches and never lead to promotion. The implementation also has calibrated and Model Review intermediate states, while Job, Feedback and Series maintain separate projections. Continue a failed attempt with `retry`, never by editing or promoting it.

Registry groups:

- `contracts/`: asset type, direction, pose, anchor, tolerances, outputs and rights.
- `jobs/` and `packets/`: canonical prompts, input roles and hashes. Identical inputs produce the same job ID.
- `attempts/`: state and immutable artifact/report references.
- `approvals/`: candidate and semantic-mask hashes plus explicit reviewer receipt.
- `legacy-assets.json`: complete historical PNG inventory. `legacy-unresolved` records have no inferred lineage and cannot be anchors.
- `artifacts/`, `reports/`, `reviews/`: hash-bound working outputs. Review images are evidence, not formal assets.

Semantic masks are same-size RGBA PNGs using exact label colors: core `#ff0000`, head appendage `#ffff00`, near/far hand `#00ff00/#00c800`, near/far foot `#0080ff/#0000ff`, equipment `#ff00ff`, wings `#00ffff`, and effect `#ff8000`. Transparent pixels must be `(0,0,0,0)`.

Run the CLI from the repository root:

```powershell
python .agents/skills/pure-run-artwork-pipeline/scripts/artwork_pipeline.py --root . check --strict
```

Current production progress must be read from the immutable Series/Attempt/Approval records and the synced OKF summary rather than a static example in this README. The Demonbound series now contains promoted, active and pending poses; older failed/prepared attempts remain historical evidence and do not become mother images automatically.
