# Authorization hardening

## Decision

Authorization combines global RBAC with resource ownership.

- `Staff` represents platform/system staff.
- `GardenOwner` is a global capability, but store operations still require ownership or an
  explicitly permitted store membership.
- Garden Staff is an `Accepted` `GardenStaffAssignment` scoped to one store, not a global role.
- Admin bypass is explicit and is never inferred from the numeric role mask.

## Resource rules

- Product and 3D operations: Admin, store owner, or Accepted Garden Staff of that store.
- Delivery list: owners/Admin see all store deliveries; Garden Staff sees assigned deliveries.
- Delivery status/detail: Garden Staff must be Accepted and be the delivery assignee.
- Delivery assignment: store owner or Admin.
- User status, roles, session revocation, and user audit history: Admin only.

## Session invalidation

JWT includes `token_version`. Authentication rejects the token when the user is inactive or
the claim differs from the database. Role/status/session changes increment the version and
revoke all active refresh tokens.

## Auditing

`authorization_audit_logs` records user role/status/session changes, store owner/staff
membership changes, and delivery assignment.

## Database rollout

Migration `AuthorizationHardening` adds:

- `users.token_version`
- `deliveries.assigned_staff_id`
- `authorization_audit_logs`

Creating this migration does not update a database. It must be applied explicitly through the
normal deployment migration process after review.
