# Implementation phases

Each phase ends with a build, appropriate checks, and a commit in Spanish, as requested by the user. The user handles pushing to the remote repository.

| Phase | Deliverable | Verification | Completed commit |
| --- | --- | --- | --- |
| 1. Foundation and versions | C# library, models, and numeric package selection by product | Invalid versions, different products, and version-based selection | `63ec306` |
| 2. HTTPS queries | API client with BasicAuth and JSON | Requests, fields, paths, and HTTP errors using a simulated server | `ea281cd` |
| 3. Downloads | Temporary ZIP, validation, and result returned to Tytan | Sample ZIP, interruptions, cancellation, and existing destinations | `63d7037` |
| 4. Usage and integration | CLI tool and C# integration example | Complete local execution and usage documentation | `585b658` |

The C# implementation uses .NET 9, available on the development machine, without external dependencies. The server implementation uses PHP 8.x. Tests run as a local executable that returns a nonzero exit code if any case fails. They do not need access to the production server.

Integration with Tytan is provided as an example and a contract: its application is not in this repository. Live endpoint verification is recorded separately from local tests and is only claimed when actually performed.

## Delivered work

- Phase 1: library, models, numeric version comparison, and product selection.
- Phase 2: HTTPS queries and tests for authentication, JSON, and errors. The live request returned HTTP 404; see [Verification record](verificacion.md).
- Phase 3: temporary downloads, size and ZIP readability checks, delivery to Tytan, and failure and concurrency tests.
- Phase 4: CLI commands `list`, `download`, and `demo`, usage instructions, and an integration example. Connecting the module to Tytan's actual application remains pending in its project.

Console help and application-defined result messages were subsequently translated into English in commit `bec6b20`.

## Phase 5: PHP endpoints after the scope correction

On October 6, the user clarified that this project must also create api.php and download.php. This phase adds those endpoints, common.php, an Apache configuration example, deployment instructions, and actual-PHP tests with C# integration. Local implementation is complete; hosting deployment and production verification remain pending. See [PHP server](server.md).
