using ConsoleAppFramework;
using Spectre.Console;
using go2web.Configuration;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace go2web.Commands;

public partial class Commands
{
    /// <summary>Open the configuration file in the default editor.</summary>
    [Command("--config")]
    public void EditConfig()
    {
        // Ensure config is loaded/created first
        ConfigLoader.Load();

        string configPath = ConfigLoader.GetConfigFilePath();

        string editor = Environment.GetEnvironmentVariable("VISUAL") 
            ?? Environment.GetEnvironmentVariable("EDITOR") 
            ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "notepad" : "nano");

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = editor,
                    UseShellExecute = false
                }
            };
            
            process.StartInfo.ArgumentList.Add(configPath);

            process.Start();
            process.WaitForExit();

            AnsiConsole.MarkupLine($"[green]Configuration file at[/] [dim]{configPath}[/] [green]closed.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error opening editor '{editor}':[/] {ex.Message}");
            AnsiConsole.MarkupLine($"[yellow]You can manually edit the config file at:[/] {configPath}");
        }
    }
}
