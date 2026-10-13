# Native shared-history runtime probe

This standalone Python probe exercises the installed native `codex app-server`
using two entirely fresh `CODEX_HOME` directories. It never uses a saved profile,
real credentials, existing conversation, `turn/start`, or a model inference call.
All API/token/secret environment variables are removed for each child process;
the synthetic model provider and HTTP proxy point to loopback port 9. Generated
protocol schema, fixtures, RPC transcripts, SQLite files, and result JSON are kept
under the caller's new output directory.

Both account homes link `sessions` and `archived_sessions` to one raw-history
directory, and use one `CODEX_SQLITE_HOME`. With `--share-writer-locks`, both also
link `thread-writer-locks` to one directory. These junctions may require an
elevated fixture-only run on Windows.

```powershell
python tests/SharedHistoryRuntimeProbe/probe.py `
  --executable 'C:\path\to\installed\codex.exe' `
  --output 'C:\path\to\new\synthetic-evidence' `
  --share-writer-locks
```

Run once without `--share-writer-locks` as a comparison control. The probe checks:

- Native creation of a named, empty thread and cross-home metadata reads.
- Cross-home visibility of an owner metadata update.
- One physical raw-history file and databases only in the shared SQLite root.
- With shared lock directory: the second native resume is rejected for an active
  writer, and succeeds after the owner exits.
- Without shared lock directory: both native resumes report
  `canAcceptDirectInput=true`, demonstrating why sharing SQLite alone is unsafe.

The runtime version tested on 2026-10-13 was `0.162.0-alpha.17.2` bundled in
`OpenAI.Codex_26.1007.2314.0`. Its exclusive per-thread locks were physically
observed under `CODEX_HOME/thread-writer-locks`, not `CODEX_SQLITE_HOME`.

This proves local storage and idle writer exclusion for that native runtime. It
does not validate inference, active-turn handoff, live desktop follower UI,
automatic UI refresh, packaged desktop launch, account authentication, or future
runtime compatibility. The standalone app-server rejects a second writer; it
does not automatically become an execution follower in this probe.
