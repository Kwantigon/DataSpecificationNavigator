using DataSpecificationNavigatorBackend.BusinessCoreLayer;
using DataSpecificationNavigatorBackend.BusinessCoreLayer.Abstraction;
using DataSpecificationNavigatorBackend.ConnectorsLayer;
using DataSpecificationNavigatorBackend.ConnectorsLayer.Abstraction;
using DataSpecificationNavigatorBackend.ConnectorsLayer.JsonDataClasses;
using DataSpecificationNavigatorBackend.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataSpecificationNavigator.Tests.BusinessCoreLayer;

public class ConversationServiceTests
{
	private AppDbContext CreateInMemoryDb()
	{
		var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
				.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
				.Options;
		return new AppDbContext(dbOptions);
	}

	/// <summary>
	/// Data specification has 2 items, 1 object property
	/// and 1 datatype property.<br/>
	/// Result:<br/>
	/// The created conversation must have exactly 1 message,
	/// which is the welcome message.<br/>
	/// Welcome message must say that there are 2 items and 3 properties
	/// in the data specification.<br/>
	/// The substructure, suggested message and user selections must be empty.
	/// </summary>
	[Fact]
	public async Task StartNewConversationTestAsync()
	{
		#region Arrange
		// Create a logger instance.
		var logger = LoggerFactory.Create(builder =>
		{
			builder.AddConsole();
			builder.SetMinimumLevel(LogLevel.Trace);
		}).CreateLogger<ConversationService>();

		var database = CreateInMemoryDb();

		var llmConnectorMock = new Mock<ILlmConnector>();
		llmConnectorMock
			.Setup(connector => connector.GetDataSpecificationSummaryAndClassSuggestionsAsync(It.IsAny<DataSpecification>()))
			.ReturnsAsync(new WelcomeMessageDataSpecificationSummaryJson("Mock summary", [new("Class item one", "Mock reason string")]));

		var sparqlTranslationServiceMock = new Mock<ISparqlTranslationService>();

		var dataSpecification = new DataSpecification()
		{
			Id = 1,
			DataspecerPackageUuid = "mock-uuid",
			Name = "Mock data specification",
			OwlContent = "Mock OWL value"
		};
		var classItem1 = new ClassItem()
		{
			Iri = "http://mock.com/class-items#one",
			Label = "Class item one",
			Type = ItemType.Class,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DataSpecificationId = 1,
			DataSpecification = dataSpecification
		};
		var classItem2 = new ClassItem()
		{
			Iri = "http://mock.com/class-items#two",
			Label = "Class item two",
			Type = ItemType.Class,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DataSpecificationId = 1,
			DataSpecification = dataSpecification
		};
		var objProperty = new ObjectPropertyItem()
		{
			Iri = "http://mock.com/object-properties#one",
			Label = "Object property one",
			Type = ItemType.ObjectProperty,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DomainIri = classItem1.Iri,
			Domain = classItem1,
			RangeIri = classItem2.Iri,
			Range = classItem2,
			DataSpecificationId = 1,
			DataSpecification = dataSpecification
		};
		var dtProperty1 = new DatatypePropertyItem()
		{
			Iri = "http://mock.com/datatype-properties#one",
			Label = "Datatype property one",
			Type = ItemType.ObjectProperty,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DomainIri = classItem2.Iri,
			Domain = classItem2,
			RangeDatatypeIri = "http://mock.com/simple-types#Literal",
			DataSpecificationId = 1,
			DataSpecification = dataSpecification
		};
		var dtProperty2 = new DatatypePropertyItem()
		{
			Iri = "http://mock.com/datatype-properties#two",
			Label = "Datatype property two",
			Type = ItemType.ObjectProperty,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DomainIri = classItem2.Iri,
			Domain = classItem2,
			RangeDatatypeIri = "http://mock.com/simple-types#Literal",
			DataSpecificationId = 1,
			DataSpecification = dataSpecification
		};

		await database.DataSpecifications.AddAsync(dataSpecification);
		await database.DataSpecificationItems.AddRangeAsync([classItem1, classItem2, objProperty, dtProperty1, dtProperty2]);
		await database.SaveChangesAsync();

		#endregion Arrange

		#region Act
		var conversationService = new ConversationService(
			logger,
			database,
			llmConnectorMock.Object,
			sparqlTranslationServiceMock.Object);
		Conversation conversation = await conversationService.StartNewConversationAsync("Test conversation", dataSpecification);
		#endregion Act

		#region Assert
		Assert.Equal("Test conversation", conversation.Title);
		Assert.True(DateTime.Now >= conversation.LastUpdated);
		Assert.Single(conversation.Messages);
		Assert.Empty(conversation.DataSpecificationSubstructure.ClassItems);
		Assert.Empty(conversation.UserSelections);
		Assert.True(string.IsNullOrWhiteSpace(conversation.SuggestedMessage));

		Message msg = conversation.Messages.First();
		Assert.IsType<WelcomeMessage>(msg);
		WelcomeMessage welcome = (WelcomeMessage)msg;
		Assert.Equal(
			"Your data specification has been loaded.\nIt contains 2 classes and 3 properties.",
			welcome.TextContent);       // The important parts are the numbers 2 and 3 here.
																	// There are 2 mock classes and 3 mock properties in the data specification.
		Assert.False(string.IsNullOrWhiteSpace(welcome.DataSpecificationSummary));
		Assert.NotEmpty(welcome.SuggestedClasses);
		#endregion Assert
	}

	/// <summary>
	/// Conversation's suggested message is null.<br/>
	/// The LLM maps message to 1 property in the data specification.<br/>
	/// Result:<br/>
	/// The range and domain of the mapped property must also be manually mapped
	/// (so 3 mapped items in total).<br/>
	/// The manually mapped items must have empty MappedWords property.<br/>
	/// All 3 mapped items must be in the conversation's substructure.
	/// </summary>
	[Fact]
	public async Task AddUserMessageTestAsync_NotSuggestedMessage()
	{
		#region Arrange
		// Create a logger instance.
		var logger = LoggerFactory.Create(builder =>
		{
			builder.AddConsole();
			builder.SetMinimumLevel(LogLevel.Trace);
		}).CreateLogger<ConversationService>();

		var database = CreateInMemoryDb();

		var llmConnectorMock = new Mock<ILlmConnector>();

		var dataSpecification = new DataSpecification()
		{
			Id = 1,
			DataspecerPackageUuid = "mock-uuid",
			Name = "Mock data specification",
			OwlContent = "Mock OWL value"
		};
		var classItem1 = new ClassItem()
		{
			Iri = "http://mock.com/class-items#one",
			Label = "Class item one",
			Type = ItemType.Class,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DataSpecificationId = 1,
			DataSpecification = dataSpecification,
			Summary = "Mock item 1 summary"
		};
		var classItem2 = new ClassItem()
		{
			Iri = "http://mock.com/class-items#two",
			Label = "Class item two",
			Type = ItemType.Class,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DataSpecificationId = 1,
			DataSpecification = dataSpecification,
			Summary = "Mock item 2 summary"
		};
		var objProperty = new ObjectPropertyItem()
		{
			Iri = "http://mock.com/object-properties#one",
			Label = "Object property one",
			Type = ItemType.ObjectProperty,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DomainIri = classItem1.Iri,
			Domain = classItem1,
			RangeIri = classItem2.Iri,
			Range = classItem2,
			DataSpecificationId = 1,
			DataSpecification = dataSpecification,
			Summary = "Mock object property summary"
		};

		var conversation = new Conversation()
		{
			DataSpecification = dataSpecification,
			Title = "Mock conversation",
		};
		conversation.AddMessage(new WelcomeMessage()
		{
			Conversation = conversation,
			TextContent = "Mock welcome message",
			Timestamp = DateTime.Now
		});

		await database.DataSpecifications.AddAsync(dataSpecification);
		await database.Conversations.AddAsync(conversation);
		await database.DataSpecificationItems.AddRangeAsync([classItem1, classItem2, objProperty]);
		await database.SaveChangesAsync();

		llmConnectorMock
			.Setup(connector => connector.MapUserMessageToDataSpecificationAsync(It.IsAny<DataSpecification>(), It.IsAny<UserMessage>()))
			.ReturnsAsync((DataSpecification ds, UserMessage usrMsg) => [
				new DataSpecificationItemMapping()
				{
					ItemDataSpecificationId = ds.Id,
					ItemIri = objProperty.Iri,
					Item = objProperty,
					UserMessageId = usrMsg.Id,
					UserMessage = usrMsg,
					MappedWords = "Mock mapped words"
				}
			]);
		llmConnectorMock
			.Setup(connector => connector.GenerateItemSummariesAsync(It.IsAny<DataSpecification>(), It.IsAny<List<DataSpecificationItem>>()))
			.Returns(Task.CompletedTask);
		llmConnectorMock
			.Setup(connector => connector.GetSuggestedPropertiesAsync(
				It.IsAny<DataSpecification>(),
				It.IsAny<DataSpecificationSubstructure>(),
				It.IsAny<UserMessage>()))
			.ReturnsAsync([]);

		var sparqlTranslationServiceMock = new Mock<ISparqlTranslationService>();
		#endregion Arrange

		#region Act
		var conversationService = new ConversationService(
			logger,
			database,
			llmConnectorMock.Object,
			sparqlTranslationServiceMock.Object);
		UserMessage userMessage = await conversationService.AddUserMessageAsync(conversation, "Mock message", DateTime.Now);
		#endregion Act

		#region Assert
		Assert.Equal("Mock message", userMessage.TextContent);
		Assert.Equal(2, conversation.Messages.Count); // Only mock welcome message and user message.
		Assert.Equal(userMessage, conversation.Messages[1]); // userMessage should be at the end of the list.

		// Mock llmConnector returned only 1 object property mapping.
		// Check that both the domain and range have also been mapped.
		List<DataSpecificationItemMapping> mappings = database.ItemMappings
			.Where(m => m.UserMessageId == userMessage.Id)
			.ToList();
		Assert.Equal(3, mappings.Count);
		Assert.Contains(mappings, m =>
											m.ItemIri == objProperty.Iri
											&& m.UserMessageId == userMessage.Id);

		// Manually mapped domain must have MappedWords == string.empty
		DataSpecificationItemMapping? classMapping1 =
			mappings.Find(m =>
											m.ItemIri == classItem1.Iri
											&& m.UserMessageId == userMessage.Id);
		Assert.NotNull(classMapping1);
		Assert.Equal(string.Empty, classMapping1.MappedWords);

		// Manually mapped range must have MappedWords == string.empty
		DataSpecificationItemMapping? classMapping2 =
			mappings.Find(m =>
											m.ItemIri == classItem2.Iri
											&& m.UserMessageId == userMessage.Id);
		Assert.NotNull(classMapping2);
		Assert.Equal(string.Empty, classMapping2.MappedWords);

		// Check that all mapped items are in the substructure.
		var classItemsInSubstructure = conversation.DataSpecificationSubstructure.ClassItems;
		Assert.Equal(2, classItemsInSubstructure.Count);
		var substructureClassItem1 = classItemsInSubstructure.Find(c => c.Iri == classItem1.Iri);
		Assert.NotNull(substructureClassItem1);
		var substructureObjProperty = substructureClassItem1.ObjectProperties
			.Find(property => property.Iri == objProperty.Iri);
		Assert.NotNull(substructureObjProperty);
		Assert.Equal(classItem2.Iri, substructureObjProperty.Range);
		Assert.Contains(classItemsInSubstructure, c => c.Iri == classItem2.Iri);

		// The suggested message and user selections must be cleared.
		Assert.Empty(conversation.UserSelections);
		Assert.True(string.IsNullOrWhiteSpace(conversation.SuggestedMessage));
		#endregion Assert
	}

	/// <summary>
	/// User sends the suggested message.<br/>
	/// The conversation has 1 class item in the substructure.<br/>
	/// User selected 1 object property and 1 datatype property.<br/>
	/// LLM returns mapping to substructure
	/// only for datatype property.<br/>
	/// 
	/// Result:<br/>
	/// Substructure must contain original class,
	/// object property and its range class
	/// and the datatype property.
	/// So 4 items in total (2 classes and 2 properties).
	/// </summary>
	[Fact]
	public async Task AddUserMessageTestAsync_SuggestedMessage()
	{
		#region Arrange
		// Create a logger instance.
		var logger = LoggerFactory.Create(builder =>
		{
			builder.AddConsole();
			builder.SetMinimumLevel(LogLevel.Trace);
		}).CreateLogger<ConversationService>();

		var database = CreateInMemoryDb();

		var llmConnectorMock = new Mock<ILlmConnector>();

		var dataSpecification = new DataSpecification()
		{
			Id = 1,
			DataspecerPackageUuid = "mock-uuid",
			Name = "Mock data specification",
			OwlContent = "Mock OWL value"
		};
		var classItem1 = new ClassItem()
		{
			Iri = "http://mock.com/class-items#one",
			Label = "Class item one",
			Type = ItemType.Class,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DataSpecificationId = 1,
			DataSpecification = dataSpecification,
			Summary = "Mock item 1 summary"
		};
		var classItem2 = new ClassItem()
		{
			Iri = "http://mock.com/class-items#two",
			Label = "Class item two",
			Type = ItemType.Class,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DataSpecificationId = 1,
			DataSpecification = dataSpecification,
			Summary = "Mock item 2 summary"
		};
		var objProperty = new ObjectPropertyItem()
		{
			Iri = "http://mock.com/object-properties#one",
			Label = "Object property one",
			Type = ItemType.ObjectProperty,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DomainIri = classItem1.Iri,
			Domain = classItem1,
			RangeIri = classItem2.Iri,
			Range = classItem2,
			DataSpecificationId = 1,
			DataSpecification = dataSpecification,
			Summary = "Mock object property summary"
		};
		var datatypeProperty = new DatatypePropertyItem()
		{
			Iri = "http://mock.com/datatype-properties#one",
			Label = "Datatype property one",
			Type = ItemType.ObjectProperty,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DomainIri = classItem1.Iri,
			Domain = classItem1,
			RangeDatatypeIri = "http://mock.com/type#Literal",
			DataSpecificationId = 1,
			DataSpecification = dataSpecification,
			Summary = "Mock datatype property summary"
		};

		const string mockSuggestedMessage = "MockSuggestedMessageValue";
		var conversation = new Conversation()
		{
			DataSpecification = dataSpecification,
			Title = "Mock conversation",
			SuggestedMessage = mockSuggestedMessage
		};
		conversation.DataSpecificationSubstructure.ClassItems.Add(new()
		{
			Iri = classItem1.Iri,
			Label = classItem1.Label,
		});
		conversation.UserSelections = [
			new UserSelection() {
				Conversation = conversation,
				ConversationId = conversation.Id,
				SelectedPropertyIri = objProperty.Iri,
				IsOptional = true
			},
			new UserSelection() {
				Conversation = conversation,
				ConversationId = conversation.Id,
				SelectedPropertyIri = datatypeProperty.Iri,
				FilterExpression = "{?var} > 100",
				IsOptional = false
			},
		];
		conversation.AddMessage(new WelcomeMessage()
		{
			Conversation = conversation,
			TextContent = "Mock welcome message",
			Timestamp = DateTime.Now
		});

		await database.DataSpecifications.AddAsync(dataSpecification);
		await database.Conversations.AddAsync(conversation);
		await database.DataSpecificationItems.AddRangeAsync([classItem1, classItem2, objProperty, datatypeProperty]);
		await database.SaveChangesAsync();

		llmConnectorMock
			.Setup(connector => connector.MapUserMessageToSubstructureAsync(
				It.IsAny<DataSpecification>(),
				It.IsAny<DataSpecificationSubstructure>(),
				It.IsAny<UserMessage>()))
			.ReturnsAsync((
					DataSpecification ds,
					DataSpecificationSubstructure substructure,
					UserMessage usrMsg) => [
				new DataSpecificationItemMapping()
				{
					ItemDataSpecificationId = ds.Id,
					ItemIri = datatypeProperty.Iri,
					Item = datatypeProperty,
					UserMessageId = usrMsg.Id,
					UserMessage = usrMsg,
					MappedWords = "Mock mapped words"
				}
			]);
		llmConnectorMock
			.Setup(connector => connector.GenerateItemSummariesAsync(It.IsAny<DataSpecification>(), It.IsAny<List<DataSpecificationItem>>()))
			.Returns(Task.CompletedTask);
		llmConnectorMock
			.Setup(connector => connector.GetSuggestedPropertiesAsync(
				It.IsAny<DataSpecification>(),
				It.IsAny<DataSpecificationSubstructure>(),
				It.IsAny<UserMessage>()))
			.ReturnsAsync([]);

		var sparqlTranslationServiceMock = new Mock<ISparqlTranslationService>();
		#endregion Arrange

		#region Act
		var conversationService = new ConversationService(
			logger,
			database,
			llmConnectorMock.Object,
			sparqlTranslationServiceMock.Object);
		UserMessage userMessage = await conversationService.AddUserMessageAsync(
			conversation, mockSuggestedMessage, DateTime.Now); // user sends the suggested message.
		#endregion Act

		#region Assert
		Assert.Equal(mockSuggestedMessage, userMessage.TextContent);
		Assert.Equal(2, conversation.Messages.Count); // Only mock welcome message and user message.
		Assert.Equal(userMessage, conversation.Messages[1]); // userMessage should be at the end of the list.

		// There should be 4 mapped items.
		List<DataSpecificationItemMapping> mappings = database.ItemMappings
			.Where(m => m.UserMessageId == userMessage.Id)
			.ToList();
		Assert.Equal(4, mappings.Count);

		// Manually mapped object property must have MappedWords == string.empty
		var objPropertyMapping = mappings.Find(m =>
											m.ItemIri == objProperty.Iri
											&& m.UserMessageId == userMessage.Id);
		Assert.NotNull(objPropertyMapping);
		Assert.Equal(string.Empty, objPropertyMapping.MappedWords);

		// Manually mapped domain must have MappedWords == string.empty
		DataSpecificationItemMapping? classMapping1 =
			mappings.Find(m =>
											m.ItemIri == classItem1.Iri
											&& m.UserMessageId == userMessage.Id);
		Assert.NotNull(classMapping1);
		Assert.Equal(string.Empty, classMapping1.MappedWords);

		// Manually mapped range must have MappedWords == string.empty
		DataSpecificationItemMapping? classMapping2 =
			mappings.Find(m =>
											m.ItemIri == classItem2.Iri
											&& m.UserMessageId == userMessage.Id);
		Assert.NotNull(classMapping2);
		Assert.Equal(string.Empty, classMapping2.MappedWords);

		// LLM mapped the datatype property so MappedWords must exist.
		var datatypePropertyMapping = mappings.Find(m =>
											m.ItemIri == datatypeProperty.Iri
											&& m.UserMessageId == userMessage.Id);
		Assert.NotNull(datatypePropertyMapping);
		Assert.Equal("Mock mapped words", datatypePropertyMapping.MappedWords);

		// Check that all mapped items are in the substructure.
		var classItemsInSubstructure = conversation.DataSpecificationSubstructure.ClassItems;
		Assert.Equal(2, classItemsInSubstructure.Count);

		// classItem1
		var substructureClassItem1 = classItemsInSubstructure.Find(c => c.Iri == classItem1.Iri);
		Assert.NotNull(substructureClassItem1);

		// objProperty
		var substructureObjProperty = substructureClassItem1.ObjectProperties
			.Find(property => property.Iri == objProperty.Iri);
		Assert.NotNull(substructureObjProperty);
		Assert.Equal(classItem2.Iri, substructureObjProperty.Range);
		// datatypeProperty
		Assert.Contains(
			substructureClassItem1.DatatypeProperties,
			p => p.Iri == datatypeProperty.Iri);

		// classItem2
		Assert.Contains(classItemsInSubstructure, c => c.Iri == classItem2.Iri);

		// Should have called the LLM to get suggestions.
		llmConnectorMock.Verify(
			connector => connector.GetSuggestedPropertiesAsync(
															It.IsAny<DataSpecification>(),
															It.IsAny<DataSpecificationSubstructure>(),
															It.IsAny<UserMessage>()),
			Times.Once);

		// The suggested message and user selections must be cleared.
		Assert.Empty(conversation.UserSelections);
		Assert.True(string.IsNullOrWhiteSpace(conversation.SuggestedMessage));
		#endregion Assert
	}

	/// <summary>
	/// There is no item mapping in the database for the user message.<br/>
	/// Result:<br/>
	/// The reply message does not contain a SPARQL query.
	/// In fact, the method for SPARQL query generation must not be called.
	/// </summary>
	[Fact]
	public async Task GenerateReplyMessageTestAsync_NoMappedItemsFound()
	{
		#region Arrange
		// Create a logger instance.
		var logger = LoggerFactory.Create(builder =>
		{
			builder.AddConsole();
			builder.SetMinimumLevel(LogLevel.Trace);
		}).CreateLogger<ConversationService>();

		var database = CreateInMemoryDb();

		var llmConnectorMock = new Mock<ILlmConnector>();

		var sparqlTranslationServiceMock = new Mock<ISparqlTranslationService>();

		var dataSpecification = new DataSpecification()
		{
			Id = 1,
			DataspecerPackageUuid = "mock-uuid",
			Name = "Mock data specification",
			OwlContent = "Mock OWL value"
		};
		var conversation = new Conversation()
		{
			DataSpecification = dataSpecification,
			Title = "Mock conversation",
		};
		conversation.AddMessage(new WelcomeMessage()
		{
			Conversation = conversation,
			TextContent = "Mock welcome message",
			Timestamp = DateTime.Now
		});
		var userMessage = new UserMessage()
		{
			Conversation = conversation,
			TextContent = "Mock user message",
			Timestamp = DateTime.Now
		};
		conversation.AddMessage(userMessage);

		await database.DataSpecifications.AddAsync(dataSpecification);
		await database.Conversations.AddAsync(conversation);
		await database.SaveChangesAsync();
		#endregion Arrange

		#region Act
		var conversationService = new ConversationService(
			logger,
			database,
			llmConnectorMock.Object,
			sparqlTranslationServiceMock.Object);
		ReplyMessage? replyMessage = await conversationService.GenerateReplyMessageAsync(userMessage);
		#endregion Act

		#region Assert
		Assert.NotNull(replyMessage);
		Assert.Equal(userMessage.Id, replyMessage.PrecedingUserMessageId);

		// No mapping means nothing to translate to sparql.
		sparqlTranslationServiceMock.Verify(
			mock => mock.TranslateSubstructure(It.IsAny<DataSpecificationSubstructure>()),
			Times.Never);
		Assert.Equal(string.Empty, replyMessage.SparqlQuery);
		#endregion Assert
	}

	/// <summary>
	/// Some data specification items have been mapped.<br/>
	/// Result:<br/>
	/// The SPARQL generation method was called and the reply message
	/// contains the generated query.
	/// </summary>
	[Fact]
	public async Task GenerateReplyMessageTestAsync_MappedItemsExist()
	{
		#region Arrange
		// Create a logger instance.
		var logger = LoggerFactory.Create(builder =>
		{
			builder.AddConsole();
			builder.SetMinimumLevel(LogLevel.Trace);
		}).CreateLogger<ConversationService>();

		var database = CreateInMemoryDb();

		var llmConnectorMock = new Mock<ILlmConnector>();

		var sparqlTranslationServiceMock = new Mock<ISparqlTranslationService>();
		sparqlTranslationServiceMock
			.Setup(mock => mock.TranslateSubstructure(It.IsAny<DataSpecificationSubstructure>()))
			.Returns("Mock sparql query");

		var dataSpecification = new DataSpecification()
		{
			Id = 1,
			DataspecerPackageUuid = "mock-uuid",
			Name = "Mock data specification",
			OwlContent = "Mock OWL value"
		};
		var conversation = new Conversation()
		{
			DataSpecification = dataSpecification,
			Title = "Mock conversation",
		};
		conversation.AddMessage(new WelcomeMessage()
		{
			Conversation = conversation,
			TextContent = "Mock welcome message",
			Timestamp = DateTime.Now
		});
		var userMessage = new UserMessage()
		{
			Conversation = conversation,
			TextContent = "Mock user message",
			Timestamp = DateTime.Now
		};
		conversation.AddMessage(userMessage);

		var mockItem = new ClassItem()
		{
			DataSpecification = dataSpecification,
			DataSpecificationId = dataSpecification.Id,
			Iri = "mock.item.iri",
			Label = "Mock class item",
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			Type = ItemType.Class
		};
		var mapping = new DataSpecificationItemMapping()
		{
			ItemDataSpecificationId = dataSpecification.Id,
			Item = mockItem, // Have to create an item.
			ItemIri = mockItem.Iri,
			UserMessage = userMessage,
			UserMessageId = userMessage.Id,
			MappedWords = "Mock mapped words"
		};

		await database.DataSpecifications.AddAsync(dataSpecification);
		await database.ClassItems.AddAsync(mockItem);
		await database.ItemMappings.AddAsync(mapping);
		await database.Conversations.AddAsync(conversation);
		await database.SaveChangesAsync();
		#endregion Arrange

		#region Act
		var conversationService = new ConversationService(
			logger,
			database,
			llmConnectorMock.Object,
			sparqlTranslationServiceMock.Object);
		ReplyMessage? replyMessage = await conversationService.GenerateReplyMessageAsync(userMessage);
		#endregion Act

		#region Assert
		Assert.NotNull(replyMessage);
		Assert.Equal(userMessage.Id, replyMessage.PrecedingUserMessageId);

		sparqlTranslationServiceMock.Verify(
			mock => mock.TranslateSubstructure(It.IsAny<DataSpecificationSubstructure>()),
			Times.Once);
		Assert.False(string.IsNullOrWhiteSpace(replyMessage.SparqlQuery));
		#endregion Assert
	}

	/// <summary>
	/// Conversation already contains 4 user selected items.<br/>
	/// User changes selection to 2 items.<br/>
	/// Result:<br/>
	/// The conversation now contains only the 2 items selected.
	/// </summary>
	[Fact]
	public async Task UpdateSelectedPropertiesAndGenerateSuggestedMessageTestAsync_AlreadyContainsSelectedItems()
	{
		#region Arrange
		// Create a logger instance.
		var logger = LoggerFactory.Create(builder =>
		{
			builder.AddConsole();
			builder.SetMinimumLevel(LogLevel.Trace);
		}).CreateLogger<ConversationService>();

		var database = CreateInMemoryDb();

		var llmConnectorMock = new Mock<ILlmConnector>();
		llmConnectorMock
			.Setup(llm => llm.GenerateSuggestedMessageAsync(
								It.IsAny<DataSpecification>(),
								It.IsAny<UserMessage>(),
								It.IsAny<DataSpecificationSubstructure>(),
								It.IsAny<List<DataSpecificationItem>>()))
			.ReturnsAsync("Mock suggested message");

		var sparqlTranslationServiceMock = new Mock<ISparqlTranslationService>();

		var dataSpecification = new DataSpecification()
		{
			Id = 1,
			DataspecerPackageUuid = "mock-uuid",
			Name = "Mock data specification",
			OwlContent = "Mock OWL value"
		};
		var conversation = new Conversation()
		{
			DataSpecification = dataSpecification,
			Title = "Mock conversation"
		};
		conversation.AddMessage(new WelcomeMessage()
		{
			Conversation = conversation,
			TextContent = "Mock welcome message",
			Timestamp = DateTime.Now
		});
		conversation.AddMessage(new UserMessage()
		{
			Conversation = conversation,
			TextContent = "Mock welcome message",
			Timestamp = DateTime.Now
		});
		conversation.UserSelections = [
			new UserSelection() {
				Conversation = conversation,
				ConversationId = conversation.Id,
				SelectedPropertyIri = "mock iri",
			},
			new UserSelection() {
				Conversation = conversation,
				ConversationId = conversation.Id,
				SelectedPropertyIri = "mock iri",
			},
			new UserSelection() {
				Conversation = conversation,
				ConversationId = conversation.Id,
				SelectedPropertyIri = "mock iri",
			},
			new UserSelection() {
				Conversation = conversation,
				ConversationId = conversation.Id,
				SelectedPropertyIri = "mock iri",
			}
		];

		var classItem = new ClassItem()
		{
			Iri = "http://mock.com/class-items#mock",
			Label = "Class item mock",
			Type = ItemType.Class,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DataSpecificationId = 1,
			DataSpecification = dataSpecification
		};
		var dtProperty1 = new DatatypePropertyItem()
		{
			Iri = "http://mock.com/datatype-properties#one",
			Label = "Datatype property one",
			Type = ItemType.ObjectProperty,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DomainIri = classItem.Iri,
			Domain = classItem,
			RangeDatatypeIri = "http://mock.com/simple-types#Literal",
			DataSpecificationId = 1,
			DataSpecification = dataSpecification
		};
		var dtProperty2 = new DatatypePropertyItem()
		{
			Iri = "http://mock.com/datatype-properties#two",
			Label = "Datatype property two",
			Type = ItemType.ObjectProperty,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DomainIri = classItem.Iri,
			Domain = classItem,
			RangeDatatypeIri = "http://mock.com/simple-types#Literal",
			DataSpecificationId = 1,
			DataSpecification = dataSpecification
		};

		await database.DataSpecifications.AddAsync(dataSpecification);
		await database.Conversations.AddAsync(conversation);
		await database.DataSpecificationItems.AddRangeAsync([classItem, dtProperty1, dtProperty2]);

		HashSet<string> selectedIris = [dtProperty1.Iri, dtProperty2.Iri];
		List<UserSelection> userSelections = [
			new UserSelection() {
				Conversation = conversation,
				ConversationId = conversation.Id,
				SelectedPropertyIri = dtProperty1.Iri,
				FilterExpression = "{?var} = 0"
			},
			new UserSelection() {
				Conversation = conversation,
				ConversationId = conversation.Id,
				SelectedPropertyIri = dtProperty2.Iri,
				IsOptional = true
			}
		];
		#endregion Arrange

		#region Act
		var conversationService = new ConversationService(
			logger,
			database,
			llmConnectorMock.Object,
			sparqlTranslationServiceMock.Object);
		string ?suggestedMessage = await conversationService.UpdateSelectedPropertiesAndGenerateSuggestedMessageAsync(
			conversation,
			selectedIris,
			userSelections);
		#endregion Act

		#region Assert
		Assert.NotNull(suggestedMessage);
		Assert.Equal("Mock suggested message", suggestedMessage);
		Assert.Equal(2, conversation.UserSelections.Count);

		UserSelection? selection1 = conversation.UserSelections
			.Find(s => s.SelectedPropertyIri == dtProperty1.Iri);
		Assert.NotNull(selection1);
		Assert.False(selection1.IsOptional);
		Assert.Equal("{?var} = 0", selection1.FilterExpression);

		UserSelection? selection2 = conversation.UserSelections
			.Find(s => s.SelectedPropertyIri == dtProperty2.Iri);
		Assert.NotNull(selection2);
		Assert.True(selection2.IsOptional);
		Assert.True(string.IsNullOrWhiteSpace(selection2.FilterExpression));
		#endregion
	}

	/// <summary>
	/// Conversation does not yet have any selected items.<br/>
	/// User selects 2 items.<br/>
	/// Result:<br/>
	/// The conversation now contains the 2 items selected.
	/// </summary>
	[Fact]
	public async Task UpdateSelectedPropertiesAndGenerateSuggestedMessageTestAsync_NoSelectedItemsYet()
	{
		#region Arrange
		// Create a logger instance.
		var logger = LoggerFactory.Create(builder =>
		{
			builder.AddConsole();
			builder.SetMinimumLevel(LogLevel.Trace);
		}).CreateLogger<ConversationService>();

		var database = CreateInMemoryDb();

		var llmConnectorMock = new Mock<ILlmConnector>();
		llmConnectorMock
			.Setup(llm => llm.GenerateSuggestedMessageAsync(
								It.IsAny<DataSpecification>(),
								It.IsAny<UserMessage>(),
								It.IsAny<DataSpecificationSubstructure>(),
								It.IsAny<List<DataSpecificationItem>>()))
			.ReturnsAsync("Mock suggested message");

		var sparqlTranslationServiceMock = new Mock<ISparqlTranslationService>();

		var dataSpecification = new DataSpecification()
		{
			Id = 1,
			DataspecerPackageUuid = "mock-uuid",
			Name = "Mock data specification",
			OwlContent = "Mock OWL value"
		};
		var conversation = new Conversation()
		{
			DataSpecification = dataSpecification,
			Title = "Mock conversation"
		};
		conversation.AddMessage(new WelcomeMessage()
		{
			Conversation = conversation,
			TextContent = "Mock welcome message",
			Timestamp = DateTime.Now
		});
		conversation.AddMessage(new UserMessage()
		{
			Conversation = conversation,
			TextContent = "Mock welcome message",
			Timestamp = DateTime.Now
		});

		var classItem = new ClassItem()
		{
			Iri = "http://mock.com/class-items#mock",
			Label = "Class item mock",
			Type = ItemType.Class,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DataSpecificationId = 1,
			DataSpecification = dataSpecification
		};
		var dtProperty1 = new DatatypePropertyItem()
		{
			Iri = "http://mock.com/datatype-properties#one",
			Label = "Datatype property one",
			Type = ItemType.ObjectProperty,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DomainIri = classItem.Iri,
			Domain = classItem,
			RangeDatatypeIri = "http://mock.com/simple-types#Literal",
			DataSpecificationId = 1,
			DataSpecification = dataSpecification
		};
		var dtProperty2 = new DatatypePropertyItem()
		{
			Iri = "http://mock.com/datatype-properties#two",
			Label = "Datatype property two",
			Type = ItemType.ObjectProperty,
			OwlAnnotation = string.Empty,
			RdfsComment = string.Empty,
			DomainIri = classItem.Iri,
			Domain = classItem,
			RangeDatatypeIri = "http://mock.com/simple-types#Literal",
			DataSpecificationId = 1,
			DataSpecification = dataSpecification
		};

		await database.DataSpecifications.AddAsync(dataSpecification);
		await database.Conversations.AddAsync(conversation);
		await database.DataSpecificationItems.AddRangeAsync([classItem, dtProperty1, dtProperty2]);

		HashSet<string> selectedIris = [dtProperty1.Iri, dtProperty2.Iri];
		List<UserSelection> userSelections = [
			new UserSelection() {
				Conversation = conversation,
				ConversationId = conversation.Id,
				SelectedPropertyIri = dtProperty1.Iri,
				FilterExpression = "{?var} = 0"
			},
			new UserSelection() {
				Conversation = conversation,
				ConversationId = conversation.Id,
				SelectedPropertyIri = dtProperty2.Iri,
				IsOptional = true
			}
		];
		#endregion Arrange

		#region Act
		var conversationService = new ConversationService(
			logger,
			database,
			llmConnectorMock.Object,
			sparqlTranslationServiceMock.Object);
		string? suggestedMessage = await conversationService.UpdateSelectedPropertiesAndGenerateSuggestedMessageAsync(
			conversation,
			selectedIris,
			userSelections);
		#endregion Act

		#region Assert
		Assert.NotNull(suggestedMessage);
		Assert.Equal("Mock suggested message", suggestedMessage);
		Assert.Equal(2, conversation.UserSelections.Count);

		UserSelection? selection1 = conversation.UserSelections
			.Find(s => s.SelectedPropertyIri == dtProperty1.Iri);
		Assert.NotNull(selection1);
		Assert.False(selection1.IsOptional);
		Assert.Equal("{?var} = 0", selection1.FilterExpression);

		UserSelection? selection2 = conversation.UserSelections
			.Find(s => s.SelectedPropertyIri == dtProperty2.Iri);
		Assert.NotNull(selection2);
		Assert.True(selection2.IsOptional);
		Assert.True(string.IsNullOrWhiteSpace(selection2.FilterExpression));
		#endregion
	}
}
