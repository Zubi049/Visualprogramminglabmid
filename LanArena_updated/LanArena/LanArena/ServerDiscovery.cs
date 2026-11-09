using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text;

namespace LanArena;

/// <summary>
/// Cloud-based room discovery service. Allows players to find and join games across any network.
/// Communicates with a central server to register and discover game rooms.
/// </summary>
public class ServerDiscovery
{
    /// <summary>Default public server URL (can be overridden in UI)</summary>
    public static string DefaultServerUrl = "https://lanarena-server.azurewebsites.net";

    public event Action<Room> RoomSeen;
    public event Action<string> Error;

    readonly string serverId;
    readonly string serverUrl;
    readonly HttpClient http;
    CancellationTokenSource pollCts;
    bool isPolling;

    public ServerDiscovery(string serverId, string serverUrl = "")
    {
        this.serverId = serverId;
        this.serverUrl = string.IsNullOrWhiteSpace(serverUrl) ? DefaultServerUrl : serverUrl;
        http = new HttpClient();
    }

    /// <summary>Register a game room with the cloud server</summary>
    public async Task RegisterRoomAsync(string hostId, string hostName, string game, int tcpPort, string localIp)
    {
        try
        {
            var payload = new
            {
                id = hostId,
                name = hostName,
                game = game,
                port = tcpPort,
                ipAddress = localIp,
                timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await http.PostAsync($"{serverUrl}/api/rooms/register", content);

            if (!response.IsSuccessStatusCode)
                Error?.Invoke($"Failed to register room: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Registration error: {ex.Message}");
        }
    }

    /// <summary>Unregister a game room from the cloud server</summary>
    public async Task UnregisterRoomAsync(string hostId)
    {
        try
        {
            var response = await http.DeleteAsync($"{serverUrl}/api/rooms/{hostId}");
            if (!response.IsSuccessStatusCode)
                Error?.Invoke($"Failed to unregister room: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Unregistration error: {ex.Message}");
        }
    }

    /// <summary>Fetch available rooms from the cloud server</summary>
    public async Task<List<Room>> FetchRoomsAsync()
    {
        var rooms = new List<Room>();
        try
        {
            var response = await http.GetAsync($"{serverUrl}/api/rooms");
            if (!response.IsSuccessStatusCode)
            {
                Error?.Invoke($"Failed to fetch rooms: {response.StatusCode}");
                return rooms;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var roomElement in root.EnumerateArray())
                {
                    if (int.TryParse(roomElement.GetProperty("port").GetString(), out int port))
                    {
                        rooms.Add(new Room
                        {
                            Id = roomElement.GetProperty("id").GetString(),
                            Host = roomElement.GetProperty("name").GetString(),
                            Game = roomElement.GetProperty("game").GetString(),
                            Ip = roomElement.GetProperty("ipAddress").GetString(),
                            Port = port,
                            Seen = DateTime.Now
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Fetch error: {ex.Message}");
        }

        return rooms;
    }

    /// <summary>Start polling the server for available rooms</summary>
    public void StartPolling(int intervalMs = 2000)
    {
        if (isPolling) return;
        isPolling = true;
        pollCts = new CancellationTokenSource();

        Task.Run(async () =>
        {
            while (!pollCts.Token.IsCancellationRequested)
            {
                try
                {
                    var rooms = await FetchRoomsAsync();
                    foreach (var room in rooms)
                        RoomSeen?.Invoke(room);
                    await Task.Delay(intervalMs, pollCts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Error?.Invoke($"Polling error: {ex.Message}");
                }
            }
        });
    }

    /// <summary>Stop polling the server</summary>
    public void StopPolling()
    {
        isPolling = false;
        pollCts?.Cancel();
    }

    public void Dispose()
    {
        StopPolling();
        http?.Dispose();
        pollCts?.Dispose();
    }
}
