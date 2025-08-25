namespace DataSpecificationNavigatorBackend.ConnectorsLayer.JsonDataClasses;

public record PropertySuggestionJson(
	string Iri,
	string Summary,
	string Reason,
	PropertySuggestionJson.ClassJson DomainClass,
	PropertySuggestionJson.ClassJson RangeClass)
{
	public record ClassJson(
		string Iri,
		string Summary);
}
