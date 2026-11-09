# LAN Arena Cloud Server - Sample Implementation

## Quick Start

### Prerequisites
- .NET 8 SDK
- Visual Studio or VS Code
- Azure (optional, for hosting)

## Option 1: ASP.NET Core Minimal API (Recommended)

Create a new ASP.NET Core Web API project:

```bash
dotnet new webapi -n LanArenaServer
cd LanArenaServer
```

### Program.cs

```csharp
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");
app.MapControllers();

// In-memory room storage (use database for production)
var rooms = new ConcurrentDictionary<string, RoomInfo>();

// Background task to clean old rooms
_ = Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        var now = DateTime.UtcNow;
        var stale = rooms.Where(kv => (now - kv.Value.RegisteredAt).TotalSeconds > 35)
                         .Select(kv => kv.Key)
                         .ToList();
        foreach (var id in stale)
            rooms.TryRemove(id, out _);
    }
});

// Register Room
app.MapPost("/api/rooms/register", (RoomInfo info) =>
{
    info.RegisteredAt = DateTime.UtcNow;
    rooms[info.Id] = info;
    return Results.Ok(new { message = "Room registered" });
})
.WithName("RegisterRoom")
.WithOpenApi();

// Get All Rooms
app.MapGet("/api/rooms", () =>
{
    var now = DateTime.UtcNow;
    var active = rooms.Values
        .Where(r => (now - r.RegisteredAt).TotalSeconds < 30)
        .ToList();
    return Results.Ok(active);
})
.WithName("GetRooms")
.WithOpenApi();

// Unregister Room
app.MapDelete("/api/rooms/{hostId}", (string hostId) =>
{
    rooms.TryRemove(hostId, out _);
    return Results.Ok(new { message = "Room unregistered" });
})
.WithName("UnregisterRoom")
.WithOpenApi();

app.Run();

// Data Models
public class RoomInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Game { get; set; }
    public int Port { get; set; }
    public string IpAddress { get; set; }
    public DateTime RegisteredAt { get; set; }
}
```

## Option 2: Using Controllers (Alternative)

### RoomsController.cs

```csharp
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace LanArenaServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    // Static for demo; use proper DI and database in production
    private static readonly ConcurrentDictionary<string, RoomInfo> Rooms = new();

    /// <summary>Register a new game room</summary>
    [HttpPost("register")]
    public IActionResult RegisterRoom([FromBody] RoomInfo room)
    {
        if (string.IsNullOrWhiteSpace(room.Id) || string.IsNullOrWhiteSpace(room.IpAddress))
            return BadRequest("Invalid room data");

        room.RegisteredAt = DateTime.UtcNow;
        Rooms[room.Id] = room;

        return Ok(new { 
            message = "Room registered successfully",
            expiresIn = "30 seconds"
        });
    }

    /// <summary>Get all active rooms</summary>
    [HttpGet]
    public IActionResult GetRooms()
    {
        var now = DateTime.UtcNow;

        // Filter rooms that haven't expired (30 second TTL)
        var activeRooms = Rooms.Values
            .Where(r => (now - r.RegisteredAt).TotalSeconds < 30)
            .OrderBy(r => r.Name)
            .ToList();

        return Ok(activeRooms);
    }

    /// <summary>Unregister a room</summary>
    [HttpDelete("{hostId}")]
    public IActionResult UnregisterRoom(string hostId)
    {
        if (Rooms.TryRemove(hostId, out var removed))
            return Ok(new { message = "Room unregistered", room = removed });

        return NotFound(new { message = "Room not found" });
    }

    /// <summary>Get specific room info</summary>
    [HttpGet("{hostId}")]
    public IActionResult GetRoom(string hostId)
    {
        if (Rooms.TryGetValue(hostId, out var room))
            return Ok(room);

        return NotFound(new { message = "Room not found" });
    }

    /// <summary>Keep-alive: refresh room registration</summary>
    [HttpPut("{hostId}/keepalive")]
    public IActionResult KeepAlive(string hostId)
    {
        if (Rooms.TryGetValue(hostId, out var room))
        {
            room.RegisteredAt = DateTime.UtcNow;
            return Ok(new { message = "Room refreshed" });
        }

        return NotFound(new { message = "Room not found" });
    }

    // Cleanup old rooms (run periodically)
    [ApiExplorerSettings(IgnoreApi = true)]
    public static void CleanupExpiredRooms()
    {
        var now = DateTime.UtcNow;
        var expired = Rooms.Where(kv => (now - kv.Value.RegisteredAt).TotalSeconds > 35)
                          .Select(kv => kv.Key)
                          .ToList();

        foreach (var id in expired)
            Rooms.TryRemove(id, out _);
    }
}

public class RoomInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Game { get; set; }
    public int Port { get; set; }
    public string IpAddress { get; set; }
    public DateTime RegisteredAt { get; set; }
}
```

## Option 3: With Entity Framework (Production-Ready)

### Models/RoomDbContext.cs

```csharp
using Microsoft.EntityFrameworkCore;

namespace LanArenaServer.Models;

public class RoomDbContext : DbContext
{
    public RoomDbContext(DbContextOptions<RoomDbContext> options) : base(options) { }

    public DbSet<Room> Rooms { get; set; }
}

public class Room
{
    public string Id { get; set; }
    public string HostName { get; set; }
    public string GameType { get; set; }
    public int Port { get; set; }
    public string IpAddress { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime LastUpdated { get; set; }
}
```

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=LanArenaDb;Trusted_Connection=true;TrustServerCertificate=true;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Program.cs (EF Core)

```csharp
using LanArenaServer.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<RoomDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();

var app = builder.Build();

// Auto-migrate database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RoomDbContext>();
    db.Database.Migrate();
}

app.MapControllers();
app.Run();
```

## Deployment Options

### Azure App Service (Free Tier Available)

```bash
# Create resource group
az group create --name LanArenaRG --location eastus

# Create App Service plan
az appservice plan create --name LanArenaASP --resource-group LanArenaRG --sku FREE

# Create web app
az webapp create --name lanarenapp --resource-group LanArenaRG --plan LanArenaASP

# Deploy
dotnet publish -c Release
cd bin/Release/net8.0/publish
az webapp deployment source config-zip --resource-group LanArenaRG --name lanarenapp --src-path app.zip
```

### Local Testing

```bash
dotnet run --launch-profile https
# API will be at: https://localhost:7000/api/rooms
```

### Docker Deployment

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 80
ENTRYPOINT ["dotnet", "LanArenaServer.dll"]
```

Build and run:
```bash
docker build -t lanarana-server .
docker run -p 80:80 lanarana-server
```

## Testing the API

### Using cURL

```bash
# Register a room
curl -X POST http://localhost:5000/api/rooms/register \
  -H "Content-Type: application/json" \
  -d '{
    "id": "test-host-123",
    "name": "John'\''s Chess Game",
    "game": "Chess",
    "port": 47778,
    "ipAddress": "192.168.1.100"
  }'

# Get all rooms
curl http://localhost:5000/api/rooms

# Unregister room
curl -X DELETE http://localhost:5000/api/rooms/test-host-123
```

### Using Postman

1. Create a new POST request
2. URL: `http://localhost:5000/api/rooms/register`
3. Body (JSON):
```json
{
  "id": "my-game-123",
  "name": "My Game Room",
  "game": "Ludo",
  "port": 47778,
  "ipAddress": "203.0.113.42"
}
```

## Monitoring

Add logging to track room registrations:

```csharp
[HttpPost("register")]
public IActionResult RegisterRoom([FromBody] RoomInfo room)
{
    _logger.LogInformation($"Room registered: {room.Name} ({room.Game}) from {room.IpAddress}");
    room.RegisteredAt = DateTime.UtcNow;
    Rooms[room.Id] = room;
    return Ok(new { message = "Registered" });
}
```

## Security Checklist

- [ ] Add authentication (API key, OAuth, etc.)
- [ ] Implement rate limiting
- [ ] Validate IP address format
- [ ] Sanitize room name/player name
- [ ] Use HTTPS in production
- [ ] Add request size limits
- [ ] Implement CORS properly
- [ ] Monitor for suspicious activity
- [ ] Use database instead of in-memory storage
- [ ] Add proper error handling
- [ ] Implement logging
- [ ] Set up alerts for failures
