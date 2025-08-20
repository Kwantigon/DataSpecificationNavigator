using DataSpecificationNavigatorBackend.BusinessCoreLayer.DTO;

namespace DataSpecificationNavigatorBackend.BusinessCoreLayer.Abstraction;

public interface IConversationController
{
	Task<IResult> StartConversationAsync(PostConversationsDTO payload);
	Task<IResult> GetAllConversationsAsync();

	Task<IResult> GetConversationAsync(int conversationId);

	Task<IResult> GetConversationMessagesAsync(int conversationId);

	Task<IResult> GetMessageAsync(int conversationId, Guid messageId);

	Task<IResult> ProcessIncomingUserMessage(int conversationId, PostConversationMessagesDTO payload);

	Task<IResult> DeleteConversationAsync(int conversationId);

	Task<IResult> StoreUserSelectionAndGetSuggestedMessage(int conversationId, PutUserSelectedItemsDTO payload);

	Task<IResult> GetDataSpecificationSubstructureAsync(int conversationId);
}
