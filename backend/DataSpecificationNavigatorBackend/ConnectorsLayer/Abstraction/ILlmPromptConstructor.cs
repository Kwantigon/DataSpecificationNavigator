using DataSpecificationNavigatorBackend.Model;

namespace DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;

public interface ILlmPromptConstructor
{
	string BuildMapToDataSpecificationPrompt(
		DataSpecification dataSpecification,
		string userQuestion);

	string BuildMapToSubstructurePrompt(
		DataSpecification dataSpecification, string userQuestion,
		DataSpecificationSubstructure substructure);

	string BuildGetSuggestedPropertiesPrompt(
		DataSpecification dataSpecification, string userQuestion,
		DataSpecificationSubstructure substructure);

	string BuildGenerateSuggestedMessagePrompt(
		DataSpecification dataSpecification, string userQuestion,
		DataSpecificationSubstructure substructure,
		List<DataSpecificationItem> selectedItems);

	string BuildDataSpecificationSummaryPrompt(
		DataSpecification dataSpecification);
}
