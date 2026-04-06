using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Graphode.BillingEntitlementsService.Contracts.ContractArtifacts;
using NJsonSchema;
using NJsonSchema.Generation;

var outputDirectory = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../helper-ssot/contracts"));

Directory.CreateDirectory(outputDirectory);

var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
serializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

var generatorSettings = new SystemTextJsonSchemaGeneratorSettings
{
    SerializerOptions = serializerOptions
};

var indexEntries = new List<object>();

foreach (var definition in ContractArtifactCatalog.Definitions.OrderBy(definition => definition.FileName, StringComparer.Ordinal))
{
    var schema = JsonSchema.FromType(definition.ContractType, generatorSettings);
    var rawJson = schema.ToJson();
    var formattedJson = JsonSerializer.Serialize(
        JsonSerializer.Deserialize<JsonElement>(rawJson),
        new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });

    var filePath = Path.Combine(outputDirectory, definition.FileName);
    await File.WriteAllTextAsync(filePath, formattedJson + Environment.NewLine, Encoding.UTF8);

    indexEntries.Add(new
    {
        contractId = definition.ContractId,
        category = GetCategory(definition.ContractId),
        subject = GetSubject(definition.ContractId),
        artifactKind = GetArtifactKind(definition.ContractId),
        fileName = definition.FileName,
        description = definition.Description,
        schemaTitle = schema.Title,
        typeName = GetReadableTypeName(definition.ContractType),
        clrType = definition.ContractType.FullName
    });
}

var indexPath = Path.Combine(outputDirectory, "index.json");
await File.WriteAllTextAsync(
    indexPath,
    JsonSerializer.Serialize(
        new
        {
            catalog = "graphode-baseline-contracts",
            description = "Baseline-local machine-readable contract catalog for the canonical Graphode .NET skeleton.",
            artifacts = indexEntries
        },
        new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }) + Environment.NewLine,
    Encoding.UTF8);

Console.WriteLine($"Generated {ContractArtifactCatalog.Definitions.Count} contract schemas into {outputDirectory}.");

static string GetCategory(string contractId) => contractId.Split('.', StringSplitOptions.RemoveEmptyEntries)[0];

static string GetArtifactKind(string contractId) => contractId.Split('.', StringSplitOptions.RemoveEmptyEntries)[^1];

static string GetSubject(string contractId)
{
    var parts = contractId.Split('.', StringSplitOptions.RemoveEmptyEntries);
    return parts.Length <= 2 ? contractId : string.Join('.', parts[1..^1]);
}

static string GetReadableTypeName(Type type)
{
    if (!type.IsGenericType)
    {
        return type.Name;
    }

    var baseName = type.Name.Split('`', 2)[0];
    var genericArguments = string.Join(", ", type.GetGenericArguments().Select(GetReadableTypeName));
    return $"{baseName}<{genericArguments}>";
}
