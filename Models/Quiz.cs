using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace SoruDeneme.Models
{
    public class Quiz
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Sınav adı boş olamaz.")]
        [StringLength(40, ErrorMessage = "Sınav adı en fazla 40 karakter olabilir.")]
        public string QuizName { get; set; } = "";

        [StringLength(25, ErrorMessage = "Kategori en fazla 25 karakter olabilir.")]
        public string? Genre { get; set; }

        public int? TimeLimitMinutes { get; set; }

        public bool IsPublic { get; set; } = true;

        [StringLength(20, ErrorMessage = "Kod en fazla 20 karakter olabilir.")]
        public string? AccessCode { get; set; }

        public int? OwnerTeacherId { get; set; }

        [ValidateNever]
        public AppUser? OwnerTeacher { get; set; }

        [ValidateNever]
        public List<Question> Questions { get; set; } = new();
    }
}
