# Contributing

Contributions of code, tests, documentation, compatibility evidence, and reproducible
bug reports are welcome.

## Before you start

- Read the [Code of Conduct](https://github.com/purelogiccode/5DO/blob/master/CODE_OF_CONDUCT.md).
- Search [issues](https://github.com/purelogiccode/5DO/issues) and
  [discussions](https://github.com/purelogiccode/5DO/discussions).
- For a large change, open a discussion or issue first.
- Never include BIOS ROMs, game images, or extracted copyrighted assets.

## Development

```powershell
git clone https://github.com/purelogiccode/5DO.git
cd 5DO
dotnet build 5DO.sln
dotnet test 5DO.sln
dotnet format 5DO.sln --verify-no-changes
```

For emulation changes, include frame/audio/state hashes and update the relevant
compatibility or test-corpus evidence. See the
[Test strategy](https://purelogiccode.github.io/5DO/TestStrategy.html).

For documentation changes:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements-docs.txt
mkdocs build --strict
```

## Pull requests

Use the pull request template, keep the change focused, add tests and documentation,
and keep required CI checks green. See the full
[contributing guide](https://github.com/purelogiccode/5DO/blob/master/CONTRIBUTING.md)
and [developer guide](https://purelogiccode.github.io/5DO/DeveloperGuide.html).

## Security

Do not open a public issue for a vulnerability. Use GitHub's private vulnerability
reporting as described in the
[security policy](https://github.com/purelogiccode/5DO/blob/master/SECURITY.md).
