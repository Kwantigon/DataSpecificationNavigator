using DataSpecificationNavigatorBackend.BusinessCoreLayer;
using DataSpecificationNavigatorBackend.BusinessCoreLayer.Abstraction;
using DataSpecificationNavigatorBackend.BusinessCoreLayer.DTO;
using DataSpecificationNavigatorBackend.BusinessCoreLayer.Facade;
using DataSpecificationNavigatorBackend.BusinessCoreLayer.SparqlTranslation;
using DataSpecificationNavigatorBackend.ConnectorsLayer;
using DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;
using DataSpecificationNavigatorBackend.ConnectorsLayer.LlmConnectors;
using DataSpecificationNavigatorBackend.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
	//.AddScoped<ILlmConnector, GeminiConnector>()
	.AddScoped<ILlmConnector, OllamaConnector>()
	.AddScoped<IRdfProcessor, RdfProcessor>()
	.AddScoped<ILlmResponseProcessor, ResponseProcessor>()
	.AddScoped<ISparqlTranslationService, SparqlTranslationService>()
	.AddScoped<IPromptConstructor, PromptConstructor>()
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
/*builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new OpenApiInfo {
		Title = "Data specification helper API",
		Description = "The back end for the data specification helper project.",
		Version = "v1" }
	);
});*/

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
/*if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI(c =>
	{
		c.SwaggerEndpoint("/swagger/v1/swagger.json", "Data specification helper API");
	});
}*/

// Sanity check.
app.MapGet("/", () => "Hello there!");

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

/* This endpoint is currently not used for anything.
	app.MapPost("/data-specifications",
				async ([FromBody] PostDataSpecificationsDTO payload,
							IDataSpecificationController controller) => await controller.ProcessDataspecerPackage(payload))
	.WithOpenApi(endpoint =>
	{
		endpoint.Summary = "Add a new data specification.";
		endpoint.Description = "Exports and processes the necessary data from the Dataspecer package given in the payload's IRI.";
		return endpoint;
	});*/

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


// Test endpoints
app.MapGet("/tests/new-conversation", async (AppDbContext database) =>
{
	DataSpecification ds = new()
	{
		DataspecerPackageUuid = "test-package",
		Name = "Test package",
		OwlContent = File.ReadAllText("C:\\Users\\nquoc\\MatFyz\\research-project\\repo\\research-project\\backend\\DataSpecificationNavigatorBackend\\local\\data-specification.owl.ttl")
	};

	Conversation c = new()
	{
		DataSpecification = ds,
		Title = "Conversation title",
		DataSpecificationSubstructure = new(),
		LastUpdated = DateTime.Now
	};

	await database.Conversations.AddAsync(c);
	await database.SaveChangesAsync();

	return Results.Created("", $"{{ DataSpecificationId = {ds.Id}, ConversationId = {c.Id} }}");
});

app.MapGet("/tests/mapping-prompt", async (ILlmConnector llmConnector) =>
{
	DataSpecification ds = new()
	{
		DataspecerPackageUuid = "test-package",
		Name = "Test package",
		OwlContent = File.ReadAllText("C:\\Users\\nquoc\\MatFyz\\research-project\\repo\\research-project\\backend\\DataSpecificationNavigatorBackend\\local\\data-specification.owl.ttl")
	};

	Conversation c = new()
	{
		DataSpecification = ds,
		Title = "Conversation title",
		DataSpecificationSubstructure = new(),
		LastUpdated = DateTime.Now
	};

	UserMessage userMessage = new()
	{
		Conversation = c,
		TextContent = "I want to see public services providing electronic signatures.",
	};

	List<DataSpecificationItemMapping> mappings = await llmConnector.MapUserMessageToDataSpecificationAsync(ds, userMessage);
	return Results.Ok();
});

app.MapGet("/tests/add-user-msg", async (AppDbContext database, IConversationService service) =>
{
	Conversation? conversation = await database.Conversations.SingleOrDefaultAsync(c => c.Id == 1);
	if (conversation is null)
	{
		return Results.NotFound();
	}

	UserMessage userMessage = await service.AddUserMessageAsync(conversation, "I want public services that provide electronic signatures.", DateTime.Now);

	await database.SaveChangesAsync();
	return Results.Ok();
});

app.Run();
