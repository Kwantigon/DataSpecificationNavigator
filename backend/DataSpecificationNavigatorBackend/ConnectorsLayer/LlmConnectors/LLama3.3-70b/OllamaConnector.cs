using DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;
using DataSpecificationNavigatorBackend.ConnectorsLayer.JsonDataClasses;
using DataSpecificationNavigatorBackend.Model;
using OllamaSharp;
using System.Text;

namespace DataSpecificationNavigatorBackend.ConnectorsLayer.LlmConnectors;

public class OllamaConnector : ILlmConnector
{
	private readonly ILogger<OllamaConnector> _logger;
	private readonly ILlmPromptConstructor _promptConstructor;
	private readonly ILlmResponseProcessor _responseProcessor;
	private readonly int _retryAttempts;
	private readonly Chat _chat;

	public OllamaConnector(
		ILogger<OllamaConnector> logger,
		IConfiguration config,
		ILlmPromptConstructor promptConstructor,
		ILlmResponseProcessor responseProcessor)
	{
		_logger = logger;
		_promptConstructor = promptConstructor;
		_responseProcessor = responseProcessor;

		#region Values from configuration
		string? uri = config["Env:Llm:Ollama:Uri"];
		if (string.IsNullOrWhiteSpace(uri))
		{
			uri = config["Llm:Ollama:Uri"];

			if (string.IsNullOrWhiteSpace(uri))
				throw new Exception("The uri for Ollama is missing from configuration.");
		}

		string? model = config["Env:Llm:Ollama:Model"];
		if (string.IsNullOrWhiteSpace(model))
		{
			model = config["Llm:Ollama:Model"];

			if (string.IsNullOrWhiteSpace(model))
				throw new Exception("The Ollama model is missing from configuration.");
		}

		string? retryAttemptsStr = config["Env:Llm:Ollama:RetryAttempts"];
		if (string.IsNullOrWhiteSpace(retryAttemptsStr))
		{
			_retryAttempts = config.GetValue("Llm:Ollama:RetryAttempts", 3);
		}
		else
		{
			_retryAttempts = int.TryParse(retryAttemptsStr, out int parsedRetries) ? parsedRetries : 3;
		}
		#endregion Values from configuration

		_logger.LogInformation("Using Ollama LLM at {Uri} with model {Model}. Retry count is set to: {Retries}.", uri, model, _retryAttempts);

		OllamaApiClient ollamaApiClient = new(uri);
		ollamaApiClient.SelectedModel = model;
		_chat = new(ollamaApiClient);
	}

	public async Task<WelcomeMessageDataSpecificationSummaryJson?> GetDataSpecificationSummaryAndClassSuggestionsAsync(
		DataSpecification dataSpecification)
	{
		_logger.LogDebug("Generating a summary and class suggestions for the first message.");
		string prompt = _promptConstructor.BuildDataSpecificationSummaryPrompt(dataSpecification);
		_logger.LogDebug("Prompting the LLM.");

		int attempts = 0;
		WelcomeMessageDataSpecificationSummaryJson? result = null;
		while (attempts < _retryAttempts && result is null)
		{
			try
			{
				_logger.LogDebug("Prompt attempt number {AttemptCount}", attempts + 1);
				string response = await SendPromptAsync(prompt);
				_logger.LogDebug("LLM response: {Response}", response);

				result = _responseProcessor.ExtractWelcomeMessageSummaryAndSuggestions(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve a summary and initial suggestions from the LLM.");
				result = null; // Set result to null to attempt the prompt again.
			}
			attempts++;
		}

		if (result is null)
		{
			_logger.LogError("Data specification summary and suggestions are still null after " + attempts + " attempts.");
		}

		return result;
	}

	public async Task<List<DataSpecificationItemMapping>> MapUserMessageToDataSpecificationAsync(
		DataSpecification dataSpecification, UserMessage userMessage)
	{
		_logger.LogDebug("Mapping message \"{UserMessageText}\" to data specification items.", userMessage.TextContent);

		int attempts = 0;
		List<DataSpecificationItemMapping>? mapped = null;
		string prompt = _promptConstructor.BuildMapToDataSpecificationPrompt(dataSpecification, userMessage.TextContent);
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
				_logger.LogError(ex, "Failed to retrieve the items mapping from LLM.");
				mapped = null; // Reset mapped to null to trigger another attempt.
			}
			
			attempts++;
		}

		if (mapped is null)
		{
			_logger.LogError("The data specification items list is still null after " + attempts + " attempts.");
			return [];
		}

		return mapped;
	}

	public async Task<List<DataSpecificationPropertySuggestion>> GetSuggestedPropertiesAsync(
		DataSpecification dataSpecification, DataSpecificationSubstructure dataSpecificationSubstructure, UserMessage userMessage)
	{
		string prompt = _promptConstructor.BuildGetSuggestedPropertiesPrompt(
			dataSpecification, userMessage.TextContent, dataSpecificationSubstructure);
		_logger.LogDebug(prompt);

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
			_logger.LogError("The relatedItems list is still null after " + attempts + " attempts.");
			return [];
		}

		return suggestedItems;
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

	public async Task GenerateItemSummariesAsync(
		DataSpecification dataSpecification,
		List<DataSpecificationItem> dataSpecificationItems)
	{
		string prompt = _promptConstructor.BuildItemsSummaryPrompt(dataSpecification, dataSpecificationItems);
		int attempts = 0;
		string? response = null;
		while (attempts < _retryAttempts && string.IsNullOrWhiteSpace(response))
		{
			try
			{
				_logger.LogDebug("Prompt attempt number {AttemptCount}", attempts + 1);
				response = await SendPromptAsync(prompt);
				_logger.LogDebug("LLM response: {Response}", response);
				if (!string.IsNullOrWhiteSpace(response))
					_responseProcessor.ExtractDataSpecificationItemSummaries(response, dataSpecificationItems);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "An error occured while prompting the LLM.");
				response = null;
			}

			attempts++;
		}
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
