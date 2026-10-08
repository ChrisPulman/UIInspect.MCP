# Release package signing

## Release versioning

The `BuildDeploy` workflow calculates the release version from the latest stable
`vX.Y.Z` tag, with a minimum baseline of `0.1.0`. A `minor` bump with channel
`none` creates the next stable minor version: from `v1.1.0`, it produces
`1.2.0` and tag `v1.2.0`. The workflow passes this calculated version directly
to MinVer during package creation and uses it for the GitHub release.

`BuildDeploy` signs the unsigned NuGet artifacts on `ubuntu-latest` using the
headless ssign PKCS11 module and jsign. Configure these secrets in the GitHub
`release` environment before dispatching a release:

- `CERTUM_USER_ID`: the Certum account email, passed to ssign as `CERTUM_EMAIL`.
- `CERTUM_OTP_URI`: the account's `otpauth://` TOTP URI, passed as `CERTUM_OTP`.
- `CERTUM_CERT_FINGERPRINT`: the signing certificate's SHA-256 fingerprint
  (64 hexadecimal characters; whitespace and colons are accepted).

The local `setup-ssign` action installs Java 21, ssign 0.1.7, and jsign 7.4.
Both downloaded signing tools are checked against pinned SHA-256 digests.
Authentication uses a private temporary runtime directory that is removed when
the signing step exits. Each package must pass `dotnet nuget verify` against the
configured certificate fingerprint before signed artifacts can be uploaded.

The signing setup is checked out at `github.workflow_sha`, the exact revision
that defines the running workflow. An optional `sourceRef` selects the source
built by the release job, whose resolved SHA is retained as the release target;
it does not change the signing action. This also permits releasing older source
revisions that predate the local signing action.

The signing flow and tool pins were adopted from
[ChrisPulman/CP.ReactiveUI.Primitives.Windows](https://github.com/ChrisPulman/CP.ReactiveUI.Primitives.Windows/tree/0754f7975d1b5b49f1d810caec946d1d0d622a36/.github)
at commit `0754f7975d1b5b49f1d810caec946d1d0d622a36`.
