using DataSpecificationNavigatorBackend.Model;

namespace DataSpecificationNavigatorBackend.ConnectorsLayer.JsonDataClasses;

public record PropertySuggestionJson(
	string Iri,
	string Summary,
	string Reason,
	PropertySuggestionJson.Class DomainClass,
	PropertySuggestionJson.Class RangeClass)
{
	public record Class(
		string Iri,
		string Summary);
}
