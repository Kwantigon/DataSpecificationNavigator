using System.Text.Json.Serialization;

namespace DataSpecificationNavigatorBackend.Model;

public abstract class Message
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public virtual MessageSource Sender { get; set; }

	public required string TextContent { get; set; }

	public DateTime Timestamp { get; set; }

	public virtual required Conversation Conversation { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<MessageSource>))]
public enum MessageSource
{
	System,
	User
}

/// <summary>
/// This is just the very first message in the conversation.
/// It does not contain anything interesting except the text.
/// </summary>
public class WelcomeMessage : Message
{
	public override MessageSource Sender { get => MessageSource.System; }

	public string? DataSpecificationSummary { get; set; }

	public List<string> SuggestedClasses { get; set; } = [];
}

public class UserMessage : Message
{
	public override MessageSource Sender { get => MessageSource.User; }

	public Guid? ReplyMessageId { get; set; } // For Entity Framework configuration.

	public virtual ReplyMessage? ReplyMessage { get; set; }
}

public class ReplyMessage : Message
{
	public override MessageSource Sender { get => MessageSource.System; }

	public required Guid PrecedingUserMessageId { get; set; } // For Entity Framework configuration.
	public virtual required UserMessage PrecedingUserMessage { get; set; }
	public string? SparqlQuery { get; set; }
}
