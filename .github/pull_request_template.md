## Summary

Describe the problem and the focused change that addresses it.

## Verification

List the commands or checks you ran.

## Checklist

- [ ] Tests cover behavior changes.
- [ ] Public API or setup changes are documented, and CHANGELOG `[Unreleased]`
      is updated for user-visible changes.
- [ ] Unknown or unpriced models still surface an explicit status, never a
      silent `0`.
- [ ] Price changes are appended as a new `ValidFrom` entry, not edited in
      place.
