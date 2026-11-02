## Summary

<!-- What problem does this change solve? Keep it focused. -->

## Related issue

<!-- Fixes #123, refs #456, or "none". -->

## Type of change

- [ ] Bug fix
- [ ] New feature
- [ ] Compatibility improvement
- [ ] Documentation
- [ ] Build / CI / packaging
- [ ] Refactor or cleanup

## Approach

<!-- Why this implementation? Note important tradeoffs or invariants. -->

## Verification

<!-- Commands, tests, hashes, frame counts, and real-content evidence. -->

```text
dotnet build 5DO.sln -c Release
dotnet test 5DO.sln -c Release
mkdocs build --strict   # if docs changed
```

## Checklist

- [ ] Tests cover the behavior change, or the change is documentation-only.
- [ ] Documentation and the changelog are updated when behavior changes.
- [ ] `dotnet format 5DO.sln --verify-no-changes` passes.
- [ ] Trimmed/AOT paths were considered for JSON, settings, and CLI output changes.
- [ ] No BIOS ROMs, game images, or extracted copyrighted assets are included.
- [ ] New third-party code or data is license-compatible and attributed.
- [ ] I have read the contributing guide and code of conduct.
