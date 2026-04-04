using System.Text.Json.Serialization;

namespace go2web.Configuration;

// The context used to generate source code for JSON serialization of the AppConfig record type
[JsonSerializable(typeof(AppConfig))]
public partial class AppConfigContext : JsonSerializerContext { }