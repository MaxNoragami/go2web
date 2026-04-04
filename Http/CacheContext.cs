using System.Text.Json.Serialization;

namespace go2web.Http;

// The context used to generate source code for JSON serialization of the CacheEntry record type
[JsonSerializable(typeof(CacheEntry))]
public partial class CacheContext : JsonSerializerContext { }