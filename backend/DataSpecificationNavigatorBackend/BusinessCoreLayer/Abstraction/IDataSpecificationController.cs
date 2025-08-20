using DataSpecificationNavigatorBackend.BusinessCoreLayer.DTO;

namespace DataSpecificationNavigatorBackend.BusinessCoreLayer.Abstraction;

public interface IDataSpecificationController
{
	Task<IResult> ProcessDataspecerPackage(PostDataSpecificationsDTO payload);
}
