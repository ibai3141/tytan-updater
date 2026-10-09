# Workspace cleanup

The cleanup script removes an explicit list of development artifacts: desktop/CLI bin and obj directories, release-build staging, extracted verification copies, temporary demo/diagnostic projects, abandoned Swagger tools, and generated development screenshots.

It preserves the complete dist directory (including both valid x64/x86 releases), downloads/Barcin_Wodbar, application/server source, tests, examples, documentation, scripts, and Git history. It does not remove actual customer downloads or run a blanket git clean.

From the repository root, preview the proposed paths:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/clean_workspace.ps1 -WhatIf
```

To apply that explicit cleanup:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/clean_workspace.ps1
```

The script resolves all targets inside the repository, rejects linked paths and Git-tracked content, and validates the entire list before deleting. It uses native PowerShell operations and literal paths. Close any application running from bin before cleanup. Subsequent dotnet build/run/publish commands regenerate their output directories as needed. Cleanup does not require rebuilding the retained portable executables in dist.

On October 9, the agent's automatic approval review rejected both the combined deletion and a deletion of the explicit release-builds directory, reporting only blocked by policy. No deletion was executed by the agent. The cleanup script is prepared for manual use; its existence does not mean the workspace has already been cleaned. The inventoried generated artifacts occupy approximately 2.2 GiB.
