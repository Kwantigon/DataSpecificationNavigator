namespace DataSpecificationNavigatorBackend.Model;

public class UserSelection
{
	public int Id { get; set; }

	public required int ConversationId { get; set; }

	public virtual required Conversation Conversation { get; set; }

	public required string SelectedPropertyIri { get; set; }

	public bool IsOptional { get; set; } = false;

	public bool IsSelectTarget { get; set; } = false;

	public string? FilterExpression { get; set; }
}
