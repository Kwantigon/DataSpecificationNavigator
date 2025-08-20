using DataSpecificationNavigatorBackend.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DataSpecificationNavigatorBackend.ConnectorsLayer;

public class AppDbContext : DbContext
{
	public AppDbContext(DbContextOptions options) : base(options) { }

	public DbSet<DataSpecification> DataSpecifications { get; set; }

	public DbSet<DataSpecificationItem> DataSpecificationItems { get; set; }
	public DbSet<ClassItem> ClassItems { get; set; }
	public DbSet<ObjectPropertyItem> ObjectPropertyItems { get; set; }
	public DbSet<DatatypePropertyItem> DatatypePropertyItems { get; set; }

	public DbSet<Conversation> Conversations { get; set; }

	public DbSet<Message> Messages { get; set; }
	public DbSet<UserMessage> UserMessages { get; set; }
	// I won't query WelcomeMessages directly,
	// but it doesn't hurt to have them in the DbContext.
	public DbSet<WelcomeMessage> WelcomeMessages { get; set; }
	public DbSet<ReplyMessage> ReplyMessages { get; set; }

	public DbSet<DataSpecificationItemMapping> ItemMappings { get; set; }

	public DbSet<DataSpecificationPropertySuggestion> PropertySuggestions { get; set; }

	public DbSet<UserSelection> UserSelections { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		#region DataSpecificationItem
		modelBuilder.Entity<DataSpecificationItem>()
			.HasOne(item => item.DataSpecification)
			.WithMany()
			.HasForeignKey(item => item.DataSpecificationId)
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<ObjectPropertyItem>()
			.HasOne(property => property.Domain)
			.WithMany()
			.HasForeignKey(property => new { property.DataSpecificationId, property.DomainIri })
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<ObjectPropertyItem>()
			.HasOne(property => property.Range)
			.WithMany()
			.HasForeignKey(property => new { property.DataSpecificationId, property.RangeIri })
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<DatatypePropertyItem>()
			.HasOne(prop => prop.Domain)
			.WithMany()
			.HasForeignKey(prop => new { prop.DataSpecificationId, prop.DomainIri })
			.OnDelete(DeleteBehavior.Cascade);
		#endregion DataSpecificationItem

		#region Message
		modelBuilder.Entity<UserMessage>()
			.HasOne(message => message.ReplyMessage)
			.WithOne()
			.HasForeignKey<UserMessage>(message => message.ReplyMessageId)
			.OnDelete(DeleteBehavior.SetNull);

		modelBuilder.Entity<ReplyMessage>()
			.HasOne(message => message.PrecedingUserMessage)
			.WithOne()
			.HasForeignKey<ReplyMessage>(message => message.PrecedingUserMessageId)
			.OnDelete(DeleteBehavior.Cascade);
		#endregion Message

		#region DataSpecificationItemMapping
		modelBuilder.Entity<DataSpecificationItemMapping>()
			.HasOne(mapping => mapping.Item)
			.WithMany()
			.HasForeignKey(mapping => new { mapping.ItemDataSpecificationId, mapping.ItemIri })
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<DataSpecificationItemMapping>()
			.HasOne(mapping => mapping.UserMessage)
			.WithMany()
			.HasForeignKey(mapping => mapping.UserMessageId)
			.OnDelete(DeleteBehavior.Cascade);
		#endregion DataSpecificationItemMapping

		#region DataSpecificationPropertySuggestion
		modelBuilder.Entity<DataSpecificationPropertySuggestion>()
			.HasOne(suggestion => suggestion.SuggestedProperty)
			.WithMany()
			.HasForeignKey(suggestion => new { suggestion.PropertyDataSpecificationId, suggestion.SuggestedPropertyIri })
			.OnDelete(DeleteBehavior.Cascade);

		modelBuilder.Entity<DataSpecificationPropertySuggestion>()
			.HasOne(suggestion => suggestion.UserMessage)
			.WithMany()
			.HasForeignKey(suggestion => suggestion.UserMessageId)
			.OnDelete(DeleteBehavior.Cascade);
		#endregion DataSpecificationPropertySuggestion

		#region Conversation
		// Configure the conversation's data specification substructure to be stored as a JSON column.
		JsonSerializerOptions serializerOptions = new()
		{
			Encoder = JavaScriptEncoder.Create(UnicodeRanges.All), // To properly store non-ASCII characters.
			WriteIndented = true // For better readability in the database.
		};
		modelBuilder.Entity<Conversation>()
			.Property(c => c.DataSpecificationSubstructure)
			.HasConversion(
				substructure => JsonSerializer.Serialize(substructure, serializerOptions),
				substructureString => JsonSerializer.Deserialize<DataSpecificationSubstructure>(substructureString, serializerOptions)!)
				// Not handling the possible null value because I want to throw an exception if the deserialization fails.
			.HasColumnType("json");
		#endregion Conversation

		#region UserSelection
		modelBuilder.Entity<UserSelection>()
			.HasOne(selection => selection.Conversation)
			.WithMany(conversation => conversation.UserSelections)
			.HasForeignKey(selection => selection.ConversationId)
			.OnDelete(DeleteBehavior.Cascade);
		#endregion UserSelection
	}
}
