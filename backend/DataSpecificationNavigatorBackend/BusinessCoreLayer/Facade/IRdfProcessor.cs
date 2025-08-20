namespace DataSpecificationNavigatorBackend.BusinessCoreLayer.Facade;

/// <summary>
/// A facade for the dotnetRdf library used to process the DSVs from Dataspecer.
/// </summary>
public interface IRdfProcessor
{
	string ConvertDsvGraphToOwlGraph(string dsv);

	List<ItemInfoFromGraph> ExtractDataSpecificationItemsFromOwl(string owl);
}