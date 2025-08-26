using DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;
using DataSpecificationNavigatorBackend.Model;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace DataSpecificationNavigatorBackend.ConnectorsLayer.LlmConnectors.LLama3._3_70b;

public class LlamaPromptConstructor : ILlmPromptConstructor
{
	private readonly ILogger<LlamaPromptConstructor> _logger;
	private readonly AppDbContext _database;
	private readonly JsonSerializerOptions _jsonSerializerOptions;

	private readonly string _dataSpecificationSummaryTemplate;

	private readonly string _mapToDataSpecificationTemplate;

	private readonly string _getSuggestedPropertiesTemplate;

	private readonly string _generateSuggestedMessageTemplate;

	private readonly string _mapToSubstructureTemplate;

	private readonly string _summarizeItemsTemplate;

	public LlamaPromptConstructor(
		ILogger<LlamaPromptConstructor> logger,
		IConfiguration config,
		AppDbContext appDbContext)
	{
		_logger = logger;
		_database = appDbContext;
		_jsonSerializerOptions = new JsonSerializerOptions
		{
			Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
			WriteIndented = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};

		#region Load templates from files
		string? baseDirectory = config["Llm:Ollama:Prompts:BaseDirectory"];
		if (baseDirectory is null)
		{
			throw new Exception("The key Llm:Ollama:Prompts:BaseDirectory is missing in the config file.");
		}

		string? itemsMapping = config["Llm:Ollama:Prompts:ItemsMapping"];
		if (itemsMapping is null)
		{
			throw new Exception("The key Llm:Ollama::ItemsMapping is missing in the config file.");
		}
		string templateFile = Path.Combine(baseDirectory, itemsMapping);
		if (!File.Exists(templateFile))
		{
			throw new Exception($"The template file \"{templateFile}\" does not exist");
		}
		else
		{
			_mapToDataSpecificationTemplate = File.ReadAllText(templateFile);
		}

		string? getRelatedItems = config["Llm:Ollama:Prompts:GetRelatedItems"];
		if (getRelatedItems is null)
		{
			throw new Exception("The key Llm:Ollama:Prompts:GetRelatedItems is missing in the config file.");
		}
		templateFile = Path.Combine(baseDirectory, getRelatedItems);
		if (!File.Exists(templateFile))
		{
			throw new Exception($"The template file \"{templateFile}\" does not exist");
		}
		else
		{
			_getSuggestedPropertiesTemplate = File.ReadAllText(templateFile);
		}

		string? generateSuggestedMessage = config["Llm:Ollama:Prompts:GenerateSuggestedMessage"];
		if (generateSuggestedMessage is null)
		{
			throw new Exception("The key Llm:Ollama:Prompts:GenerateSuggestedMessage is missing in the config file.");
		}
		templateFile = Path.Combine(baseDirectory, generateSuggestedMessage);
		if (!File.Exists(templateFile))
		{
			throw new Exception($"The template file \"{templateFile}\" does not exist");
		}
		else
		{
			_generateSuggestedMessageTemplate = File.ReadAllText(templateFile);
		}

		string? substructureItemMapping = config["Llm:Ollama:Prompts:DataSpecSubstructureItemsMapping"];
		if (substructureItemMapping is null)
		{
			throw new Exception("The key Llm:Ollama::DataSpecSubstructureItemsMapping is missing in the config file.");
		}
		templateFile = Path.Combine(baseDirectory, substructureItemMapping);
		if (!File.Exists(templateFile))
		{
			throw new Exception($"The template file \"{templateFile}\" does not exist");
		}
		else
		{
			_mapToSubstructureTemplate = File.ReadAllText(templateFile);
		}

		string? welcomeMessageDataSpecificationSummary = config["Llm:Ollama:Prompts:WelcomeMessageDataSpecificationSummary"];
		if (welcomeMessageDataSpecificationSummary is null)
		{
			throw new Exception("The key Llm:Ollama:Prompts:WelcomeMessageDataSpecificationSummary is missing in the config file.");
		}
		templateFile = Path.Combine(baseDirectory, welcomeMessageDataSpecificationSummary);
		if (!File.Exists(templateFile))
		{
			throw new Exception($"The template file \"{templateFile}\" does not exist");
		}
		else
		{
			_dataSpecificationSummaryTemplate = File.ReadAllText(templateFile);
		}

		string? summarizeItems = config["Llm:Ollama:Prompts:SummarizeDataSpecItems"];
		if (summarizeItems is null)
		{
			throw new Exception("The key Llm:Ollama:Prompts:SummarizeDataSpecItems is missing in the config file.");
		}
		templateFile = Path.Combine(baseDirectory, summarizeItems);
		if (!File.Exists(templateFile))
		{
			throw new Exception($"The template file \"{templateFile}\" does not exist.");
		}
		else
		{
			_summarizeItemsTemplate = File.ReadAllText(templateFile);
		}
		#endregion Load templates from files

		_logger.LogInformation("Prompt templates loaded sucessfully.");
	}

	public string BuildDataSpecificationSummaryPrompt(DataSpecification dataSpecification)
	{
		string dataSpecificationFlatJson = SerializeAllDataSpecItemsFlattened(dataSpecification);
		return string.Format(_dataSpecificationSummaryTemplate, dataSpecificationFlatJson);
	}

	public string BuildMapToDataSpecificationPrompt(DataSpecification dataSpecification, string userQuestion)
	{
		string dataSpecificationFlatJson = SerializeAllDataSpecItemsFlattened(dataSpecification);
		return string.Format(_mapToDataSpecificationTemplate, dataSpecificationFlatJson, userQuestion);
	}

	public string BuildGetSuggestedPropertiesPrompt(
		DataSpecification dataSpecification,
		string userQuestion,
		DataSpecificationSubstructure substructure)
	{
		HashSet<string> classIris = substructure.ClassItems
			.Select(item => item.Iri)
			.ToHashSet();

		List<ClassItem> classesInSubstructure = _database.ClassItems
			.Where(item => item.DataSpecificationId == dataSpecification.Id &&
											classIris.Contains(item.Iri))
			.ToList();

		string substructureString = SerializeSubstructureFlattened(substructure);
		string substructureProximity = SerializeSubstructureProximity(dataSpecification, classesInSubstructure);

		return string.Format(_getSuggestedPropertiesTemplate, substructureProximity, userQuestion, substructureString);
	}

	public string BuildGenerateSuggestedMessagePrompt(
		DataSpecification dataSpecification, string userQuestion,
		DataSpecificationSubstructure substructure, List<DataSpecificationItem> selectedItems)
	{
		// Not using 'dataSpecification' in this prompt to reduce token size.

		string substructureFlattenedJson = SerializeSubstructureFlattened(substructure);
		var selectedItemsInfo = selectedItems.Select(i => new { i.Iri, i.Label }).ToList();
		string selectedItemsFlattenedJson = JsonSerializer.Serialize(selectedItemsInfo, _jsonSerializerOptions);

		return string.Format(_generateSuggestedMessageTemplate, userQuestion, substructureFlattenedJson, selectedItemsFlattenedJson);
	}

	public string BuildMapToSubstructurePrompt(
		DataSpecification dataSpecification, string userQuestion, DataSpecificationSubstructure substructure)
	{
		// Not using 'dataSpecification' in this prompt to reduce token size.

		// For mapping prompt, give the full nested structure so that the LLM sees exactly what each class owns.
		string substructureString = JsonSerializer.Serialize(substructure, _jsonSerializerOptions);
		return string.Format(_mapToSubstructureTemplate, userQuestion, substructureString);
	}

	public string BuildItemsSummaryPrompt(
		DataSpecification dataSpecification,
		List<ClassItem> dataSpecificationItems)
	{
		string dataSpecificationString = SerializeAllDataSpecItemsFlattened(dataSpecification);
		var items = dataSpecificationItems.Select(i => new { i.Iri, i.Label, i.Type, i.OwlAnnotation, i.RdfsComment });
		string itemsListJson = JsonSerializer.Serialize(items, _jsonSerializerOptions);

		return string.Format(_summarizeItemsTemplate, dataSpecificationString, itemsListJson);
	}

	private string SerializeAllDataSpecItemsFlattened(DataSpecification dataSpecification)
	{
		List<Object> items = [];
		foreach (DataSpecificationItem item in _database.DataSpecificationItems)
		{
			if (item is ClassItem cl)
			{
				items.Add(new
				{
					cl.Iri,
					cl.Label,
					Type = "Class",
					cl.OwlAnnotation,
					cl.RdfsComment
				});
			}

			if (item is ObjectPropertyItem objectProperty)
			{
				items.Add(new
				{
					objectProperty.Iri,
					objectProperty.Label,
					Type = "ObjectProperty",
					Domain = objectProperty.DomainIri,
					Range = objectProperty.RangeIri,
					objectProperty.OwlAnnotation,
					objectProperty.RdfsComment
				});
			}

			if (item is DatatypePropertyItem datatypeProperty)
			{
				items.Add(new
				{
					datatypeProperty.Iri,
					datatypeProperty.Label,
					Type = "DatatypeProperty",
					Domain = datatypeProperty.DomainIri,
					Range = datatypeProperty.RangeDatatypeIri,
					datatypeProperty.OwlAnnotation,
					datatypeProperty.RdfsComment
				});
			}
		}

		return JsonSerializer.Serialize(items, _jsonSerializerOptions);
	}

	private string SerializeSubstructureProximity(
		DataSpecification dataSpecification,
		List<ClassItem> currentClasses,
		int maxHops = 1)
	{
		List<Object> classesList = new(); // List of anonymous objects.

		int currentHops = 0;
		List<ClassItem> classesToIterate = currentClasses;
		HashSet<string> alreadyAdded = [];
		while (currentHops < maxHops)
		{
			List<ClassItem> nextIteration = [];
			foreach (ClassItem classItem in classesToIterate)
			{
				// ObjectProperties, where the current class is the domain.
				List<ObjectPropertyItem> classItemIsDomain = _database.ObjectPropertyItems
				.Where(item => item.DataSpecificationId == dataSpecification.Id &&
												item.DomainIri == classItem.Iri)
				.ToList();
				foreach (ObjectPropertyItem property in classItemIsDomain)
				{
					if (!alreadyAdded.Contains(property.RangeIri) &&
							!nextIteration.Any(c => c.Iri == property.RangeIri))
					{
						nextIteration.Add(property.Range);
					}
				}

				// ObjectProperties, where the current class is the range.
				List<ObjectPropertyItem> classItemIsRange = _database.ObjectPropertyItems
					.Where(item => item.DataSpecificationId == dataSpecification.Id &&
													item.RangeIri == classItem.Iri)
					.ToList();
				foreach (ObjectPropertyItem property in classItemIsRange)
				{
					if (!alreadyAdded.Contains(property.DomainIri) &&
							!nextIteration.Any(c => c.Iri == property.DomainIri))
					{
						nextIteration.Add(property.Domain);
					}
				}

				List<DatatypePropertyItem> datatypeProperties = _database.DatatypePropertyItems
					.Where(item => item.DataSpecificationId == dataSpecification.Id &&
													item.DomainIri == classItem.Iri)
					.ToList();

				classesList.Add(new
				{
					classItem.Iri,
					classItem.Label,
					Type = "Class",
					classItem.OwlAnnotation,
					classItem.RdfsComment,
					ObjectProperties = classItemIsDomain
						.Select(objProperty => new
						{
							objProperty.Iri,
							objProperty.Label,
							Type = "ObjectProperty",
							objProperty.DomainIri,
							objProperty.RangeIri,
							objProperty.OwlAnnotation,
							objProperty.RdfsComment
						})
						.ToList(),
					DatatypeProperties = datatypeProperties
						.Select(dtProperty => new
						{
							dtProperty.Iri,
							dtProperty.Label,
							Type = "DatatypeProperty",
							dtProperty.DomainIri,
							Datatype = dtProperty.RangeDatatypeIri,
							dtProperty.OwlAnnotation,
							dtProperty.RdfsComment
						})
						.ToList()
				});

				alreadyAdded.Add(classItem.Iri);
			}
			classesToIterate = nextIteration;
			currentHops++;
		}

		// 'classesToIterate contains ranges and domains to be processed in the next hop.
		// Add those classes so that I don't have dangling references.
		foreach (ClassItem classItem in classesToIterate)
		{
			classesList.Add(new
			{
				classItem.Iri,
				classItem.Label,
				Type = "Class",
				classItem.OwlAnnotation,
				classItem.RdfsComment,
				ObjectProperties = new List<Object>(),
				DatatypeProperties = new List<Object>()
			});

			alreadyAdded.Add(classItem.Iri);
		}

		return JsonSerializer.Serialize(classesList, _jsonSerializerOptions);
	}

	private string SerializeSubstructureFlattened(DataSpecificationSubstructure substructure)
	{
		HashSet<string> alreadyListed = [];
		List<Object> substructureFlatList = [];
		foreach (var substructureClass in substructure.ClassItems)
		{
			if (!alreadyListed.Contains(substructureClass.Iri))
			{
				substructureFlatList.Add(new
				{
					substructureClass.Iri,
					substructureClass.Label
				});
				alreadyListed.Add(substructureClass.Iri);
			}

			foreach (var objProperty in substructureClass.ObjectProperties)
			{
				if (!alreadyListed.Contains(objProperty.Iri))
				{
					substructureFlatList.Add(new
					{
						objProperty.Iri,
						objProperty.Label
					});
					alreadyListed.Add(objProperty.Iri);
				}
			}
			foreach (var dtProperty in substructureClass.DatatypeProperties)
			{
				if (!alreadyListed.Contains(dtProperty.Iri))
				{
					substructureFlatList.Add(new
					{
						dtProperty.Iri,
						dtProperty.Label
					});
					alreadyListed.Add(dtProperty.Iri);
				}
			}
		}
		return JsonSerializer.Serialize(substructureFlatList, _jsonSerializerOptions);
	}
}
