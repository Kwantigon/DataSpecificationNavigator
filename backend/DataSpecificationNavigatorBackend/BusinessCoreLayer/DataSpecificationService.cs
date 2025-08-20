using DataSpecificationNavigatorBackend.BusinessCoreLayer.Abstraction;
using DataSpecificationNavigatorBackend.BusinessCoreLayer.Facade;
using DataSpecificationNavigatorBackend.ConnectorsLayer;
using DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;
using DataSpecificationNavigatorBackend.Model;
using Microsoft.EntityFrameworkCore;

namespace DataSpecificationNavigatorBackend.BusinessCoreLayer;

public class DataSpecificationService(
	ILogger<DataSpecificationService> logger,
	IDataspecerConnector dataspecerConnector,
	IRdfProcessor rdfProcessor,
	AppDbContext appDbContext) : IDataSpecificationService
{
	private readonly ILogger<DataSpecificationService> _logger = logger;
	private readonly IDataspecerConnector _dataspecerConnector = dataspecerConnector;
	private readonly IRdfProcessor _rdfProcessor = rdfProcessor;
	private readonly AppDbContext _database = appDbContext;

	public async Task<DataSpecification?> ExportDataSpecificationFromDataspecerAsync(string dataspecerPackageUuid, string dataspecerPackageName)
	{
		string? dsv = await _dataspecerConnector.ExportDsvFileFromPackageAsync(dataspecerPackageUuid);
		string? owl;
		if (string.IsNullOrEmpty(dsv))
		{
			// Failed to export the DSV file, try to export the OWL file directly.
			_logger.LogError("Failed to export the DSV file from Dataspecer.");
			owl = await _dataspecerConnector.ExportOwlFileFromPackageAsync(dataspecerPackageUuid);
			if (string.IsNullOrEmpty(owl))
			{
				_logger.LogError("Failed to export the DSV file and the OWL file from Dataspecer.");
				return null;
			}
		}
		else
		{
			_logger.LogDebug("Converting DSV to OWL.");
			try
			{
				owl = _rdfProcessor.ConvertDsvGraphToOwlGraph(dsv);
			}
			catch (Exception ex)
			{
				// Failed to convert DSV to OWL, try to export the OWL file directly.
				_logger.LogError(ex, "An exception occured during DSV to OWL conversion.");
				owl = await _dataspecerConnector.ExportOwlFileFromPackageAsync(dataspecerPackageUuid);
				if (string.IsNullOrEmpty(owl))
				{
					_logger.LogError("Failed to export the DSV file and the OWL file from Dataspecer.");
					return null;
				}
			}
		}

		DataSpecification dataSpecification = new DataSpecification()
		{
			Name = dataspecerPackageName,
			DataspecerPackageUuid = dataspecerPackageUuid,
			OwlContent = owl
		};
		_logger.LogTrace("Extracting data specification items from the OWL graph.");
		List<ItemInfoFromGraph> items = _rdfProcessor.ExtractDataSpecificationItemsFromOwl(owl);
		if (items.Count == 0)
		{
			_logger.LogError("No data specification items found in the OWL graph.");
			return null;
		}
		List<ItemInfoFromGraph> classes = [];
		List<ItemInfoFromGraph> properties = [];
		// Split items into classes and properties.
		foreach (ItemInfoFromGraph item in items)
		{
			if (item.Type is ItemType.Class)
				classes.Add(item);
			else
				properties.Add(item);
		}

		// Validate extracted items and create DataSpecificationItem objects from them.
		Dictionary<string, ClassItem> classItemsMap = [];
		List<DataSpecificationItem> itemsToAddToDb = [];

		// First, create all classes.
		// I will need them later for domain and range references of properties.
		foreach (ItemInfoFromGraph clazz in classes)
		{
			if (clazz.Iri is null)
			{
				_logger.LogError("Class item from OWL graph has no IRI.");
				continue;
			}

			if (clazz.Label is null)
			{
				// Failed to retrieve the label during DSV to OWL conversion.
				// Use the iri as label (but only the fragment part).
				_logger.LogWarning("Class item from OWL graph has no label. Using IRI fragment as label.");
				string iriFragment = new Uri(clazz.Iri).Fragment.Substring(1); // The first character is '#' so I don't want that.
				iriFragment = Uri.UnescapeDataString(iriFragment);
				iriFragment.Replace('-', ' ');
				clazz.Label = iriFragment;
			}

			if (classItemsMap.ContainsKey(clazz.Iri))
			{
				_logger.LogWarning("Class with IRI {Iri} already exists in the classItemsMap. Skipping.", clazz.Iri);
				continue;
			}

			ClassItem classItem = new()
			{
				Iri = clazz.Iri,
				Label = clazz.Label,
				Type = ItemType.Class,
				DataSpecificationId = dataSpecification.Id,
				DataSpecification = dataSpecification
			};
			classItemsMap.Add(clazz.Iri, classItem);
			itemsToAddToDb.Add(classItem);
			_logger.LogTrace("Added class item with IRI {Iri} and label \"{Label}\" to the items to add to the database.", clazz.Iri, clazz.Label);
		}

		// Create properties.
		foreach (ItemInfoFromGraph property in properties)
		{
			if (property.Iri is null)
			{
				_logger.LogError("Property item from OWL graph has no IRI.");
				continue;
			}

			if (property.Label is null)
			{
				// Failed to retrieve the label during DSV to OWL conversion.
				// Use the iri as label (but only the fragment part).
				_logger.LogWarning("Property item from OWL graph has no label. Using IRI fragment as label.");
				string iriFragment = new Uri(property.Iri).Fragment.Substring(1); // The first character is '#' so I don't want that.
				iriFragment = Uri.UnescapeDataString(iriFragment);
				iriFragment.Replace('-', ' ');
				property.Label = iriFragment;
			}

			if (property.DomainIri is null || property.RangeIri is null)
			{
				// Both object and datatype properties must have domain and range.
				_logger.LogError("Property item from OWL graph has either no domain or no range IRI.");
				continue;
			}

			if (property.Type is ItemType.ObjectProperty)
			{
				if (classItemsMap.TryGetValue(property.DomainIri, out ClassItem? domainItem) &&
						classItemsMap.TryGetValue(property.RangeIri, out ClassItem? rangeItem))
				{
					ObjectPropertyItem objectProperty = new()
					{
						Iri = property.Iri,
						Label = property.Label,
						Type = ItemType.ObjectProperty,
						DomainIri = domainItem.Iri,
						Domain = domainItem,
						RangeIri = rangeItem.Iri,
						Range = rangeItem,
						DataSpecificationId = dataSpecification.Id,
						DataSpecification = dataSpecification
					};
					itemsToAddToDb.Add(objectProperty);
				}
				else // Domain or range (or both) not found in the classItemsMap.
				{
					_logger.LogError("Object property with IRI {Iri} has domain or range that is not present in the classItemsMap. Skipping.", property.Iri);
					_logger.LogError("Domain IRI: {DomainIri}, Range IRI: {RangeIri}", property.DomainIri, property.RangeIri);
					continue;
				}
			}

			if (property.Type is ItemType.DatatypeProperty)
			{
				if (classItemsMap.TryGetValue(property.DomainIri, out ClassItem? domainItem))
				{
					DatatypePropertyItem datatypeProperty = new()
					{
						Iri = property.Iri,
						Label = property.Label,
						Type = ItemType.DatatypeProperty,
						DomainIri = domainItem.Iri,
						Domain = domainItem,
						RangeDatatypeIri = property.RangeIri,
						DataSpecificationId = dataSpecification.Id,
						DataSpecification = dataSpecification
					};
					itemsToAddToDb.Add(datatypeProperty);
				}
				else // Domain item not found.
				{
					_logger.LogError("Datatype property with IRI {Iri} has domain that is not present in the classItemsMap. Skipping.", property.Iri);
					continue;
				}
			}
		}

		await _database.DataSpecifications.AddAsync(dataSpecification);
		await _database.DataSpecificationItems.AddRangeAsync(itemsToAddToDb);
		await _database.SaveChangesAsync();
		return dataSpecification;
	}

	public async Task<List<DataSpecificationItem>> GetDataSpecificationItemsAsync(int dataSpecificationId, List<string> itemIriList)
	{
		return await _database.DataSpecificationItems
					.Where(item => item.DataSpecificationId == dataSpecificationId && itemIriList.Contains(item.Iri))
					.ToListAsync();
	}
}

public record ItemInfoFromGraph
{
	internal string? Iri { get; set; }

	internal string? Label { get; set; }

	internal ItemType? Type { get; set; }

	internal string? DomainIri { get; set; }

	internal string? RangeIri { get; set; }
}
