using DataSpecificationNavigatorBackend.Model;
using System.Diagnostics.CodeAnalysis;

namespace DataSpecificationNavigatorBackend.BusinessCoreLayer.Abstraction;

public interface IDataSpecificationService
{
	Task<DataSpecification?> ExportDataSpecificationFromDataspecerAsync(string dataspecerPackageUuid, string dataspecerPackageName);

	Task<List<DataSpecificationItem>> GetDataSpecificationItemsAsync(
		int dataSpecificationId,
		List<string> itemIriList);
}
