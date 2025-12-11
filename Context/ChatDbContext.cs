using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RealtimeChat.Models;
using System.Reflection.Emit;

namespace RealtimeChat.Context
{
    public class ChatDbContext : IdentityDbContext<AppUser>
    {
        public ChatDbContext(DbContextOptions<ChatDbContext> options): base(options)
        {
            
        }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<SeenUpdate>().HasNoKey();

            builder.Entity<Messages>()
            .HasOne(m => m.ReplyToMessage)
            .WithMany()
            .HasForeignKey(m => m.ReplyToMessageId)
            .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ChatGroup>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.GroupName).IsRequired().HasMaxLength(100);
                entity.HasMany(e => e.Members)
                    .WithOne(m => m.Group)
                    .HasForeignKey(m => m.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.Messages)
                    .WithOne(m => m.Group)
                    .HasForeignKey(m => m.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<GroupMember>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.GroupId, e.UserId }).IsUnique();
            });

            builder.Entity<GroupMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Message).IsRequired();
            });

            base.OnModelCreating(builder);
        }

        public DbSet<Messages> Messages { get; set; }
        public DbSet<Group_chats> GroupChats { get; set; }
        public DbSet<FriendRequest> FriendRequests { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<ChatGroup> ChatGroups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<GroupMessage> GroupMessages { get; set; }

    }

    
}
