using System.Text.Json.Serialization;

namespace DataSpecificationNavigatorBackend.BusinessCoreLayer.DTO;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "MessageTypeDiscriminator")]
[JsonDerivedType(typeof(WelcomeMessageDTO), nameof(MessageDTOType.WelcomeMessage))]
[JsonDerivedType(typeof(UserMessageDTO), nameof(MessageDTOType.UserMessage))]
[JsonDerivedType(typeof(ReplyMessageDTO), nameof(MessageDTOType.ReplyMessage))]
public class MessageDTO
{
	[JsonPropertyName("id")]
	public Guid Id { get; set; }

	[JsonPropertyName("text")]
	public required string Text { get; set; }

	[JsonPropertyName("timestamp")]
	public DateTime Timestamp { get; set; }

	[JsonPropertyName("type")]
	public virtual MessageDTOType Type { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<MessageDTOType>))]
public enum MessageDTOType
{
	WelcomeMessage,
	UserMessage,
	ReplyMessage
}

public class WelcomeMessageDTO : MessageDTO
{
	[JsonPropertyName("dataSpecificationSummary")]
	public string? DataSpecificationSummary { get; set; }

	[JsonPropertyName("suggestedClasses")]
	public List<string> SuggestedClasses { get; set; } = [];

	public override MessageDTOType Type { get; set; } = MessageDTOType.WelcomeMessage;
}

public class UserMessageDTO : MessageDTO
{
	[JsonPropertyName("replyMessageUri")]
	public required string ReplyMessageUri { get; set; }

	public override MessageDTOType Type { get; set; } = MessageDTOType.UserMessage;
}

public class ReplyMessageDTO : MessageDTO
{
	[JsonPropertyName("mappedItems")]
	public List<MappedDataSpecificationItemDTO> MappedItems { get; set; } = [];

	[JsonPropertyName("sparqlQuery")]
	public string? SparqlQuery { get; set; }

	[JsonPropertyName("suggestions")]
	public required SuggestionsDTO Suggestions { get; set; }

	public override MessageDTOType Type { get; set; } = MessageDTOType.ReplyMessage;
}
