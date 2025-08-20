namespace DataSpecificationNavigatorBackend.BusinessCoreLayer.DTO;

/// <summary>
/// DTO for the POST /conversations endpoint.
/// </summary>
/// <param name="ConversationTitle">Title of the new conversation.</param>
/// <param name="DataspecerPackageUuid">UUID of the Dataspecer package used for the conversation.</param>
/// <param name="DataspecerPackageName">Name of the Dataspecer package.</param>
public record PostConversationsDTO(string ConversationTitle, string DataspecerPackageUuid, string DataspecerPackageName);
