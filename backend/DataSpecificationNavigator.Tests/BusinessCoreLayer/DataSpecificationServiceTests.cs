using DataSpecificationNavigatorBackend.BusinessCoreLayer;
using DataSpecificationNavigatorBackend.BusinessCoreLayer.Facade;
using DataSpecificationNavigatorBackend.ConnectorsLayer;
using DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;
using DataSpecificationNavigatorBackend.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataSpecificationNavigator.Tests.BusinessCoreLayer;

public class DataSpecificationServiceTests
{
	[Fact]
	public async Task Test1()
	{
		#region Arrange
		string dsvContent;
		try
		{
			dsvContent = File.ReadAllText("TestData/mock-dsv.ttl");
		}
		catch (Exception ex)
		{
			throw new Exception("Failed to read the mock DSV file.", ex);
		}

		// Mock the DataspecerConnector to return the DSV content.
		var mockDataspecerConnector = new Mock<IDataspecerConnector>();
		mockDataspecerConnector
			.Setup(connector => connector.ExportDsvFileFromPackageAsync(It.IsAny<string>()))
			.ReturnsAsync(dsvContent);

		// Use an instance of RdfProcessor to process the DSV content.
		IRdfProcessor rdfProcessor = new RdfProcessor();

		// Use in-memory database.
		var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
				.UseInMemoryDatabase(databaseName: "TestDb")
				.Options;
		var appDbContext = new AppDbContext(dbOptions);

		// Create a logger instance.
		var logger = LoggerFactory.Create(builder =>
		{
			builder.AddConsole();
			builder.SetMinimumLevel(LogLevel.Trace);
		}).CreateLogger<DataSpecificationService>();

		// Create an instance of DataSpecificationService.
		var dataSpecificationService = new DataSpecificationService(
			logger,
			mockDataspecerConnector.Object,
			rdfProcessor,
			appDbContext);
		#endregion Arrange

		#region Act
		DataSpecification? dataSpecification =
			await dataSpecificationService.ExportDataSpecificationFromDataspecerAsync("mock-uuid", "Mock Dataspecer Package");
		#endregion Act

		#region Assert
		Assert.NotNull(dataSpecification);
		Assert.Equal("mock-uuid", dataSpecification.DataspecerPackageUuid);
		Assert.Equal("Mock Dataspecer Package", dataSpecification.Name);
		Assert.NotEmpty(dataSpecification.OwlContent);

		// Check if the items were correctly extracted.
		var items = await appDbContext.DataSpecificationItems.ToListAsync();
		Assert.Equal(3, items.Count);
		Assert.All(items, item => Assert.NotNull(item.Iri));
		Assert.All(items, item => Assert.NotNull(item.Label));

		// Check if the classes and properties were correctly identified.
		var classes = items.Where(item => item.Type == ItemType.Class).ToList();
		var properties = items.Where(item => item.Type != ItemType.Class).ToList();
		Assert.Equal(2, classes.Count);
		Assert.Single(properties);
		#endregion Assert
	}
}
