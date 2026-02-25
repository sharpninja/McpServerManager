## Summary

- What changed:
- Why this change is needed:

## Compliance Boundary Checklist (MVP-APP-006)

Confirm each item before requesting review.

- [ ] No new ViewModel app logic was added (no filesystem/process/network/watcher/timer/composition logic in ViewModels).
- [ ] Commands/queries do not carry `ViewModel` references.
- [ ] CQRS handlers do not call `ViewModel.*Internal(...)` or access ViewModel private services/mediator state.
- [ ] Code-behind changes are UI-only (event wiring, control sync, layout/splitter persistence) or a documented exception is linked below.
- [ ] If a documented exception was used, I linked the rationale and removal plan: `docs/EXCEPTION-EVALUATION.md` or equivalent.
- [ ] Legacy project (`src/McpServerManager`) scope impact is stated (unchanged / intentionally touched with justification).

## Validation

- [ ] Desktop build tested (command + result)
- [ ] Android build tested (command + result)
- [ ] Manual smoke checks listed for impacted tabs/features

## Notes for Reviewers

- Compliance/audit docs updated (if applicable):
- Remaining known risks:

