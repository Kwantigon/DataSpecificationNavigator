using DataSpecificationNavigatorBackend.BusinessCoreLayer.Abstraction;
using DataSpecificationNavigatorBackend.BusinessCoreLayer.DTO;
using DataSpecificationNavigatorBackend.BusinessCoreLayer.DTO.Transformer;
using DataSpecificationNavigatorBackend.Model;

namespace DataSpecificationNavigatorBackend.BusinessCoreLayer;

/// <summary>
/// Converts the results of <see cref="IConversationService""/> to HTTP responses.
/// </summary>
public class ConversationController(
	ILogger<ConversationController> logger,
	IConversationService conversationService,
	IDataSpecificationService dataSpecificationService) : IConversationController
{
	#region Private fields
	private readonly ILogger<ConversationController> _logger = logger;
	private readonly IConversationService _conversationService = conversationService;
	private readonly IDataSpecificationService _dataSpecificationService = dataSpecificationService;
	#endregion Private fields

	public async Task<IResult> StartConversationAsync(PostConversationsDTO payload)
	{
		DataSpecification? dataSpecification = await _dataSpecificationService.ExportDataSpecificationFromDataspecerAsync(payload.DataspecerPackageUuid, payload.DataspecerPackageName);
		if (dataSpecification is null)
		{
			return Results.InternalServerError(new ErrorDTO() { Reason = "There was an error while retrieving and processing the Dataspecer package." });
		}
		Conversation conversation = await _conversationService.StartNewConversationAsync(payload.ConversationTitle, dataSpecification);
		return Results.Created($"/conversations/{conversation.Id}", (ConversationDTO)conversation);
	}

	public async Task<IResult> GetAllConversationsAsync()
	{
		IReadOnlyList<Conversation> conversations = await _conversationService.GetAllConversationsAsync();
		return Results.Ok(
			conversations.Select(conv => (ConversationDTO)conv)
		);
	}

	public async Task<IResult> GetConversationAsync(int conversationId)
	{
		Conversation? conversation = await _conversationService.GetConversationAsync(conversationId);
		if (conversation is null)
		{
			return Results.NotFound(new ErrorDTO { Reason = $"Conversation with ID {conversationId} not found." });
		}

		return Results.Ok((ConversationDTO)conversation);
	}

	public async Task<IResult> GetConversationMessagesAsync(int conversationId)
	{
		Conversation? conversation = await _conversationService.GetConversationAsync(conversationId);
		if (conversation == null)
		{
			return Results.NotFound(new ErrorDTO { Reason = $"Conversation with ID {conversationId} not found." });
		}

		List<ConversationMessageDTO> responseDTO = [];
		foreach (Message msg in conversation.Messages)
		{
			ConversationMessageDTO messageDTO = await BuildMessageDTO(msg);
			responseDTO.Add(messageDTO);
		}

		return Results.Ok(responseDTO);
	}

	public async Task<IResult> GetMessageAsync(int conversationId, Guid messageId)
	{
		Conversation? conversation = await _conversationService.GetConversationAsync(conversationId);
		if (conversation is null)
		{
			_logger.LogError("Conversation with Id {Id} not found.", conversationId);
			return Results.NotFound(new ErrorDTO { Reason = $"Conversation with ID {conversationId} not found." });
		}

		Message? requestedMessage = conversation.Messages.Find(msg => msg.Id == messageId);
		if (requestedMessage is null)
		{
			_logger.LogError("Conversation [Title={ConvTitle}, Id={ConvId}] does not contain the message with ID {MsgId}.",
																		conversation.Title, conversationId, messageId);
			return Results.NotFound(new ErrorDTO { Reason = $"Message with ID {messageId} not found." });
		}

		ConversationMessageDTO messageDTO = await BuildMessageDTO(requestedMessage);
		return Results.Ok(messageDTO);
	}

	public async Task<IResult> ProcessIncomingUserMessage(int conversationId, PostConversationMessagesDTO payload)
	{
		if (string.IsNullOrWhiteSpace(payload.TextValue))
		{
			return Results.BadRequest(new ErrorDTO { Reason = "The user message does not contain any text." });
		}

		Conversation? conversation = await _conversationService.GetConversationAsync(conversationId);
		if (conversation == null)
		{
			_logger.LogError("Conversation with ID {ConvId} not found.", conversationId);
			return Results.NotFound(new ErrorDTO { Reason = "Conversation not found." });
		}

		_logger.LogDebug("Adding the user message to the conversation.");
		UserMessage userMessage = await _conversationService.AddUserMessageAsync(conversation, payload.TextValue, DateTime.Now);
		ReplyMessage? replyMessage = await _conversationService.GenerateReplyMessageAsync(userMessage);

		return Results.Created(
			$"/conversations/{conversation.Id}/messages/{userMessage.Id}",
			new ConversationMessageDTO
			{
				Id = userMessage.Id,
				Sender = userMessage.Sender,
				TextContent = userMessage.TextContent,
				Timestamp = userMessage.Timestamp,
				ReplyMessageUri = $"/conversations/{conversation.Id}/messages/{userMessage.ReplyMessageId}"
			}
		);
	}

	public async Task<IResult> StoreUserSelectionAndGetSuggestedMessage(int conversationId, PutUserSelectedItemsDTO payload)
	{
		_logger.LogDebug("Searching for the conversation with ID={Id}", conversationId);
		Conversation? conversation = await _conversationService.GetConversationAsync(conversationId);
		if (conversation is null)
		{
			_logger.LogError("Conversation with ID={Id} not found.", conversationId);
			return Results.NotFound(new ErrorDTO() { Reason = "Conversation not found" });
		}

		// Make sure the items in the payload are unique
		HashSet<string> uniqueIris = [.. payload.UserSelections.Select(s => s.PropertyIri)];
		List<DataSpecificationItem> selectedItems = await _dataSpecificationService.GetDataSpecificationItemsAsync(
			conversation.DataSpecification.Id, uniqueIris.ToList());

		if (selectedItems.Count != uniqueIris.Count)
		{
			List<string> itemsNotFound = uniqueIris.Where(iri => !selectedItems.Any(itemFound => itemFound.Iri == iri)).ToList();
			_logger.LogError("The following item IRIs were not found: {Items}", itemsNotFound);
			return Results.BadRequest(new ErrorDTO() { Reason = "One or more selected items are not present in the data specification." });
		}

		List<UserSelection?> userSelections = payload.UserSelections
			.Select(selection =>
			{
				if (uniqueIris.Contains(selection.PropertyIri))
				{
					return new UserSelection()
					{
						ConversationId = conversation.Id,
						Conversation = conversation,
						SelectedPropertyIri = selection.PropertyIri,
						IsOptional = selection.IsOptional,
						IsSelectTarget = selection.IsSelectTarget,
						FilterExpression = selection.FilterExpression
					};
				}
				else
				{
					return null;
				}
			})
			.Where(selection => selection != null)
			.ToList();
		string? suggestedMessage = await _conversationService.UpdateSelectedPropertiesAndGenerateSuggestedMessageAsync(conversation, uniqueIris, userSelections!);
		if (string.IsNullOrEmpty(suggestedMessage))
		{
			_logger.LogError("The suggested message is either null or empty.");
			return Results.InternalServerError(new ErrorDTO() { Reason = "There was an error while generating the suggested message." });
		}

		return Results.Ok(new SuggestedMessageDTO(suggestedMessage));
	}

	public async Task<IResult> DeleteConversationAsync(int conversationId)
	{
		bool result = await _conversationService.DeleteConversationAndAssociatedDataSpecificationAsync(conversationId);
		if (result)
		{
			return Results.Ok();
		}
		else
		{
			return Results.InternalServerError(new ErrorDTO { Reason = "There was an unexpected error while deleting the conversation." });
		}
	}

	public async Task<IResult> GetDataSpecificationSubstructureAsync(int conversationId)
	{
		Conversation? conversation = await _conversationService.GetConversationAsync(conversationId);
		if (conversation is null)
		{
			return Results.NotFound(new ErrorDTO { Reason = $"Conversation with ID {conversationId} not found." });
		}
		DataSpecificationSubstructure substructure = conversation.DataSpecificationSubstructure;
		if (substructure == null)
		{
			_logger.LogWarning("Conversation with ID {Id} does not have a data specification substructure.", conversationId);
			return Results.Ok(new DataSpecificationSubstructure());
		}
		return Results.Ok(substructure);
	}

	private async Task<ConversationMessageDTO> BuildMessageDTO(Message message)
	{
		ConversationMessageDTO messageDTO;
		if (message is UserMessage userMessage)
		{
			messageDTO = new()
			{
				Id = message.Id,
				Sender = message.Sender,
				TextContent = message.TextContent,
				Timestamp = message.Timestamp,
				ReplyMessageUri = $"/conversations/{message.Conversation.Id}/messages/{userMessage.ReplyMessageId}"
			};
		}
		else if (message is ReplyMessage replyMessage)
		{
			messageDTO = await BuildMessageDTOFromReplyAsync(replyMessage);
		}
		else // This is a WelcomeMessage.
		{
			messageDTO = new()
			{
				Id = message.Id,
				Sender = message.Sender,
				TextContent = message.TextContent,
				Timestamp = message.Timestamp
			};
		}

		return messageDTO;
	}

	private async Task<ConversationMessageDTO> BuildMessageDTOFromReplyAsync(ReplyMessage replyMessage)
	{
		ConversationMessageDTO messageDTO = new();
		messageDTO.Id = replyMessage.Id;
		messageDTO.MappingText = replyMessage.MappingText;

		List<DataSpecificationItemMapping> itemMappings =
			await _conversationService.GetMappingsOfReplyMessage(replyMessage);
		messageDTO.MappedItems = itemMappings
			.Select(m => new MappedItemDTO
			{
				Iri = m.ItemIri,
				Label = m.Item.Label,
				Summary = m.Item.Summary,
				MappedWords = m.MappedWords
			})
			.ToList();

		messageDTO.SparqlText = replyMessage.SparqlText;
		messageDTO.SparqlQuery = replyMessage.SparqlQuery;
		messageDTO.SuggestItemsText = replyMessage.SuggestPropertiesText;

		List<DataSpecificationPropertySuggestion> itemSuggestions =
			await _conversationService.GetSuggestedPropertiesOfReplyMessage(replyMessage);
		SuggestionsTransformer transformer = new();
		messageDTO.Suggestions = transformer.TransformSuggestedProperties(itemSuggestions, replyMessage.Conversation.DataSpecificationSubstructure);
		return messageDTO;
	}
}
