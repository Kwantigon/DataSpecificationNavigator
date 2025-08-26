using DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;
using DataSpecificationNavigatorBackend.Model;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace DataSpecificationNavigatorBackend.ConnectorsLayer.LlmConnectors.LLama3._3_70b;

public class LlamaPromptConstructor(
	ILogger<LlamaPromptConstructor> logger,
	AppDbContext appDbContext) : ILlmPromptConstructor
{
	private readonly ILogger<LlamaPromptConstructor> _logger = logger;
	private readonly AppDbContext _database = appDbContext;
	private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
	{
		Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	private readonly string _dataSpecificationSummaryTemplate = """
		You are a system that summarizes data specifications.

		### Data specification
		Here is the data specification as structured items extracted from an OWL file:

		{0}

		Each item may include:
		- `Iri`: identifier from OWL
		- `Label`: the class or property name
		- `Type`: Class, ObjectProperty, or DatatypeProperty
		- `OwlAnnotation`: annotation extracted from the OWL file
		- `RdfsComment`: comment extracted from the OWL file

		### Task
		1. Provide a **concise, user-friendly summary** (3–5 sentences) describing the main domain of this specification in plain English.
		   - Focus on what real-world entities it models (e.g., people, organizations, events, products) and how they relate.
		   - Avoid technical jargon such as "ontology", "object property", "domain", or "range".
		   - Write the summary so that a non-technical user can understand what kinds of questions the specification helps answer.

		2. Suggest **3–5 key classes** that would make good starting points for user queries.
		   - Select the most central or frequently connected classes based on the provided items.
		   - For each suggested class, provide its `Label` and a short `Reason` why it is important.

		IMPORTANT:
		- Only suggest classes present in the provided data specification.
		- Do NOT invent any classes.
		- Only use classes present in the specification.
		- If a suggested class is not present in the provided data, do not include it in the output at all.
		- Do NOT provide any explanations, commentary, reasoning, or apologies.
		- Return ONLY a raw JSON.
		- Avoid technical jargon such as "ontology", "object property", "domain", or "range".

		## Output format
		Return a **raw JSON object** with the following fields:
		{{
			"Summary": "A user-friendly summary (3–5 sentences) describing the domain of the specification",
			"SuggestedClasses": [
				{{
					"Label": "The class label",
					"Reason": "A brief explanation of why this class is important"
				}}
			]
		}}

		IMPORTANT:
		- Return ONLY a raw JSON.
		- Do NOT provide explanations, commentary, or apologies.
		
		""";

	private readonly string _mapToDataSpecificationTemplate = """
		You are an expert system designed to extract structured item mappings from user questions based on OWL data specifications.

		### Data specification
		The following is a flattened list of data specification items in JSON format, extracted from the OWL file.

		{0}

		### User question
		The question that user asked is:
		"{1}"

		### Task
		1. Identify which items from the data specification are explicitly mentioned in the user's question.
		   - Only include items that can be directly mapped to words or phrases in the question.
		   - Each word or phrase may only map to one item.
		   - If an item cannot be mapped to any phrase, do not include it in the output.

		2. For each identified item, provide:
		   - The item's IRI.
		   - A brief, user-friendly summary (3–4 sentences) describing the item in the context of the data specification.
		   - The exact words or phrase from the question (`MappedWords`).

		IMPORTANT:
		- The output must contain only classes or properties from the provided data specification.
		- Do NOT invent any classes or properties.
		- If a suggested property is not present in the provided data specification, do not include it in the output at all.
		- Do NOT provide any explanations, commentary, reasoning, or apologies.
		- Return ONLY a raw JSON array.
		- If the user's question is incoherent or unrelated to the specification, return an empty array.

		### Output format
		Return only a raw JSON array. Each element must follow this structure exactly:

		[
		  {{
		    "Iri": "string",
		    "Summary": "string",
		    "MappedWords": "string"
		  }}
		]

		Do not include markdown, commentary, or explanations.
		Return **only the JSON array**.
		
		""";

	private readonly string _getSuggestedItemsTemplate = """
		You are assisting me in exploring a data specification.

		### Specification subset
		Here is the relevant part of the specification, grouped by class:

		{0}

		### Current question
		"{1}"

		### Current context
		These items are already mentioned or used in the current question:

		{2}

		### Task
		Suggest 8 additional properties from the specification that could expand my question.

		IMPORTANT:
		- Only suggest ObjectProperty or DatatypeProperty present in the provided data specification subset.
		- Do not suggest any items listed in the current context.
		- Do NOT invent any properties.
		- Only use properties present in the specification subset.
		- If a suggested property is not present in the provided data, do not include it in the output at all.
		- Suggestions must have either their domain or range class in the current context list above.
		- Do NOT provide any explanations, commentary, reasoning, or apologies.
		- Exclude unrelated items. If nothing fits, return an empty array.
		- Return ONLY a raw JSON array.

		### Output format
		Return a **raw JSON array**. Each element must be an object with these fields:

		[
		  {{
		    "Iri": "string",
		    "Summary": "User-friendly summary in 2-3 sentences describing the property",
		    "Reason": "Why this property is relevant to the current question",
		    "DomainClass": {{
		      "Iri": "string",
		      "Summary": "User-friendly summary in 2-3 sentences describing the domain class in the context of the data specification"
		    }},
		    "RangeClass": {{
		      "Iri": "string",
		      "Summary": "User-friendly summary in 2-3 sentences describing the range class in the context of the data specification (empty for datatypes)"
		    }}
		  }}
		]

		IMPORTANT:
		- Return ONLY a raw JSON array.
		- Do NOT provide explanations, commentary, or apologies.
		
		""";

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

		return string.Format(_getSuggestedItemsTemplate, substructureProximity, userQuestion, substructureString);
	}

	public string BuildGenerateSuggestedMessagePrompt(DataSpecification dataSpecification, string userQuestion, DataSpecificationSubstructure substructure, List<DataSpecificationItem> selectedItems)
	{
		throw new NotImplementedException();
	}

	public string BuildMapToSubstructurePrompt(DataSpecification dataSpecification, string userQuestion, DataSpecificationSubstructure substructure)
	{
		throw new NotImplementedException();
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
