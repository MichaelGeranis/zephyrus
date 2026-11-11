# Task Breakdown: as-a-user-of-zephyrus-ui-i-need-to-be-able-to-delete-project

Total tasks: 14

## Task 1: Configure cascade delete relationships in Entity Framework
**Agent:** DB

## Description
Configure Entity Framework Core relationships between Projects, Features, and Artifacts to support cascade delete operations.

## Implementation Details
- Update `ApplicationDbContext` to configure cascade delete for Feature → Project relationship
- Update `ApplicationDbContext` to configure cascade delete for Artifact → Feature relationship
- Create and apply EF Core migration named `ConfigureCascadeDeleteRelationships`
- Ensure foreign key constraints properly cascade deletions

## Acceptance Criteria
- [ ] Feature.ProjectId foreign key configured with CASCADE DELETE
- [ ] Artifact.FeatureId foreign key configured with CASCADE DELETE
- [ ] Migration file created and can be applied successfully
- [ ] Database schema updated to support cascading deletions
- [ ] No breaking changes to existing relationships

## Task 2: Create DeletionSummaryDto for deletion confirmation
**Agent:** BE

## Description
Create DTO classes to represent deletion summary information that will be shown in confirmation dialogs.

## Implementation Details
- Create `ProjectDeletionSummaryDto` with ProjectName, FeatureCount, ArtifactCount properties
- Create `FeatureDeletionSummaryDto` with FeatureName, ArtifactCount properties
- Place DTOs in Api layer following existing conventions
- Include proper validation attributes and documentation

## Acceptance Criteria
- [ ] `ProjectDeletionSummaryDto` class created with required properties
- [ ] `FeatureDeletionSummaryDto` class created with required properties
- [ ] DTOs follow existing naming and structure conventions
- [ ] Properties have appropriate data types (string, int)
- [ ] DTOs are placed in correct namespace within Api layer

## Task 3: Implement DeleteArtifactUseCase
**Agent:** BE

## Description
Create use case class to handle individual artifact deletion business logic.

## Implementation Details
- Create `DeleteArtifactUseCase` class in Application layer
- Implement constructor with `IArtifactRepository` dependency
- Add `ExecuteAsync(int artifactId)` method
- Validate artifact exists before deletion
- Call repository delete method
- Handle and wrap any database exceptions
- Return appropriate success/error responses

## Acceptance Criteria
- [ ] `DeleteArtifactUseCase` class created in Application layer
- [ ] Constructor properly injects `IArtifactRepository`
- [ ] `ExecuteAsync` method validates artifact existence
- [ ] Method calls `IArtifactRepository.DeleteAsync()`
- [ ] Proper error handling for not found scenarios
- [ ] Database exceptions wrapped in appropriate business exceptions
- [ ] Method returns success indicator or throws business exception

## Task 4: Implement DeleteFeatureUseCase with deletion summary
**Agent:** BE

## Description
Create use case class to handle feature deletion with cascading artifact deletion and summary generation.

## Implementation Details
- Create `DeleteFeatureUseCase` class in Application layer
- Implement constructor with `IFeatureRepository` and `IArtifactRepository` dependencies
- Add `GetDeletionSummaryAsync(int featureId)` method to count associated artifacts
- Add `ExecuteAsync(int featureId)` method for actual deletion
- Use EF Include() to load feature with artifacts for summary
- Validate feature exists before operations
- Handle cascade deletion through EF configuration

## Acceptance Criteria
- [ ] `DeleteFeatureUseCase` class created in Application layer
- [ ] Constructor properly injects required repositories
- [ ] `GetDeletionSummaryAsync` returns feature name and artifact count
- [ ] `ExecuteAsync` method validates feature existence
- [ ] EF Include used to load related artifacts for summary
- [ ] Cascade deletion handled by EF configuration
- [ ] Proper error handling for not found scenarios
- [ ] Methods return appropriate DTOs or success indicators

## Task 5: Implement DeleteProjectUseCase with deletion summary
**Agent:** BE

## Description
Create use case class to handle project deletion with cascading feature and artifact deletion and summary generation.

## Implementation Details
- Create `DeleteProjectUseCase` class in Application layer
- Implement constructor with `IProjectRepository`, `IFeatureRepository`, `IArtifactRepository` dependencies
- Add `GetDeletionSummaryAsync(int projectId)` method to count associated features and artifacts
- Add `ExecuteAsync(int projectId)` method for actual deletion
- Use EF Include() to load project with features and artifacts for summary
- Validate project exists before operations
- Handle cascade deletion through EF configuration

## Acceptance Criteria
- [ ] `DeleteProjectUseCase` class created in Application layer
- [ ] Constructor properly injects required repositories
- [ ] `GetDeletionSummaryAsync` returns project name, feature count, and artifact count
- [ ] `ExecuteAsync` method validates project existence
- [ ] EF Include used to load related features and artifacts for summary
- [ ] Cascade deletion handled by EF configuration
- [ ] Proper error handling for not found scenarios
- [ ] Methods return appropriate DTOs or success indicators

## Task 6: Add artifact deletion endpoints to ArtifactsController
**Agent:** BE

## Description
Add DELETE endpoint to ArtifactsController to handle individual artifact deletion.

## Implementation Details
- Add `DELETE /api/artifacts/{id}` endpoint to existing ArtifactsController
- Inject `DeleteArtifactUseCase` in controller constructor
- Implement endpoint to call use case and return appropriate HTTP responses
- Handle 404 Not Found for non-existent artifacts
- Handle 500 Internal Server Error for database failures
- Return 200 OK with success message on successful deletion

## Acceptance Criteria
- [ ] `DELETE /api/artifacts/{id}` endpoint added to ArtifactsController
- [ ] Controller constructor updated to inject `DeleteArtifactUseCase`
- [ ] Endpoint calls use case `ExecuteAsync` method
- [ ] Returns 404 Not Found when artifact doesn't exist
- [ ] Returns 500 Internal Server Error for database exceptions
- [ ] Returns 200 OK with `{"message": "Artifact deleted successfully"}` on success
- [ ] Proper async/await pattern used
- [ ] Exception handling follows existing controller patterns

## Task 7: Add feature deletion endpoints to FeaturesController
**Agent:** BE

## Description
Add DELETE endpoints to FeaturesController to handle feature deletion and deletion summaries.

## Implementation Details
- Add `GET /api/features/{id}/deletion-summary` endpoint to existing FeaturesController
- Add `DELETE /api/features/{id}` endpoint to existing FeaturesController
- Inject `DeleteFeatureUseCase` in controller constructor
- Summary endpoint returns `FeatureDeletionSummaryDto`
- Delete endpoint calls use case and returns appropriate HTTP responses
- Handle 404 Not Found for non-existent features
- Handle 500 Internal Server Error for database failures

## Acceptance Criteria
- [ ] `GET /api/features/{id}/deletion-summary` endpoint added
- [ ] `DELETE /api/features/{id}` endpoint added to FeaturesController
- [ ] Controller constructor updated to inject `DeleteFeatureUseCase`
- [ ] Summary endpoint returns `FeatureDeletionSummaryDto` with 200 OK
- [ ] Delete endpoint returns 200 OK with success message
- [ ] Both endpoints return 404 Not Found when feature doesn't exist
- [ ] Both endpoints return 500 Internal Server Error for database exceptions
- [ ] Proper async/await pattern used
- [ ] Exception handling follows existing controller patterns

## Task 8: Add project deletion endpoints to ProjectsController
**Agent:** BE

## Description
Add DELETE endpoints to ProjectsController to handle project deletion and deletion summaries.

## Implementation Details
- Add `GET /api/projects/{id}/deletion-summary` endpoint to existing ProjectsController
- Add `DELETE /api/projects/{id}` endpoint to existing ProjectsController
- Inject `DeleteProjectUseCase` in controller constructor
- Summary endpoint returns `ProjectDeletionSummaryDto`
- Delete endpoint calls use case and returns appropriate HTTP responses
- Handle 404 Not Found for non-existent projects
- Handle 500 Internal Server Error for database failures

## Acceptance Criteria
- [ ] `GET /api/projects/{id}/deletion-summary` endpoint added
- [ ] `DELETE /api/projects/{id}` endpoint added to ProjectsController
- [ ] Controller constructor updated to inject `DeleteProjectUseCase`
- [ ] Summary endpoint returns `ProjectDeletionSummaryDto` with 200 OK
- [ ] Delete endpoint returns 200 OK with success message
- [ ] Both endpoints return 404 Not Found when project doesn't exist
- [ ] Both endpoints return 500 Internal Server Error for database exceptions
- [ ] Proper async/await pattern used
- [ ] Exception handling follows existing controller patterns

## Task 9: Register deletion use cases in dependency injection
**Agent:** BE

## Description
Register the new deletion use case classes in the dependency injection container.

## Implementation Details
- Update DI configuration in Api layer (Program.cs or Startup.cs)
- Register `DeleteArtifactUseCase` as scoped service
- Register `DeleteFeatureUseCase` as scoped service
- Register `DeleteProjectUseCase` as scoped service
- Follow existing DI registration patterns in the codebase

## Acceptance Criteria
- [ ] `DeleteArtifactUseCase` registered in DI container
- [ ] `DeleteFeatureUseCase` registered in DI container
- [ ] `DeleteProjectUseCase` registered in DI container
- [ ] All use cases registered with appropriate lifetime (scoped)
- [ ] Registration follows existing patterns in codebase
- [ ] No circular dependencies introduced
- [ ] Application builds successfully with new registrations

## Task 10: Create reusable ConfirmationDialog component
**Agent:** FE

## Description
Create a reusable React component for deletion confirmation dialogs that can be used across different entity types.

## Implementation Details
- Create `ConfirmationDialog` component in shared components directory
- Accept props: `isOpen`, `onConfirm`, `onCancel`, `title`, `message`, `confirmText`, `cancelText`
- Use Tailwind CSS for styling following existing design patterns
- Include loading state during confirmation action
- Make component accessible with proper ARIA attributes
- Support keyboard navigation (Enter to confirm, Escape to cancel)

## Acceptance Criteria
- [ ] `ConfirmationDialog` component created in shared components
- [ ] Component accepts required props with TypeScript interfaces
- [ ] Modal overlay blocks interaction with background
- [ ] Confirm button shows loading spinner during action
- [ ] Cancel button closes dialog without action
- [ ] Accessible with proper ARIA labels and roles
- [ ] Keyboard shortcuts work (Enter/Escape)
- [ ] Styling matches existing UI patterns
- [ ] Component is responsive on mobile devices

## Task 11: Create useDeletion custom React hook
**Agent:** FE

## Description
Create a custom React hook to handle deletion operations including fetching deletion summaries and executing deletions.

## Implementation Details
- Create `useDeletion` hook in hooks directory
- Accept parameters: `entityType` ('project' | 'feature' | 'artifact'), `entityId`, `onSuccess` callback
- Implement `fetchDeletionSummary()` function for projects and features
- Implement `executeDeletion()` function for all entity types
- Handle loading states, error states, and success states
- Use existing API client patterns
- Return hook interface with summary data, loading states, and action functions

## Acceptance Criteria
- [ ] `useDeletion` hook created with TypeScript interfaces
- [ ] Hook handles all three entity types (project, feature, artifact)
- [ ] `fetchDeletionSummary` calls appropriate summary endpoints
- [ ] `executeDeletion` calls appropriate DELETE endpoints
- [ ] Loading states managed for both summary fetch and deletion
- [ ] Error handling with user-friendly error messages
- [ ] Success callback triggered after successful deletion
- [ ] Hook follows existing patterns in codebase
- [ ] Proper cleanup to prevent memory leaks

## Task 12: Add delete functionality to artifact detail page
**Agent:** FE

## Description
Add delete button and confirmation dialog to the artifact detail page.

## Implementation Details
- Add delete button to artifact detail page layout
- Integrate `useDeletion` hook for artifact deletion
- Use `ConfirmationDialog` component for deletion confirmation
- Show simple confirmation message (artifacts have no child entities)
- Handle successful deletion by redirecting to parent feature page
- Display error messages for failed deletions
- Add loading state to delete button during operation

## Acceptance Criteria
- [ ] Delete button added to artifact detail page with trash icon
- [ ] `useDeletion` hook integrated with 'artifact' entity type
- [ ] `ConfirmationDialog` shows when delete button clicked
- [ ] Confirmation message: "Delete this artifact? This action cannot be undone."
- [ ] Successful deletion redirects to parent feature detail page
- [ ] Error toast/message displayed for failed deletions
- [ ] Delete button shows loading spinner during deletion
- [ ] Button is disabled during deletion operation
- [ ] Confirmation dialog closes on cancel without action

## Task 13: Add delete functionality to feature detail page
**Agent:** FE

## Description
Add delete button and confirmation dialog to the feature detail page with cascade deletion summary.

## Implementation Details
- Add delete button to feature detail page layout
- Integrate `useDeletion` hook for feature deletion
- Use `ConfirmationDialog` component with deletion summary
- Fetch and display artifact count in confirmation message
- Handle successful deletion by redirecting to parent project page
- Display error messages for failed deletions and summary fetch
- Add loading states for both summary fetch and deletion

## Acceptance Criteria
- [ ] Delete button added to feature detail page with trash icon
- [ ] `useDeletion` hook integrated with 'feature' entity type
- [ ] Deletion summary fetched when delete button clicked
- [ ] `ConfirmationDialog` shows summary: "Delete feature '[name]' and [count] artifacts?"
- [ ] Loading spinner shown while fetching deletion summary
- [ ] Successful deletion redirects to parent project detail page
- [ ] Error handling for both summary fetch and deletion failures
- [ ] Delete button shows loading spinner during deletion
- [ ] Button is disabled during any loading operation
- [ ] Confirmation dialog handles zero artifacts gracefully

## Task 14: Add delete functionality to project detail page
**Agent:** FE

## Description
Add delete button and confirmation dialog to the project detail page with full cascade deletion summary.

## Implementation Details
- Add delete button to project detail page layout
- Integrate `useDeletion` hook for project deletion
- Use `ConfirmationDialog` component with complete deletion summary
- Fetch and display feature and artifact counts in confirmation message
- Handle successful deletion by redirecting to projects dashboard/list page
- Display error messages for failed deletions and summary fetch
- Add loading states for both summary fetch and deletion

## Acceptance Criteria
- [ ] Delete button added to project detail page with trash icon
- [ ] `useDeletion` hook integrated with 'project' entity type
- [ ] Deletion summary fetched when delete button clicked
- [ ] `ConfirmationDialog` shows full summary: "Delete project '[name]' with [X] features and [Y] artifacts?"
- [ ] Loading spinner shown while fetching deletion summary
- [ ] Successful deletion redirects to projects list/dashboard page
- [ ] Error handling for both summary fetch and deletion failures
- [ ] Delete button shows loading spinner during deletion
- [ ] Button is disabled during any loading operation
- [ ] Confirmation dialog handles zero features/artifacts gracefully
