using Microsoft.EntityFrameworkCore;

namespace DataSpecificationNavigatorBackend.Model;

[PrimaryKey(nameof(PropertyDataSpecificationId), nameof(SuggestedPropertyIri), nameof(UserMessageId))]
public class DataSpecificationPropertySuggestion
{
	public required int PropertyDataSpecificationId { get; set; }

	public required string SuggestedPropertyIri { get; set; }

	public required Guid UserMessageId { get; set; }

	public virtual required PropertyItem SuggestedProperty { get; set; }

	public virtual required UserMessage UserMessage { get; set; }

	public required string ReasonForSuggestion { get; set; }
}
