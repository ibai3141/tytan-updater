# Documentation index

Updated: October 9, 2026. All guides are in English.

The documented delivery is the retained first package, TytanUpdater-win-x64-20261009-094714-e05a10. Its desktop loads an installation JSON or the example; its console accepts exactly two folder names and asks for the save location in a window. Subsequent automatic-configuration changes were reverted. The retained executable ZIP is unchanged.

## Main guides

| Audience | Markdown | Word | Coverage |
| --- | --- | --- | --- |
| Developers and technical reviewers | [Technical guide](technical-guide.md) | [Technical guide](technical-guide.docx) | Architecture, file/method responsibilities, actual source excerpts, inputs, auth, endpoints, comparison, downloads, packaging, results, integration limits |
| End users and customer support | [User guide](user-guide.md) | [User guide](user-guide.docx) | Extraction, desktop steps, installation-file example, PowerShell credentials, two-argument CLI, Save As, status meanings, troubleshooting |
| PHP developers | [PHP walkthrough](technical-walkthrough.md) | [PHP walkthrough](technical-walkthrough.docx) | Additional endpoint and shared-function source excerpts |

## Focused references

- [Hosting deployment and configuration](server.md)
- [Endpoint URL examples](uso.md)
- [Desktop workflow and development phases](desktop.md)
- [CLI syntax, credentials, and checks](cli.md)
- [Windows publishing and distribution](distribution.md)
- [Short instructions included in the original delivery](distribution-readme.txt)
- [Scope and responsibility contract](planteamiento.md)
- [Implementation status](fases.md)
- [Verification history and observed production results](verificacion.md)
- [Pending installation integration inputs](installation-contract.md)

## Regenerate Word copies

From the repository root:

```powershell
python -m pip install -r scripts/requirements-docs.txt
python scripts/export_technical_doc.py
```

The exporter generates the three Word documents listed above, with English metadata, styled headings, tables, code blocks, hyperlinks, and page numbers. Markdown is the editable source. Keep relative guide links together when sharing the complete documentation set.

Documentation is delivered separately from the preserved original ZIP. Publishing a new executable package is not needed to read or share these guides.
