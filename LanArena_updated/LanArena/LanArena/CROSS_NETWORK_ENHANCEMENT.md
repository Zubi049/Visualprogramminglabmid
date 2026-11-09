# Cross-Network Game Discovery - Enhancement Guide

## Overview
LAN Arena has been enhanced to support players from **any network**, not just local LANs. The application now combines:
- **Local LAN Discovery** (UDP broadcast) - for same-network players
- **Cloud-Based Discovery** (HTTP REST API) - for players on different networks

## How It Works

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    LAN Arena Client                          │
├─────────────────────────────────────────────────────────────┤
│  MainForm (Discovery Manager)                               │
│  ├─ RoomListener (UDP Broadcast) → Local LAN rooms         │
│  ├─ ServerDiscovery (HTTP API) → Cloud rooms               │
│  └─ Merged room list for player selection                  │
└─────────────────────────────────────────────────────────────┘
         │
         ├──► Local Network (UDP Port 47777)
         │    └─ Broadcasts & listens for rooms
         │
         └──► Internet (HTTPS)
              └─ Communicates with central game server
                 - Register game rooms
                 - Query available rooms
                 - Unregister rooms
```

### Components

#### 1. **ServerDiscovery.cs** (NEW)
Handles cloud-based room discovery and registration.

**Key Methods:**
- `RegisterRoomAsync()` - Host publishes their game room to the cloud server
- `UnregisterRoomAsync()` - Host removes their room from the cloud server
- `FetchRoomsAsync()` - Client retrieves all available rooms
- `StartPolling()` - Continuously polls for room updates
- `StopPolling()` - Stops polling

**REST API Endpoints:**
```
POST   /api/rooms/register     - Register a game room
DELETE /api/rooms/{hostId}     - Unregister a room
GET    /api/rooms              - List all available rooms
```

#### 2. **MainForm.cs** (UPDATED)
Integrated cloud discovery alongside existing LAN discovery.

**New Fields:**
```csharp
ServerDiscovery serverDiscovery;
bool useCloudDiscovery = true;  // Toggle cloud discovery on/off
```

**Updated Methods:**
- `HostToggleAsync()` - Now registers with cloud server
- `HostLudo()` - Ludo rooms also register with cloud server
- `StopHosting()` - Unregisters from cloud server
- Constructor - Initializes both discovery methods

## Usage

### For Game Hosts
1. Select game type (Chess, Tic Tac Toe, or Ludo)
2. Click **"Host a game"**
3. The room is automatically:
   - Broadcast on local LAN (UDP)
   - Registered with cloud server (if enabled)
4. Remote players can find and join

### For Remote Players (Different Network)
1. Look for rooms in the **"ROOMS FOUND ON YOUR NETWORK"** list
2. Rooms from both local LAN and cloud server appear in the same list
3. Select a room and click **"Join selected room"**
4. Or manually enter host IP/port using **"Join by IP"**

## Configuration

### Enable/Disable Cloud Discovery
Edit `MainForm.cs` line where `useCloudDiscovery` is defined:

```csharp
bool useCloudDiscovery = true;  // Set to false to disable cloud discovery
```

### Change Cloud Server URL
Modify in `ServerDiscovery.cs`:

```csharp
public static string DefaultServerUrl = "https://your-server.com";
```

Or pass custom URL to constructor:
```csharp
serverDiscovery = new ServerDiscovery(myId, "https://custom-server.com");
```

## Server Requirements

A simple ASP.NET Core REST API server is needed to handle room registration and queries.

### Example Server Implementation (ASP.NET Core)

```csharp
[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private static Dictionary<string, RoomInfo> rooms = new();

    [HttpPost("register")]
    public IActionResult Register([FromBody] RoomInfo room)
    {
        rooms[room.id] = room;
        room.registeredAt = DateTime.UtcNow;
        return Ok();
    }

    [HttpDelete("{hostId}")]
    public IActionResult Unregister(string hostId)
    {
        rooms.Remove(hostId);
        return Ok();
    }

    [HttpGet]
    public IActionResult GetRooms()
    {
        // Filter out stale rooms (older than 30 seconds)
        var validRooms = rooms.Values
            .Where(r => (DateTime.UtcNow - r.registeredAt).TotalSeconds < 30)
            .ToList();
        return Ok(validRooms);
    }
}

public class RoomInfo
{
    public string id { get; set; }
    public string name { get; set; }
    public string game { get; set; }
    public int port { get; set; }
    public string ipAddress { get; set; }
    public DateTime registeredAt { get; set; }
}
```

## Network Flow

### Hosting a Game (Cross-Network)

```
1. Host clicks "Host a game"
   ├─ Create TCP listener on port 47778
   ├─ Start UDP broadcast (local LAN)
   ├─ Send registration to cloud server with:
   │  ├─ Room ID (unique)
   │  ├─ Host name
   │  ├─ Game type
   │  ├─ Port number
   │  └─ Public IP address
   └─ Wait for player connection

2. Cloud server receives registration
   ├─ Store room info
   ├─ Add to active rooms list
   └─ Keep for ~30 seconds (with polling refresh)
```

### Joining a Game (Cross-Network)

```
1. Client starts app
   ├─ Listen for UDP broadcasts (local LAN)
   ├─ Poll cloud server every 2 seconds
   └─ Merge results into single room list

2. Client selects a room
   ├─ Extract host IP from room info
   ├─ Connect to host:47778
   ├─ Send "HELLO|PlayerName"
   └─ Host responds with game parameters

3. Game begins
```

## Security Considerations

### Current Implementation (Basic)
- No authentication required
- No encryption (HTTP/HTTPS at transport level)
- Server trusts client-provided IP addresses

### For Production, Consider:
1. **API Key Authentication** - Require valid API key from clients
2. **HTTPS Only** - Use TLS/SSL for all communication
3. **Rate Limiting** - Prevent registration spam
4. **IP Validation** - Verify host IP belongs to claiming player
5. **Room Expiration** - Auto-remove stale rooms
6. **Content Validation** - Sanitize room names and player names
7. **CORS Policy** - Restrict API access as needed

## Troubleshooting

### Rooms not appearing from other networks
- Check if cloud server URL is correct
- Verify cloud server is running and responding
- Check firewall rules allow HTTPS outbound
- Ensure `useCloudDiscovery = true` in code

### Can't connect after joining
- Verify host's public IP is correct
- Check if host has port 47778 open/forwarded
- Ensure NAT/firewall allows inbound on port 47778
- Try "Join by IP" and manually enter full IP:port

### Server registration fails
- Check cloud server is online
- Verify network connectivity
- Check for firewall blocking HTTPS
- Review server error logs

## Migration from Pure LAN

The enhancement is **backward compatible**:
- Existing LAN discovery still works
- Cloud discovery is additive
- Both methods run simultaneously
- Hosts without internet can disable cloud discovery
- Single room list for seamless user experience

## Performance Notes

- Cloud polling interval: 2000ms (configurable)
- Local UDP broadcasts: 1000ms (existing)
- Room timeout: 4 seconds for LAN, 30 seconds for cloud (can adjust)
- Minimal bandwidth usage (~100 bytes per registration)

## Future Enhancements

1. **Relay Server** - For players behind strict NAT
2. **Matchmaking** - Automatic pairing for 1v1 games
3. **Game Lobby Rooms** - Chat before game starts
4. **Skill Rating** - Track player ratings
5. **Mobile Support** - iOS/Android companion app
6. **WebSocket Support** - Real-time room updates vs polling
