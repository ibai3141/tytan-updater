# Using the deployed endpoints

Updated: October 6, 2026.

Use a browser for manual checks or an HTTP client with BasicAuth for application integration. When the browser requests credentials, enter the account configured by the hosting administrator. No credentials are stored in this document.

## URLs

| Operation | URL |
| --- | --- |
| List clients | https://tytan.poznan.pl/SQLupdate/api.php |
| List Barcin_Wodbar packages | https://tytan.poznan.pl/SQLupdate/api.php?dir=Barcin_Wodbar |
| Download Faktury | https://tytan.poznan.pl/SQLupdate/download.php?file=Barcin_Wodbar/Faktury_008.000.043.zip |
| Download FK2025 | https://tytan.poznan.pl/SQLupdate/download.php?file=Barcin_Wodbar/FK2025_005.005.007.zip |
| Download FK2026 | https://tytan.poznan.pl/SQLupdate/download.php?file=Barcin_Wodbar/FK2026_005.005.040.zip |

For another folder or package, use the path returned by the listing. Encode query parameter values with the HTTP client's URL encoder, especially names containing spaces or ampersands. Do not send absolute filesystem paths.

## Manual acceptance

1. Open api.php and confirm client folders are listed.
2. Open api.php?dir=Barcin_Wodbar and confirm the available ZIP metadata.
3. Open download.php with one returned path and confirm a download occurs.
4. Compare the downloaded byte count to size in the listing and inspect the ZIP locally.
5. Confirm that a request without authentication is rejected, using a fresh session or a client without an Authorization header; an existing browser may reuse credentials.

The user has supplied successful results for steps 1 and 2 and reported that downloading works. Exact downloaded size and archive integrity were not supplied as production evidence.

## Application integration

Tytan queries its client folder, selects the highest version for the requested product, compares it to its installed version, and downloads through download.php when newer. The application should check HTTP failures and downloaded integrity before installation. Tytan applies the package and records its installed version only after installation succeeds.

The repository no longer includes a C# CLI or demo. Tests use Python and actual PHP; see server.md.
