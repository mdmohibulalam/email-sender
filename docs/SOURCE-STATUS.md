# Source baseline — 15 October 2026

This repository begins with the 0.1.0 source recovered from the previously saved CampaignStudio-0.1.0.zip. The archive passed ZIP integrity validation and matched its original SHA-256:

`09d0935dbc01ce356711a31d046e0d2fc166cfd32e382c39016e90c34e6832fe`

Later 0.2 development files were lost when the execution environment disconnected. They are not part of this commit. In particular, this baseline does not contain the later SMTP profiles/retries, migration recovery, block designer, or autosave work. Those improvements must be restored or reimplemented and verified before being claimed as available.

The historical 0.1 results in VALIDATION.md describe the original verification: 38 functional/integration checks and 10 browser checks. The JavaScript syntax check was repeated successfully during recovery. A .NET SDK was unavailable in the recovered environment, so the C# suite and Windows builds were not rerun locally for this push. GitHub Actions is configured to run functional checks and a Windows build; consult the actual run before treating those checks as passing.

Only source, sample data, build configuration, documentation, and tests are committed. Local databases, credentials, generated captures, and compiled portable artifacts are excluded. No live cloud deployment or real email sending was performed.
