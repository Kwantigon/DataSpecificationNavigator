using Microsoft.EntityFrameworkCore;

namespace DataSpecificationNavigatorBackend.Model;

[PrimaryKey(nameof(ItemDataSpecificationId), nameof(ItemIri), nameof(UserMessageId))]
public class DataSpecificationItemMapping
{
	public required int ItemDataSpecificationId { get; set; }

	public required string ItemIri { get; set; }

	public required Guid UserMessageId { get; set; }

	public virtual required DataSpecificationItem Item { get; set; }

	public virtual required UserMessage UserMessage { get; set; }

	public required string MappedWords { get; set; }

	public bool IsSelectTarget { get; set; } = false;
}
