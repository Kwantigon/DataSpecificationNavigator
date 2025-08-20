using DataSpecificationNavigatorBackend.Model;

namespace DataSpecificationNavigatorBackend.BusinessCoreLayer.Abstraction;

public interface IConversationService
{
	Task<Conversation> StartNewConversationAsync(
		string conversationTitle, DataSpecification dataSpecification);

	Task<IReadOnlyList<Conversation>> GetAllConversationsAsync();

	Task<Conversation?> GetConversationAsync(int conversationId);

	Task<UserMessage> AddUserMessageAsync(
		Conversation conversation, string messageText, DateTime timestamp);

	Task<ReplyMessage?> GenerateReplyMessageAsync(UserMessage userMessage);

	Task<string?> UpdateSelectedPropertiesAndGenerateSuggestedMessageAsync(
		Conversation conversation,
		HashSet<string> selectedProperties,
		List<UserSelection> userSelections);

	Task<bool> DeleteConversationAndAssociatedDataSpecificationAsync(int conversationId);

	Task<List<DataSpecificationItemMapping>> GetMappingsOfReplyMessage(ReplyMessage replyMessage);

	Task <List<DataSpecificationPropertySuggestion>> GetSuggestedPropertiesOfReplyMessage(ReplyMessage replyMessage);
}
