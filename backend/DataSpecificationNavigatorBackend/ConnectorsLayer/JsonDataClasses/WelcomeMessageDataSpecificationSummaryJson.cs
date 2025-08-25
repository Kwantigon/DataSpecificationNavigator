namespace DataSpecificationNavigatorBackend.ConnectorsLayer.JsonDataClasses;

public record WelcomeMessageDataSpecificationSummaryJson(
	string Summary,
	List<WelcomeMessageDataSpecificationSummaryJson.ClassSuggestionJson> SuggestedClasses)
{
	public record ClassSuggestionJson(
	string Label,
	string? Reason);
}
