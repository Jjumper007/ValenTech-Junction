# Results

What AYA produced on the four challenge branches of
`te-industry-tech-office/junction26-messagepack-csharp`, in its last pass of 2026-11-15. These are the files a
reviewer would be handed. Nothing in them was edited by hand, with one exception stated below. `ju26-issue-2129`
was reviewed once more after a rule was corrected: a test about a new feature that "passes" on the old code, where
the feature does not exist, is no longer counted as a regression.

| Branch | Change | Read first |
|---|---|---|
| `ju26-issue-2297` | Marks generated code as excluded from coverage (237 files) | [summary](ju26-issue-2297/summary.md) |
| `ju26-issue-2283` | Fixes a crash on generic types from a referenced assembly (4 files) | [summary](ju26-issue-2283/summary.md) |
| `ju26-issue-2234` | Analyzer and `SuppressSourceGeneration` (4 files) | [summary](ju26-issue-2234/summary.md) |
| `ju26-issue-2129` | New feature: formatters for types of another assembly (28 files) | [summary](ju26-issue-2129/summary.md) |

In each folder:

- `summary.md`: the one screen to read before approving. What to look at, in order, and why.
- `report.md`: the whole ordered list, every measurement, what was not checked, and the sign-off block.
- `evidence.json`: every number the report quotes. The report names this file by its sha256.
- `pipeline.log`: what AYA did, step by step, with timings.
- `attacks/`, `probes/`, `proposed-tests/`: the code that models wrote, as it was built and run. Files named
  `K*.cs` are tests kept from an earlier review because they showed a regression, run again in this one. Files
  ending in `.rejected.cs` were refused and are kept for the record.

Next to them:

- `requirements/`: the text of each upstream issue, as given to AYA with `--req-file`.
- `reviews/`: what an independent review by reading and running found on each branch. AYA was not shown it.
  `reviews/tests/` holds the tests the reviewers wrote for their findings, and `reviews/arbiter/` what AYA's
  arbiter said of each when it ran them on the old and the new code.
- [`contrast.md`](contrast.md): those findings set against what AYA puts in front of the owner.

The exception: the folders of the machine that ran the reviews were replaced by `<home>` and `<aya>` in every
file, and the sha256 quoted in each report is that of `evidence.json` as published here.

The command behind each folder:

```
aya review --repo <clone> --head ju26-issue-<n> --req "MessagePack-CSharp#<n>" --req-file requirements/<n>.md \
    --mutate --compare --sweep --max-inputs 3000 --write-tests --attack --exhaustive --judge --max-reverts 8
```

What a model writes or believes differs from one run to the next; what AYA measures does not. A second run of
the same command gives the same tests, coverage, deliberate breaks, undone parts and sweep, and may give other
attack tests and other readings from the judge.
