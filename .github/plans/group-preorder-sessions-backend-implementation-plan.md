# Group Pre-Order Sessions API Implementation Plan

## Requirement

Implement `GET /guest/groups/{groupReservationId}/sessions` so only the group reservation owner can retrieve each participant's session, dish items, per-participant total, submission status, and the aggregate total.

## Scope and assumptions

- Reuse the existing `GetAllSessionsResponse`, `Participant`, and dish response models.
- Authenticate with the Cognito `sub` claim and independently validate ownership in the service, matching the existing owner-only delete operation.
- Return participant sessions belonging to the requested group, including an owner session if one exists, with the appropriate participant type.
- Keep existing user changes to `README.md` and `deployment_resources.json` intact; add only the necessary GET deployment method.

## Acceptance criteria

- The API Gateway route is registered and configured with Cognito authorization.
- Missing identity claims return 401; non-owners receive 403; missing groups/reservations receive the established not-found response.
- A successful response contains participant ids, types, names, item dish details and quantities, participant totals, submission flags, and the aggregate total.
- Focused service/handler tests cover successful mapping, authorization, and relevant failure paths.
- API documentation and OpenAPI response schema match the implemented response.

## Tasks

| Task | Owner | Status | Depends on |
|---|---|---|---|
| Add owner-authorized session-list service and GET handler; register API and deployment routes | Orchestrator | Done | — |
| Add focused service and handler unit tests | Restaurant Backend Test Writer | Done | Implementation |
| Review changed backend code for defects | Restaurant Backend Code Reviewer | Done | Implementation, tests |
| Run focused tests/build and record outcomes | Orchestrator | Done | Implementation, tests, review |

## Validation

- Run the focused `GuestSessionServiceTests` and endpoint handler tests through `dotnet test`.
- Build the API Lambda project or test project to verify compilation.
- Review the final backend diff and confirm the API route, Cognito configuration, response schema, and docs agree.

## Results

- Focused test run: 43 passed, 0 failed, 0 skipped.
- Syndicate deployment JSON parsed successfully and includes the Cognito-protected GET route.
- `git diff --check` passed.
- Code review: no actionable findings after correcting the response serializer to emit lower-camel JSON.
- Test/build output includes existing NuGet vulnerability warnings for dependencies; package upgrades were outside this feature's scope.
