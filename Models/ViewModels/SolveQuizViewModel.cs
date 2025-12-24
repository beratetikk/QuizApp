using System.Collections.Generic;
using SoruDeneme.Models;

public class SolveQuizViewModel
{
    public int QuizId { get; set; }
    public int Order { get; set; }

    public Question? CurrentQuestion { get; set; }
    public bool IsFinished { get; set; }

    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }

    public int BlankCount { get; set; }
    public bool ReviewMode { get; set; } = false;

    public int? TimeLimitMinutes { get; set; }
    public int RemainingSeconds { get; set; }
    public string? SelectedOption { get; set; }

    public List<ReviewItem> ReviewItems { get; set; } = new();

    public class ReviewItem
    {
        public int QuestionNum { get; set; }
        public string? Text { get; set; }
        public string? ImagePath { get; set; }

        public string? ChoiceA { get; set; }
        public string? ChoiceB { get; set; }
        public string? ChoiceC { get; set; }
        public string? ChoiceD { get; set; }
        public string? ChoiceE { get; set; }

        public string? CorrectOption { get; set; }
        public string? SelectedOption { get; set; }
    }
}
