namespace DataSpecificationNavigatorBackend.BusinessCoreLayer.DTO;

public record PutUserSelectedItemsDTO(List<UserSelectionDTO> UserSelections);

public record UserSelectionDTO(
	string PropertyIri,
	bool IsOptional = false,
	bool IsSelectTarget = true,
	string? FilterExpression = null);
