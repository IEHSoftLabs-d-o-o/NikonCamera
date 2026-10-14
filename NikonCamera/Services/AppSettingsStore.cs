using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace NikonCamera.Services;

public sealed class AppSettings
{
    public const string DefaultFotoFolder = @"C:\Fotos";
    public const string DefaultBaseUrl = "https://arianalab.drberns.de";
    public const string DefaultAuth = "ArianaLab.NikonCamera:ArianaLab.NikonCamera";
    public const string DefaultArianaLabClient = @"C:\LK\Tools\Ariana Lab Client\ArianaLabClient.UI.exe";

    public double Angle { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public double WindowWidth { get; set; } = 1000;
    public double WindowHeight { get; set; } = 800;
    public double WindowLeft { get; set; }
    public double WindowTop { get; set; }
    public WindowState WindowState { get; set; } = WindowState.Normal;
    public string FotoFolder { get; set; } = DefaultFotoFolder;
    public string BaseUrl { get; set; } = DefaultBaseUrl;
    public string ArianaLabClient { get; set; } = DefaultArianaLabClient;
    public string? AuthProtected { get; set; }

    [JsonIgnore]
    public string Auth { get; set; } = DefaultAuth;
}

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NikonCamera",
        "settings.json");

    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.Auth = Unprotect(settings.AuthProtected);
            MigrateArianaLabClient(json, settings);
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new AppSettings();
        }
    }

    private static void MigrateArianaLabClient(string json, AppSettings settings)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || document.RootElement.TryGetProperty(nameof(AppSettings.ArianaLabClient), out _)
            || !document.RootElement.TryGetProperty("ArianaLabClientPath", out var pathElement)
            || !document.RootElement.TryGetProperty("ArianaLabClientExecutable", out var executableElement))
            return;

        var path = pathElement.GetString();
        var executable = executableElement.GetString();
        if (!string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(executable))
            settings.ArianaLabClient = Path.Combine(path, executable);
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        settings.AuthProtected = Protect(settings.Auth);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static string Unprotect(string? protectedAuth)
    {
        if (string.IsNullOrWhiteSpace(protectedAuth))
            return AppSettings.DefaultAuth;

        try
        {
            var protectedBytes = Convert.FromBase64String(protectedAuth);
            var authBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var auth = Encoding.UTF8.GetString(authBytes);
            return string.IsNullOrWhiteSpace(auth) ? AppSettings.DefaultAuth : auth;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return AppSettings.DefaultAuth;
        }
    }

    private static string Protect(string auth)
    {
        if (string.IsNullOrWhiteSpace(auth))
            return string.Empty;

        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(auth),
            optionalEntropy: null,
            DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }
}
