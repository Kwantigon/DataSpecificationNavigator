using DataSpecificationNavigatorBackend.BusinessCoreLayer.Abstraction;
using DataSpecificationNavigatorBackend.ConnectorsLayer;
using DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;
using DataSpecificationNavigatorBackend.Model;
using Microsoft.EntityFrameworkCore;
using static DataSpecificationNavigatorBackend.Model.DataSpecificationSubstructure;

namespace DataSpecificationNavigatorBackend.BusinessCoreLayer;

public class ConversationService(
	ILogger<ConversationService> logger,
	AppDbContext appDbContext,
	ILlmConnector llmConnector,
	ISparqlTranslationService sparqlTranslationService) : IConversationService
{
	#region Private fields
	private readonly ILogger<ConversationService> _logger = logger;
	private readonly AppDbContext _database = appDbContext;
	private readonly ILlmConnector _llmConnector = llmConnector;
	private readonly ISparqlTranslationService _sparqlTranslationService = sparqlTranslationService;
	#endregion Private fields

	public async Task<Conversation> StartNewConversationAsync(string conversationTitle, DataSpecification dataSpecification)
	{
		Conversation conversation = new()
		{
			Title = conversationTitle,
			DataSpecification = dataSpecification
		};

		WelcomeMessage welcomeMessage = new()
		{
			TextContent = "Your data specification has been loaded. What would you like to know?",
			Timestamp = DateTime.Now,
			Conversation = conversation
		};
		conversation.AddMessage(welcomeMessage);

		_database.Conversations.Add(conversation);
		_database.WelcomeMessages.Add(welcomeMessage);
		await _database.SaveChangesAsync();
		return conversation;
	}

	public async Task<IReadOnlyList<Conversation>> GetAllConversationsAsync()
	{
		return await _database.Conversations.ToListAsync();
	}

	public async Task<Conversation?> GetConversationAsync(int conversationId)
	{
		return await _database.Conversations.SingleOrDefaultAsync(conv => conv.Id == conversationId);
	}

	public async Task<UserMessage> AddUserMessageAsync(Conversation conversation, string messageContent, DateTime timestamp)
	{
		if (string.IsNullOrWhiteSpace(messageContent))
		{
			throw new ArgumentException("Message content cannot be null or empty.", nameof(messageContent));
		}

		// Create the new user message object.
		UserMessage userMessage = new()
		{
			TextContent = messageContent,
			Timestamp = timestamp,
			Conversation = conversation
		};

		conversation.AddMessage(userMessage);
		await _database.UserMessages.AddAsync(userMessage);

		// Map the user message to the data specification items.
		if (string.IsNullOrWhiteSpace(conversation.SuggestedMessage) ||
				userMessage.TextContent.ToLower() != conversation.SuggestedMessage.ToLower())
		{
			// If suggested message is null or whitespace, it means that the user has not selected any of the suggested properties.
			// If the user message is not the same as the suggested message, then the user has modified the suggested message.
			// In both cases, we need treat the user message as a completely new mesage and map it to the data specification items.
			List<DataSpecificationItemMapping> mappings = await MapToDataSpecificationAsync(conversation.DataSpecification, userMessage);
			if (mappings.Count == 0)
			{
				_logger.LogError("No suitable data specification items found for the question mapping.");
			}
			else
			{
				_logger.LogDebug("Mapped the user question to the following items: [{MappedItems}]", string.Join(", ", mappings.Select(m => m.Item.Label)));
				// Create a new data specification substructure with the newly mapped items.
				conversation.DataSpecificationSubstructure = new DataSpecificationSubstructure();
				List<DataSpecificationItem> itemsToAdd = mappings.Select(m => m.Item).ToList();
				AddDataSpecItemsToConversationSubstructure(conversation, itemsToAdd, []); // No user selections at this point, so passing an empty list.
			}
		}
		else // User sent the suggested message as is, without any modifications.
		{
			_logger.LogDebug("User did not modify the suggested message.");
			if (conversation.UserSelections.Count == 0)
			{
				_logger.LogError("User sent the suggested message but there are no items for expansion selected by the user in the conversation.");
				// Just do nothing, I think.
				// To do: Return or throw an exception?
			}
			else
			{
				_logger.LogInformation("User selected properties: [{SelectedProperties}]", conversation.UserSelections);
				List<string> iriUserSelections = conversation.UserSelections
					.Select(selection => selection.SelectedPropertyIri)
					.ToList();
				List<PropertyItem> selectedProperties = await _database.DataSpecificationItems
						.Where(item => (item.Type == ItemType.ObjectProperty || item.Type == ItemType.DatatypeProperty)
								&& item.DataSpecificationId == conversation.DataSpecification.Id
								&& iriUserSelections.Contains(item.Iri))
						.Select(item => (PropertyItem)item)
						.ToListAsync();
				_logger.LogDebug("Found the following selected properties in the database: {PropertyLabels}", selectedProperties.Select(p => p.Label));

				var foundProperties = selectedProperties.Select(p => p.Iri).ToHashSet();
				var missingProperties = conversation.UserSelections
					.Select(s => s.SelectedPropertyIri)
					.Where(iri => !foundProperties.Contains(iri))
					.ToList();
				if (missingProperties.Count > 0)
				{
					_logger.LogWarning("The following UserSelectedItems were not found in the database: {MissingIris}", missingProperties);
				}

				if (selectedProperties.Count == 0)
				{
					_logger.LogError("No selected properties found in the database for the conversation.");
				}
				else
				{
					_logger.LogDebug("Creating a list with the selected properties, their domains and ranges.");
					List<DataSpecificationItem> itemsToAdd = [];
					foreach (PropertyItem property in selectedProperties)
					{
						itemsToAdd.Add(property);
						if (!itemsToAdd.Any(item => item.Iri == property.DomainIri))
						{
							itemsToAdd.Add(property.Domain);
						}

						if (property is ObjectPropertyItem objectProperty &&
								!itemsToAdd.Any(item => item.Iri == objectProperty.RangeIri))
						{
							itemsToAdd.Add(objectProperty.Range);
						}
					}

					_logger.LogDebug("Adding the selected properties and their domains and ranges to the conversation substructure.");
					AddDataSpecItemsToConversationSubstructure(conversation, itemsToAdd, conversation.UserSelections);

					// Doing this mapping so that the frontend can show which words or phrases in the user message correspond to which items.
					_logger.LogDebug("Mapping the user message to the conversation data specification substructure.");
					List<DataSpecificationItemMapping> mappings = await MapToSubstructureAsync(
						conversation.DataSpecification, conversation.DataSpecificationSubstructure, userMessage);
				}
			}
		}

		// Get suggestions for the user message.
		List<DataSpecificationPropertySuggestion> suggestions = await GetSuggestionsAsync(
			conversation.DataSpecification, conversation.DataSpecificationSubstructure, userMessage);
		_logger.LogInformation("The LLM suggested the following properties: [{SuggestedProperties}]",
			suggestions.Select(s => s.SuggestedProperty.Label));

		conversation.UserSelections.Clear();
		conversation.SuggestedMessage = null;

		// Remove all previous user selections because a new user message has been added.
		_database.UserSelections.RemoveRange(conversation.UserSelections);
		await _database.SaveChangesAsync();
		return userMessage;
	}

	public async Task<ReplyMessage?> GenerateReplyMessageAsync(UserMessage userMessage)
	{
		ReplyMessage? replyMessage = null;
		List<DataSpecificationItemMapping> itemMappings = await _database.ItemMappings
			.Where(mapping => mapping.UserMessageId == userMessage.Id)
			.ToListAsync();
		if (itemMappings.Count > 0)
		{
			string mappingText = "I have identified the following items from your data specification which play a role in your message.";
			List<string> mappedIris = itemMappings
				.Select(mapping => mapping.Item.Iri)
				.ToList();

			string sparqlText = "I have formulated a Sparql query for you:";
			string sparqlQuery = _sparqlTranslationService.TranslateSubstructure(userMessage.Conversation.DataSpecificationSubstructure);

			List<DataSpecificationPropertySuggestion> suggestions = await _database.PropertySuggestions
				.Where(suggestion => suggestion.UserMessageId == userMessage.Id)
				.ToListAsync();

			List<string> suggestedIris = [];
			string? suggestPropertiesText;
			if (suggestions.Count == 0)
			{
				suggestPropertiesText = "Unfortunately, I did not manage to find any suitable items to suggest to you to further expand your message.";
			}
			else
			{
				suggestPropertiesText = "I found some items in the data specification which could be of interest to you.";
				suggestedIris = suggestions
					.Select(suggestion => suggestion.SuggestedPropertyIri)
					.ToList();
			}

			replyMessage = new()
			{
				Timestamp = DateTime.Now,
				Conversation = userMessage.Conversation,
				PrecedingUserMessageId = userMessage.Id,
				PrecedingUserMessage = userMessage,
				TextContent = "I have processed your message and found some relevant information.",
				MappingText = mappingText,
				MappedItemsIri = mappedIris,
				SuggestPropertiesText = suggestPropertiesText,
				SparqlText = sparqlText,
				SparqlQuery = sparqlQuery
			};
		}
		else
		{
			replyMessage = new()
			{
				Timestamp = DateTime.Now,
				Conversation = userMessage.Conversation,
				PrecedingUserMessageId = userMessage.Id,
				PrecedingUserMessage = userMessage,
				TextContent = "I could not find any relevant information in the data specification for your message.",
				MappingText = "No items were mapped from your message.",
				SuggestPropertiesText = "No suggestions could be made.",
				SparqlText = "No Sparql query was generated.",
				SparqlQuery = string.Empty
			};
		}

		if (replyMessage is not null)
		{
			userMessage.ReplyMessageId = replyMessage.Id;
			userMessage.ReplyMessage = replyMessage;
			userMessage.Conversation.AddMessage(replyMessage);
			await _database.ReplyMessages.AddAsync(replyMessage);
		}
		else
		{
			_logger.LogError("Failed to generate a reply message for the user message: \"{MessageText}\"", userMessage.TextContent);
		}

		await _database.SaveChangesAsync();
		return replyMessage;
	}

	public async Task<string?> UpdateSelectedPropertiesAndGenerateSuggestedMessageAsync(
		Conversation conversation,
		HashSet<string> selectedPropertiesIri,
		List<UserSelection> userSelections)
	{
		_logger.LogDebug("Searching for the selected items.");
		IEnumerable<PropertyItem> selectedProperties = await _database.DataSpecificationItems
						.Where(item => item.DataSpecificationId == conversation.DataSpecification.Id
														&& selectedPropertiesIri.Contains(item.Iri))
						.Select(item => (PropertyItem)item)
						.ToListAsync();

		_logger.LogDebug("Searching for the most recent user message.");
		UserMessage? userMessage = null;
		for (int i = conversation.Messages.Count - 1; i >= 0; i--)
		{
			if (conversation.Messages[i] is UserMessage)
			{
				userMessage = (UserMessage?)conversation.Messages[i];
				break;
			}
		}
		if (userMessage is null)
		{
			_logger.LogError("The conversation does not contain any user messages.");
			conversation.UserSelections.Clear();
			return null;
		}

		// To generate a suggested message, we need to pass all the selected properties
		// and their domains and ranges to the LLM.
		// So we need to get the domains and ranges.
		List<DataSpecificationItem> itemsToAdd = [];
		foreach (PropertyItem property in selectedProperties)
		{
			itemsToAdd.Add(property);
			if (!conversation.DataSpecificationSubstructure.ClassItems.Any(item => item.Iri == property.DomainIri)
					&& !itemsToAdd.Any(item => item.Iri == property.DomainIri))
			{
				itemsToAdd.Add(property.Domain);
			}

			if (property is ObjectPropertyItem objectProperty
				&& !conversation.DataSpecificationSubstructure.ClassItems.Any(item => item.Iri == objectProperty.RangeIri)
				&& !itemsToAdd.Any(item => item.Iri == objectProperty.RangeIri))
			{
				itemsToAdd.Add(objectProperty.Range);
			}
		}

		string suggestedMessage = await _llmConnector.GenerateSuggestedMessageAsync(conversation.DataSpecification, userMessage, conversation.DataSpecificationSubstructure, itemsToAdd);
		/*await _database.UserSelections
			.Where(s => s.ConversationId == conversation.Id)
			.ExecuteDeleteAsync();*/

		// Remove all previous selections before adding the newly updated ones.
		_database.UserSelections.RemoveRange(conversation.UserSelections);

		await _database.UserSelections.AddRangeAsync(userSelections);
		conversation.UserSelections = userSelections;
		conversation.SuggestedMessage = suggestedMessage;

		await _database.SaveChangesAsync();
		return suggestedMessage;
	}

	public async Task<bool> DeleteConversationAndAssociatedDataSpecificationAsync(int conversationId)
	{
		Conversation? conversation = await _database.Conversations.SingleOrDefaultAsync(c => c.Id == conversationId);
		if (conversation is null)
		{
			_logger.LogWarning("Conversation with ID {Id} not found. There is nothing to delete.", conversationId);
			return true;
		}

		DataSpecification dataSpecification = conversation.DataSpecification;
		_database.Conversations.Remove(conversation);
		_database.DataSpecifications.Remove(dataSpecification);
		try
		{
			await _database.SaveChangesAsync();
			return true;
		}
		catch (Exception e)
		{
			_logger.LogError("An exception occured while saving to the database: {Ex}", e);
			return false;
		}
	}

	public async Task<List<DataSpecificationItemMapping>> GetMappingsOfReplyMessage(ReplyMessage replyMessage)
	{
		return await _database.ItemMappings
			.Where(mapping => mapping.UserMessageId == replyMessage.PrecedingUserMessageId)
			.ToListAsync();
	}

	public async Task<List<DataSpecificationPropertySuggestion>> GetSuggestedPropertiesOfReplyMessage(ReplyMessage replyMessage)
	{
		return await _database.PropertySuggestions
			.Where(suggestion => suggestion.UserMessageId == replyMessage.PrecedingUserMessageId)
			.ToListAsync();
	}

	#region Private methods

	private async Task<List<DataSpecificationItemMapping>> MapToDataSpecificationAsync(DataSpecification dataSpecification, UserMessage userMessage)
	{
		List<DataSpecificationItemMapping> mappings = await _llmConnector.MapUserMessageToDataSpecificationAsync(dataSpecification, userMessage);
		if (mappings.Count > 0)
		{
			await _database.ItemMappings.AddRangeAsync(mappings);
		}
		else
		{
			_logger.LogWarning("Failed to map the data specification items for the user message: \"{MessageText}\"", userMessage.TextContent);
		}
		return mappings;
	}

	private async Task<List<DataSpecificationItemMapping>> MapToSubstructureAsync(DataSpecification dataSpecification, DataSpecificationSubstructure substructure, UserMessage userMessage)
	{
		List<DataSpecificationItemMapping> mappings = await _llmConnector.MapUserMessageToSubstructureAsync(dataSpecification, substructure, userMessage);
		if (mappings.Count > 0)
		{
			await _database.ItemMappings.AddRangeAsync(mappings);
		}
		else
		{
			_logger.LogWarning("Failed to map the substructure items for the user message: \"{MessageText}\"", userMessage.TextContent);
		}
		return mappings;
	}

	private async Task<List<DataSpecificationPropertySuggestion>> GetSuggestionsAsync(DataSpecification dataSpecification, DataSpecificationSubstructure substructure, UserMessage userMessage)
	{
		List<DataSpecificationPropertySuggestion> suggestedProperties = await _llmConnector.GetSuggestedPropertiesAsync(
				userMessage.Conversation.DataSpecification, substructure, userMessage);
		if (suggestedProperties.Count > 0)
		{
			await _database.PropertySuggestions.AddRangeAsync(suggestedProperties);
		}
		else
		{
			_logger.LogWarning("Failed to get property suggestions for the user message: \"{MessageText}\"", userMessage.TextContent);
		}
		return suggestedProperties;
	}

	private void AddDataSpecItemsToConversationSubstructure(
		Conversation conversation,
		IReadOnlyCollection<DataSpecificationItem> itemsToAdd,
		IReadOnlyCollection<UserSelection> userSelections)
	{
		DataSpecificationSubstructure substructure = conversation.DataSpecificationSubstructure;

		// Add all classes to the substructure.
		IEnumerable<DataSpecificationItem> classesToAdd = itemsToAdd.Where(item => item.Type is ItemType.Class);
		foreach (var classToAdd in classesToAdd)
		{
			if (substructure.ClassItems.Any(c => c.Iri == classToAdd.Iri))
			{
				// The class is already in the substructure. Skip it.
				continue;
			}
			else
			{
				SubstructureClass classItem = new()
				{
					Iri = classToAdd.Iri,
					Label = classToAdd.Label,
					IsSelectTarget = true
				};
				substructure.ClassItems.Add(classItem);
			}
		}

		// Add properties to the substructure.
		IEnumerable<DataSpecificationItem> propertiesToAdd = itemsToAdd
																	.Where(item => item.Type is ItemType.ObjectProperty || item.Type is ItemType.DatatypeProperty);
		foreach (PropertyItem property in propertiesToAdd)
		{
			SubstructureClass? domainInSubstructure = substructure.ClassItems.Find(c => c.Iri == property.DomainIri);
			if (domainInSubstructure is null)
			{
				// At this point, all properties to be added should have their domains and range in the substructure.
				// Because we added them earlier at the beginning of the method.
				_logger.LogError("The property {PropertyLabel} does not have its domain in the substructure.", property.Label);
				continue;
			}

			// User selections apply to properties.
			// (because only properties can be selected by the user)
			UserSelection? userSelection = userSelections
				.FirstOrDefault(selection => selection.SelectedPropertyIri == property.Iri);
			if (property is ObjectPropertyItem objectProperty)
			{
				domainInSubstructure.ObjectProperties.Add(new SubstructureObjectProperty
				{
					Iri = property.Iri,
					Label = property.Label,
					Domain = property.DomainIri,
					Range = objectProperty.RangeIri,
					IsOptional = userSelection?.IsOptional ?? false
				});
			}
			else if (property is DatatypePropertyItem datatypeProperty)
			{
				domainInSubstructure.DatatypeProperties.Add(new SubstructureDatatypeProperty
				{
					Iri = property.Iri,
					Label = property.Label,
					Domain = property.DomainIri,
					Range = datatypeProperty.RangeDatatypeIri,
					IsSelectTarget = userSelection?.IsSelectTarget ?? true,
					FilterExpression = userSelection?.FilterExpression,
					IsOptional = userSelection?.IsOptional ?? false
				});
			}
			else
			{
				_logger.LogError("Property has unexpected type: {PropertyType}.", property.GetType().Name);
				continue;
			}
		}

		_database.Entry(conversation)
			.Property(c => c.DataSpecificationSubstructure)
			.IsModified = true; // This triggers an update to the JSON column.
	}

	#endregion Private methods
}
