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
	public async Task ExportDataSpecificationFromDataspecerTestAsync_ExpectsFourItems()
	{
		#region Arrange
		string dsvContent = """
			@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>.
			@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#>.
			@prefix dct: <http://purl.org/dc/terms/>.
			@prefix dsv: <https://w3id.org/dsv#>.
			@prefix owl: <http://www.w3.org/2002/07/owl#>.
			@prefix skos: <http://www.w3.org/2004/02/skos/core#>.
			@prefix vann: <http://purl.org/vocab/vann/>.
			@prefix cardinality: <https://w3id.org/dsv/cardinality#>.
			@prefix requirement: <https://w3id.org/dsv/requirement-level#>.
			@prefix role: <https://w3id.org/dsv/class-role#>.
			@prefix prof: <http://www.w3.org/ns/dx/prof/>.
			@prefix : <http://www.example.unit-test.com/>.

			<> a prof:Profile, dsv:ApplicationProfile.

			<turistický-cíl> dct:isPartOf <>;
			    a dsv:TermProfile;
			    dsv:reusesPropertyValue [
			  a dsv:PropertyValueReuse;
			  dsv:reusedProperty skos:prefLabel;
			  dsv:reusedFromResource <https://slovník.gov.cz/datový/turistické-cíle/pojem/turistický-cíl>
			], [
			  a dsv:PropertyValueReuse;
			  dsv:reusedProperty skos:definition;
			  dsv:reusedFromResource <https://slovník.gov.cz/datový/turistické-cíle/pojem/turistický-cíl>
			];
			    a dsv:ClassProfile;
			    dsv:class <https://slovník.gov.cz/datový/turistické-cíle/pojem/turistický-cíl>.

			<bezbariérový-přístup> dct:isPartOf <>;
			    a dsv:TermProfile;
			    dsv:reusesPropertyValue [
			  a dsv:PropertyValueReuse;
			  dsv:reusedProperty skos:prefLabel;
			  dsv:reusedFromResource <https://slovník.gov.cz/generický/bezbariérové-přístupy/pojem/bezbariérový-přístup>
			], [
			  a dsv:PropertyValueReuse;
			  dsv:reusedProperty skos:definition;
			  dsv:reusedFromResource <https://slovník.gov.cz/generický/bezbariérové-přístupy/pojem/bezbariérový-přístup>
			];
			    a dsv:ClassProfile;
			    dsv:class <https://slovník.gov.cz/generický/bezbariérové-přístupy/pojem/bezbariérový-přístup>.

			<bezbariérovost> dct:isPartOf <>;
			    a dsv:TermProfile;
			    dsv:reusesPropertyValue [
			  a dsv:PropertyValueReuse;
			  dsv:reusedProperty skos:prefLabel;
			  dsv:reusedFromResource <https://slovník.gov.cz/datový/turistické-cíle/pojem/bezbariérovost>
			], [
			  a dsv:PropertyValueReuse;
			  dsv:reusedProperty skos:definition;
			  dsv:reusedFromResource <https://slovník.gov.cz/datový/turistické-cíle/pojem/bezbariérovost>
			];
			    dsv:cardinality cardinality:0n;
			    dsv:property <https://slovník.gov.cz/datový/turistické-cíle/pojem/bezbariérovost>;
			    dsv:domain <turistický-cíl>;
			    a dsv:ObjectPropertyProfile;
			    dsv:objectPropertyRange <bezbariérový-přístup>.

			<kapacita> dct:isPartOf <>;
			    a dsv:TermProfile;
			    dsv:reusesPropertyValue [
			  a dsv:PropertyValueReuse;
			  dsv:reusedProperty skos:prefLabel;
			  dsv:reusedFromResource <https://slovník.gov.cz/datový/sportoviště/pojem/kapacita>
			], [
			  a dsv:PropertyValueReuse;
			  dsv:reusedProperty skos:definition;
			  dsv:reusedFromResource <https://slovník.gov.cz/datový/sportoviště/pojem/kapacita>
			];
			    dsv:cardinality cardinality:0n;
			    dsv:property <https://slovník.gov.cz/datový/sportoviště/pojem/kapacita>;
			    dsv:domain <turistický-cíl>;
			    a dsv:DatatypePropertyProfile;
			    dsv:datatypePropertyRange rdfs:Literal.
			""";

		// Mock the DataspecerConnector to return the DSV content.
		var mockDataspecerConnector = new Mock<IDataspecerConnector>();
		mockDataspecerConnector
			.Setup(connector => connector.ExportDsvFileFromPackageAsync(It.IsAny<string>()))
			.ReturnsAsync(dsvContent);

		// Use an instance of RdfProcessor to process the DSV content.
		IRdfProcessor rdfProcessor = new RdfProcessor(LoggerFactory
			.Create(c => c.AddConsole().SetMinimumLevel(LogLevel.Trace))
			.CreateLogger<RdfProcessor>()
		);

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

		var classItems = await appDbContext.ClassItems.ToListAsync();
		Assert.Equal(2, classItems.Count);

		var objectProperties = await appDbContext.ObjectPropertyItems.ToListAsync();
		Assert.Single(objectProperties);
		ObjectPropertyItem objectProperty = objectProperties.First();
		Assert.Equal(dataSpecification.Id, objectProperty.DataSpecificationId);
		Assert.False(string.IsNullOrWhiteSpace(objectProperty.Iri));
		Assert.False(string.IsNullOrWhiteSpace(objectProperty.Label));

		ClassItem domain = objectProperty.Domain;
		Assert.Equal(dataSpecification.Id, domain.DataSpecificationId);
		Assert.False(string.IsNullOrWhiteSpace(domain.Iri));
		Assert.False(string.IsNullOrWhiteSpace(domain.Label));

		ClassItem range = objectProperty.Range;
		Assert.Equal(dataSpecification.Id, range.DataSpecificationId);
		Assert.False(string.IsNullOrWhiteSpace(range.Iri));
		Assert.False(string.IsNullOrWhiteSpace(range.Label));

		var datatypeProperties = await appDbContext.DatatypePropertyItems.ToListAsync();
		Assert.Single(datatypeProperties);
		DatatypePropertyItem datatypeProperty = datatypeProperties.First();
		Assert.False(string.IsNullOrWhiteSpace(datatypeProperty.Iri));
		Assert.False(string.IsNullOrWhiteSpace(datatypeProperty.Label));
		Assert.Equal(domain, datatypeProperty.Domain);

		#endregion Assert
	}
}
