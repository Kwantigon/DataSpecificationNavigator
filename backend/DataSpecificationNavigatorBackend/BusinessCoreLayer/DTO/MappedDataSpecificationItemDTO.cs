using System.Text.Json.Serialization;

namespace DataSpecificationNavigatorBackend.BusinessCoreLayer.DTO;

public class MappedDataSpecificationItemDTO
{
	[JsonPropertyName("iri")]
	public required string Iri { get; set; }

	[JsonPropertyName("label")]
	public required string Label { get; set; }

	[JsonPropertyName("summary")]
	public required string Summary { get; set; }

	[JsonPropertyName("mappedPhrase")]
	public required string MappedPhrase { get; set; }

	[JsonPropertyName("startIndex")]
	public required int StartIndex { get; set; }

	[JsonPropertyName("endIndex")]
	public required int EndIndex { get; set; }
}
