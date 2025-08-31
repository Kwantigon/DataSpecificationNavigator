using DataSpecificationNavigatorBackend.BusinessCoreLayer.SparqlTranslation;
using DataSpecificationNavigatorBackend.Model;
using Microsoft.Extensions.Logging;

namespace DataSpecificationNavigator.Tests.BusinessCoreLayer;

public class SparqlTranslationServiceTests
{
	[Fact]
	public void TranslateSubstructureTest_GraphWithTwoCycles()
	{
		#region Arrange
		var logger = LoggerFactory.Create(builder =>
		{
			builder.AddConsole();
			builder.SetMinimumLevel(LogLevel.Trace);
		}).CreateLogger<SparqlTranslationService>();

		var substructure = new DataSpecificationSubstructure
		{
			ClassItems = [
        // === Cycle 1: A -> B -> C -> A ===
        new DataSpecificationSubstructure.SubstructureClass
				{
						Iri = "http://example.com/ClassA",
						Label = "Class A",
						ObjectProperties =
						[
								new DataSpecificationSubstructure.SubstructureObjectProperty
								{
										Iri = "http://example.com/propAtoB",
										Label = "A to B",
										Domain = "http://example.com/ClassA",
										DomainLabel = "Class A",
										Range = "http://example.com/ClassB",
										RangeLabel = "Class B"
								}
						]
				},
				new DataSpecificationSubstructure.SubstructureClass
				{
						Iri = "http://example.com/ClassB",
						Label = "Class B",
						ObjectProperties =
						[
								new DataSpecificationSubstructure.SubstructureObjectProperty
								{
										Iri = "http://example.com/propBtoC",
										Label = "B to C",
										Domain = "http://example.com/ClassB",
										DomainLabel = "Class B",
										Range = "http://example.com/ClassC",
										RangeLabel = "Class C"
								}
						]
				},
				new DataSpecificationSubstructure.SubstructureClass
				{
						Iri = "http://example.com/ClassC",
						Label = "Class C",
						ObjectProperties =
						[
								new DataSpecificationSubstructure.SubstructureObjectProperty
								{
										Iri = "http://example.com/propCtoA",
										Label = "C to A",
										Domain = "http://example.com/ClassC",
										DomainLabel = "Class C",
										Range = "http://example.com/ClassA",
										RangeLabel = "Class A"
								}
						]
				},

        // === Cycle 2: X -> Y -> Z -> X ===
        new DataSpecificationSubstructure.SubstructureClass
				{
						Iri = "http://example.com/ClassX",
						Label = "Class X",
						ObjectProperties =
						[
								new DataSpecificationSubstructure.SubstructureObjectProperty
								{
										Iri = "http://example.com/propXtoY",
										Label = "X to Y",
										Domain = "http://example.com/ClassX",
										DomainLabel = "Class X",
										Range = "http://example.com/ClassY",
										RangeLabel = "Class Y"
								}
						]
				},
				new DataSpecificationSubstructure.SubstructureClass
				{
						Iri = "http://example.com/ClassY",
						Label = "Class Y",
						ObjectProperties =
						[
								new DataSpecificationSubstructure.SubstructureObjectProperty
								{
										Iri = "http://example.com/propYtoZ",
										Label = "Y to Z",
										Domain = "http://example.com/ClassY",
										DomainLabel = "Class Y",
										Range = "http://example.com/ClassZ",
										RangeLabel = "Class Z"
								}
						]
				},
				new DataSpecificationSubstructure.SubstructureClass
				{
						Iri = "http://example.com/ClassZ",
						Label = "Class Z",
						ObjectProperties =
						[
								new DataSpecificationSubstructure.SubstructureObjectProperty
								{
										Iri = "http://example.com/propZtoX",
										Label = "Z to X",
										Domain = "http://example.com/ClassZ",
										DomainLabel = "Class Z",
										Range = "http://example.com/ClassX",
										RangeLabel = "Class X"
								}
						]
				}
			]
		};
		#endregion Arrange

		#region Act
		var sparqlTranslationService = new SparqlTranslationService(logger);
		string sparql = sparqlTranslationService.TranslateSubstructure(substructure);
		#endregion Act

		#region Assert
		const string expectedSparql = """
			SELECT DISTINCT *
			WHERE {
			  # Class A
			  ?Class_A a <http://example.com/ClassA> .
			  ?Class_A <http://example.com/propAtoB> ?Class_B .
			  # Class B
			  ?Class_B a <http://example.com/ClassB> .
			  ?Class_B <http://example.com/propBtoC> ?Class_C .
			  # Class C
			  ?Class_C a <http://example.com/ClassC> .
			  ?Class_C <http://example.com/propCtoA> ?Class_A .
			  # Class X
			  ?Class_X a <http://example.com/ClassX> .
			  ?Class_X <http://example.com/propXtoY> ?Class_Y .
			  # Class Y
			  ?Class_Y a <http://example.com/ClassY> .
			  ?Class_Y <http://example.com/propYtoZ> ?Class_Z .
			  # Class Z
			  ?Class_Z a <http://example.com/ClassZ> .
			  ?Class_Z <http://example.com/propZtoX> ?Class_X .
			}

			""";
		Assert.Equal(expectedSparql, sparql);
		#endregion Assert
	}

	[Fact]
	public void TranslateSubstructureTest_GraphWithTwoCyclesOverlapping()
	{
		#region Arrange
		var logger = LoggerFactory.Create(builder =>
		{
			builder.AddConsole();
			builder.SetMinimumLevel(LogLevel.Trace);
		}).CreateLogger<SparqlTranslationService>();

		var substructure = new DataSpecificationSubstructure
		{
			ClassItems =
		[
        // === Cycle 1 Part ===
        new DataSpecificationSubstructure.SubstructureClass
				{
						Iri = "http://example.com/ClassA",
						Label = "Class A",
						ObjectProperties =
						[
								new DataSpecificationSubstructure.SubstructureObjectProperty
								{
										Iri = "http://example.com/propAtoB",
										Label = "A to B",
										Domain = "http://example.com/ClassA",
										DomainLabel = "Class A",
										Range = "http://example.com/ClassB",
										RangeLabel = "Class B"
								}
						]
				},
				new DataSpecificationSubstructure.SubstructureClass
				{
						Iri = "http://example.com/ClassB",
						Label = "Class B",
						ObjectProperties =
						[
                // B → C (Cycle 1)
                new DataSpecificationSubstructure.SubstructureObjectProperty
								{
										Iri = "http://example.com/propBtoC",
										Label = "B to C",
										Domain = "http://example.com/ClassB",
										DomainLabel = "Class B",
										Range = "http://example.com/ClassC",
										RangeLabel = "Class C"
								},
                // B → Y (Cycle 2)
                new DataSpecificationSubstructure.SubstructureObjectProperty
								{
										Iri = "http://example.com/propBtoY",
										Label = "B to Y",
										Domain = "http://example.com/ClassB",
										DomainLabel = "Class B",
										Range = "http://example.com/ClassY",
										RangeLabel = "Class Y"
								}
						]
				},
				new DataSpecificationSubstructure.SubstructureClass
				{
						Iri = "http://example.com/ClassC",
						Label = "Class C",
						ObjectProperties =
						[
								new DataSpecificationSubstructure.SubstructureObjectProperty
								{
										Iri = "http://example.com/propCtoA",
										Label = "C to A",
										Domain = "http://example.com/ClassC",
										DomainLabel = "Class C",
										Range = "http://example.com/ClassA",
										RangeLabel = "Class A"
								}
						]
				},

        // === Cycle 2 Part ===
        new DataSpecificationSubstructure.SubstructureClass
				{
						Iri = "http://example.com/ClassX",
						Label = "Class X",
						ObjectProperties =
						[
								new DataSpecificationSubstructure.SubstructureObjectProperty
								{
										Iri = "http://example.com/propXtoB",
										Label = "X to B",
										Domain = "http://example.com/ClassX",
										DomainLabel = "Class X",
										Range = "http://example.com/ClassB",
										RangeLabel = "Class B"
								}
						]
				},
				new DataSpecificationSubstructure.SubstructureClass
				{
						Iri = "http://example.com/ClassY",
						Label = "Class Y",
						ObjectProperties =
						[
								new DataSpecificationSubstructure.SubstructureObjectProperty
								{
										Iri = "http://example.com/propYtoX",
										Label = "Y to X",
										Domain = "http://example.com/ClassY",
										DomainLabel = "Class Y",
										Range = "http://example.com/ClassX",
										RangeLabel = "Class X"
								}
						]
				}
		]
		};
		#endregion Arrange

		#region Act
		var sparqlTranslationService = new SparqlTranslationService(logger);
		string sparql = sparqlTranslationService.TranslateSubstructure(substructure);
		#endregion Act
		
		#region Assert
		const string expectedSparql = """
			SELECT DISTINCT *
			WHERE {
			  # Class A
			  ?Class_A a <http://example.com/ClassA> .
			  ?Class_A <http://example.com/propAtoB> ?Class_B .
			  # Class B
			  ?Class_B a <http://example.com/ClassB> .
			  ?Class_B <http://example.com/propBtoC> ?Class_C .
			  # Class C
			  ?Class_C a <http://example.com/ClassC> .
			  ?Class_C <http://example.com/propCtoA> ?Class_A .
			  ?Class_B <http://example.com/propBtoY> ?Class_Y .
			  # Class Y
			  ?Class_Y a <http://example.com/ClassY> .
			  ?Class_Y <http://example.com/propYtoX> ?Class_X .
			  # Class X
			  ?Class_X a <http://example.com/ClassX> .
			  ?Class_X <http://example.com/propXtoB> ?Class_B .
			}

			""";
		Assert.Equal(expectedSparql, sparql);
		#endregion Assert
	}
}
