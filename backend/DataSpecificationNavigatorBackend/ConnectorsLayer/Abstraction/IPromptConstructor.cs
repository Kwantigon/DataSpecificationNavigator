using DataSpecificationNavigatorBackend.Model;

namespace DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;

public interface IPromptConstructor
{
	string BuildMapToDataSpecificationPrompt(DataSpecification dataSpecification, string userQuestion);

	string BuildMapToSubstructurePrompt(DataSpecification dataSpecification, string userQuestion, DataSpecificationSubstructure substructure);

	string BuildGetSuggestedItemsPrompt(DataSpecification dataSpecification, string userQuestion, DataSpecificationSubstructure substructure);

	string BuildGenerateSuggestedMessagePrompt(DataSpecification dataSpecification, string userQuestion, DataSpecificationSubstructure substructure, List<DataSpecificationItem> selectedItems);
}
