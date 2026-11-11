# ADR-0028: A New Incident May Name Any Reportable Device (QR Scan)

**Date**: 2026-11-12
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Every device gets a QR label that links to `{App:PublicBaseUrl}/scan/asset/{id}`. IT staff who scan it open the device. Employees who scan it should report a fault on that device. Until now a new incident could name a device only when it was assigned to the requester (`CreateTicket.cs`, UX-10). Shared devices, such as a printer or a meeting-room screen, and devices assigned to a colleague could not be named, so a scan from them would lose the device.

The incident form must show which device was scanned. Until now employees could not read any asset that was not assigned to them: the asset endpoints are for IT staff and admins only.

Commit: `117a084`.

## Decision

- A new incident may name any existing device whose status is `InStock`, `Assigned` or `Maintenance` (`Asset.CanBeReported`). `Lost` and `Retired` devices, and unknown ids, are refused with `tickets.asset_not_reportable` ("Bu cihaz için arıza kaydı açılamaz.").
- Only creation changes. Linking a device to an existing ticket (`Links.cs`) keeps the assigned-to-me rule for employees.
- `GET /api/v1/scan/assets/{id}` is open to every signed-in user. It returns only `id`, `assetTag`, `type`, `brand`, `model` and `canReport`. It does not return the assignee, location or notes. An unknown id returns 404.
- The "İlgili cihaz" list on the incident form still lists only the requester's own devices. Other devices arrive only through a scanned link.

## Alternatives Considered

### Alternative 1: Keep the assigned-to-me rule
- **Pros**: No rule change; employees see only their own devices.
- **Cons**: Shared and colleagues' devices cannot be reported from their label; the employee must describe the device in text and IT must find it.
- **Why not**: The owner wants every employee to report a fault from any device's label.

### Alternative 2: Relax linking on existing tickets too
- **Pros**: One rule everywhere.
- **Cons**: A larger change to `Links.cs` and its tests; employees could attach any device to any of their tickets after the fact.
- **Why not**: The owner limited the change to creation.

### Alternative 3: A public device page without sign-in
- **Pros**: A scan works on any phone without a session.
- **Cons**: Exposes inventory data to anyone who finds or photographs a label.
- **Why not**: The scan link goes through the normal sign-in and permission checks.

## Consequences

### Positive
- A scan from any reportable device leads to an incident linked to that device; IT sees the device on the ticket.
- Existing labels need no stored data: the link is derived from the asset id.

### Negative
- Any signed-in user who knows an asset id can read its tag, type, brand and model, and name it on a new incident. The owner accepts this: the data is not sensitive, ids are uuid v7 and not guessable in practice, and the link is audited (`TicketAssetLinked`).
- Scans themselves are not audited.

### Risks
- An employee can name a device that is not theirs on purpose. The timeline and audit show who linked it; IT can unlink it.
