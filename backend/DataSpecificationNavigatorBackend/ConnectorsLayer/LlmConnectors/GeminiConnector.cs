using DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;
using DataSpecificationNavigatorBackend.Model;
using GenerativeAI;

namespace DataSpecificationNavigatorBackend.ConnectorsLayer.LlmConnectors;

public class GeminiConnector : ILlmConnector
{
	private readonly ILogger<GeminiConnector> _logger;
	private readonly IPromptConstructor _promptConstructor;
	private readonly ILlmResponseProcessor _responseProcessor;
	private const int _retryAttempts = 3; // If the response processor fails to extract the necessary classes, retry the prompt this many times.
	private GenerativeModel? _gemini;

	/*
	 * To do: The Gemini API could return an error response
	 * if the model is not available or if the API key is invalid.
	 * Catch those errors and handle them when calling SendPromptAsync.
	 */
	public GeminiConnector(
		ILogger<GeminiConnector> logger,
		IConfiguration appSettings,
		IPromptConstructor promptConstructor,
		ILlmResponseProcessor responseProcessor)
	{
		_logger = logger;
		_promptConstructor = promptConstructor;
		_responseProcessor = responseProcessor;

		string? apiKeyFile = appSettings["Llm:Gemini:ApiKeyFile"];
		if (apiKeyFile is null)
		{
			throw new Exception("The key Llm:Gemini:ApiKeyFile is missing in the config file.");
		}
		string apiKey = File.ReadAllText(apiKeyFile);

		string? model = appSettings["Llm:Gemini:Model"];
		if (model is null)
		{
			throw new Exception("The key Llm:Gemini:Model is missing in the config file.");
		}

		GoogleAi googleAi = new(apiKey);
		_gemini = googleAi.CreateGenerativeModel(model);
	}

	public async Task<List<DataSpecificationItemMapping>> MapUserMessageToDataSpecificationAsync(DataSpecification dataSpecification, UserMessage userMessage)
	{
		_logger.LogDebug("Mapping message \"{UserMessageText}\" to data specification items.", userMessage.TextContent);

		int attempts = 0;
		List<DataSpecificationItemMapping>? mapped = null;
		string prompt = _promptConstructor.BuildMapToDataSpecificationPrompt(dataSpecification, userMessage.TextContent);
		while (attempts < _retryAttempts && mapped is null)
		{
			_logger.LogDebug("Prompt attempt number {AttemptCount}", attempts + 1);
			string response = await SendPromptAsync(prompt);
			_logger.LogDebug("LLM response: {Response}", response);

			mapped = _responseProcessor.ExtractMappedItems(response, userMessage);
			attempts++;
		}

		if (mapped is null)
		{
			_logger.LogError("The data specification items list is still null after " + _retryAttempts + " attempts.");
			return [];
		}

		return mapped;
	}

	public async Task<List<DataSpecificationItemMapping>> MapUserMessageToSubstructureAsync(DataSpecification dataSpecification, DataSpecificationSubstructure substructure, UserMessage userMessage)
	{
		_logger.LogDebug("Mapping message \"{UserMessageText}\" to substructure items.", userMessage.TextContent);

		int attempts = 0;
		List<DataSpecificationItemMapping>? mapped = null;
		string prompt = _promptConstructor.BuildMapToSubstructurePrompt(dataSpecification, userMessage.TextContent, substructure);

		while (attempts < _retryAttempts && mapped is null)
		{
			_logger.LogDebug("Prompt attempt number {AttemptCount}", attempts + 1);
			string response = await SendPromptAsync(prompt);
			_logger.LogDebug("LLM response: {Response}", response);

			mapped = _responseProcessor.ExtractSubstructureMapping(response, userMessage);
			attempts++;
		}

		if (mapped is null)
		{
			_logger.LogError("The data specification items list is still null after " + _retryAttempts + " attempts.");
			return [];
		}

		return mapped;
	}

	public async Task<List<DataSpecificationPropertySuggestion>> GetSuggestedPropertiesAsync(DataSpecification dataSpecification, DataSpecificationSubstructure substructure, UserMessage userMessage)
	{
		_logger.LogDebug("Getting suggested properties for the user message: {UserMessageText}", userMessage.TextContent);

		int attempts = 0;
		List<DataSpecificationPropertySuggestion>? suggestedItems = null;
		string prompt = _promptConstructor.BuildGetSuggestedItemsPrompt(dataSpecification, userMessage.TextContent, substructure);
		while (attempts < _retryAttempts && suggestedItems is null)
		{
			_logger.LogDebug("Prompt attempt number {AttemptCount}", attempts + 1);
			string response = await SendPromptAsync(prompt);
			_logger.LogDebug("LLM response: {Response}", response);

			suggestedItems = _responseProcessor.ExtractSuggestedItems(response, userMessage);
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
		_logger.LogDebug("Generating a suggested message for the user message: {UserMessageText}", userMessage.TextContent);
		string prompt = _promptConstructor.BuildGenerateSuggestedMessagePrompt(
			dataSpecification, userMessage.TextContent, substructure, selectedItems);

		_logger.LogDebug("Prompting the LLM.");
		string response = await SendPromptAsync(prompt);
		_logger.LogDebug("LLM response: {Response}", response);

		string? itemSummary = _responseProcessor.ExtractSuggestedMessage(response);
		if (itemSummary is null)
		{
			_logger.LogError("Failed to extract the item summary from the LLM response.");
			return string.Empty;
		}

		return itemSummary;
	}

	private async Task<string> SendPromptAsync(string prompt)
	{
		if (_gemini is null)
		{
			throw new Exception("The Gemini model has not been initialized.");
		}
		var response = await _gemini.GenerateContentAsync(prompt);
		return response.Text;
	}

	public async Task<string> GenerateItemSummaryAsync(DataSpecificationItem dataSpecificationItem)
	{
		await Task.CompletedTask;
		return "Item summary generation is deprecated.";
	}
}
