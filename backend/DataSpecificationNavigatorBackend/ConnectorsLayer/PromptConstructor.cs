using DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;
using DataSpecificationNavigatorBackend.Model;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace DataSpecificationNavigatorBackend.ConnectorsLayer;

public class PromptConstructor : IPromptConstructor
{
	private readonly ILogger<PromptConstructor> _logger;

	/// <summary>
	/// Has the following parameters:<br/>
	/// {0} = Data specification (OWL file).<br/>
	/// {1} = User question.
	/// </summary>
	private readonly string _itemsMappingTemplate;

	/// <summary>
	/// Has the following parameters:<br/>
	/// {0} = Data specification (OWL file).<br/>
	/// {1} = User question.<br/>
	/// {2} = Current substructure (a list of items that the conversation has built).
	/// </summary>
	private readonly string _getRelatedItemsTemplate;

	/// <summary>
	/// Has the following parameters:<br/>
	/// {0} = Data specification (OWL file).<br/>
	/// {1} = User question.<br/>
	/// {2} = Current substructure (a list of items that the conversation has built).<br/>
	/// {3} = Selected items (a list of items that the user has selected to add to their question).
	/// </summary>
	private readonly string _generateSuggestedMessageTemplate;

	/// <summary>
	/// Has the following parameters:<br/>
	/// {0} = Data specification (OWL file).<br/>
	/// {1} = User question.<br/>
	/// {2} = Current substructure (a list of items that the conversation has built).
	/// </summary>
	private readonly string _dataSpecSubstructureItemsMappingTemplate;

	private readonly JsonSerializerOptions _jsonSerializerOptions;

	public PromptConstructor(
		ILogger<PromptConstructor> logger,
		IConfiguration appSettings)
	{
		_logger = logger;
		_jsonSerializerOptions = new JsonSerializerOptions
		{
			Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
			WriteIndented = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};

		#region Load templates from files
		string? baseDirectory = appSettings["Prompts:BaseDirectory"];
		if (baseDirectory is null)
		{
			throw new Exception("The key Prompts:BaseDirectory is missing in the config file.");
		}

		string? itemsMapping = appSettings["Prompts:ItemsMapping"];
		if (itemsMapping is null)
		{
			throw new Exception("The key Prompts:ItemsMapping is missing in the config file.");
		}
		string file = Path.Combine(baseDirectory, itemsMapping);
		if (!File.Exists(file))
		{
			throw new Exception($"The template file \"{file}\" does not exist");
		}
		else
		{
			_itemsMappingTemplate = File.ReadAllText(file);
		}

		string? getRelatedItems = appSettings["Prompts:GetRelatedItems"];
		if (getRelatedItems is null)
		{
			throw new Exception("The key Prompts:GetRelatedItems is missing in the config file.");
		}
		file = Path.Combine(baseDirectory, getRelatedItems);
		if (!File.Exists(file))
		{
			throw new Exception($"The template file \"{file}\" does not exist");
		}
		else
		{
			_getRelatedItemsTemplate = File.ReadAllText(file);
		}

		string? generateSuggestedMessage = appSettings["Prompts:GenerateSuggestedMessage"];
		if (generateSuggestedMessage is null)
		{
			throw new Exception("The key Prompts:GenerateSuggestedMessage is missing in the config file.");
		}
		file = Path.Combine(baseDirectory, generateSuggestedMessage);
		if (!File.Exists(file))
		{
			throw new Exception($"The template file \"{file}\" does not exist");
		}
		else
		{
			_generateSuggestedMessageTemplate = File.ReadAllText(file);
		}

		string? substructureItemMapping = appSettings["Prompts:DataSpecSubstructureItemsMapping"];
		if (substructureItemMapping is null)
		{
			throw new Exception("The key Prompts:DataSpecSubstructureItemsMapping is missing in the config file.");
		}
		file = Path.Combine(baseDirectory, substructureItemMapping);
		if (!File.Exists(file))
		{
			throw new Exception($"The template file \"{file}\" does not exist");
		}
		else
		{
			_dataSpecSubstructureItemsMappingTemplate = File.ReadAllText(file);
		}
		#endregion Load templates from files
	}

	public string BuildMapToDataSpecificationPrompt(DataSpecification dataSpecification, string userQuestion)
	{
		_logger.LogDebug("Map to data specification template:\n{Template}", _itemsMappingTemplate);
		_logger.LogDebug("User question: {UserQuestion}", userQuestion);
		return string.Format(_itemsMappingTemplate, dataSpecification.OwlContent, userQuestion);
	}

	public string BuildMapToSubstructurePrompt(
		DataSpecification dataSpecification, string userQuestion, DataSpecificationSubstructure substructure)
	{
		_logger.LogDebug("Map to substructure template:\n{Template}", _dataSpecSubstructureItemsMappingTemplate);
		_logger.LogDebug("User question: {UserQuestion}", userQuestion);

		// For mapping prompt, give the full nested structure so that the LLM sees exactly what each class owns.
		string substructureString = SubstructureToJson(substructure);
		_logger.LogDebug("Substructure:\n{Substructure}", substructureString);
		return string.Format(_dataSpecSubstructureItemsMappingTemplate, dataSpecification.OwlContent, userQuestion, substructureString);
	}

	public string BuildGetSuggestedItemsPrompt(
		DataSpecification dataSpecification, string userQuestion, DataSpecificationSubstructure substructure)
	{
		_logger.LogDebug("Prompt template:\n{Template}", _getRelatedItemsTemplate);
		_logger.LogDebug("User question: {UserQuestion}", userQuestion);

		// For suggestion prompt, give the substructure as a flattened JSON array so the LLM can more easily scan for candidate properties.
		string substructureString = SubstructureToFlattenedJson(substructure);
		_logger.LogDebug("Substructure:\n{Substructure}", substructureString);
		return string.Format(_getRelatedItemsTemplate, dataSpecification.OwlContent, userQuestion, substructureString);
	}

	public string BuildGenerateSuggestedMessagePrompt(
		DataSpecification dataSpecification, string userQuestion, DataSpecificationSubstructure substructure, List<DataSpecificationItem> selectedItems)
	{
		_logger.LogDebug("Prompt template:\n{Template}", _generateSuggestedMessageTemplate);
		_logger.LogDebug("User question: {UserQuestion}", userQuestion);
		string substructureString = SubstructureToFlattenedJson(substructure);
		_logger.LogDebug("Substructure:\n{Substructure}", substructureString);

		var selectedList = selectedItems.Select(item => new
		{
			item.Iri,
			item.Label
		});

		string selectedString = JsonSerializer.Serialize(selectedList, _jsonSerializerOptions);
		_logger.LogDebug("Items to add to substructure:\n{SelectedItems}", selectedString);

		return string.Format(_generateSuggestedMessageTemplate, dataSpecification.OwlContent, userQuestion, substructureString, selectedString);
	}

	private string SubstructureToJson(DataSpecificationSubstructure substructure)
	{
		return JsonSerializer.Serialize(substructure, _jsonSerializerOptions);
	}

	private string SubstructureToFlattenedJson(DataSpecificationSubstructure substructure)
	{
		List<object> flattenedSubstructure = new();
		foreach (var classItem in substructure.ClassItems)
		{
			flattenedSubstructure.Add(new DataSpecificationSubstructure.SubstructureClass()
			{
				Iri = classItem.Iri,
				Label = classItem.Label,
				IsSelectTarget = classItem.IsSelectTarget,
				DatatypeProperties = null!,
				ObjectProperties = null!,
			});

			flattenedSubstructure.AddRange(classItem.ObjectProperties);
			flattenedSubstructure.AddRange(classItem.DatatypeProperties);
		}

		return JsonSerializer.Serialize(flattenedSubstructure, _jsonSerializerOptions);
	}
}
