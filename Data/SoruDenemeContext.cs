using Microsoft.EntityFrameworkCore;
using SoruDeneme.Models;

namespace SoruDeneme.Data
{
    public class SoruDenemeContext : DbContext
    {
        public SoruDenemeContext(DbContextOptions<SoruDenemeContext> options)
            : base(options)
        { }

        public DbSet<Question> Question { get; set; } = default!;
        public DbSet<Quiz> Quiz { get; set; } = default!;
        public DbSet<AppUser> Users { get; set; } = default!;
        public DbSet<QuizAttempt> QuizAttempts { get; set; } = default!;
        public DbSet<AttemptAnswer> AttemptAnswers { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Question -> Quiz
            modelBuilder.Entity<Question>()
                .HasOne(q => q.Quiz)
                .WithMany(z => z.Questions)
                .HasForeignKey(q => q.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            // Quiz -> OwnerTeacher (opsiyonel)
            modelBuilder.Entity<Quiz>()
                .HasOne(q => q.OwnerTeacher)
                .WithMany()
                .HasForeignKey(q => q.OwnerTeacherId)
                .OnDelete(DeleteBehavior.NoAction);

            // QuizAttempt -> Quiz
            modelBuilder.Entity<QuizAttempt>()
                .HasOne(a => a.Quiz)
                .WithMany()
                .HasForeignKey(a => a.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            // QuizAttempt -> User
            modelBuilder.Entity<QuizAttempt>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // AttemptAnswer -> QuizAttempt
            modelBuilder.Entity<AttemptAnswer>()
                .HasOne(x => x.QuizAttempt)
                .WithMany(a => a.Answers)
                .HasForeignKey(x => x.QuizAttemptId)
                .OnDelete(DeleteBehavior.Cascade);

            // AttemptAnswer -> Question
            modelBuilder.Entity<AttemptAnswer>()
                .HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
