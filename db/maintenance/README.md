# Maintenance jobs

Register per environment once the instance is provisioned:

- Index maintenance + statistics update: Ola Hallengren `IndexOptimize` weekly
  (rebuild/reorg thresholds 30/5), stats update nightly.
- Backups (spec 09B): full nightly, differential every 6h, log every 5 minutes
  (RPO <= 5 min). Restores tested monthly.
- Ledger reconciliation: run `ledger.usp_Ledger_VerifyBalanced @ThrowOnMismatch = 1`
  nightly; a failure is a release-gating finance alert.

Scripts land here as environments are provisioned; they are not part of
`deploy.sh`.
