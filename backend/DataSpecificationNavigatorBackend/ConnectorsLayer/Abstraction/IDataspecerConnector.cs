namespace DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;

public interface IDataspecerConnector
{
	Task<string?> ExportDsvFileFromPackageAsync(string packageIri);
	Task<string?> ExportOwlFileFromPackageAsync(string packageIri);
}
