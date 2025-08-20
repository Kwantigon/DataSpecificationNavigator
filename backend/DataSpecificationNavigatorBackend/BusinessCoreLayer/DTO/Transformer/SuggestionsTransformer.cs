using DataSpecificationNavigatorBackend.Model;

namespace DataSpecificationNavigatorBackend.BusinessCoreLayer.DTO.Transformer;

public class SuggestionsTransformer
{
	public SuggestionsDTO TransformSuggestedProperties(
			IEnumerable<DataSpecificationPropertySuggestion> suggestions,
			DataSpecificationSubstructure substructure)
	{
		var directConnections = new List<GroupedSuggestionsDto>();
		var indirectConnections = new List<GroupedSuggestionsDto>();

		foreach (var classItem in substructure.ClassItems)
		{
			List<ArrowSuggestionDto> relevant = suggestions
				.Where(suggestion =>
				{
					if (suggestion.SuggestedProperty.DomainIri == classItem.Iri)
						return true;
					else if (suggestion.SuggestedProperty is ObjectPropertyItem objectProperty)
						return objectProperty.RangeIri == classItem.Iri;
					else
						return false;
				})
				.Select(suggestion =>
				{
					bool directionForward = suggestion.SuggestedProperty.DomainIri == classItem.Iri;
					string otherClass;
					if (directionForward)
					{
						if (suggestion.SuggestedProperty is ObjectPropertyItem objectProperty)
						{
							otherClass = objectProperty.Range.Label;
						}
						else if (suggestion.SuggestedProperty is DatatypePropertyItem datatypeProperty)
						{
							otherClass = datatypeProperty.RangeDatatypeIri;
						}
						else
						{
							// This should never happen.
							// But handle it just in case.
							otherClass = "[Unknown range]";
						}
					}
					else
					{
						otherClass = suggestion.SuggestedProperty.Domain.Label;
					}

					string connection = directionForward
						? $"→ {suggestion.SuggestedProperty.Label} → {otherClass}"
						: $"← {suggestion.SuggestedProperty.Label} ← {otherClass}";

					return new ArrowSuggestionDto(
						suggestion.SuggestedPropertyIri,
						suggestion.SuggestedProperty.Label,
						connection,
						suggestion.ReasonForSuggestion,
						suggestion.SuggestedProperty.Summary ?? string.Empty);
				})
				.ToList();

			if (relevant.Any())
			{
				directConnections.Add(new GroupedSuggestionsDto(classItem.Label, relevant));
			}
		}

		indirectConnections = suggestions
				.Where(suggestion =>
				{
					if (substructure.ClassItems.Any(item => suggestion.SuggestedProperty.DomainIri == item.Iri))
					{
						// Domain is in the substructure, so this is a direct connection.
						return false;
					}
					if (suggestion.SuggestedProperty is ObjectPropertyItem objectProperty)
					{
						if (substructure.ClassItems.Any(item => objectProperty.RangeIri == item.Iri)) {
							// Range is in the substructure, so this is a direct connection.
							return false;
						}
					}

					// If suggestion is a datatype property and domain is not in the substructure,
					// then it is an indirect connection.
					// If suggestion is an object property and domain is not in the substructure,
					// and range is also not in the substructure, then it is an indirect connection.
					return true;
				})
				.GroupBy(s => s.SuggestedProperty.Domain.Label)
				.Select(group => new GroupedSuggestionsDto(
						group.Key,
						group.Select(suggestion =>
						{
							string? range = null;
							if (suggestion.SuggestedProperty is ObjectPropertyItem objectProperty)
							{
								range = objectProperty.Range.Label;
							}
							else if (suggestion.SuggestedProperty is DatatypePropertyItem datatypeProperty)
							{
								range = datatypeProperty.RangeDatatypeIri;
							}
							else
							{
								// This should never happen.
								// But handle it just in case.
								range = "[Unknown range]";
							}

								return new ArrowSuggestionDto(
											suggestion.SuggestedProperty.Iri,
											suggestion.SuggestedProperty.Label,
											$"→ {suggestion.SuggestedProperty.Label} → {range}",
											suggestion.ReasonForSuggestion,
											suggestion.SuggestedProperty.Summary
									);
						}).ToList()
				))
				.ToList();

		return new SuggestionsDTO
		{
			DirectConnections = directConnections,
			IndirectConnections = indirectConnections
		};
	}
}

