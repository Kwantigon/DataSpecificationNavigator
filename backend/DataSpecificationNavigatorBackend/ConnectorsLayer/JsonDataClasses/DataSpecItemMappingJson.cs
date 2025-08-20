using DataSpecificationNavigatorBackend.Model;

namespace DataSpecificationNavigatorBackend.ConnectorsLayer.JsonDataClasses;

public record DataSpecItemMappingJson(
	string Iri,
	string Summary,
	string MappedWords,
	bool IsSelectTarget = false);
