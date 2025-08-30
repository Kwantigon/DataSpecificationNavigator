using DataSpecificationNavigatorBackend.BusinessCoreLayer;
using DataSpecificationNavigatorBackend.BusinessCoreLayer.Abstraction;
using DataSpecificationNavigatorBackend.BusinessCoreLayer.DTO;
using DataSpecificationNavigatorBackend.BusinessCoreLayer.Facade;
using DataSpecificationNavigatorBackend.BusinessCoreLayer.SparqlTranslation;
using DataSpecificationNavigatorBackend.ConnectorsLayer;
using DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;
using DataSpecificationNavigatorBackend.ConnectorsLayer.LlmConnectors;
using DataSpecificationNavigatorBackend.ConnectorsLayer.LlmConnectors.Gemini;
using DataSpecificationNavigatorBackend.ConnectorsLayer.LlmConnectors.LLama3._3_70b;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
	.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
	.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
	.AddJsonFile("usersettings.json", optional: true, reloadOnChange: true)
	.AddEnvironmentVariables();

builder.Services
	.AddScoped<IConversationService, ConversationService>()
	.AddScoped<IConversationController, ConversationController>()
	.AddScoped<IDataSpecificationService, DataSpecificationService>()
	.AddScoped<IDataSpecificationController, DataSpecificationController>()
	.AddScoped<IDataspecerConnector, DataspecerConnector>()
	/*.AddScoped<ILlmConnector, GeminiConnector>()
	.AddScoped<ILlmResponseProcessor, GeminiResponseProcessor>()
	.AddScoped<ILlmPromptConstructor, GeminiPromptConstructor>()*/
	.AddScoped<ILlmConnector, OllamaConnector>()
	.AddScoped<ILlmPromptConstructor, LlamaPromptConstructor>()
	.AddScoped<ILlmResponseProcessor, LlamaResponseProcessor>()
	.AddScoped<IRdfProcessor, RdfProcessor>()
	.AddScoped<ISparqlTranslationService, SparqlTranslationService>()
	;

builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(policy =>
	{
		policy.AllowAnyOrigin()
			.AllowAnyMethod()
			.AllowAnyHeader()
			.SetIsOriginAllowed(origin => true);
	});
});
builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "Data specification navigator API",
		Description = "The back end for the data specification navigator project.",
		Version = "v1"
	}
	);
});

var connectionString = builder.Configuration
	.GetConnectionString("Sqlite") ?? "Data Source=/app/data/DataSpecificationNavigatorDB.db";
builder.Services
	.AddDbContext<AppDbContext>(b => b.UseSqlite(connectionString)
	.UseLazyLoadingProxies());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
	// Apply database migrations.
	var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
	db.Database.Migrate();
}

app.UseCors();
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI(c =>
	{
		c.SwaggerEndpoint("/swagger/v1/swagger.json", "Data specification navigator API");
	});
}

// Sanity check. Also check the environment variables.
app.MapGet("/hello", (IConfiguration config) =>
{
	#region Ollama configuration
	string? ollamaUri = config["Env:Llm:Ollama:Uri"];
	if (string.IsNullOrWhiteSpace(ollamaUri))
	{
		ollamaUri = config["Llm:Ollama:Uri"];

		if (string.IsNullOrWhiteSpace(ollamaUri))
			ollamaUri = "MISSING";
	}

	string? ollamaModel = config["Env:Llm:Ollama:Model"];
	if (string.IsNullOrWhiteSpace(ollamaModel))
	{
		ollamaModel = config["Llm:Ollama:Model"];

		if (string.IsNullOrWhiteSpace(ollamaModel))
			ollamaModel = "MISSING";
	}

	string? retryAttemptsStr = config["Env:Llm:Ollama:RetryAttempts"];
	int retryAttempts = -1;
	if (string.IsNullOrWhiteSpace(retryAttemptsStr))
	{
		retryAttempts = config.GetValue("Llm:Ollama:RetryAttempts", 3);
	}
	else
	{
		retryAttempts = int.TryParse(retryAttemptsStr, out int parsedRetries) ? parsedRetries : 3;
	}
	#endregion Ollama configuration

	return Results.Ok(new
	{
		Message = "Hello from Data specification navigator backend!",
		Ollama = new
		{
			Uri = ollamaUri,
			Model = ollamaModel,
			RetryAttempts = retryAttempts
		}
	});
});

app.MapGet("/conversations",
			async (IConversationController controller) => await controller.GetAllConversationsAsync())
	.WithOpenApi(endpoint =>
	{
		endpoint.Summary = "Get all ongoing conversations.";
		endpoint.Description = "Frontend calls this to display all conversations in the conversations management tab.";
		return endpoint;
	});

app.MapGet("/conversations/{conversationId}",
			async ([FromRoute] int conversationId, IConversationController controller) => await controller.GetConversationAsync(conversationId))
	.WithOpenApi(endpoint =>
	{
		endpoint.Summary = "Get information about the conversation.";
		endpoint.Description = "This endpoint is only for debugging. The frontend does not need to call this for anything.";
		return endpoint;
	});

app.MapGet("/conversations/{conversationId}/messages",
			async ([FromRoute] int conversationId, IConversationController controller) => await controller.GetConversationMessagesAsync(conversationId))
	.WithOpenApi(endpoint =>
	{
		endpoint.Summary = "Get all messages in the conversation.";
		endpoint.Description = "Returns all messages ordered by their timestamps. The frontend calls this when it loads a conversation and needs to display messages in the conversation.";
		return endpoint;
	});

app.MapGet("/conversations/{conversationId}/messages/{messageId}",
			async ([FromRoute] int conversationId, [FromRoute] Guid messageId,
						IConversationController controller) => await controller.GetMessageAsync(conversationId, messageId))
	.WithOpenApi(endpoint =>
	{
		endpoint.Summary = "Get the concrete message from a conversation.";
		endpoint.Description = "The frontend calls this to get the reply to an user's message.";
		return endpoint;
	});

app.MapGet(
	"/conversations/{conversationId}/data-specification-substructure",
	async (int conversationId,
				IConversationController controller) => await controller.GetDataSpecificationSubstructureAsync(conversationId))
	.WithOpenApi(endpoint =>
	{
		endpoint.Summary = "Get the data specification substructure that is being built in the conversation.";
		endpoint.Description = "The frontend calls this endpoint to display all the mapped classes and properties to the user.";
		return endpoint;
	});

app.MapPost("/conversations/{conversationId}/messages",
				async ([FromRoute] int conversationId,
							[FromBody] PostConversationMessagesDTO payload,
							IConversationController controller) => await controller.ProcessIncomingUserMessage(conversationId, payload))
	.WithOpenApi(endpoint =>
	{
		endpoint.Summary = "Add a message to the conversation.";
		endpoint.Description = "Add a new user message to the conversation and generate a reply to that message.";
		return endpoint;
	});

app.MapPost("/conversations",
				async ([FromBody] PostConversationsDTO payload,
							IConversationController controller) => await controller.StartConversationAsync(payload))
	.WithOpenApi(endpoint =>
	{
		endpoint.Summary = "Start a new conversation.";
		endpoint.Description = "Starts a new conversation with the given title and using the given data specification in the payload.";
		return endpoint;
	});

app.MapPut("/conversations/{conversationId}/user-selected-items",
	async ([FromRoute] int conversationId, [FromBody] PutUserSelectedItemsDTO payload,
				IConversationController controller) => await controller.StoreUserSelectionAndGetSuggestedMessage(conversationId, payload))
	.WithOpenApi(endpoint =>
	{
		endpoint.Summary = "Save user selection and generate suggested message.";
		endpoint.Description = "Saves the suggested properties selected by the user and generate a suggested message based on that selection. Returns the generated suggested message.";
		return endpoint;
	});

app.MapDelete("/conversations/{conversationId}",
				async ([FromRoute] int conversationId,
							IConversationController controller) => await controller.DeleteConversationAsync(conversationId))
	.WithOpenApi(endpoint =>
	{
		endpoint.Summary = "Delete a conversation.";
		endpoint.Description = "Deletes the conversation, all messages and the associated data specification.";
		return endpoint;
	});

app.Run();
