# Source status
The original recovered 0.1 baseline is preserved in Git history. Version 0.2 adds the marketing workspace, database migrations, native/portable build scripts and CI coverage.

An execution-environment outage interrupted a larger local checkpoint. Its feature work was reconstructed into feature/complete-marketing-workspace and verified through GitHub Actions before promotion. The implementation, tests and migrations now live in this repository; the old 0.1 ZIP is historical.

See FEATURE-MATRIX.md for scope and VALIDATION-V02.md for evidence. A source checkpoint is not a public cloud deployment or a signed production release.
