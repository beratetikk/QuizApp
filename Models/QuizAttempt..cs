using System;
using System.Collections.Generic;

namespace SoruDeneme.Models
{
    public class QuizAttempt
    {
        public int Id { get; set; }

        public int QuizId { get; set; }
        public Quiz? Quiz { get; set; }

        public int UserId { get; set; }
        public AppUser? User { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectCount { get; set; }

        public List<AttemptAnswer> Answers { get; set; } = new List<AttemptAnswer>();
    }
}
