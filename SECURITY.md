# Security Policy

## Supported versions

Zephyrus is pre-1.0. Only the latest commit on `main` receives security fixes.

## Reporting a vulnerability

**Please do not report security problems in public issues, discussions or pull
requests.**

Report them privately through GitHub:
[**Report a vulnerability**](https://github.com/MichaelGeranis/zephyrus/security/advisories/new).

Please include:

- what the problem is and which component it affects (API, web UI, an agent,
  the GitHub integration, …);
- steps to reproduce, or a proof of concept;
- the impact you expect, and any suggested fix.

Zephyrus is maintained by one person in their own time. The aim is to
acknowledge a report within 7 days and to agree a disclosure date with you once
a fix is understood. You will be credited in the advisory unless you prefer not
to be.

## Scope

Of particular interest:

- bypassing authentication or the role-based approval gates;
- forging or replaying GitHub webhook deliveries;
- reading or recovering stored code-host tokens;
- making an agent write outside what a reviewer would approve, for example
  through prompt injection in a feature prompt, issue or existing code.

Out of scope: vulnerabilities that require an already-compromised host or
configuration, and issues in third-party dependencies that are already publicly
known (please still let us know if Zephyrus is affected in a way that needs a
change here).

## Security model

How authentication, approval authority, webhook verification and secret
encryption work is documented in [ARCHITECTURE.md](ARCHITECTURE.md#authentication-and-approval-authority).
