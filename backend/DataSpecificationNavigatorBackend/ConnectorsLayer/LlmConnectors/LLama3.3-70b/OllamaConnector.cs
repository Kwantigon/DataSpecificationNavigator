using DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;
using DataSpecificationNavigatorBackend.Model;
using OllamaSharp;
using System.Text;

namespace DataSpecificationNavigatorBackend.ConnectorsLayer.LlmConnectors;

public class OllamaConnector : ILlmConnector
{
	private readonly ILogger<OllamaConnector> _logger;
	private readonly IPromptConstructor _promptConstructor;
	private readonly ILlmResponseProcessor _responseProcessor;
	private readonly int _retryAttempts;
	private readonly Chat _chat;

	public OllamaConnector(
		ILogger<OllamaConnector> logger,
		IConfiguration appSettings,
		IPromptConstructor promptConstructor,
		ILlmResponseProcessor responseProcessor)
	{
		_logger = logger;
		_promptConstructor = promptConstructor;
		_responseProcessor = responseProcessor;

		#region Values from appsettings.json
		string? uri = appSettings["Llm:Ollama:Uri"];
		if (uri is null)
		{
			throw new Exception("The key Llm:Ollama:Uri is missing in the config file.");
		}

		string? model = appSettings["Llm:Ollama:Model"];
		if (model is null)
		{
			throw new Exception("The key Llm:Ollama:Model is missing in the config file.");
		}

		_retryAttempts = appSettings.GetValue("Llm:Ollama:RetryAttempts", 3);
		#endregion Values from appsettings.json

		OllamaApiClient ollamaApiClient = new(uri);
		ollamaApiClient.SelectedModel = model;
		_chat = new(ollamaApiClient);
	}

	public async Task<List<DataSpecificationItemMapping>> MapUserMessageToDataSpecificationAsync(
		DataSpecification dataSpecification, UserMessage userMessage)
	{
		string prompt = _promptConstructor.BuildMapToDataSpecificationPrompt(dataSpecification, userMessage.TextContent);
		int attempts = 0;
		List<DataSpecificationItemMapping>? mapped = null;
		while (attempts < _retryAttempts && mapped is null)
		{
			try
			{
				_logger.LogDebug("Prompt attempt number {AttemptCount}", attempts + 1);
				string response = await SendPromptAsync(prompt);
				_logger.LogDebug("LLM response: {Response}", response);
				mapped = _responseProcessor.ExtractMappedItems(response, userMessage);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occured while prompting the LLM.");
				mapped = null; // Reset mapped to null to retry
			}

			attempts++;
		}

		if (mapped is null)
		{
			_logger.LogError("The data specification items list is still null after " + _retryAttempts + " attempts.");
			return [];
		}

		return mapped;
	}

	public async Task<List<DataSpecificationItemMapping>> MapUserMessageToSubstructureAsync(
		DataSpecification dataSpecification, DataSpecificationSubstructure dataSpecificationSubstructure, UserMessage userMessage)
	{
		string prompt = _promptConstructor.BuildMapToSubstructurePrompt(dataSpecification, userMessage.TextContent, dataSpecificationSubstructure);
		int attempts = 0;
		List<DataSpecificationItemMapping>? mapped = null;
		while (attempts < _retryAttempts && mapped is null)
		{
			try
			{
				_logger.LogDebug("Prompt attempt number {AttemptCount}", attempts + 1);
				string response = await SendPromptAsync(prompt);
				_logger.LogDebug("LLM response: {Response}", response);
				mapped = _responseProcessor.ExtractSubstructureMapping(response, userMessage);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occured while prompting the LLM.");
				mapped = null; // Reset mapped to null to retry.
			}

			attempts++;
		}

		if (mapped is null)
		{
			_logger.LogError("The data specification items list is still null after " + _retryAttempts + " attempts.");
			return [];
		}

		return mapped;
	}

	public async Task<List<DataSpecificationPropertySuggestion>> GetSuggestedPropertiesAsync(
		DataSpecification dataSpecification, DataSpecificationSubstructure dataSpecificationSubstructure, UserMessage userMessage)
	{
		string prompt = _promptConstructor.BuildGetSuggestedItemsPrompt(dataSpecification, userMessage.TextContent, dataSpecificationSubstructure);
		int attempts = 0;
		List<DataSpecificationPropertySuggestion>? suggestedItems = null;
		while (attempts < _retryAttempts && suggestedItems is null)
		{
			try
			{
				_logger.LogDebug("Prompt attempt number {AttemptCount}", attempts + 1);
				string response = await SendPromptAsync(prompt);
				_logger.LogDebug("LLM response: {Response}", response);
				suggestedItems = _responseProcessor.ExtractSuggestedItems(response, userMessage);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occured while prompting the LLM.");
				suggestedItems = null; // Reset suggestedItems to null to retry.
			}

			attempts++;
		}

		if (suggestedItems is null)
		{
			_logger.LogError("The relatedItems list is still null after " + _retryAttempts + " attempts.");
			return [];
		}

		return suggestedItems;
	}

	public async Task<string> GenerateSuggestedMessageAsync(DataSpecification dataSpecification, UserMessage userMessage, DataSpecificationSubstructure substructure, List<DataSpecificationItem> selectedItems)
	{
		string prompt = _promptConstructor.BuildGenerateSuggestedMessagePrompt(
			dataSpecification, userMessage.TextContent, substructure, selectedItems);
		int attempts = 0;
		string? itemSummary = null;
		while (attempts < _retryAttempts && itemSummary is null)
		{
			try
			{
				_logger.LogDebug("Prompting the LLM.");
				string response = await SendPromptAsync(prompt);
				_logger.LogDebug("LLM response: {Response}", response);
				itemSummary = _responseProcessor.ExtractSuggestedMessage(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occured while prompting the LLM.");
				itemSummary = null; // Reset itemSummary to null to retry.
			}
			attempts++;
		}

		if (itemSummary is null)
		{
			_logger.LogError("Failed to extract the item summary from the LLM response after " + _retryAttempts + " attempts.");
			return string.Empty;
		}

		return itemSummary;
	}

	public async Task<string> GenerateItemSummaryAsync(DataSpecificationItem dataSpecificationItem)
	{
		await Task.CompletedTask;
		return "Item summary generation is deprecated.";
	}

	public async Task<string> SendTestPrompt(string prompt)
	{
		string? answer = null;
		int attempts = 0;
		while (attempts < _retryAttempts && answer is null)
		{
			try
			{
				_logger.LogDebug("Prompt attempt number {AttemptCount}", attempts + 1);
				string response = await SendPromptAsync(prompt);
				_logger.LogDebug("LLM response: {Response}", response);
				answer = response;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occured while prompting the LLM.");
				answer = null; // Reset mapped to null to retry
			}
		}

		if (answer is null)
		{
			_logger.LogError("The answer is still null after " + _retryAttempts + " attempts.");
			return string.Empty;
		}
		return answer;
	}

	private async Task<string> SendPromptAsync(string prompt)
	{
		IAsyncEnumerable<string> responseStream = _chat.SendAsync(prompt);
		StringBuilder responseBuilder = new StringBuilder();

		await foreach (string? token in responseStream)
		{
			responseBuilder.Append(token);
		}
		return responseBuilder.ToString();
	}
}
