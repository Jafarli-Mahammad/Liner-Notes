# 📁 Liner Notes V1 — Solution & Folder Structure Blueprint

**Target Framework:** .NET 10.0 (C# 13.0)  
**Architecture:** Clean Architecture + CQRS (MediatR) + Dedicated Background Worker  
**Purpose:** Lean, production-ready directory and project structure specifically tailored for the **Liner Notes** weekly music recommendation and digest email platform.  

---

## 📑 Table of Contents

1. [High-Level Directory Overview](#1-high-level-directory-overview)
2. [Complete Solution Tree](#2-complete-solution-tree)
3. [Project-by-Project Breakdown](#3-project-by-project-breakdown)
   - 3.1. [Web API Presentation (`src/LinerNotes.Api`)](#31-web-api-presentation-srclinernotesapi)
   - 3.2. [Background Batch Engine (`src/LinerNotes.Worker`)](#32-background-batch-engine-srclinernotesworker)
   - 3.3. [Application Layer (`src/LinerNotes.Application`)](#33-application-layer-srclinernotesapplication)
   - 3.4. [Domain Layer (`src/LinerNotes.Domain`)](#34-domain-layer-srclinernotesdomain)
   - 3.5. [Infrastructure & Data Access (`src/LinerNotes.Infrastructure`)](#35-infrastructure--data-access-srclinernotesinfrastructure)
   - 3.6. [Test Suite (`tests/LinerNotes.Tests`)](#36-test-suite-testslinernotestests)
4. [Cross-Cutting & Root Configuration Files](#4-cross-cutting--root-configuration-files)
5. [Architectural Comparison: DevJourney vs. Liner Notes V1](#5-architectural-comparison-devjourney-vs-liner-notes-v1)

---

## 1. High-Level Directory Overview

```
LinerNotes/
├── src/
│   ├── LinerNotes.Api/            # Lightweight Web API (Auth, account linking, settings, manual test emails)
│   ├── LinerNotes.Worker/         # Dedicated background batch processor for scheduled weekly digest generation
│   ├── LinerNotes.Application/    # MediatR CQRS features, pipeline behaviors, interfaces & DTOs
│   ├── LinerNotes.Domain/         # Domain models (User, UserMusicConnection, WeeklyDigest, Recommendation)
│   └── LinerNotes.Infrastructure/ # EF Core 10, Polly rate-limited music clients, IMemoryCache, Email delivery
├── tests/
│   └── LinerNotes.Tests/          # Unit tests, handler tests, recommendation algorithm tests
├── docker/                        # Multi-stage Dockerfiles and compose setups
└── scripts/                       # Local dev helpers and database migration utilities
```

---

## 2. Complete Solution Tree

```
LinerNotes
├── src
│   ├── LinerNotes.Api
│   │   ├── Controllers
│   │   │   ├── AuthController.cs            # Register, Login, RefreshToken
│   │   │   ├── ConnectionsController.cs     # Connect Last.fm, ListenBrainz, Spotify
│   │   │   ├── DigestsController.cs         # On-demand preview, digest history, unsubscribe
│   │   │   └── UsersController.cs           # User preferences (schedule, genre exclusions)
│   │   ├── Filters
│   │   │   └── ApiExceptionFilter.cs        # RFC 7807 ProblemDetails mapping
│   │   ├── Properties
│   │   │   └── launchSettings.json
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Dockerfile
│   │   ├── Program.cs                       # Kestrel bootstrap, single Swagger UI, auth middleware
│   │   └── LinerNotes.Api.csproj
│   │
│   ├── LinerNotes.Worker
│   │   ├── Jobs
│   │   │   ├── IDigestBatchOrchestrator.cs  # Orchestrates batch user selection and dispatch
│   │   │   └── DigestBatchOrchestrator.cs
│   │   ├── Services
│   │   │   ├── IRecommendationEngine.cs     # Computes candidate tracks & personalized picks
│   │   │   ├── RecommendationEngine.cs
│   │   │   ├── IEmailTemplateRenderer.cs   # Renders HTML and plain-text email layouts
│   │   │   └── FluidEmailTemplateRenderer.cs
│   │   ├── Templates
│   │   │   ├── WeeklyDigest.html            # Fluid / Scriban HTML email template
│   │   │   └── WeeklyDigest.txt             # Plaintext fallback template
│   │   ├── Workers
│   │   │   └── WeeklyDigestWorker.cs        # BackgroundService running PeriodicTimer
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Dockerfile
│   │   ├── Program.cs                       # Generic host bootstrap for worker
│   │   └── LinerNotes.Worker.csproj
│   │
│   ├── LinerNotes.Application
│   │   ├── Common
│   │   │   ├── Behaviors
│   │   │   │   ├── LoggingBehavior.cs       # Logs execution timing and errors
│   │   │   │   └── ValidationBehavior.cs    # FluentValidation MediatR pipeline interceptor
│   │   │   ├── Exceptions
│   │   │   │   ├── NotFoundException.cs
│   │   │   │   ├── UnauthorizedException.cs
│   │   │   │   └── ValidationException.cs
│   │   │   ├── Interfaces
│   │   │   │   ├── IAppDbContext.cs         # EF Core abstraction for handlers
│   │   │   │   ├── ICurrentUserService.cs   # Provides authenticated user context
│   │   │   │   ├── IEmailService.cs         # Outbound transactional email abstraction
│   │   │   │   ├── ILastFmClient.cs         # Last.fm scrobble & similar artist client
│   │   │   │   └── IListenBrainzClient.cs   # ListenBrainz scrobble client
│   │   │   └── Models
│   │   │       └── PagedResult.cs
│   │   ├── Modules
│   │   │   ├── Auth
│   │   │   │   ├── Commands
│   │   │   │   │   ├── Login
│   │   │   │   │   │   ├── LoginCommand.cs
│   │   │   │   │   │   ├── LoginCommandHandler.cs
│   │   │   │   │   │   └── LoginCommandValidator.cs
│   │   │   │   │   └── Register
│   │   │   │   │       ├── RegisterCommand.cs
│   │   │   │   │       ├── RegisterCommandHandler.cs
│   │   │   │   │       └── RegisterCommandValidator.cs
│   │   │   │   └── Dtos
│   │   │   │       └── AuthResponseDto.cs
│   │   │   ├── Connections
│   │   │   │   ├── Commands
│   │   │   │   │   ├── ConnectLastFm
│   │   │   │   │   │   ├── ConnectLastFmCommand.cs
│   │   │   │   │   │   └── ConnectLastFmCommandHandler.cs
│   │   │   │   │   └── ConnectListenBrainz
│   │   │   │   │       ├── ConnectListenBrainzCommand.cs
│   │   │   │   │       └── ConnectListenBrainzCommandHandler.cs
│   │   │   │   └── Queries
│   │   │   │       └── GetUserConnections
│   │   │   │           ├── GetUserConnectionsQuery.cs
│   │   │   │           └── UserConnectionDto.cs
│   │   │   ├── Digests
│   │   │   │   ├── Commands
│   │   │   │   │   ├── SendPreviewDigest
│   │   │   │   │   │   ├── SendPreviewDigestCommand.cs
│   │   │   │   │   │   └── SendPreviewDigestCommandHandler.cs
│   │   │   │   │   └── UnsubscribeUser
│   │   │   │   │       ├── UnsubscribeUserCommand.cs
│   │   │   │   │       └── UnsubscribeUserCommandHandler.cs
│   │   │   │   ├── Dtos
│   │   │   │   │   ├── WeeklyDigestDto.cs
│   │   │   │   │   └── RecommendedTrackDto.cs
│   │   │   │   └── Queries
│   │   │   │       └── GetDigestHistory
│   │   │   │           ├── GetDigestHistoryQuery.cs
│   │   │   │           └── GetDigestHistoryQueryHandler.cs
│   │   │   └── Users
│   │   │       ├── Commands
│   │   │       │   └── UpdatePreferences
│   │   │       │       ├── UpdatePreferencesCommand.cs
│   │   │       │       └── UpdatePreferencesCommandHandler.cs
│   │   │       └── Queries
│   │   │           └── GetUserProfile
│   │   │               ├── GetUserProfileQuery.cs
│   │   │               └── UserProfileDto.cs
│   │   ├── Application.csproj
│   │   └── DependencyInjection.cs
│   │
│   ├── LinerNotes.Domain
│   │   ├── Common
│   │   │   ├── BaseEntity.cs                # Id (Guid) declaration
│   │   │   └── IAuditableEntity.cs          # CreatedAt, LastModifiedAt
│   │   ├── Entities
│   │   │   ├── User.cs                      # Aggregate Root (Email, Schedule, TimeZone)
│   │   │   ├── UserMusicConnection.cs       # Last.fm / ListenBrainz connection record
│   │   │   ├── UserPreferences.cs           # Delivery day, genre blacklists, novelty ratio
│   │   │   ├── WeeklyDigest.cs              # Digest record (WeekStartDate, Status, SentAt)
│   │   │   └── WeeklyRecommendation.cs      # Individual recommended track & rationale
│   │   ├── Enums
│   │   │   ├── DigestDeliveryDay.cs         # Sunday, Monday, etc.
│   │   │   ├── DigestStatus.cs              # Pending, InProgress, Sent, Failed
│   │   │   └── MusicServiceType.cs          # LastFm, ListenBrainz, Spotify
│   │   └── LinerNotes.Domain.csproj
│   │
│   └── LinerNotes.Infrastructure
│       ├── Background
│       │   └── BackgroundQueue.cs           # Optional in-memory channel for quick tasks
│       ├── Clients
│       │   ├── LastFm
│       │   │   ├── LastFmClient.cs          # Polly-wrapped HTTP client
│       │   │   ├── LastFmOptions.cs
│       │   │   └── Models/                  # Last.fm JSON response mapping models
│       │   └── ListenBrainz
│       │       ├── ListenBrainzClient.cs    # Polly-wrapped HTTP client
│       │       ├── ListenBrainzOptions.cs
│       │       └── Models/                  # ListenBrainz JSON models
│       ├── Email
│       │   ├── EmailOptions.cs              # API keys and sender addresses
│       │   └── ResendEmailService.cs        # Transactional email dispatcher via Resend/Postmark
│       ├── Persistence
│       │   ├── Configurations
│       │   │   ├── UserConfiguration.cs
│       │   │   ├── UserMusicConnectionConfiguration.cs
│       │   │   ├── WeeklyDigestConfiguration.cs
│       │   │   └── WeeklyRecommendationConfiguration.cs
│       │   ├── Migrations
│       │   │   └── AppDbContextModelSnapshot.cs
│       │   └── AppDbContext.cs              # Single EF Core 10 DbContext
│       ├── Security
│       │   ├── DataProtectionTokenEncryptor.cs # Encrypts 3rd-party tokens at rest
│       │   └── JwtTokenGenerator.cs
│       ├── Infrastructure.csproj
│       └── DependencyInjection.cs
│
├── tests
│   └── LinerNotes.Tests
│       ├── Architecture
│       │   └── CleanArchitectureTests.cs    # Validates layer dependency boundaries
│       ├── Handlers
│       │   ├── ConnectLastFmCommandHandlerTests.cs
│       │   └── UpdatePreferencesCommandHandlerTests.cs
│       ├── Recommendation
│       │   └── RecommendationEngineTests.cs # Unit tests for recommendation & ranking logic
│       ├── Worker
│       │   └── DigestBatchOrchestratorTests.cs
│       └── LinerNotes.Tests.csproj
│
├── compose.yaml                             # Docker compose: API, Worker, PostgreSQL
├── Directory.Build.props                     # Centralized .NET 10 compilation flags
├── V1_Infrastructure.md                     # Lean V1 architectural & infrastructure specification
├── V1_FolderStructure.md                    # This document
└── README.md
```

---

## 3. Project-by-Project Breakdown

### 3.1. Web API Presentation (`src/LinerNotes.Api`)
A lightweight ASP.NET Core Web API serving as the administrative and user-facing entrypoint:
- **`Program.cs`:** Registers standard Kestrel, single unified Swagger documentation (`/swagger/v1`), EF Core `AppDbContext`, and authentication middleware.
- **`Controllers/`:**
  - `AuthController`: Handles email registration, login, and JWT generation.
  - `ConnectionsController`: Verifies and links users' Last.fm / ListenBrainz usernames.
  - `UsersController`: Allows users to update their preferred digest delivery day, hour, and music preferences.
  - `DigestsController`: Allows users to trigger an immediate test/preview digest to verify email delivery.

---

### 3.2. Background Batch Engine (`src/LinerNotes.Worker`)
An independent, lightweight .NET `BackgroundService` project executing the weekly batch processing pipeline:
- **`WeeklyDigestWorker.cs`:** Uses a standard `PeriodicTimer` (ticking hourly) to find users whose scheduled digest window is open.
- **`DigestBatchOrchestrator.cs`:** Coordinates the batch pipeline:
  1. Queries pending users from the database.
  2. Fetches scrobbles and listening history via upstream music API clients.
  3. Executes the recommendation engine.
  4. Renders the HTML newsletter email using `FluidEmailTemplateRenderer`.
  5. Dispatches the email through `IEmailService`.
  6. Idempotently marks the digest as `Sent` and updates `User.NextDigestAt`.
- **`Templates/`:** Holds the responsive HTML email template (`WeeklyDigest.html`) and plaintext counterpart.

---

### 3.3. Application Layer (`src/LinerNotes.Application`)
Encapsulates all business logic using **CQRS with MediatR**:
- **`Modules/`:** Vertical feature slices (`Auth`, `Users`, `Connections`, `Digests`):
  - Each module contains commands, handlers, and FluentValidation validators.
- **`Common/Behaviors/`:**
  - `ValidationBehavior`: Automatically executes FluentValidation validators before handlers run.
  - `LoggingBehavior`: Structured logging of command duration and user context.
- **`Common/Interfaces/`:** Declares contracts implemented by the Infrastructure layer (`IAppDbContext`, `IEmailService`, `ILastFmClient`, `IListenBrainzClient`).

---

### 3.4. Domain Layer (`src/LinerNotes.Domain`)
Pure C# domain layer with zero external dependencies:
- **`Entities/User.cs`:** Core user entity managing identity, time zone, and digest preferences.
- **`Entities/UserMusicConnection.cs`:** Stores linked music services (Last.fm, ListenBrainz, Spotify) with encrypted credentials.
- **`Entities/WeeklyDigest.cs`:** Represents a specific week's digest run, enforcing uniqueness on `(UserId, WeekStartDate)`.
- **`Entities/WeeklyRecommendation.cs`:** Individual track recommendations with contextual rationale (e.g., *"Recommended because you scrobbled Aphex Twin 24 times this week"*).
- **`Enums/`:** Strongly typed states (`DigestStatus`, `MusicServiceType`, `DigestDeliveryDay`).

---

### 3.5. Infrastructure & Data Access (`src/LinerNotes.Infrastructure`)
Concrete implementations of external concerns:
- **`Persistence/AppDbContext.cs`:** Single EF Core 10 `DbContext` using PostgreSQL (or SQLite). Handles schema mapping, soft-delete query filters, and audit timestamps.
- **`Clients/LastFm/LastFmClient.cs`:** Typed `HttpClient` implementing Last.fm REST API calls. Configured with a Polly resilience pipeline enforcing the 5 req/sec rate limit.
- **`Clients/ListenBrainz/ListenBrainzClient.cs`:** Typed `HttpClient` fetching ListenBrainz scrobbles.
- **`Email/ResendEmailService.cs`:** Transactional email delivery service using modern REST APIs (Resend / SendGrid / Postmark).
- **`Security/DataProtectionTokenEncryptor.cs`:** Encrypts user API keys and tokens at rest using ASP.NET Core Data Protection.

---

### 3.6. Test Suite (`tests/LinerNotes.Tests`)
Unit and integration tests focusing on high-value business logic:
- **`Recommendation/RecommendationEngineTests.cs`:** Verifies candidate selection, deduplication against recent listening history, and ranking heuristics.
- **`Worker/DigestBatchOrchestratorTests.cs`:** Mocks external APIs to test failure recovery and idempotency (verifying duplicate emails are never sent).
- **`Architecture/CleanArchitectureTests.cs`:** Tests verifying that Domain and Application layers never reference Infrastructure or Web hosts.

---

## 4. Cross-Cutting & Root Configuration Files

| File | Purpose |
| :--- | :--- |
| **`compose.yaml`** | Minimal production Docker Compose stack (API, Worker, PostgreSQL). Zero Redis containers. |
| **`Directory.Build.props`** | Centralized compiler properties: C# 13, nullable enabled, implicit usings. |
| **`V1_Infrastructure.md`** | Comprehensive architectural and infrastructure specification for Liner Notes V1. |
| **`V1_FolderStructure.md`** | Detailed solution folder tree and module layout (this document). |

---

## 5. Architectural Comparison: DevJourney vs. Liner Notes V1

| Architectural Concern | DevJourney (Live Hackathon Platform) | Liner Notes V1 (Weekly Email Digest Tool) |
| :--- | :--- | :--- |
| **Real-Time Gateway** | SignalR + MessagePack + Redis Pub/Sub backplane | **None** (Email delivery is completely asynchronous) |
| **Read/Write Persistence** | EF Core 10 writes + Dapper Micro-ORM paginated reads | **EF Core 10 Alone** (Batch generation needs no micro-ORM) |
| **Caching Layer** | .NET 10 HybridCache (L1 Memory + L2 Redis) | **`IMemoryCache`** (Upstream Last.fm rate-limit caching only) |
| **Background Execution** | In-process bounded channel (`IBackgroundTaskQueue`) | **Dedicated `Worker` Project** (`BackgroundService` with `PeriodicTimer`) |
| **Server Hardware Target** | Multi-node cluster with 100k socket connections | **Single inexpensive VPS** ($4–$6/month) |
| **API Documentation** | 4 separate OpenAPI documents (Public, Partner, Company, Admin) | **Single unified Swagger document** (`/swagger/v1`) |
| **Upstream Protections** | None (DevJourney is the system of record) | **Polly Rate Limiters** (Enforces Last.fm 5 req/sec ceiling) |
