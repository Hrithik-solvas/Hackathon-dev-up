# Implementation Plan: CodeCompass UI

## Overview

Build a React + TypeScript SPA using Vite that provides a conversational chat interface, document/code ingestion panel, and health monitoring dashboard. The app communicates with the existing .NET 9 API via REST endpoints and manages state client-side using React Context.

## Tasks

- [ ] 1. Set up project structure, tooling, and core types
  - [ ] 1.1 Initialize Vite + React + TypeScript project and install dependencies
    - Run `npm create vite@latest` with React + TypeScript template
    - Install dependencies: axios, react-router-dom, @radix-ui/react-*, tailwindcss, zod, sonner, fast-check, vitest, @testing-library/react, jsdom
    - Configure Vite with `VITE_API_BASE_URL` environment variable (default: `https://localhost:7133`)
    - Configure Tailwind CSS with base styles and consistent design tokens (typography, colors, spacing)
    - Configure Vitest with jsdom environment and React Testing Library setup
    - _Requirements: 7.6, 8.1_

  - [ ] 1.2 Define TypeScript types and interfaces
    - Create `src/types/chat.ts` with `ChatMessage`, `Citation` interfaces
    - Create `src/types/health.ts` with `HealthStatus`, `ServiceHealth`, `HealthState` interfaces
    - Create `src/types/ingestion.ts` with `FileUploadState`, `IngestResult`, `KnowledgeBaseState`, `PipelineResult` interfaces
    - Create `src/types/errors.ts` with `ApiError` interface (type: network | timeout | validation | server | unavailable)
    - _Requirements: 1.3, 1.4, 3.4, 5.3, 6.1, 8.2, 8.5, 8.7_

  - [ ] 1.3 Create Zod validation schemas
    - Create `src/validation/schemas.ts` with `chatInputSchema` (min 1, max 2000, non-whitespace), `knowledgeBaseSchema` (targetPath required, mode enum), `fileUploadSchema` (1-20 files, max 50 MB each), `repositoryNameSchema` (max 128, optional)
    - _Requirements: 1.6, 3.1, 3.5, 3.8, 4.6, 5.4_

  - [ ]* 1.4 Write property tests for validation schemas
    - **Property 4: Whitespace-only input rejected by validation**
    - **Property 7: File upload validation enforces count and size constraints**
    - **Validates: Requirements 1.6, 3.1, 3.8, 5.4**

- [ ] 2. Implement API client layer
  - [ ] 2.1 Create centralized Axios API client with interceptors
    - Create `src/api/client.ts` with base URL from `VITE_API_BASE_URL`, 60-second timeout
    - Implement response interceptor that classifies errors into `ApiError` types (network, timeout, validation, server, unavailable)
    - Set Content-Type headers: `application/json` for JSON bodies, `multipart/form-data` for file uploads
    - _Requirements: 8.1, 8.4, 8.5, 8.6, 8.7_

  - [ ] 2.2 Implement Chat API module
    - Create `src/api/chat.ts` with `sendChatMessage(request: ChatApiRequest): Promise<ChatApiResponse>`
    - Ensure request body includes `question` and `sessionId`
    - _Requirements: 1.2, 2.3_

  - [ ] 2.3 Implement Ingest API module
    - Create `src/api/ingest.ts` with `ingestDocs(files: File[])`, `ingestCode(files: File[], repositoryName?: string)`, `ingestKnowledgeBase(targetPath: string, mode: 'Full' | 'Incremental')`
    - Use multipart/form-data for file uploads; include repositoryName as form field when provided
    - _Requirements: 3.3, 4.3, 4.5, 5.2_

  - [ ] 2.4 Implement Health API module
    - Create `src/api/health.ts` with `getHealth(): Promise<HealthResponse>`
    - Handle 503 with valid body as successful health data (not an error)
    - _Requirements: 6.2, 6.10_

  - [ ]* 2.5 Write property tests for API client
    - **Property 1: Chat request body formation**
    - **Property 5: Error display never leaks internal details**
    - **Validates: Requirements 1.2, 1.8, 2.3**

- [ ] 3. Implement state management
  - [ ] 3.1 Create Notification Context
    - Create `src/contexts/NotificationContext.tsx` with `NotificationProvider`
    - Implement `addNotification` and `dismissNotification` actions
    - Cap visible notifications at 3, discard oldest when exceeded
    - Support `success` (auto-dismiss 5s) and `error` (persistent, manual dismiss) notification types
    - Wire notifications to Sonner toast library with ARIA live region announcements
    - _Requirements: 8.2, 8.3, 8.5, 8.7, 8.9, 9.3_

  - [ ] 3.2 Create Session Context
    - Create `src/contexts/SessionContext.tsx` with `SessionProvider`
    - Generate UUID v4 session ID on initialization
    - Implement `sendMessage(question)` that appends user message, calls chat API, appends assistant response
    - Implement `newConversation()` that clears messages and generates new session ID
    - _Requirements: 2.1, 2.3, 2.5, 2.6_

  - [ ]* 3.3 Write property tests for state management
    - **Property 2: Conversation grows by exactly one message pair**
    - **Property 13: Notification queue capped at maximum 3 visible**
    - **Validates: Requirements 1.3, 8.9**

- [ ] 4. Checkpoint - Core infrastructure
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 5. Implement navigation and app shell
  - [ ] 5.1 Create App Shell with React Router
    - Create `src/App.tsx` with `NotificationProvider`, `SessionProvider`, and `AppLayout`
    - Configure React Router v6 with routes: `/` (chat, default), `/ingest`, `/health`
    - Use `<Outlet>` for panel rendering to preserve component state on navigation
    - _Requirements: 7.1, 7.3, 7.4_

  - [ ] 5.2 Create Navigation component
    - Create `src/components/Navigation.tsx` with links to Chat, Ingestion, Health panels
    - Visually distinguish active navigation item (background, border, or font weight)
    - Ensure navigation remains visible on all panels
    - Use semantic HTML `<nav>` element with ARIA labels
    - _Requirements: 7.1, 7.2, 9.6_

  - [ ] 5.3 Create NotificationStack component
    - Create `src/components/NotificationStack.tsx` that renders visible notifications from context
    - Support dismiss action for persistent notifications
    - Use ARIA live region for screen reader announcements
    - _Requirements: 8.2, 8.3, 8.9, 9.3_

- [ ] 6. Implement Chat Panel
  - [ ] 6.1 Create ChatPanel component with MessageList and ChatInput
    - Create `src/components/chat/ChatPanel.tsx` as container
    - Create `src/components/chat/MessageList.tsx` that renders messages in chronological order
    - Create `src/components/chat/ChatMessage.tsx` with visual differentiation for user vs assistant roles
    - Create `src/components/chat/ChatInput.tsx` with text input (max 2000 chars), submit button, Enter key submission
    - Validate input with `chatInputSchema`; show inline error for empty/whitespace input
    - Display loading indicator and disable submit while awaiting response
    - _Requirements: 1.1, 1.2, 1.3, 1.5, 1.6, 1.7, 2.4, 9.2_

  - [ ] 6.2 Create CitationCard and error handling for chat
    - Create `src/components/chat/CitationCard.tsx` displaying SourceUri, RelevanceScore as percentage, ChunkContent excerpt
    - Format relevance score: multiply by 100, round to integer, append "%"
    - Display generic error message for API errors without exposing internals
    - Preserve user input text on API failure
    - _Requirements: 1.4, 1.5, 1.8, 8.8_

  - [ ] 6.3 Implement auto-scroll and New Conversation button
    - Implement auto-scroll when within 50px of bottom; show jump-to-bottom indicator otherwise
    - Create `src/components/chat/ScrollToBottomIndicator.tsx`
    - Add "New Conversation" button that clears messages and generates new session ID
    - _Requirements: 2.2, 2.5, 9.4, 9.5_

  - [ ]* 6.4 Write property tests for chat
    - **Property 3: Relevance score formatted as whole-number percentage**
    - **Property 6: Messages rendered in chronological order**
    - **Property 14: User input preserved on API failure**
    - **Property 15: Auto-scroll triggered by proximity to bottom**
    - **Validates: Requirements 1.4, 2.4, 8.8, 9.4, 9.5**

- [ ] 7. Checkpoint - Chat panel complete
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 8. Implement Ingestion Panel
  - [ ] 8.1 Create IngestionPanel container and FileUploadZone component
    - Create `src/components/ingestion/IngestionPanel.tsx` with sections for docs, code, and knowledge base
    - Create `src/components/ingestion/FileUploadZone.tsx` supporting drag-and-drop and file browser selection
    - Display selected file names and total count before submission
    - Validate file count (max 20) and file size (max 50 MB each) using `fileUploadSchema`
    - Show inline validation errors for constraint violations
    - _Requirements: 3.1, 3.2, 3.5, 3.8, 4.1, 4.2, 4.6_

  - [ ] 8.2 Create DocIngestionSection
    - Create `src/components/ingestion/DocIngestionSection.tsx`
    - Integrate FileUploadZone, submit button, progress indicator
    - On success: display ChunksIngested and SourcesProcessed, clear file selection
    - On error: display ProblemDetails title and detail
    - Disable submit during upload; preserve input on failure
    - _Requirements: 3.3, 3.4, 3.6, 3.7, 8.8_

  - [ ] 8.3 Create CodeIngestionSection
    - Create `src/components/ingestion/CodeIngestionSection.tsx`
    - Include FileUploadZone and optional repository name input (max 128 chars)
    - On success: display ChunksIngested and SourcesProcessed
    - On error: display ProblemDetails title and detail
    - Allow submission without repository name; disable submit during upload
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.7, 4.8, 8.8_

  - [ ] 8.4 Create KnowledgeBaseSection
    - Create `src/components/ingestion/KnowledgeBaseSection.tsx`
    - Include target path input (max 500 chars), mode dropdown (Full/Incremental, default Full), submit button
    - Validate with `knowledgeBaseSchema`; show inline error for empty path
    - On success: display TotalFilesProcessed, TotalChunksGenerated, TotalErrors
    - On error: display ProblemDetails title and detail
    - Support Enter key submission; disable submit during processing
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 9.2_

  - [ ]* 8.5 Write property tests for ingestion
    - **Property 8: File selection display shows accurate count and all names**
    - **Property 9: IngestResponse values displayed correctly**
    - **Property 10: ProblemDetails error rendering extracts title and detail**
    - **Property 11: PipelineResult displays processing summary**
    - **Validates: Requirements 3.2, 3.4, 3.7, 4.2, 4.4, 4.8, 5.3, 5.5**

- [ ] 9. Implement Health Dashboard
  - [ ] 9.1 Create HealthDashboard with OverallStatus and ServiceStatusCard
    - Create `src/components/health/HealthDashboard.tsx` that fetches health data on mount
    - Create `src/components/health/OverallStatus.tsx` with color-coded status (green=Healthy, red=Unhealthy, yellow=Degraded)
    - Create `src/components/health/ServiceStatusCard.tsx` showing service name, status, response time in ms
    - Display "no services reported" message when services array is empty
    - Include refresh button; show loading indicator during fetch; disable refresh while loading
    - Display warning when health status cannot be determined (network error, timeout, non-health error response)
    - Handle 503 with valid HealthResponse body as normal data display
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.8, 6.9, 6.10_

  - [ ]* 9.2 Write property tests for health dashboard
    - **Property 12: HealthResponse rendering shows all services or empty message**
    - **Validates: Requirements 6.1, 6.3**

- [ ] 10. Accessibility and responsive polish
  - [ ] 10.1 Add accessibility attributes and responsive layout
    - Add ARIA labels to all interactive controls, form inputs, and landmark regions
    - Ensure visible focus indicators with minimum 3:1 contrast ratio on all interactive elements
    - Ensure all text meets WCAG 2.1 Level AA contrast ratios (4.5:1 normal, 3:1 large)
    - Ensure no horizontal scrolling or overlapping on viewports ≥768px width
    - Add ARIA live regions for dynamic content updates (notifications, chat messages)
    - Use semantic HTML throughout (`<main>`, `<nav>`, `<section>`, `<article>`, `<form>`)
    - _Requirements: 7.5, 9.1, 9.6, 9.7_

- [ ] 11. Final checkpoint - All features complete
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The app uses Vite's environment variable system (`VITE_API_BASE_URL`) for API configuration
- All state is ephemeral (browser session only) — no localStorage or server persistence

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3"] },
    { "id": 2, "tasks": ["1.4", "2.1"] },
    { "id": 3, "tasks": ["2.2", "2.3", "2.4"] },
    { "id": 4, "tasks": ["2.5", "3.1", "3.2"] },
    { "id": 5, "tasks": ["3.3", "5.1"] },
    { "id": 6, "tasks": ["5.2", "5.3"] },
    { "id": 7, "tasks": ["6.1", "8.1", "9.1"] },
    { "id": 8, "tasks": ["6.2", "6.3", "8.2", "8.3", "8.4", "9.2"] },
    { "id": 9, "tasks": ["6.4", "8.5", "10.1"] }
  ]
}
```
