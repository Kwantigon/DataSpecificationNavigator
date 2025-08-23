using System.Text.Json.Serialization;

namespace DataSpecificationNavigatorBackend.BusinessCoreLayer.DTO;

public class SuggestionsDTO
{
	public List<GroupedSuggestionsDto> DirectConnections { get; set; } = new();

	public List<GroupedSuggestionsDto> IndirectConnections { get; set; } = new();
}
public record SuggestedPropertyDTO
{
	[JsonPropertyName("iri")]
	public required string Iri { get; init; }

	[JsonPropertyName("label")]
	public required string Label { get; init; }

	[JsonPropertyName("connection")]
	public required string Connection { get; init; }

	[JsonPropertyName("reason")]
	public required string Reason { get; init; }

	[JsonPropertyName("summary")]
	public required string Summary { get; set; }

	[JsonPropertyName("type")]
	public required SuggestedPropertyDTOType Type { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<SuggestedPropertyDTOType>))]
public enum SuggestedPropertyDTOType
{
	ObjectProperty,
	DatatypeProperty
}

public record GroupedSuggestionsDto(string ItemExpanded, List<SuggestedPropertyDTO> Suggestions);
