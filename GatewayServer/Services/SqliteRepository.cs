using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GatewayServer.Services;

public sealed class SqliteRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteRepository> _logger;

    public SqliteRepository(IConfiguration configuration, ILogger<SqliteRepository> logger)
    {
        _logger = logger;

        var dbPath = configuration["Database:Path"] ?? "edge.db";
        var fullPath = Path.GetFullPath(dbPath);

        _connectionString = $"Data Source={fullPath}";

        _logger.LogInformation("SQLite DB PATH = {Path}", fullPath);

        EnsureTables();
    }

    private void EnsureTables()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        _logger.LogInformation("SQLite connected OK");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        CREATE TABLE IF NOT EXISTS gateway_mqtt (
            gateway_id TEXT PRIMARY KEY,
            last_heartbeat_at TEXT NULL,
            online_status INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS devices (
            device_id TEXT PRIMARY KEY,
            key TEXT NOT NULL,
            gateway_id TEXT NULL,
            auth_status INTEGER NOT NULL DEFAULT 0,
            device_profile_name TEXT NULL,
            device_profile_id TEXT NULL
        );
        """;
        cmd.ExecuteNonQuery();
    }

    public bool GatewayExists(string gatewayId)
    {
        if (string.IsNullOrWhiteSpace(gatewayId))
            return false;

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(1)
            FROM gateway_mqtt
            WHERE gateway_id = $gateway_id
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$gateway_id", gatewayId);

        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public void MarkGatewayHeartbeat(string gatewayId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE gateway_mqtt
            SET last_heartbeat_at = datetime('now'),
                online_status = 1
            WHERE gateway_id = $gateway_id;
            """;
        cmd.Parameters.AddWithValue("$gateway_id", gatewayId);

        cmd.ExecuteNonQuery();
    }
    public void UpsertDeviceProfileInfo(string deviceId, string? deviceProfileName, string? deviceProfileId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return;

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        UPDATE devices
        SET device_profile_name = $device_profile_name,
            device_profile_id   = $device_profile_id
        WHERE device_id = $device_id;
        """;

        cmd.Parameters.AddWithValue("$device_id", deviceId);
        cmd.Parameters.AddWithValue("$device_profile_name",
            string.IsNullOrWhiteSpace(deviceProfileName) ? DBNull.Value : deviceProfileName);
        cmd.Parameters.AddWithValue("$device_profile_id",
            string.IsNullOrWhiteSpace(deviceProfileId) ? DBNull.Value : deviceProfileId);

        cmd.ExecuteNonQuery();
    }
    public List<string> GetExpiredGatewayIds(int timeoutSeconds)
    {
        var result = new List<string>();

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        SELECT gateway_id
        FROM gateway_mqtt
        WHERE online_status = 1
          AND last_heartbeat_at IS NOT NULL
          AND datetime(last_heartbeat_at) < datetime('now', '-' || $timeout_seconds || ' seconds');
        """;

        cmd.Parameters.AddWithValue("$timeout_seconds", timeoutSeconds);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }
    public void MarkGatewayOffline(string gatewayId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE gateway_mqtt
            SET online_status = 0
            WHERE gateway_id = $gateway_id;
            """;
        cmd.Parameters.AddWithValue("$gateway_id", gatewayId);

        cmd.ExecuteNonQuery();
    }

    public void SetDevicesAuthStatusByGateway(string gatewayId, int authStatus)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE devices
            SET auth_status = $auth_status
            WHERE gateway_id = $gateway_id;
            """;
        cmd.Parameters.AddWithValue("$auth_status", authStatus);
        cmd.Parameters.AddWithValue("$gateway_id", gatewayId);

        cmd.ExecuteNonQuery();
    }

    public DeviceState? GetDeviceState(string deviceId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT device_id, otpCode, gateway_id, auth_status
            FROM devices
            WHERE device_id = $device_id
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$device_id", deviceId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        return new DeviceState
        {
            DeviceId = reader["device_id"]?.ToString() ?? "",
            OtpCode = reader["otpCode"]?.ToString() ?? "",
            GatewayId = reader["gateway_id"] == DBNull.Value ? null : reader["gateway_id"]?.ToString(),
            AuthStatus = Convert.ToInt32(reader["auth_status"])
        };
    }

    public int UpdateAuthStatus(string deviceId, int authStatus)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE devices
            SET auth_status = $auth_status
            WHERE device_id = $device_id;
            """;
        cmd.Parameters.AddWithValue("$auth_status", authStatus);
        cmd.Parameters.AddWithValue("$device_id", deviceId);

        return cmd.ExecuteNonQuery();
    }

    public int UpdateGatewayAndAuthStatus(string deviceId, string gatewayId, int authStatus)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE devices
            SET gateway_id = $gateway_id,
                auth_status = $auth_status
            WHERE device_id = $device_id;
            """;
        cmd.Parameters.AddWithValue("$gateway_id", gatewayId);
        cmd.Parameters.AddWithValue("$auth_status", authStatus);
        cmd.Parameters.AddWithValue("$device_id", deviceId);

        return cmd.ExecuteNonQuery();
    }

    public AuthorizedRoute? GetAuthorizedRouteByDeviceId(string deviceId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT d.device_id, d.gateway_id
            FROM devices d
            INNER JOIN gateway_mqtt g ON g.gateway_id = d.gateway_id
            WHERE d.device_id = $device_id
              AND d.auth_status = 1
              AND d.gateway_id IS NOT NULL
              AND TRIM(d.gateway_id) <> ''
              AND g.online_status = 1
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$device_id", deviceId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        return new AuthorizedRoute
        {
            DeviceId = reader["device_id"]?.ToString() ?? "",
            GatewayId = reader["gateway_id"]?.ToString() ?? ""
        };
    }
    public List<BootstrapDeviceInfo> GetBootstrapDevicesByGatewayId(string gatewayId)
    {
        var result = new List<BootstrapDeviceInfo>();

        if (string.IsNullOrWhiteSpace(gatewayId))
            return result;

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        SELECT device_id, MacAddress
        FROM devices
        WHERE gateway_id = $gateway_id
          AND device_id IS NOT NULL
          AND TRIM(device_id) <> ''
          AND MacAddress IS NOT NULL
          AND TRIM(MacAddress) <> '';
        """;
        cmd.Parameters.AddWithValue("$gateway_id", gatewayId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new BootstrapDeviceInfo
            {
                DeviceId = reader["device_id"]?.ToString() ?? "",
                MacAddress = reader["MacAddress"]?.ToString() ?? "",
                Port = 502
            });
        }

        return result;
    }
    public DeviceProfileInfo? GetDeviceProfileInfo(string deviceId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT device_id, device_profile_name, device_profile_id
            FROM devices
            WHERE device_id = $device_id
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$device_id", deviceId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        return new DeviceProfileInfo
        {
            DeviceId = reader["device_id"]?.ToString() ?? "",
            DeviceProfileName = reader["device_profile_name"]?.ToString() ?? "",
            DeviceProfileId = reader["device_profile_id"]?.ToString() ?? ""
        };
    }
}

public sealed class DeviceState
{
    public string DeviceId { get; set; } = "";
    public string OtpCode { get; set; } = "";
    public string? GatewayId { get; set; }
    public int AuthStatus { get; set; }
}

public sealed class AuthorizedRoute
{
    public string DeviceId { get; set; } = "";
    public string GatewayId { get; set; } = "";
}

public sealed class DeviceProfileInfo
{
    public string DeviceId { get; set; } = "";
    public string DeviceProfileName { get; set; } = "";
    public string DeviceProfileId { get; set; } = "";
}

public sealed class BootstrapDeviceInfo
{
    public string DeviceId { get; set; } = "";
    public string MacAddress { get; set; } = "";
    public int Port { get; set; } = 502;
}