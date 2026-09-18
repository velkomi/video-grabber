# Backup and isolated recovery runbook

Backups and restore drills are limited to qualified stage/test names. Backup-Platform.ps1 accepts only vg-stage-*; Restore-PlatformDrill.ps1 accepts only vg_test_restore_*. Neither script accepts a production destination name.

The business database is captured with PostgreSQL 17 pg_dump custom format from the digest-pinned PostgreSQL image. The dump is hashed, pg_restore --list is recorded, and the dump is stored in a restic 0.18.0 repository using an external password file. Passwords and DSNs are read from protected file references and are not written into evidence.

A restore drill restores into a new disposable database, never over the source database. It then runs the application ConsistencyReport. Required zero-violation checks include negative buckets, grant/ledger mismatch, missing completed artifacts, artifact/account/fence mismatch, invalid terminal reservations, duplicate purchase references, payment projection gaps, invalid subscription state and duplicate identity binding.

Protected signing and lease keys are a separate recovery scope and are not recreated from the database. If required protected key material is absent, affected services remain fail-closed. Restoring the DB does not authorize replaying purchases from client history; pending payment states are reconciled from provider evidence only after external connectivity is separately authorized.

RPO target is <=15 minutes and RTO target is <=60 minutes. These are targets only; restore evidence records actual timestamps. Existing neighboring databases and user media directories are not touched.


## Latest local restore drill

On 2026-09-18 an isolated recovery fixture was migrated through 18 schema migrations, populated with accounts, identities, credit ledger/reservations, device/offline lease, completed job/artifact, delivery_unknown, succeeded/refunded payments and a canceled recurring subscription. ConsistencyReport returned zero violations before backup.

A PostgreSQL 17 custom-format dump was stored in an encrypted restic 0.18.0 repository, restic check and pg_restore --list passed, and snapshot 192d8f6b5030… was restored into vg_test_restore_p7t2. Source and restore matched across 15 table groups plus all 18 migrations. The restored dump SHA-256 matched the source dump (fe7634791114cc318edbc9a3fe3dab469e5283cbb3b9ebc4a30014e4bfdeeafc), and restored ConsistencyReport remained zero. Snapshot completion to validated restore was 2.579 minutes, below the 15-minute RPO-age and 60-minute RTO targets for this isolated drill.
