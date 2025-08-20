using DataSpecificationNavigatorBackend.Model;

namespace DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;

public interface ILlmConnector
{
	Task<List<DataSpecificationItemMapping>> MapUserMessageToDataSpecificationAsync(DataSpecification dataSpecification, UserMessage userMessage);

	Task<List<DataSpecificationItemMapping>> MapUserMessageToSubstructureAsync(DataSpecification dataSpecification, DataSpecificationSubstructure dataSpecificationSubstructure, UserMessage userMessage);

	Task<List<DataSpecificationPropertySuggestion>> GetSuggestedPropertiesAsync(DataSpecification dataSpecification, DataSpecificationSubstructure dataSpecificationSubstructure, UserMessage userMessage);

	Task<string> GenerateItemSummaryAsync(DataSpecificationItem dataSpecificationItem);

	Task<string> GenerateSuggestedMessageAsync(DataSpecification dataSpecification, UserMessage userMessage, DataSpecificationSubstructure substructure, List<DataSpecificationItem> selectedItems);
}
