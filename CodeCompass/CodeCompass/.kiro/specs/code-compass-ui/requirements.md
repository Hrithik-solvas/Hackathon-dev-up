# Requirements Document

## Introduction

This feature adds a web-based user interface to the CodeCompass application. The UI serves as the frontend for the existing .NET 9 API, enabling users to interact with the RAG-powered chat functionality, manage document and code ingestion, and monitor system health. The interface communicates with the API exclusively through its REST endpoints (POST /api/chat, POST /api/ingest/docs, POST /api/ingest/code, POST /api/ingest/knowledge-base, and GET /api/health).

## Glossary

- **UI_Application**: The web-based frontend application that provides the user interface for CodeCompass.
- **Chat_Panel**: The primary interface area where users submit questions and view AI-generated answers with citations.
- **Ingestion_Panel**: The interface area where users upload documentation and code files for indexing into the vector store.
- **Health_Dashboard**: The interface area displaying the operational status of all CodeCompass backend services.
- **Chat_Message**: A single message within a conversation, either from the user or from the assistant.
- **Conversation**: A sequence of Chat_Messages within a single session, identified by a SessionId.
- **Citation_Card**: A visual component displaying a citation's source URI, relevance score, and chunk content excerpt.
- **API_Client**: The HTTP client module within the UI_Application that communicates with the CodeCompass API endpoints.
- **File_Upload_Zone**: An interactive area where users can drag-and-drop or browse to select files for ingestion.
- **Service_Status_Indicator**: A visual element showing the health state (Healthy, Unhealthy, Degraded) of an individual backend service.

## Requirements

### Requirement 1: Chat Interface for Question Submission

**User Story:** As a user, I want to type questions and receive grounded answers with citations, so that I can query my indexed documentation and code through a conversational interface.

#### Acceptance Criteria

1. THE Chat_Panel SHALL display a text input field that accepts a maximum of 2000 characters and a submit button for entering questions.
2. WHEN the user submits a non-empty question, THE API_Client SHALL send a POST request to /api/chat with the question text and the current SessionId in the request body.
3. WHEN the API returns a successful ChatResponse, THE Chat_Panel SHALL display the answer text and append the message pair (user question and assistant answer) to the Conversation history.
4. WHEN the API returns a successful ChatResponse containing one or more citations, THE Chat_Panel SHALL render a Citation_Card for each citation showing the SourceUri, RelevanceScore formatted as a whole-number percentage (e.g., "73%"), and the ChunkContent excerpt as returned by the API.
5. WHEN the API returns a successful ChatResponse containing zero citations, THE Chat_Panel SHALL display the answer text without rendering any Citation_Card elements.
6. IF the user submits an empty or whitespace-only question, THEN THE Chat_Panel SHALL display an inline validation message indicating that a question is required and SHALL NOT send a request to the API.
7. WHILE the API_Client is awaiting a response from the chat endpoint, THE Chat_Panel SHALL display a loading indicator and disable the submit button to prevent duplicate submissions.
8. IF the API returns an error response (HTTP 4xx or 5xx), THEN THE Chat_Panel SHALL display an error message indicating the general nature of the failure (e.g., validation error, server error, or service unavailable) without exposing HTTP status codes, stack traces, or internal exception details.

### Requirement 2: Conversation Session Management

**User Story:** As a user, I want my chat messages to be organized into sessions, so that I can maintain context within a conversation and start fresh conversations when needed.

#### Acceptance Criteria

1. WHEN the UI_Application loads and no SessionId exists in the application state, THE UI_Application SHALL generate a new unique SessionId (UUID v4) and store it in the application state for the duration of the browser session.
2. THE Chat_Panel SHALL include a "New Conversation" button that is visible at all times regardless of whether messages exist in the current Conversation.
3. WHILE a SessionId is stored in the application state, THE API_Client SHALL include that same SessionId in every POST request to /api/chat to maintain conversation continuity.
4. THE Chat_Panel SHALL display all Chat_Messages for the current session in chronological order (oldest at top, newest at bottom), with user messages and assistant messages visually differentiated by alignment or labeling so that the sender of each message is unambiguous.
5. WHEN the user activates the "New Conversation" button, THE Chat_Panel SHALL clear all displayed Chat_Messages, reset the text input field to empty, and THE UI_Application SHALL generate and store a new SessionId (UUID v4) replacing the previous one.
6. IF the user refreshes or reopens the browser tab, THEN THE UI_Application SHALL treat it as a new session by generating a new SessionId, and the previous Conversation messages SHALL NOT be restored.

### Requirement 3: Document File Ingestion

**User Story:** As a user, I want to upload documentation files through the UI, so that I can add new content to the knowledge base without using API tools directly.

#### Acceptance Criteria

1. THE Ingestion_Panel SHALL display a File_Upload_Zone that accepts files via drag-and-drop or file browser selection, allowing a maximum of 20 files per upload with a maximum individual file size of 50 MB.
2. WHEN the user selects one or more documentation files, THE Ingestion_Panel SHALL display the selected file names and total file count before submission.
3. WHEN the user confirms the upload, THE API_Client SHALL send a POST request to /api/ingest/docs with the selected files as multipart/form-data.
4. WHEN the API returns a successful IngestResponse, THE Ingestion_Panel SHALL display the number of ChunksIngested and SourcesProcessed from the response and clear the file selection state.
5. IF the user attempts to submit without selecting any files, THEN THE Ingestion_Panel SHALL display a validation message indicating that at least one file must be selected.
6. WHILE the API_Client is uploading files to the ingestion endpoint, THE Ingestion_Panel SHALL display an indeterminate progress indicator and disable the submit button to prevent duplicate submissions.
7. IF the API returns an error response during document ingestion, THEN THE Ingestion_Panel SHALL display the error title and detail from the ProblemDetails response.
8. IF the user selects a file that exceeds 50 MB, THEN THE Ingestion_Panel SHALL display a validation message indicating the file exceeds the maximum allowed size and SHALL NOT include that file in the upload.

### Requirement 4: Code File Ingestion

**User Story:** As a user, I want to upload source code files with an optional repository name, so that I can index code into the knowledge base with proper source tracking.

#### Acceptance Criteria

1. THE Ingestion_Panel SHALL display a separate section for code ingestion with a File_Upload_Zone and an optional text input field for the repository name that accepts a maximum of 128 characters.
2. WHEN the user selects one or more code files, THE Ingestion_Panel SHALL display the selected file names and total file count before submission.
3. WHEN the user confirms the code file upload, THE API_Client SHALL send a POST request to /api/ingest/code with the files as multipart/form-data and the repository name as a form field.
4. WHEN the API returns a successful IngestResponse for code ingestion, THE Ingestion_Panel SHALL display the ChunksIngested count and SourcesProcessed count from the response.
5. IF the user submits code files without entering a repository name, THEN THE API_Client SHALL send the request without the repositoryName field and THE Ingestion_Panel SHALL proceed without validation error.
6. IF the user attempts to submit code ingestion without selecting any files, THEN THE Ingestion_Panel SHALL display a validation message indicating that at least one code file must be selected.
7. WHILE the API_Client is uploading files to the code ingestion endpoint, THE Ingestion_Panel SHALL display a progress indicator showing that ingestion is in progress and disable the submit button to prevent duplicate submissions.
8. IF the API returns an error response during code ingestion, THEN THE Ingestion_Panel SHALL display the error title and detail from the ProblemDetails response.

### Requirement 5: Knowledge Base Directory Ingestion

**User Story:** As an administrator, I want to trigger knowledge base ingestion from a directory path, so that I can index entire knowledge base directories into the search index via the RAG pipeline.

#### Acceptance Criteria

1. THE Ingestion_Panel SHALL display a knowledge base ingestion section with a text input for the target directory path (maximum 500 characters), a dropdown selector for the indexing mode (Full or Incremental) defaulting to Full, and a submit button.
2. WHEN the user enters a target path, selects a mode, and clicks the submit button, THE API_Client SHALL send a POST request to /api/ingest/knowledge-base with the targetPath and mode values in the JSON request body.
3. WHEN the API returns a successful PipelineResult, THE Ingestion_Panel SHALL display TotalFilesProcessed, TotalChunksGenerated, and TotalErrors from the response.
4. IF the user submits without entering a target path, THEN THE Ingestion_Panel SHALL display a validation message indicating that a target directory path is required and SHALL NOT send a request to the API.
5. IF the API returns an error response during knowledge base ingestion, THEN THE Ingestion_Panel SHALL display the error title and detail from the ProblemDetails response.
6. WHILE the API_Client is awaiting the knowledge base ingestion response, THE Ingestion_Panel SHALL display a progress indicator and disable the submit button to prevent duplicate submissions.

### Requirement 6: System Health Monitoring

**User Story:** As a user, I want to view the health status of all backend services, so that I can verify the system is operational before relying on chat or ingestion functionality.

#### Acceptance Criteria

1. THE Health_Dashboard SHALL display a Service_Status_Indicator for each service returned in the HealthResponse.Services array, showing the service name, health status, and response time in milliseconds. IF the Services array is empty, THEN THE Health_Dashboard SHALL display a message indicating that no services were reported.
2. WHEN the Health_Dashboard is displayed, THE API_Client SHALL send a GET request to /api/health and render the response.
3. THE Health_Dashboard SHALL display the overall system status from HealthResponse.Status prominently at the top of the panel.
4. WHEN the overall status is "Healthy", THE Health_Dashboard SHALL display the status with a green visual indicator.
5. WHEN the overall status is "Unhealthy", THE Health_Dashboard SHALL display the status with a red visual indicator.
6. WHEN the overall status is "Degraded", THE Health_Dashboard SHALL display the status with a yellow visual indicator.
7. THE Health_Dashboard SHALL include a refresh button that triggers a new GET request to /api/health and updates the displayed status.
8. WHILE the API_Client is awaiting a response from the health endpoint (including initial load and manual refresh), THE Health_Dashboard SHALL display a loading indicator and disable the refresh button.
9. IF the API returns a network error, times out, or returns an HTTP error status without a valid HealthResponse body, THEN THE Health_Dashboard SHALL display a warning indicating that the system status could not be determined.
10. IF the API returns an HTTP 503 response with a valid HealthResponse body, THEN THE Health_Dashboard SHALL render the response data normally (displaying overall status and individual service statuses) rather than treating it as an unreachable error.

### Requirement 7: Navigation and Layout

**User Story:** As a user, I want a clear navigation structure, so that I can easily switch between chat, ingestion, and health monitoring areas.

#### Acceptance Criteria

1. THE UI_Application SHALL provide a persistent navigation mechanism that remains visible on all panels, allowing the user to switch between the Chat_Panel, Ingestion_Panel, and Health_Dashboard, displaying only one panel at a time.
2. THE UI_Application SHALL visually distinguish the active navigation item from inactive items using a different style (such as background color, border, or font weight) so that the currently displayed panel is identifiable at a glance.
3. WHEN the UI_Application loads initially, THE UI_Application SHALL display the Chat_Panel as the default active view with the corresponding navigation item marked as active.
4. WHEN the user switches between panels via navigation, THE UI_Application SHALL preserve the state of each panel (including Conversation history in Chat_Panel and any entered form data in Ingestion_Panel) so that returning to a panel restores its previous state within the same browser session.
5. THE UI_Application SHALL render all navigation elements and panel content without horizontal scrolling or overlapping elements on viewports with a width of 768 pixels or greater.
6. THE UI_Application SHALL use a consistent visual design across all panels including the same typography family and scale, the same color palette, and the same spacing units.

### Requirement 8: API Communication and Error Handling

**User Story:** As a user, I want consistent and informative error handling across all operations, so that I understand what went wrong and what actions I can take.

#### Acceptance Criteria

1. THE API_Client SHALL set the base URL for all API requests from a configurable application setting.
2. IF the API is unreachable (network error or timeout), THEN THE UI_Application SHALL display a persistent error notification indicating that the server is unavailable and suggesting the user verify the backend is running, and the notification SHALL remain visible until the user explicitly dismisses it.
3. IF the API returns an HTTP 503 response, THEN THE UI_Application SHALL display a persistent error notification indicating that the requested service is temporarily unavailable, and the notification SHALL remain visible until the user explicitly dismisses it.
4. THE API_Client SHALL set a request timeout of 60 seconds for all API calls.
5. IF a request exceeds the 60-second timeout, THEN THE UI_Application SHALL abort the request and display a timeout error notification to the user.
6. THE API_Client SHALL include a Content-Type header of "application/json" for JSON request bodies and "multipart/form-data" for file uploads.
7. IF the API returns an HTTP 5xx response other than 503, THEN THE UI_Application SHALL display a persistent error notification indicating an unexpected server error occurred, and the notification SHALL remain visible until the user explicitly dismisses it.
8. IF an API request fails due to a network error, timeout, or server error response, THEN THE UI_Application SHALL preserve all user-entered input in the active panel so the user can retry the operation without re-entering data.
9. IF multiple API errors occur before the user dismisses previous error notifications, THEN THE UI_Application SHALL display each error notification individually, up to a maximum of 3 visible notifications at the same time, discarding the oldest notification when the limit is exceeded.

### Requirement 9: Responsive Feedback and Accessibility

**User Story:** As a user, I want immediate visual feedback for my actions and accessible interactions, so that I can use the application efficiently regardless of input method.

#### Acceptance Criteria

1. THE UI_Application SHALL provide visible focus indicators with a minimum contrast ratio of 3:1 against adjacent colors on all interactive elements for keyboard navigation.
2. WHEN the user presses the Enter key while focused on the chat question input or the knowledge base directory path input, THE UI_Application SHALL submit the corresponding form, provided the input passes validation.
3. WHEN an operation completes successfully (ingestion, health refresh), THE UI_Application SHALL display a non-modal success notification indicating the completed operation name, announced via an ARIA live region, that auto-dismisses after 5 seconds.
4. WHEN a new Chat_Message is appended to the Conversation and the Chat_Panel scroll position is within 50 pixels of the bottom, THE Chat_Panel SHALL automatically scroll to the most recent message.
5. IF the user has scrolled the Chat_Panel more than 50 pixels above the bottom when a new Chat_Message is appended, THEN THE Chat_Panel SHALL NOT auto-scroll and SHALL display an indicator allowing the user to jump to the latest message.
6. THE UI_Application SHALL use semantic HTML elements and ARIA labels on all interactive controls, form inputs, landmark regions, and dynamically updated content areas to support assistive technology.
7. THE UI_Application SHALL maintain a minimum color contrast ratio of 4.5:1 for normal text and 3:1 for large text in accordance with WCAG 2.1 Level AA.
