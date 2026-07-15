---
type: project
created: 2026-05-25
updated: 2026-07-15
---

# Project Conventions

## Git Workflow
- Always create a new dedicated branch for major code changes.
- Branch name format should follow: `feature/[task-slug]` or `fix/[bug-slug]`.

## Production & Deployment Checklist
CRITICAL: Every production deployment must verify and satisfy these layers:

1. **WinForms Build**: Release mode compilation, null reference warnings resolved.
2. **Database & Storage**: Local SQLite `tesa_hsu2000.db` integrity and migrations.
3. **Environment**: Correct paths for local AppData and watch folders.

*Trigger Rule:* Automatically invoke this checklist during any workflow targeting production deployment, architecture planning, or system audits.