using System.Text.Json;
using System.Text.Json.Serialization;

namespace go2web.Configuration;

public class AppConfig
{
    public int MaxRedirects { get; init; } = 5;
    public bool AlwaysShowHeaders { get; init; } = false;
}

[JsonSerializable(typeof(AppConfig))]
public partial class AppConfigContext : JsonSerializerContext { }

public static class ConfigLoader
{
    private static readonly string ConfigDirectory;
    private static readonly string ConfigFilePath;
    private static readonly JsonSerializerOptions Options;
    private static readonly AppConfigContext Context;

    public static string GetConfigFilePath() => ConfigFilePath;

    static ConfigLoader()
    {
        string baseDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        ConfigDirectory = Path.Combine(baseDataFolder, "go2web");
        ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");
        
        Options = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        Context = new AppConfigContext(Options);
    }

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigFilePath))
        {
            return CreateDefaultConfig();
        }

        try
        {
            string json = File.ReadAllText(ConfigFilePath);
            var config = JsonSerializer.Deserialize(json, Context.AppConfig);

            return config ?? CreateDefaultConfig();
        }
        catch (Exception ex)
        {
            Spectre.Console.AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not load config file at {ConfigFilePath}. Using defaults. Error: {ex.Message}");
            return new AppConfig();
        }
    }

    private static AppConfig CreateDefaultConfig()
    {
        var config = new AppConfig();
        
        try
        {
            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
            }

            string json = JsonSerializer.Serialize(config, Context.AppConfig);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception) { }

        return config;
    }
}
