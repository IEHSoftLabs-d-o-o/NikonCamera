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
    public const string DefaultArianaLabClientPath = @"C:\LK\Tools\Ariana Lab Client";
    public const string DefaultArianaLabClientExecutable = "ArianaLabClient.UI.exe";

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
    public string ArianaLabClientPath { get; set; } = DefaultArianaLabClientPath;
    public string ArianaLabClientExecutable { get; set; } = DefaultArianaLabClientExecutable;
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
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings();
            settings.Auth = Unprotect(settings.AuthProtected);
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new AppSettings();
        }
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
