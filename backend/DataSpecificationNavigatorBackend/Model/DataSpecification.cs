namespace DataSpecificationNavigatorBackend.Model;

public class DataSpecification
{
	public int Id { get; set; }

	public required string Name { get; set; }

	public required string OwlContent { get; set; }

	public required string DataspecerPackageUuid { get; set; }
}
