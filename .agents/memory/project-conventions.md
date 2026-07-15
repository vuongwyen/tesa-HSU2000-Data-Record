---
type: project
created: 2026-05-25
updated: 2026-07-08
---

# Project Conventions

## Git Workflow
- Always create a new dedicated branch for major code changes.
- Branch name format should follow: `feature/[task-slug]` or `fix/[bug-slug]`.

## Production & Deployment Checklist
CRITICAL: Every production deployment must verify and satisfy these architectural layers strictly:

1. **Frontend**: Production asset optimization, build sanity, and environment variables verification.
2. **APIs & Backend Logic**: Endpoint integrity, error boundary handling, and performance validation.
3. **Database & Storage**: Schema migration safety, backup state, and connection pool tuning.
4. **Auth & Permissions**: Token validation, session lifecycle rules, and role-based access control.
5. **Hosting & Deployment**: Target environment configuration, infrastructure health, and port mapping.
6. **Cloud & Compute**: Resource limits, instance metrics, and server lifecycle hooks.
7. **CI/CD & Version Control**: Automated test passes, branch protection alignment, and deployment log hooks.
8. **Security & RLS (Row Level Security)**: Data isolation logic, encryption at rest/transit, and API security headers.
9. **Rate Limiting**: IP throttling thresholds, DDoS mitigation, and brute force protection.
10. **Caching & CDN**: Static asset distribution, cache-control header policies, and edge invalidation rules.
11. **Load Balancing & Scaling**: High-availability routing, health check endpoints, and horizontal scaling limits.
12. **Error Tracking & Logs**: Centralized logging runtime, production alert systems, and tracer setups.
13. **Availability & Recovery**: Failover cluster state, disaster recovery playbooks, and uptime health check loops.

*Trigger Rule:* Automatically invoke this checklist during any workflow targeting production deployment, architecture planning, or system audits.