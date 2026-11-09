# LAN Arena - Quick Start Guide (Cross-Network Play)

## New Feature: Play with Friends Anywhere 🌍

LAN Arena now supports players joining from **any network**, not just your local Wi-Fi!

## How to Play Across Networks

### Step 1: Set Up a Server (One-Time Setup)

You need a simple server to connect players worldwide. Choose one:

**Option A: Azure (Easiest)**
- Click: [Deploy to Azure](https://portal.azure.com/)
- Use free tier initially
- See `SERVER_IMPLEMENTATION_GUIDE.md` for detailed steps

**Option B: Run Locally** (for testing)
- Install .NET 8 SDK
- Run server on your PC
- Others connect via your IP address

**Option C: Custom Server**
- Use provided ASP.NET Core code
- Deploy anywhere (AWS, Heroku, etc.)

### Step 2: Update Server URL in LAN Arena

**In Code:**
1. Open `MainForm.cs`
2. Find `ServerDiscovery` initialization
3. Update server URL:
```csharp
serverDiscovery = new ServerDiscovery(myId, "https://your-server.com");
```

**Via Settings Dialog:**
1. (Coming soon) Settings button in main window
2. Enter cloud server URL
3. Click Save

### Step 3: Host or Join Games

**Hosting:**
1. Click "Host a game"
2. Game broadcasts to:
   - Local LAN (UDP)
   - Cloud server (if configured)
3. Remote friends can see and join

**Joining:**
1. See rooms from all networks
2. Click "Join selected room"
3. Instantly connected!

## Features

✅ **Local LAN** - Play with friends on same Wi-Fi (existing)
✅ **Cloud Network** - Play with friends anywhere (NEW)
✅ **Backward Compatible** - LAN still works without internet
✅ **Easy Toggle** - Enable/disable cloud discovery in settings
✅ **No Cost** - Use free Azure tier or self-host

## Common Scenarios

### Scenario 1: Playing with Friends on Same Wi-Fi
```
Friend 1 ────┐
             ├─ Same Wi-Fi Network → Work without server!
Friend 2 ────┘
```
**No changes needed** - works as before!

### Scenario 2: Playing with Friend Across Town
```
Friend 1 (Your Home) ─────┐
                          ├─ Internet ─ Cloud Server
Friend 2 (Their Home) ───┘
                          └─ Game connects!
```
**Need**: Cloud server running

### Scenario 3: Tournament with 10+ Players
```
Player 1 ─┐
Player 2  ├─ Worldwide
...       ├─ All find each other
Player 10─┘ via cloud server
```
**Need**: Cloud server with proper setup

## Quick Server Setup (5 minutes)

### Option 1: Azure (Recommended)
```bash
# Login to Azure
az login

# Create resource group
az group create --name LanArena --location eastus

# Create free App Service
az appservice plan create --name LanArenaFree --resource-group LanArena --sku FREE

# Create web app  
az webapp create --name lanarena-game --resource-group LanArena --plan LanArenaFree

# Deploy (after building)
dotnet publish -c Release
az webapp deployment source config-zip --resource-group LanArena --name lanarena-game --src-path app.zip
```

### Option 2: Local Testing
```bash
cd LanArenaServer
dotnet run --launch-profile https
# Server runs on: https://localhost:7000
# Update MainForm.cs ServerUrl to: https://localhost:7000
# Friends use your PC IP: https://YOUR-IP:7000
```

## Settings (When Available in UI)

**Cloud Discovery**
- ☑ Enabled (find rooms worldwide)
- ☐ Disabled (LAN only)

**LAN Discovery**
- ☑ Enabled (find local rooms)
- ☐ Disabled (cloud only - not recommended)

**Server URL**
- Default: https://lanarena-server.azurewebsites.net
- Custom: https://your-custom-server.com

**Polling Interval**
- How often to check for new rooms
- 2000ms (default) - fast updates
- 5000ms (5 sec) - less network usage
- 10000ms (10 sec) - slow updates

## Network Requirements

### For Hosts
- ✅ Can be on any network
- ✅ Works behind home Wi-Fi router
- ⚠️ If remote players can't connect:
  - May need to port forward
  - Or use cloud relay (future feature)

### For Players
- ✅ Any internet connection
- ✅ Laptop, PC, potentially mobile
- ✅ Any firewall (outbound only)

## Troubleshooting

### "Can't see rooms from other networks"
**Solution:**
1. Check if cloud server URL is correct
2. Make sure server is running
3. Verify internet connection
4. Check firewall allows HTTPS

### "Joined room but can't connect"
**Solution:**
1. Host needs open port 47778
2. Try "Join by IP" with full address
3. If still fails, host may need port forwarding
4. (Future) We'll add relay server option

### "Settings won't save"
**Solution:**
1. Check AppData folder permissions
2. Try running as Administrator
3. Settings file: `C:\Users\[YOU]\AppData\Roaming\LanArena\settings.json`

### "Server connection slow"
**Solution:**
- Increase polling interval in settings
- Use closer server location
- Check internet speed

## Privacy & Security

**What data is shared?**
- Your player name
- Game type you're hosting
- Your IP address and game port
- Timestamp when room was created

**What data is NOT shared?**
- Chat messages (local only)
- Game moves (peer-to-peer)
- Personal information
- Device details

**Is it secure?**
- ✅ Uses HTTPS for server communication
- ✅ No passwords or authentication (yet)
- ✅ IP addresses are public on internet anyway
- ⚠️ For public use, ask admin for authentication

## Advanced Options

### Custom Server Setup
See `SERVER_IMPLEMENTATION_GUIDE.md` for:
- Detailed Azure deployment
- Docker containerization  
- Database setup
- Production configuration

### Testing Locally
```bash
# Terminal 1: Run server
cd LanArenaServer
dotnet run

# Terminal 2: Run game app (on same PC)
cd LanArena
dotnet run
# You'll see your game in both LAN and cloud list!

# Terminal 3+: Run game app (on other PCs)
# Update MainForm.cs to point to Terminal 1 PC
# Or use Join by IP
```

### Performance Tuning
```csharp
// In MainForm.cs

// Check for new rooms less often (save bandwidth)
serverDiscovery.StartPolling(5000);  // Every 5 seconds

// Or more often (higher CPU)
serverDiscovery.StartPolling(1000);  // Every 1 second
```

## Frequently Asked Questions

**Q: Do I need internet to play on local LAN?**
A: No! LAN discovery works offline. Cloud is optional.

**Q: Can I self-host the server?**
A: Yes! See SERVER_IMPLEMENTATION_GUIDE.md for Docker setup.

**Q: What if friend behind strict firewall?**
A: Future update will include relay server option.

**Q: How many players can the server support?**
A: Sample server handles hundreds. Production setup can handle thousands.

**Q: Can players join while game is running?**
A: Current version: No, room must be waiting for start. (Future: spectator mode)

**Q: Is it free?**
A: Yes! Server hosting might cost (Azure free tier available).

**Q: Can I use it competitively?**
A: Yes! Ratings system coming soon (future feature).

## Next Steps

1. **Test Locally**: Run sample server locally, play with friends
2. **Deploy to Azure**: Use free tier, get public URL
3. **Share with Friends**: Give them the server URL
4. **Customize**: Add authentication, logging, etc.
5. **Scale**: Upgrade server as needed

## Support

**Issues?**
- Check `IMPLEMENTATION_SUMMARY.md` for technical details
- See `SERVER_IMPLEMENTATION_GUIDE.md` for server setup
- Review `CROSS_NETWORK_ENHANCEMENT.md` for architecture

**Code:**
- `ServerDiscovery.cs` - Client-side discovery
- `MainForm.cs` - Integration point
- `AppSettings.cs` - Configuration
- Server code in `SERVER_IMPLEMENTATION_GUIDE.md`

## What's Coming Next

- ⭐ Relay server (for strict NAT/firewall)
- ⭐ In-game chat before start
- ⭐ Player ratings/rankings
- ⭐ Mobile app support
- ⭐ Tournament mode
- ⭐ Spectator mode
- ⭐ Friend lists

---

**Ready to play? Let's go! 🎮**

Questions? See the documentation files or review the code comments.
