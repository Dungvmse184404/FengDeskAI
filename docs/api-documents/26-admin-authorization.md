# Admin authorization and delivery assignment

## Admin user management

All endpoints require `AdminOnly`.

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/admin/users` | List users with `page`, `pageSize`, `query`, `role`, `isActive` |
| GET | `/api/admin/users/{id}` | Get one user and current roles |
| PATCH | `/api/admin/users/{id}/status` | Activate or lock an account |
| PUT | `/api/admin/users/{id}/roles` | Replace all roles |
| POST | `/api/admin/users/{id}/revoke-sessions` | Revoke access and refresh sessions |
| GET | `/api/admin/users/{id}/audit-logs` | Read authorization audit history |

Status request:

```json
{ "isActive": false, "reason": "Policy violation" }
```

Role request:

```json
{ "roles": ["Customer", "GardenOwner"], "reason": "Store access approved" }
```

Clients must send individual role names, not a numeric bit mask. The API prevents an Admin
from locking or removing its own Admin role and prevents disabling the final active Admin.

Role, status, and session changes increment `users.token_version` and revoke active refresh
tokens. A JWT with a stale version is rejected on its next request.

## Delivery assignment

`PUT /api/orders/deliveries/{deliveryId}/assignee`

```json
{ "staffId": "guid" }
```

Only the store owner or Admin can assign a delivery. The target user must have an `Accepted`
Garden Staff assignment at the same store.

Garden Owner/Admin can list all store deliveries. Garden Staff only sees deliveries assigned
to that user and can only read/update the assigned delivery.

## Resource authorization

Product and 3D operations remain available to the store owner, Admin, and Accepted Garden
Staff of the same store. Resource handlers verify the route resource before the controller
action, while service guards remain the second enforcement layer.
