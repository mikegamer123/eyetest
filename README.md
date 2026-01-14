# EyeExam Schedule Parser

This solution parses and verifies **Schedule of Notices of Leases** data retrieved from an (fake) external API.

---

## Solution Structure

The solution contains **two startup projects** and **one test project**:

EyeExamParser.sln
│
├─ EyeExamParser → ASP.NET Core API (main service)
├─ EyeExamAPI → External API dependency (mock / reference microservice)
└─ EyeExamAPI.Tests → Unit tests (xUnit + NSubstitute)

### Projects

### 1. EyeExamParser (Startup Project)
The main API that:
- Fetches raw schedule data from `EyeExamAPI`
- Parses it into structured `ScheduleDTO`
- Caches parsed results
- Verifies results against an external `/results` endpoint

Endpoints:
- `GET /api/schedules`
- `GET /api/schedules/AreResultsTheSame`

---

### 2. EyeExamAPI (Startup Project)
Represents the **external system** that:
- Exposes `/schedules` (raw data)
- Exposes `/results` (expected parsed output)

This project simulates the upstream dependency used for validation.

---

### 3. EyeExamAPI.Tests
Contains **unit tests only**:
- Parser tests
- Service tests
- Helper tests

---

## Running the Solution

### Prerequisites
- .NET 6 SDK (or compatible)
- Visual Studio / Rider / VS Code

---

### Step 1: Configure startup projects

In Visual Studio:
1. Right-click solution → **Set Startup Projects**
2. Select **Multiple startup projects**
3. Set both:
   - `EyeExamParser` → Start
   - `EyeExamAPI` → Start

This ensures the parser API can call the external API.

---

### Step 2: Configure appsettings

In `EyeExamParser/appsettings.json`:

```json
{
  "EyeExamApi": {
    "BaseUrl": "https://localhost:7203" (or whatever the URL for microservice is on startup),
    "Username": "testy",
    "Password": "mcTestFace"
  }
}

BaseUrl must point to the running EyeExamAPI instance.

### Step 3: Run the solution

Swagger UI will open for EyeExamParser in Development mode

Using the API
Get parsed schedules
bash
Копирај кȏд
GET /api/schedules
Fetches raw schedules from EyeExamAPI

Parses them into structured form

Caches the result for 10 minutes

Verify parsed results

GET /api/schedules/AreResultsTheSame
Compares cached parsed results with /results from EyeExamAPI

Ignores whitespace differences

Matches entries by EntryNumber

Responses:

YES → results match

NO + detailed diff → mismatch detected

Caching Behavior
Parsed schedules are cached in memory for 10 minutes

Cache key: parsed_schedule_cache

Verification always uses cached data (no re-parse)

Testing
Run tests:

dotnet test

Tests cover:

Parsing logic (including edge cases)

Normalization helpers

Service behavior (cache, HTTP calls, verification logic)

Technologies:

xUnit

NSubstitute

In-memory HttpClient handlers

### Next Steps
Make dataset easier to read 😃