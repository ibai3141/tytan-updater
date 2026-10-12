# Documentation index

Updated: October 12, 2026. All guides are in English.

The current delivery consists of the October 12 `.env` packages: TytanUpdater-win-x64-20261012-093409-401bd4 and TytanUpdater-win-x86-20261012-093353-e5e374. The desktop loads an installation JSON or the example. The console accepts two required folder names and an optional destination directory; omitting the destination opens Save As for newer packages. Older local release ZIPs and extracted folders have been removed.

A separate win-x86 package provides the same application workflow for 32-bit Windows, including the matching runtime. See [distribution instructions](distribution.md) to choose or publish the correct architecture.

New builds also support an optional `.env` beside both executables, with a credential-free `.env.example` template and environment-variable precedence. See [user configuration](user-guide.md#server-configuration).

The current source adds `TYTAN_API_BASE_URL` for both applications and an optional third CLI argument for the download directory. These features are included in the current packages. See [CLI instructions](cli.md).

## Main guides

| Audience | Markdown | Word | Coverage |
| --- | --- | --- | --- |
| Developers and technical reviewers | [Technical guide](technical-guide.md) | [Technical guide](technical-guide.docx) | Architecture, file/method responsibilities, actual source excerpts, inputs, auth, endpoints, comparison, downloads, packaging, results, integration limits |
| End users and customer support | [User guide](user-guide.md) | [User guide](user-guide.docx) | Extraction, desktop steps, installation-file example, PowerShell credentials, CLI destination parameter, server configuration, Save As, status meanings, troubleshooting |
| PHP developers | [PHP walkthrough](technical-walkthrough.md) | [PHP walkthrough](technical-walkthrough.docx) | Additional endpoint and shared-function source excerpts |

## Focused references

- [Hosting deployment and configuration](server.md)
- [Endpoint URL examples](uso.md)
- [Desktop workflow and run instructions](desktop.md)
- [CLI syntax, credentials, and checks](cli.md)
- [Windows publishing and distribution](distribution.md)
- [Short instructions included in the current delivery](distribution-readme.txt)

## Regenerate Word copies

From the repository root:

```powershell
python -m pip install -r scripts/requirements-docs.txt
python scripts/export_technical_doc.py
```

The exporter generates the three Word documents listed above, with English metadata, styled headings, tables, code blocks, hyperlinks, and page numbers. Markdown is the editable source. Keep relative guide links together when sharing the complete documentation set.

Full documentation is delivered separately from the current executable ZIPs. Publishing a new executable package is not needed to read or share these guides.
