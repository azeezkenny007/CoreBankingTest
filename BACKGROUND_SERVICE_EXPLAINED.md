# Background Service & ProcessedOn - Complete Explanation

## Overview

Your system has a **background service** that continuously monitors the database for unprocessed events and marks them as processed. This document explains exactly how it works and what `ProcessedOn` means.

---

## What is a Background Service?

A **Background Service** is code that runs continuously in the background while your application is running. It runs on a **separate thread** and performs tasks asynchronously without blocking your main application.

Think of it as a **ghost worker** that quietly does work in the background:

```
Your Application (Main Thread)
├─ Handles HTTP requests
├─ Processes user commands
└─ Returns responses to clients

Background Service (Separate Thread)
├─ Runs every 30 seconds
├─ Checks database for unprocessed events
└─ Marks them as processed
```

Both run **simultaneously** without interfering with each other.

---

## The OutBoxMessage Entity

First, let's understand what gets stored:

```csharp
public class OutBoxMessage
{
    public Guid Id { get; set; }                    // Unique message ID
    public string Type { get; set; }                // Event type name
    public string Content { get; set; }             // Event JSON
    public DateTime OccurredOn { get; set; }        // When event was created
    public DateTime? ProcessedOn { get; set; }      // ⭐ When event was processed (null = not processed yet)
    public string? Error { get; set; }              // Error message if failed
    public int RetryCount { get; set; }             // How many times we tried
}
```

### What Does `ProcessedOn` Mean?

```
ProcessedOn = null
├─ Meaning: Event has NOT been processed yet
├─ Status: WAITING to be processed
└─ Example: Event just created 2 seconds ago

ProcessedOn = 2025-11-07 16:22:08
├─ Meaning: Event HAS been processed
├─ Status: COMPLETE - already handled
└─ Example: Event processed at this timestamp
```

**In Simple Terms**: `ProcessedOn` is a timestamp that says "when was this event finished being processed?"

---

## The Background Service Code

### Code Overview

```csharp
public class OutboxBackgroundService : BackgroundService
{
    // ✅ Dependency injection
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxBackgroundService> _logger;
    
    // ✅ How often to check for events
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);
    
    // ✅ Main loop
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Every 30 seconds, process events
            // If error, log it
            // Wait 30 seconds
            // Repeat
        }
    }
}
```

### How It Works - Step by Step

#### Step 1: The Loop

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    // ... do work ...
    await Task.Delay(_interval, stoppingToken);  // Wait 30 seconds
}
```

- **Runs continuously** while the application is running
- **Waits 30 seconds** between each check
- **Stops cleanly** when application shuts down

**Timeline:**
```
T+0s:   Check for events
T+30s:  Check for events
T+60s:  Check for events
T+90s:  Check for events
...
```

#### Step 2: Creating a Scope

```csharp
using var scope = _serviceProvider.CreateScope();
var processor = scope.ServiceProvider.GetRequiredService<IOutboxMessageProcessor>();
```

**Why?** Each iteration needs a fresh database context to avoid conflicts.

```
T+0s:  scope1 created → get OutboxMessageProcessor → query DB
       ↓
       scope1 disposed → closed

T+30s: scope2 created → get fresh OutboxMessageProcessor → query DB
       ↓
       scope2 disposed → closed
```

#### Step 3: Process Events

```csharp
await processor.ProcessOutboxMessagesAsync(stoppingToken);
```

This calls the actual processor (see below).

#### Step 4: Handle Errors

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Error processing outbox messages");
}
```

If something goes wrong, **log it but keep running**.

**Important**: Service doesn't crash - it just logs and tries again in 30 seconds.

---

## The OutboxMessageProcessor

This is where the actual work happens:

### Code Overview

```csharp
public class OutboxMessageProcessor : IOutboxMessageProcessor
{
    public async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken = default)
    {
        // STEP 1: Query unprocessed messages
        var messages = await _context.OutboxMessages
            .Where(x => x.ProcessedOn == null && x.RetryCount < 3)
            .OrderBy(x => x.OccurredOn)
            .Take(20)
            .ToListAsync(cancellationToken);

        // STEP 2: Process each message
        foreach (var message in messages)
        {
            // ... try to process ...
            // ... set ProcessedOn ...
        }

        // STEP 3: Save changes
        await _context.SaveChangesAsync(cancellationToken);
    }
}
```

### Detailed Breakdown

#### Step 1: Query Unprocessed Messages

```csharp
var messages = await _context.OutboxMessages
    .Where(x => x.ProcessedOn == null && x.RetryCount < 3)
    .OrderBy(x => x.OccurredOn)
    .Take(20)
    .ToListAsync(cancellationToken);
```

**What this does:**

```
SELECT TOP 20 * FROM OutboxMessages
WHERE 
    ProcessedOn IS NULL         -- Not yet processed
    AND RetryCount < 3          -- Haven't failed 3 times
ORDER BY OccurredOn ASC;        -- Process oldest first
```

**Why these conditions?**

| Condition | Reason |
|-----------|--------|
| `ProcessedOn == null` | Only get **unprocessed** events |
| `RetryCount < 3` | Give up after 3 failures (don't retry forever) |
| `OrderBy OccurredOn` | Process **oldest first** (FIFO) |
| `Take(20)` | Process **max 20 at a time** (don't overload) |

**Example Database State:**

```
Id       ProcessedOn          RetryCount  Status
────────────────────────────────────────────────────
123      NULL                 0           ✅ WILL BE PROCESSED (unprocessed, retry < 3)
124      NULL                 1           ✅ WILL BE PROCESSED (unprocessed, retry < 3)
125      NULL                 3           ❌ SKIP (failed 3 times already)
126      2025-11-07 16:20:00  0           ❌ SKIP (already processed)
```

**Result**: Get messages 123 and 124 (up to 20 total)

#### Step 2: Process Each Message

```csharp
foreach (var message in messages)
{
    try
    {
        // A. Deserialize the event from JSON
        var domainEvent = DeserializeMessage(message);
        // Commented out: await _eventBus.PublishAsync(domainEvent, cancellationToken);

        // B. ⭐⭐⭐ MARK AS PROCESSED ⭐⭐⭐
        message.ProcessedOn = DateTime.UtcNow;
        message.Error = null;
    }
    catch (Exception ex)
    {
        // C. If error, increment retry counter
        _logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
        message.RetryCount++;
        message.Error = ex.Message;
    }
}
```

**Step 2A: Deserialize**

```csharp
private static IDomainEvent? DeserializeMessage(OutBoxMessage message)
{
    // Get the event type name (e.g., "MoneyTransferredEvent")
    var eventType = Type.GetType($"CoreBanking.Core.Accounts.Events.{message.Type}, CoreBanking.Core");
    
    if (eventType == null)
        return null;

    // Convert JSON back to object
    return JsonSerializer.Deserialize(message.Content, eventType) as IDomainEvent;
}
```

**What happens:**

```
Database: 
  Type: "MoneyTransferredEvent"
  Content: "{\"amount\": 100, \"reference\": \"TRANSFER-001\", ...}"

↓

Becomes:
  MoneyTransferredEvent object {
    Amount: 100
    Reference: "TRANSFER-001"
    ...
  }
```

**Step 2B: Mark as Processed** ⭐⭐⭐

```csharp
message.ProcessedOn = DateTime.UtcNow;  // Set to current time
message.Error = null;                    // Clear any error
```

**This is the KEY BEHAVIOR:**

| Before | After |
|--------|-------|
| `ProcessedOn = null` | `ProcessedOn = 2025-11-07 16:22:08` |
| `Error = null` | `Error = null` |
| Status: Waiting | Status: **DONE** |

Now the event is marked as **"I've been processed!"**

**Step 2C: Handle Errors**

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
    message.RetryCount++;      // Increment: 0 → 1, 1 → 2, etc.
    message.Error = ex.Message; // Store error message
}
```

If deserialization fails:
- **Don't crash** - just record the error
- **Increment retry count** - next cycle will see this and try again
- **Store error message** - for debugging

**Example Retry Behavior:**

```
Iteration 1 (T+0s):
  ProcessedOn = null, RetryCount = 0
  → Tries to deserialize, FAILS
  → ProcessedOn = null, RetryCount = 1, Error = "Invalid JSON"
  → Next cycle: Will try again (RetryCount < 3)

Iteration 2 (T+30s):
  ProcessedOn = null, RetryCount = 1
  → Tries to deserialize, FAILS AGAIN
  → ProcessedOn = null, RetryCount = 2, Error = "Invalid JSON"
  → Next cycle: Will try again (RetryCount < 3)

Iteration 3 (T+60s):
  ProcessedOn = null, RetryCount = 2
  → Tries to deserialize, FAILS AGAIN
  → ProcessedOn = null, RetryCount = 3, Error = "Invalid JSON"
  → Next cycle: SKIPPED (RetryCount >= 3)
```

#### Step 3: Save Changes

```csharp
await _context.SaveChangesAsync(cancellationToken);
```

**Saves to database:**

```sql
UPDATE OutboxMessages
SET ProcessedOn = '2025-11-07 16:22:08', Error = NULL
WHERE Id = '123';

UPDATE OutboxMessages  
SET RetryCount = 2, Error = 'Invalid JSON'
WHERE Id = '124';
```

---

## Complete Workflow: Visual Timeline

```
T+0s: APPLICATION STARTS
├─ Background service starts
└─ Creates first scope

T+0.1s: FIRST CHECK
├─ Query: WHERE ProcessedOn IS NULL AND RetryCount < 3
├─ Found: 2 messages (ids: 123, 124)
├─ Process each:
│  ├─ Message 123: ✅ SUCCESS → ProcessedOn = 16:22:08
│  └─ Message 124: ❌ FAILED → RetryCount = 1
├─ Save changes to database
└─ Wait 30 seconds

T+30s: SECOND CHECK
├─ Query: WHERE ProcessedOn IS NULL AND RetryCount < 3
├─ Found: 1 message (id: 124, ProcessedOn=null, RetryCount=1)
├─ Process:
│  └─ Message 124: ❌ FAILED AGAIN → RetryCount = 2
├─ Save changes
└─ Wait 30 seconds

T+60s: THIRD CHECK
├─ Query: WHERE ProcessedOn IS NULL AND RetryCount < 3
├─ Found: 1 message (id: 124, ProcessedOn=null, RetryCount=2)
├─ Process:
│  └─ Message 124: ❌ FAILED AGAIN → RetryCount = 3
├─ Save changes
└─ Wait 30 seconds

T+90s: FOURTH CHECK
├─ Query: WHERE ProcessedOn IS NULL AND RetryCount < 3
├─ Found: 0 messages (124 has RetryCount=3, so skipped)
├─ Nothing to do
└─ Wait 30 seconds

T+120s: FIFTH CHECK
├─ Query: WHERE ProcessedOn IS NULL AND RetryCount < 3
├─ Found: 0 messages
├─ Nothing to do
└─ Continues checking forever...
```

---

## What Does ProcessedOn Actually Mean?

### In Database:

```sql
-- Unprocessed event (just created):
SELECT * FROM OutboxMessages WHERE Id = '123';
Id:          123
Type:        MoneyTransferredEvent
Content:     {...json...}
OccurredOn:  2025-11-07 16:22:08
ProcessedOn: NULL                    ← NOT processed yet
RetryCount:  0
Error:       NULL

-- After background service processes it:
SELECT * FROM OutboxMessages WHERE Id = '123';
Id:          123
Type:        MoneyTransferredEvent
Content:     {...json...}
OccurredOn:  2025-11-07 16:22:08
ProcessedOn: 2025-11-07 16:22:10    ← PROCESSED at this time
RetryCount:  0
Error:       NULL
```

### In Code Logic:

```csharp
// During transfer (immediate - synchronous):
var event = new MoneyTransferredEvent(...);
await context.SaveChangesWithOutboxAsync();
// Database now has:
//   OutboxMessages: ProcessedOn = null (waiting)

// 30 seconds later in background service:
var unprocessed = context.OutboxMessages
    .Where(x => x.ProcessedOn == null)  // Find waiting events
    .ToList();

// Process them...
message.ProcessedOn = DateTime.UtcNow;  // Mark as done

await context.SaveChangesAsync();
// Database now has:
//   OutboxMessages: ProcessedOn = 2025-11-07 16:22:10 (complete)
```

---

## State Transitions of an OutboxMessage

```
                   Created
                      │
                      ▼
         ┌─────────────────────────┐
         │   UNPROCESSED STATE     │
         ├─────────────────────────┤
         │ ProcessedOn = null      │
         │ RetryCount = 0          │
         │ Error = null            │
         └────────┬─────────────────┘
                  │
        ┌─────────┴─────────┐
        │                   │
        ▼                   ▼
   ✅ SUCCESS         ❌ FAILURE
        │                   │
        ▼                   ▼
┌──────────────┐   ┌──────────────┐
│ PROCESSED    │   │ RETRY STATE  │
│ STATE        │   ├──────────────┤
├──────────────┤   │ ProcessedOn= │
│ProcessedOn=  │   │ null         │
│2025-11-07    │   │ RetryCount=1 │
│16:22:10      │   │ Error=msg    │
│RetryCount=0  │   └──────┬───────┘
│Error=null    │          │
└──────────────┘    ┌─────┴──────┐
                    │            │
              ✅ SUCCESS    ❌ FAILURE (again)
              (rare)       │
                           ▼
                    ┌──────────────┐
                    │ RETRY STATE  │
                    ├──────────────┤
                    │ProcessedOn=  │
                    │null          │
                    │RetryCount=2  │
                    │Error=msg     │
                    └──────┬───────┘
                           │
                      ❌ FAILURE (3x)
                           │
                           ▼
                    ┌──────────────┐
                    │ ABANDONED    │
                    ├──────────────┤
                    │ProcessedOn=  │
                    │null          │
                    │RetryCount=3  │
                    │Error=msg     │
                    │              │
                    │SKIPPED FROM  │
                    │NOW ON        │
                    └──────────────┘
```

---

## Summary: ProcessedOn Meaning

| Scenario | ProcessedOn | Meaning | What Happens |
|----------|-------------|---------|--------------|
| Event just created | `NULL` | Not processed yet | Background service will process it |
| Successfully processed | `DateTime` | Processed successfully | Background service skips it (already done) |
| Failed 3 times | `NULL` + `RetryCount=3` | Abandoned (too many failures) | Background service skips it (won't retry anymore) |

---

## Key Takeaways

1. **Background Service**: Runs every 30 seconds in the background
2. **ProcessedOn = null**: "I'm waiting to be processed"
3. **ProcessedOn = timestamp**: "I was successfully processed at this time"
4. **RetryCount**: Tracks failed attempts (max 3)
5. **Automatic Retry**: Failed events retry automatically (up to 3 times)
6. **No Crashes**: If processing fails, service continues working
7. **FIFO Processing**: Oldest events are processed first
8. **Batch Processing**: Processes up to 20 at a time to avoid overload

