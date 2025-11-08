# Advanced Banking Client - Complete Explanation

## Overview

The `advanced-banking-client.html` is a **real-time web client** that connects to your banking system using **SignalR**. It displays live transaction updates, balance changes, and alerts by maintaining a persistent connection with the server.

Think of it as a **live dashboard** that gets instant notifications from your banking backend without having to refresh the page.

---

## What is SignalR?

**SignalR** is a technology that enables **real-time, two-way communication** between client and server.

### Traditional HTTP (Request-Response)
```
Client: "Give me my balance"
        ↓ (sends request)
Server: "Your balance is $1000"
        ↓ (sends response)
Client: Receives data (but only after asking)
```

**Problem**: Client must keep asking. Server can't push updates.

### SignalR (Persistent Connection)
```
Client: "I'm connected. Tell me when something happens"
        ↓ (persistent connection stays open)
Server: "Your balance changed! New balance: $900"
        ↓ (server pushes instantly)
Client: Gets update IMMEDIATELY without asking
```

**Benefit**: Server can send updates anytime. No refresh needed.

---

## How advanced-banking-client.html Works

### Step 1: User Enters Account Number and Clicks "Connect"

```html
<input type="text" id="accountNumber" placeholder="Enter account number" value="123456789" />
<button onclick="connect()">Connect</button>
```

**Example**: User enters account "123456789" and clicks Connect.

### Step 2: JavaScript Creates SignalR Connection

```javascript
function connect() {
    const accountNumber = accountNumberInput.value.trim();  // Get "123456789"
    
    connection = new signalR.HubConnectionBuilder()
        .withUrl(`/hubs/transactions?accountNumber=${accountNumber}`)
        .withAutomaticReconnect({...})
        .configureLogging(signalR.LogLevel.Information)
        .build();
}
```

**What this does:**

| Line | Meaning |
|------|---------|
| `new signalR.HubConnectionBuilder()` | Create a connection builder |
| `.withUrl('/hubs/transactions?accountNumber=123456789')` | Connect to the server's SignalR hub at this URL with account number as parameter |
| `.withAutomaticReconnect({...})` | If connection drops, automatically reconnect |
| `.configureLogging(...)` | Log what's happening (for debugging) |
| `.build()` | Create the connection object |

**URL Breakdown:**
```
/hubs/transactions?accountNumber=123456789
├─ /hubs/transactions = Server endpoint (SignalR hub)
└─ ?accountNumber=123456789 = Pass account number as parameter
```

### Step 3: Set Up Connection Event Handlers

```javascript
// When reconnecting (connection was lost)
connection.onreconnecting(error => {
    updateConnectionStatus('reconnecting', 'Reconnecting...');
    console.log('Connection lost. Reconnecting...', error);
});

// When reconnected successfully
connection.onreconnected(connectionId => {
    updateConnectionStatus('connected', 'Reconnected');
    console.log('Reconnected with connection ID:', connectionId);
});

// When connection closes
connection.onclose(error => {
    updateConnectionStatus('disconnected', 'Disconnected');
    console.log('Connection closed', error);
});
```

**Lifecycle:**

```
Connected
    ↓ (network issue)
Reconnecting
    ↓ (network recovered)
Connected
    ↓ (user clicks Disconnect)
Disconnected
```

### Step 4: Register Message Handlers

The client tells the server: "When you have these updates, send them to me"

#### Handler 1: Transaction Notifications

```javascript
connection.on("ReceiveTransactionNotification", (notification) => {
    addNotification(
        `Transaction: ${notification.type} - $${notification.amount} - ${notification.description}`,
        'info'
    );
    console.log('New transaction:', notification);
});
```

**Server sends**: `{"type": "Withdrawal", "amount": 50, "description": "ATM withdrawal"}`

**Client receives**: Displays "Transaction: Withdrawal - $50 - ATM withdrawal"

#### Handler 2: Balance Updates

```javascript
connection.on("ReceiveBalanceUpdate", (update) => {
    addNotification(`Balance Updated: $${update.newBalance}`, 'info');
    console.log('Balance update:', update);
});
```

**Server sends**: `{"newBalance": 950}`

**Client receives**: Displays "Balance Updated: $950"

#### Handler 3: System Alerts

```javascript
connection.on("ReceiveSystemAlert", (alert) => {
    addNotification(`System Alert: ${alert.message}`, alert.severity);
    console.log('System alert:', alert);
});
```

**Server sends**: `{"message": "Maintenance starting soon", "severity": "warning"}`

**Client receives**: Displays yellow alert notification

#### Handler 4: Fraud Alerts

```javascript
connection.on("ReceiveFraudAlert", (alert) => {
    addNotification(`FRAUD ALERT: ${alert.description}`, 'error');
    console.log('Fraud alert:', alert);
});
```

**Server sends**: `{"description": "Unusual transaction from new location"}`

**Client receives**: Displays red error notification

#### Handler 5: Connection State Changes

```javascript
connection.on("ConnectionStateChanged", (state) => {
    updateConnectionStatus(
        state.isConnected ? 'connected' : 'disconnected',
        state.message
    );
    console.log('Connection state changed:', state);
});
```

**Server sends**: `{"isConnected": true, "message": "Connection established"}`

### Step 5: Start Connection and Subscribe

```javascript
connection.start()
    .then(() => {
        updateConnectionStatus('connected', 'Connected to banking service');
        console.log('Connected to SignalR hub');
        
        // Subscribe to transactions after connection
        return connection.invoke('SubscribeToTransactions', accountNumber);
    })
    .catch(err => {
        updateConnectionStatus('error', 'Connection failed');
        console.error('Connection failed:', err);
    });
```

**What this does:**

| Step | Action |
|------|--------|
| `connection.start()` | Establish WebSocket connection to server |
| `.then(...)` | If successful, invoke server method |
| `connection.invoke('SubscribeToTransactions', accountNumber)` | Tell server "Subscribe me to account 123456789" |
| `.catch(err)` | If connection fails, show error |

---

## Complete Flow: Money Transfer Example

```
TIME: T+0s
├─ User opens advanced-banking-client.html
├─ User enters account "123456789"
├─ User clicks "Connect"
├─ Browser connects to /hubs/transactions?accountNumber=123456789
├─ Server establishes WebSocket connection
├─ UI shows: "Connected to banking service" (green)
└─ Browser invokes: SubscribeToTransactions("123456789")

TIME: T+5s
├─ Server: User transfers $100 from account 123456789 to account 987654321
├─ Server creates event: MoneyTransferredEvent
├─ Server saves to OutboxMessages
├─ Server broadcasts to ALL connected clients subscribed to account 123456789:
│  └─ connection.Clients.Group("account_123456789").SendAsync("ReceiveTransactionNotification", {...})
└─ Client browser receives ReceiveTransactionNotification

TIME: T+5.1s
├─ Client JavaScript handler executes
├─ addNotification("Transaction: Transfer - $100 - ...", 'info')
├─ New <div> created with green background
├─ UI displays: "[16:22:05] Transaction: Transfer - $100 - Account transfer"
├─ Timestamp added automatically
└─ Old notifications scroll down (keeps max 20)

TIME: T+5.2s
├─ Server: Balance update
├─ Client receives ReceiveBalanceUpdate: {newBalance: 900}
├─ addNotification("Balance Updated: $900", 'info')
└─ UI displays: "[16:22:05] Balance Updated: $900"
```

---

## User Interface Breakdown

### Connection Status (Top)

```html
<div id="connectionStatus" class="connection-status disconnected">Disconnected</div>
```

**States:**
- **Green with "Connected"**: WebSocket is open, receiving updates
- **Red with "Disconnected"**: No connection, not receiving updates
- **Yellow with "Reconnecting..."**: Lost connection, trying to reconnect

### Controls

```html
<input type="text" id="accountNumber" placeholder="Enter account number" value="123456789" />
<button onclick="connect()">Connect</button>
<button onclick="disconnect()">Disconnect</button>
<button onclick="getStats()">Get Stats</button>
```

| Control | Action |
|---------|--------|
| Text input | Enter the account number to monitor |
| Connect button | Establish SignalR connection |
| Disconnect button | Close connection |
| Get Stats button | Request connection statistics from server |

### Statistics Display

```javascript
function getStats() {
    if (connection) {
        connection.invoke('GetConnectionStats')
            .then(stats => {
                connectionStats.innerHTML = `
                    <h3>Connection Statistics</h3>
                    <p>Total Connections: ${stats.totalConnections}</p>
                    <p>Active Connections: ${stats.activeConnections}</p>
                    <p>Total Messages Today: ${stats.totalMessagesToday}</p>
                    <p>Service Uptime: ${Math.round(stats.uptime.totalMinutes)} minutes</p>
                `;
            });
    }
}
```

**Server sends**: Stats object

**Client displays**: Formatted statistics

### Notifications Area

```html
<div id="notifications"></div>
```

**Displays all events** with color coding:
- **Green (.info)**: Transaction notifications, balance updates
- **Yellow (.warning)**: System alerts, warnings
- **Red (.error)**: Fraud alerts, critical errors

---

## Automatic Reconnection Strategy

```javascript
.withAutomaticReconnect({
    nextRetryDelayInMilliseconds: retryContext => {
        // Exponential backoff: 2s, 4s, 8s, 16s, then 30s max
        const delay = Math.min(30000, 2000 * Math.pow(2, retryContext.previousRetryCount));
        console.log(`Reconnecting in ${delay}ms...`);
        return delay;
    }
})
```

**Retry Timeline:**

```
Attempt 1: Wait 2 seconds    (2 * 2^0 = 2)
Attempt 2: Wait 4 seconds    (2 * 2^1 = 4)
Attempt 3: Wait 8 seconds    (2 * 2^2 = 8)
Attempt 4: Wait 16 seconds   (2 * 2^3 = 16)
Attempt 5+: Wait 30 seconds  (max 30, then cap)
```

**Why exponential backoff?**
- First retry is quick (2s) - likely a brief blip
- Subsequent retries are slower - gives server time to recover
- Never waits more than 30s - doesn't feel frozen
- Eventually stops retrying (prevents infinite loops)

---

## Notification Auto-Cleanup

```javascript
function addNotification(message, type = 'info') {
    const div = document.createElement('div');
    div.className = `notification ${type}`;
    div.textContent = `[${new Date().toLocaleTimeString()}] ${message}`;
    notifications.prepend(div);  // Add to top
    
    // Auto-remove old notifications
    if (notifications.children.length > 20) {
        notifications.removeChild(notifications.lastChild);  // Remove oldest
    }
}
```

**Behavior:**
```
New notification arrives
    ↓
Added to top (prepend)
    ↓
Existing notifications scroll down
    ↓
Check total count
    ↓
If > 20 notifications
    ├─ Remove oldest (at bottom)
    └─ Keep 20 most recent
```

---

## CSS Styling

### Connection Status Indicator

```css
.connection-status {
    padding: 10px;
    font-weight: bold;
}

.connected {
    background: #28a745;  /* Green */
    color: white;
}

.disconnected {
    background: #dc3545;  /* Red */
    color: white;
}

.reconnecting {
    background: #ffc107;  /* Yellow */
    color: black;
}
```

### Notifications

```css
.notification {
    padding: 10px;
    margin: 5px;
    border-radius: 5px;
}

.info {
    background: #d4edda;      /* Light green */
    border: 1px solid #c3e6cb;
}

.warning {
    background: #fff3cd;      /* Light yellow */
    border: 1px solid #ffeaa7;
}

.error {
    background: #f8d7da;      /* Light red */
    border: 1px solid #f5c6cb;
}
```

---

## Auto-Connect on Page Load

```javascript
// Auto-connect on page load for demo
window.addEventListener('load', () => {
    setTimeout(connect, 1000);
});
```

**What this does:**
- When page fully loads
- Wait 1 second
- Automatically call `connect()`
- Uses default account "123456789"

**Result**: Client auto-connects as soon as page opens

---

## How It Integrates with Your Banking System

```
┌─────────────────────┐
│  Banking Workflow   │
│                     │
│ 1. Money Transfer   │
│    (API endpoint)   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  Domain Event       │
│  Created in Memory  │
│                     │
│ MoneyTransferred    │
│ Event              │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Saved to Outbox     │
│ (OutboxMessages)    │
│                     │
│ ProcessedOn = null  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ SignalR Sends to    │
│ Connected Clients   │
│                     │
│ "ReceiveTransaction │
│  Notification"      │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Browser receives    │
│                     │
│ JS handler adds     │
│ notification to UI  │
│                     │
│ User sees update    │
│ INSTANTLY           │
└─────────────────────┘
```

---

## Sequence Diagram: Full Connection Lifecycle

```
Client Browser                          Server
    │                                     │
    │  1. User enters account & clicks    │
    │     Connect                         │
    │                                     │
    │  2. Create SignalR connection       │
    │     builder                         │
    │                                     │
    │  3. Connect to /hubs/transactions   │
    ├────────────────────────────────────>│
    │                                     │ Accept connection
    │                                     │
    │<────────────────────────────────────┤
    │  4. Connection established          │
    │                                     │
    │  5. Invoke SubscribeToTransactions  │
    ├────────────────────────────────────>│
    │                                     │ Add client to
    │                                     │ "account_123456789"
    │                                     │ group
    │<────────────────────────────────────┤
    │  6. Success response                │
    │                                     │
    │  [User performs transaction]        │
    │                                     │
    │     (time passes)                   │
    │                                     │
    │<────────────────────────────────────┤
    │  7. ReceiveTransactionNotification  │
    │     (pushed by server)              │
    │                                     │
    │  8. Client handler executes         │
    │     (shows notification)            │
    │                                     │
    │<────────────────────────────────────┤
    │  9. ReceiveBalanceUpdate            │
    │     (pushed by server)              │
    │                                     │
    │  10. Client handler executes        │
    │      (shows notification)           │
    │                                     │
    │  [User clicks Disconnect]           │
    │                                     │
    │  11. Close connection               │
    ├────────────────────────────────────>│
    │                                     │ Cleanup
    │<────────────────────────────────────┤
    │  12. Connection closed              │
    │                                     │
```

---

## Key Takeaways

1. **Real-time Updates**: Clients get instant notifications without polling/refreshing
2. **Persistent Connection**: WebSocket stays open, allowing server to push data
3. **Automatic Reconnection**: If connection drops, client automatically tries to reconnect
4. **Event-Driven**: Each banking event (transaction, balance change, fraud alert) is pushed to connected clients
5. **Account-Specific**: Each client subscribes to one account number
6. **Grouped Broadcasting**: Server uses SignalR groups to send updates only to relevant clients
7. **Clean UI**: Notifications are color-coded and auto-cleaned when > 20
8. **Stateless**: Multiple clients can connect to same account simultaneously
9. **Error Handling**: Gracefully handles connection drops and reconnection scenarios

