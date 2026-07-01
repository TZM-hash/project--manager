# Project Audit Trail Design

## Goal

Record every meaningful project change so users can review who changed what, when it happened, and which fields or purchase records were affected.

## Approved Scope

The selected design is option 3: field-level project changes plus purchase child-record detail.

The first version is view-only traceability. It does not restore old versions, compare arbitrary historical snapshots, or export audit logs.

## Data Captured

Each audit entry stores:

- Actor user id and display name through the existing user relationship.
- Operation time.
- Operation type: create, update, delete, or progress update.
- Entity name and entity id for compatibility with the existing audit log.
- Project id and project number for direct project history queries.
- Human-readable summary.
- Structured JSON details containing field-level changes.

Project field changes include values such as:

- Project number.
- Project name.
- Parent case number.
- Year.
- Status.
- Closed year month.
- Progress percent.
- Project amount.
- Collection percent.
- Progress description.

Purchase request changes include:

- Added purchase request with request number, purchase type, staff, purchase amount, payment percent, paid amount, sub-case contact, and notes.
- Updated purchase request with changed fields and before/after values.
- Deleted purchase request with its last known summary values.

## User Experience

Project details pages show an "Operation Records" section below the existing project and purchase details. Each row shows time, actor, operation, and summary. Field changes are shown as readable before/after lines, including purchase request additions, updates, and deletions.

Administrators see audit history in admin project details. Workbench users see audit history for projects they are already authorized to open.

## Implementation Notes

The audit trail uses the existing `AuditLog` table and service. New nullable columns preserve compatibility with existing logs and keep older audit records readable.

Change comparison is centralized in audit helper types so page handlers do not duplicate diff logic. Page handlers take a snapshot before mutating project data, apply the existing save logic, then write an audit log with the generated change set.

Workbench progress updates log progress percent and progress description changes.

## Testing

Automated tests cover:

- Structured project audit entries persist project id, project number, summary, and JSON details.
- Project field diffs include before/after values.
- Purchase request add, update, and delete changes are captured.
- Workbench progress updates write progress audit entries.
- Project details smoke tests continue rendering after the audit section is added.
