using DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;
using DataSpecificationNavigatorBackend.ConnectorsLayer.JsonDataClasses;
using DataSpecificationNavigatorBackend.Model;
using System.Text.Json;

namespace DataSpecificationNavigatorBackend.ConnectorsLayer.LlmConnectors.Gemini;

public class GeminiResponseProcessor(
	ILogger<GeminiResponseProcessor> logger,
	AppDbContext appDbContext) : ILlmResponseProcessor
{
	private readonly ILogger<GeminiResponseProcessor> _logger = logger;
	private readonly AppDbContext _database = appDbContext;

	public List<DataSpecificationItemMapping>? ExtractMappedItems(string llmResponse, UserMessage userMessage)
	{
		llmResponse = RemoveBackticks(llmResponse.Trim());
		try
		{
			List<DataSpecItemMappingJson>? jsonData = JsonSerializer.Deserialize<List<DataSpecItemMappingJson>>(llmResponse);
			if (jsonData is null)
			{
				_logger.LogError("The result of the JSON deserialization is null.");
				return null;
			}

			List<DataSpecificationItemMapping> result = [];
			foreach (DataSpecItemMappingJson jsonItem in jsonData)
			{
				DataSpecificationItem? dataSpecItem = _database.DataSpecificationItems.SingleOrDefault(
					item => item.DataSpecificationId == userMessage.Conversation.DataSpecification.Id
								&& item.Iri == jsonItem.Iri);
				if (dataSpecItem is null)
				{
					_logger.LogError("Could not find the item for the phrase \"{MappedWords}\" in the database. Item iri: {Iri}", jsonItem.MappedWords, jsonItem.Iri);
					continue;
				}

				if (string.IsNullOrWhiteSpace(dataSpecItem.Summary))
				{
					dataSpecItem.Summary = jsonItem.Summary;
				}
				DataSpecificationItemMapping mapping = new()
				{
					ItemIri = dataSpecItem.Iri,
					MappedWords = jsonItem.MappedWords,
					ItemDataSpecificationId = dataSpecItem.DataSpecificationId,
					UserMessageId = userMessage.Id,
					Item = dataSpecItem,
					UserMessage = userMessage,
				};
				result.Add(mapping);
			}
			return result;
		}
		catch (Exception e)
		{
			_logger.LogError("An exception occured while deserializing the mapped items from JSON: {Exception}", e);
			return null;
		}
	}

	public List<DataSpecificationPropertySuggestion>? ExtractSuggestedItems(string llmResponse, UserMessage userMessage)
	{
		llmResponse = RemoveBackticks(llmResponse.Trim());
		try
		{
			List<PropertySuggestionJson>? jsonData = JsonSerializer.Deserialize<List<PropertySuggestionJson>>(llmResponse);
			if (jsonData is null)
			{
				_logger.LogError("The result of the JSON deserialization is null.");
				return null;
			}

			List<DataSpecificationPropertySuggestion> result = [];
			foreach (PropertySuggestionJson jsonItem in jsonData)
			{
				DataSpecificationItem? suggestedProperty = _database.DataSpecificationItems.SingleOrDefault(
					item => item.DataSpecificationId == userMessage.Conversation.DataSpecification.Id
								&& item.Iri == jsonItem.Iri);
				if (suggestedProperty is null)
				{
					_logger.LogError("Could not find the suggested property \"{Iri}\" in the database.", jsonItem.Iri);
					continue;
				}

				if (string.IsNullOrWhiteSpace(suggestedProperty.Summary))
				{
					suggestedProperty.Summary = jsonItem.Summary;
				}

				if (suggestedProperty is PropertyItem property)
				{
					DataSpecificationPropertySuggestion suggestion = new()
					{
						PropertyDataSpecificationId = suggestedProperty.DataSpecificationId,
						SuggestedPropertyIri = suggestedProperty.Iri,
						UserMessageId = userMessage.Id,
						SuggestedProperty = property,
						UserMessage = userMessage,
						ReasonForSuggestion = jsonItem.Reason
					};
					result.Add(suggestion);

					// Update the summary of the property's domain and range, if they are empty.
					if (string.IsNullOrWhiteSpace(property.Domain.Summary))
					{
						property.Domain.Summary = jsonItem.DomainClass.Summary;
					}
					if (property is ObjectPropertyItem objectProperty &&
							string.IsNullOrWhiteSpace(objectProperty.Range.Summary))
					{
						objectProperty.Range.Summary = jsonItem.RangeClass.Summary;
					}
				}
				else
				{
					_logger.LogError("The suggested item with label {Label} and Iri {IRI} is not a property.",
																						suggestedProperty.Label, suggestedProperty.Iri);
					continue;
				}
			}
			return result;
		}
		catch (Exception e)
		{
			_logger.LogError("An exception occured while deserializing the suggested items from JSON: {Exception}", e);
			return null;
		}
	}

	public List<DataSpecificationItemMapping>? ExtractSubstructureMapping(string llmResponse, UserMessage userMessage)
	{
		llmResponse = RemoveBackticks(llmResponse.Trim());
		try
		{
			List<SubstructureItemMappingJson>? jsonData = JsonSerializer.Deserialize<List<SubstructureItemMappingJson>>(llmResponse);
			if (jsonData is null)
			{
				_logger.LogError("The result of the JSON deserialization is null.");
				return null;
			}

			List<DataSpecificationItemMapping> result = [];
			foreach (SubstructureItemMappingJson jsonItem in jsonData)
			{
				DataSpecificationItem? dataSpecItem = _database.DataSpecificationItems.SingleOrDefault(
					item => item.DataSpecificationId == userMessage.Conversation.DataSpecification.Id
								&& item.Iri == jsonItem.Iri);
				if (dataSpecItem is null)
				{
					_logger.LogError("Could not find the item for the phrase \"{MappedWords}\" in the database. Item iri: {Iri}", jsonItem.MappedWords, jsonItem.Iri);
					continue;
				}

				DataSpecificationItemMapping mapping = new()
				{
					ItemIri = dataSpecItem.Iri,
					MappedWords = jsonItem.MappedWords,
					ItemDataSpecificationId = dataSpecItem.DataSpecificationId,
					UserMessageId = userMessage.Id,
					Item = dataSpecItem,
					UserMessage = userMessage,
				};
				result.Add(mapping);
			}
			return result;
		}
		catch (Exception e)
		{
			_logger.LogError("An exception occured while deserializing the mapped items from JSON: {Exception}", e);
			return null;
		}
	}

	public WelcomeMessageDataSpecificationSummaryJson? ExtractWelcomeMessageSummaryAndSuggestions(
		string llmResponse)
	{
		llmResponse = RemoveBackticks(llmResponse.Trim());
		try
		{
			var jsonData = JsonSerializer.Deserialize<WelcomeMessageDataSpecificationSummaryJson>(llmResponse);
			if (jsonData is null)
			{
				_logger.LogError("The result of the JSON deserialization is null.");
				return null;
			}
			return jsonData;
			
			/*var suggestedClasses = jsonData.SuggestedClasses
				.Select(s => new {s.Label, s.Reason})
				.ToList();*/
		}
		catch (Exception e)
		{
			_logger.LogError("An exception occured while deserializing the welcome message from JSON: {Exception}", e);
			return null;
		}
	}

	private string RemoveBackticks(string llmResponse)
	{
		if (llmResponse.StartsWith("```json"))
		{
			return llmResponse.Substring(7, llmResponse.Length - 10);
		}
		else
		{
			return llmResponse;
		}
	}
}
