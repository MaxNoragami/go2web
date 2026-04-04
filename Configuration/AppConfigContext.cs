using System.Text.Json.Serialization;

namespace go2web.Configuration;

[JsonSerializable(typeof(AppConfig))]
public partial class AppConfigContext : JsonSerializerContext { }