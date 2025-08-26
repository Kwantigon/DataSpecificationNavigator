namespace DataSpecificationNavigatorBackend.BusinessCoreLayer.DTO;

public record PutUserSelectedItemsDTO(List<UserSelectionDTO> UserSelections);

public record UserSelectionDTO(
	string PropertyIri,
	bool IsOptional = false,
	string? FilterExpression = null);
