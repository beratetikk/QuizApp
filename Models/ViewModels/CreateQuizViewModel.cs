using System.Collections.Generic;
using SoruDeneme.Models;

namespace SoruDeneme.Models.ViewModels
{
    public class CreateQuizViewModel
    {
        public Quiz Quiz { get; set; } = new Quiz();

        public bool QuizCreated { get; set; }

        public List<Question> Questions { get; set; } = new List<Question>();

        public Question NewQuestion { get; set; } = new Question();
    }
}
