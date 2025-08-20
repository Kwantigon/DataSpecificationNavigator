using DataSpecificationNavigatorBackend.Model;

namespace DataSpecificationNavigatorBackend.BusinessCoreLayer.DTO;

public record DataSpecificationItemDTO(string Iri, string Label, ItemType type, string? Summary, string SummaryEndpoint)
{
	public static explicit operator DataSpecificationItemDTO(DataSpecificationItem item)
	{
		return new DataSpecificationItemDTO(
			item.Iri, item.Label, item.Type, item.Summary,
			$"/data-specifications/{item.DataSpecificationId}/items/summary?itemIri={Uri.EscapeDataString(item.Iri)}"
		);
	}
}
