namespace DataSpecificationNavigatorBackend.Model;

public class DataSpecificationSubstructure
{
	public List<SubstructureClass> ClassItems { get; set; } = [];
	/*
	 * Each ClassItem in this list should have unique IRI, meaning each item is in there only once.
	 * I'm not using a HashSet because the uniqueness is not bound to the instance of the ClassItem, but rather to its IRI.
	 * HashSet does not help if I create two instances with identical values and add them.
	 */

	public class SubstructureClass
	{
		public required string Iri { get; set; }

		public required string Label { get; set; }

		public List<SubstructureObjectProperty> ObjectProperties { get; set; } = [];

		public List<SubstructureDatatypeProperty> DatatypeProperties { get; set; } = [];

		public required bool IsSelectTarget { get; set; } = false;
	}

	public class SubstructureObjectProperty
	{
		public required string Iri { get; set; }
					 
		public required string Label { get; set; }
					 
		public required string Domain { get; set; }
					 
		public required string Range { get; set; }

		public bool IsOptional { get; set; } = false;
	}

	public class SubstructureDatatypeProperty
	{
		public required string Iri { get; set; }

		public required string Label { get; set; }

		public required string Domain { get; set; }

		public required string Range { get; set; }

		public bool IsOptional { get; set; } = false;

		public bool IsSelectTarget { get; set; } = false;

		public string? FilterExpression { get; set; }
	}
}
