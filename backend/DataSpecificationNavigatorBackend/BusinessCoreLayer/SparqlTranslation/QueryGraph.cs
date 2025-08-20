namespace DataSpecificationNavigatorBackend.BusinessCoreLayer.SparqlTranslation;

using System.Collections.Generic;

public class QueryGraph
{
	private readonly Dictionary<string, QueryNode> _nodesByIri = new();

	public List<QueryNode> Roots { get; } = new();

	public QueryNode GetOrCreateNode(string iri, string label, bool isSelectTarget = false)
	{
		if (_nodesByIri.TryGetValue(iri, out var node))
		{
			if (isSelectTarget)
				node.IsSelectTarget = true;
			return node;
		}

		var newNode = new QueryNode
		{
			Iri = iri,
			Label = label,
			IsSelectTarget = isSelectTarget
		};

		_nodesByIri[iri] = newNode;
		return newNode;
	}

	public IEnumerable<QueryNode> Nodes => _nodesByIri.Values;
}

public class QueryNode
{
	public required string Iri { get; set; }

	public required string Label { get; set; }

	public bool IsSelectTarget { get; set; }

	public List<QueryEdge> OutgoingEdges { get; set; } = new();

	public List<DatatypeNode> DatatypeProperties { get; set; } = new();

	internal string? VariableName { get; set; }
}

public class QueryEdge
{
	public required string PropertyIri { get; set; }

	public required string PropertyLabel { get; set; }

	public bool IsOptional { get; set; }

	public required QueryNode Target { get; set; }
}

public class DatatypeNode
{
	public required string PropertyIri { get; set; }

	public required string PropertyLabel { get; set; }

	public required string Range { get; set; }

	public bool IsOptional { get; set; }

	public bool IsSelectTarget { get; set; }

	public string? FilterExpression { get; set; }

	internal string? VariableName { get; set; }
}

