using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SoruDeneme.Models
{
    public class Question : IValidatableObject
    {
        public int Id { get; set; }
        public string? Text { get; set; }
        public int QuestionNum { get; set; }

        public string? ChoiceA { get; set; }
        public string? ChoiceB { get; set; }
        public string? ChoiceC { get; set; }
        public string? ChoiceD { get; set; }
        public string? ChoiceE { get; set; }

        public string? CorrectOption { get; set; }
        public string? ImagePath { get; set; }

        public int QuizId { get; set; }
        public Quiz? Quiz { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            
            if (string.IsNullOrWhiteSpace(Text))
                yield return new ValidationResult("Soru metni boş olamaz.", new[] { nameof(Text) });

            
            if (string.IsNullOrWhiteSpace(CorrectOption))
            {
                yield return new ValidationResult("Doğru şık seçilmelidir.", new[] { nameof(CorrectOption) });
                yield break;
            }

            var opt = CorrectOption.Trim().ToUpperInvariant();
            var validOpts = new[] { "A", "B", "C", "D", "E" };

            if (Array.IndexOf(validOpts, opt) < 0)
            {
                yield return new ValidationResult("Doğru şık A/B/C/D/E olmalı.", new[] { nameof(CorrectOption) });
                yield break;
            }

            
            bool optionFilled = opt switch
            {
                "A" => !string.IsNullOrWhiteSpace(ChoiceA),
                "B" => !string.IsNullOrWhiteSpace(ChoiceB),
                "C" => !string.IsNullOrWhiteSpace(ChoiceC),
                "D" => !string.IsNullOrWhiteSpace(ChoiceD),
                "E" => !string.IsNullOrWhiteSpace(ChoiceE),
                _ => false
            };

            if (!optionFilled)
            {
                yield return new ValidationResult(
                    $"Doğru şık olarak seçilen '{opt}' seçeneği boş bırakılamaz. Önce o şıkkı doldurun veya başka şık seçin.",
                    new[] { nameof(CorrectOption) }
                );
            }
        }
    }
}
