using Microsoft.EntityFrameworkCore;

namespace DataSpecificationNavigatorBackend.Model;

public class DataSpecificationPropertySuggestion
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public required int PropertyDataSpecificationId { get; set; }

	public required string SuggestedPropertyIri { get; set; }

	public required Guid UserMessageId { get; set; }

	public virtual required PropertyItem SuggestedProperty { get; set; }

	public virtual required UserMessage UserMessage { get; set; }

	public required string ReasonForSuggestion { get; set; }
}
