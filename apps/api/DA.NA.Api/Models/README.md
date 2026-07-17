# Models

This folder is a placeholder for request and response models (DTOs) as the API grows.

## Current approach

Request models are currently defined as records at the bottom of each controller file,
co-located with the endpoints that use them. This works well while:

- Each model is only used by one controller
- Models are simple (few fields, minimal validation)
- The API is small enough that finding things by controller is intuitive

## When to move models here

Migrate a model to this folder when any of the following become true:

- **Shared across controllers** — if two controllers need the same request shape,
  a single source of truth here avoids duplication and drift.
- **Complex validation** — data annotation attributes (`[Required]`, `[MaxLength]`,
  `[Range]`, `[RegularExpression]`) are easier to manage and test in a dedicated file
  than buried at the bottom of a controller
- **Response shaping** — as the API matures, explicit response types (rather than
  anonymous objects) make contracts clearer.
- **The controller file is getting long** — co-location stops being an advantage
  when you have to scroll past 10 model definitions to read the action methods

## Suggested structure when the time comes

```
Models/
  Requests/
    CreateOrganisationRequest.cs
    UpdateOrganisationRequest.cs
    CreateUserRequest.cs
    ...
  Responses/
    OrganisationSummary.cs
    AssessmentDetail.cs
    ...
```
