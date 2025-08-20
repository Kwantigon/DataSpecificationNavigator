namespace DataSpecificationNavigatorBackend.ConnectorsLayer.JsonDataClasses;

public record SubstructureItemMappingJson(
	string Iri,
	string MappedWords,
	bool IsSelectTarget = false);
