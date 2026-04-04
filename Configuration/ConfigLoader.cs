using System.Text.Json;
using Spectre.Console;

namespace go2web.Configuration;

// Responsible for loading and saving the application configuration to a JSON file in the user's local application data folder
public static class ConfigLoader
{
    private static readonly string ConfigDirectory;
    private static readonly string ConfigFilePath;
    private static readonly JsonSerializerOptions Options;
    private static readonly AppConfigContext Context;

    public static string GetConfigFilePath() => ConfigFilePath;

    // Static constructor to initialize configuration paths and JSON serialization options when the class is first accessed
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

    // Loads the configuration from the JSON file. If the file does not exist or cannot be read, it creates a new config with default values and saves it.
    public static AppConfig Load()
    {
        // If the config file does not exist, create a new one with default values and return it
        if (!File.Exists(ConfigFilePath))
        {
            return CreateDefaultConfig();
        }

        try
        {
            // Read the config file and deserialize it into an AppConfig instance
            string json = File.ReadAllText(ConfigFilePath);
            var config = JsonSerializer.Deserialize(json, Context.AppConfig);

            return config ?? CreateDefaultConfig();
        }
        catch (Exception ex)
        {
            // If there was an error loading the config, log a warning and return a new config with default values
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not load config file at {ConfigFilePath}. Using defaults. Error: {ex.Message}");
            return new AppConfig();
        }
    }

    // Creates a new AppConfig instance with default values and saves it to the config file
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
