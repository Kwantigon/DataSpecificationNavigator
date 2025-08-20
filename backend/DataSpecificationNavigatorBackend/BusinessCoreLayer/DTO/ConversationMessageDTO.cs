using DataSpecificationNavigatorBackend.Model;
using System.Text.Json.Serialization;

namespace DataSpecificationNavigatorBackend.BusinessCoreLayer.DTO;

public record ConversationMessageDTO
{
	#region Common fields for all types of messages.
	[JsonPropertyName("id")]
	public Guid Id { get; set; }

	[JsonPropertyName("sender")]
	public MessageSource Sender { get; set; }

	[JsonPropertyName("text")]
	public string? TextContent { get; set; }

	[JsonPropertyName("timestamp")]
	public DateTime Timestamp { get; set; }
	#endregion Common fields for all types of messages.

	#region Fields for reply messages.
	[JsonPropertyName("mappingText")]
	public string? MappingText { get; set; }

	[JsonPropertyName("mappedItems")]
	public List<MappedItemDTO> MappedItems { get; set; } = [];

	[JsonPropertyName("sparqlText")]
	public string? SparqlText { get; set; }

	[JsonPropertyName("sparqlQuery")]
	public string? SparqlQuery { get; set; }

	[JsonPropertyName("suggestItemsText")]
	public string? SuggestItemsText { get; set; }

	[JsonPropertyName("suggestions")]
	public SuggestionsDTO Suggestions { get; set; }
	#endregion Fields for reply messages.

	#region Fields for user messages.

	[JsonPropertyName("replyMessageUri")]
	public string? ReplyMessageUri { get; set; }
	#endregion Fields for user messages.
}

public record MappedItemDTO
{
	[JsonPropertyName("iri")]
	public required string Iri { get; init; }

	[JsonPropertyName("label")]
	public required string Label { get; init; }

	[JsonPropertyName("summary")]
	public string? Summary { get; set; }
	[JsonPropertyName("mappedWords")]
	public required string MappedWords { get; init; }

	[JsonPropertyName("mappedOrSuggested")]
	public string MappedOrSuggested => "Mapped"; // This property is purely for the front end to identify, which type of item it is.
}

public record SuggestedItemDTO
{
	[JsonPropertyName("iri")]
	public required string Iri { get; init; }

	[JsonPropertyName("label")]
	public required string Label { get; init; }

	[JsonPropertyName("connection")]
	public required string Connection { get; init; }

	[JsonPropertyName("summary")]
	public string? Summary { get; set; }

	[JsonPropertyName("reason")]
	public required string Reason { get; init; }

	[JsonPropertyName("mappedOrSuggested")]
	public string MappedOrSuggested => "Suggested"; // This property is purely for the front end to identify, which type of item it is.
}

public record ArrowSuggestionDto(string Iri, string Label, string Connection, string Reason, string Summary, string MappedOrSuggested = "Suggested");
public record GroupedSuggestionsDto(string ItemExpanded, List<ArrowSuggestionDto> Suggestions);

public class SuggestionsDTO
{
	public List<GroupedSuggestionsDto> DirectConnections { get; set; } = new();
	public List<GroupedSuggestionsDto> IndirectConnections { get; set; } = new();
}