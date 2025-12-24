using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoruDeneme.Data;
using SoruDeneme.Filters;
using SoruDeneme.Models;
using SoruDeneme.Models.ViewModels;

namespace SoruDeneme.Controllers
{
    [RequireLogin]
    public class QuizsController : Controller
    {
        private readonly SoruDenemeContext _context;
        private readonly IWebHostEnvironment _env;

        private const string KEY_ACTIVE_ATTEMPT_ID = "ActiveAttemptId";
        private const string KEY_ACTIVE_QUIZID = "ActiveAttemptQuizId";
        private const string KEY_UNLOCKED_QUIZ_IDS = "UnlockedQuizIds";

        // Quiz.cs ile uyumlu limitler
        private const int QUIZNAME_MAX = 60;
        private const int GENRE_MAX = 40;
        private const int ACCESSCODE_MAX = 20;

        public QuizsController(SoruDenemeContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        //Index
        public async Task<IActionResult> Index()
        {
            var role = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetInt32("UserId");

            if (string.IsNullOrEmpty(role) || userId == null)
                return RedirectToAction("Index", "Login");

            if (role == "Egitmen")
            {
                var teacherId = userId.Value;

                var teacherQuizzes = await _context.Quiz
                    .Where(q => q.OwnerTeacherId == teacherId)
                    .OrderByDescending(q => q.Id)
                    .ToListAsync();

                return View(teacherQuizzes);
            }

            var unlockedIds = GetUnlockedQuizIds();

            var studentQuizzes = await _context.Quiz
                .Where(q => q.IsPublic || unlockedIds.Contains(q.Id))
                .OrderByDescending(q => q.Id)
                .ToListAsync();

            return View(studentQuizzes);
        }

        // Kod ile acma
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireRole("Ogrenci")]
        public async Task<IActionResult> UnlockByCode(string accessCode)
        {
            accessCode = (accessCode ?? "").Trim();

            if (string.IsNullOrWhiteSpace(accessCode))
            {
                TempData["UnlockError"] = "Kod boş olamaz.";
                return RedirectToAction(nameof(Index));
            }

            if (accessCode.Length > ACCESSCODE_MAX)
            {
                TempData["UnlockError"] = $"Kod en fazla {ACCESSCODE_MAX} karakter olabilir.";
                return RedirectToAction(nameof(Index));
            }

            var quiz = await _context.Quiz
                .FirstOrDefaultAsync(q => !q.IsPublic && q.AccessCode == accessCode);

            if (quiz == null)
            {
                TempData["UnlockError"] = "Kod hatalı veya böyle bir kodlu sınav yok.";
                return RedirectToAction(nameof(Index));
            }

            var unlocked = GetUnlockedQuizIds();
            unlocked.Add(quiz.Id);
            SaveUnlockedQuizIds(unlocked);

            TempData["UnlockSuccess"] = $"Kod doğru. \"{quiz.QuizName}\" sınavı açıldı.";
            return RedirectToAction(nameof(Index));
        }

        // Create (Get)
        [HttpGet]
        [RequireRole("Egitmen")]
        public async Task<IActionResult> Create(int? id)
        {
            var teacherId = HttpContext.Session.GetInt32("UserId");
            if (teacherId == null) return RedirectToAction("Index", "Login");

            if (id == null)
            {
                return View(new CreateQuizViewModel
                {
                    QuizCreated = false,
                    Quiz = new Quiz { IsPublic = true }
                });
            }

            var quiz = await _context.Quiz
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.Id == id.Value);

            if (quiz == null) return NotFound();
            if (quiz.OwnerTeacherId != teacherId.Value) return Forbid();

            return View(new CreateQuizViewModel
            {
                QuizCreated = true,
                Quiz = quiz,
                Questions = quiz.Questions.OrderBy(x => x.QuestionNum).ToList(),
                NewQuestion = new Question { QuizId = quiz.Id }
            });
        }

        //Create (post)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireRole("Egitmen")]
        public async Task<IActionResult> Create(CreateQuizViewModel model, string step, IFormFile? imageFile)
        {
            var teacherId = HttpContext.Session.GetInt32("UserId");
            if (teacherId == null) return RedirectToAction("Index", "Login");

            step = (step ?? "").Trim();

            // Create quiz
            if (step == "createQuiz")
            {
                model.Quiz.QuizName = (model.Quiz.QuizName ?? "").Trim();
                model.Quiz.Genre = string.IsNullOrWhiteSpace(model.Quiz.Genre) ? null : model.Quiz.Genre.Trim();
                model.Quiz.AccessCode = string.IsNullOrWhiteSpace(model.Quiz.AccessCode) ? null : model.Quiz.AccessCode.Trim();

                if (string.IsNullOrWhiteSpace(model.Quiz.QuizName))
                {
                    TempData["WizardError"] = "Sınav adı boş olamaz.";
                    model.QuizCreated = false;
                    return View(model);
                }

                if (model.Quiz.QuizName.Length > QUIZNAME_MAX)
                {
                    TempData["WizardError"] = $"Sınav adı en fazla {QUIZNAME_MAX} karakter olabilir.";
                    model.QuizCreated = false;
                    return View(model);
                }

                if (model.Quiz.Genre != null && model.Quiz.Genre.Length > GENRE_MAX)
                {
                    TempData["WizardError"] = $"Kategori en fazla {GENRE_MAX} karakter olabilir.";
                    model.QuizCreated = false;
                    return View(model);
                }

                if (model.Quiz.IsPublic)
                {
                    model.Quiz.AccessCode = null;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(model.Quiz.AccessCode))
                    {
                        TempData["WizardError"] = "Kodlu sınav için kod girmelisin.";
                        model.QuizCreated = false;
                        return View(model);
                    }

                    if (model.Quiz.AccessCode!.Length > ACCESSCODE_MAX)
                    {
                        TempData["WizardError"] = $"Sınav kodu en fazla {ACCESSCODE_MAX} karakter olabilir.";
                        model.QuizCreated = false;
                        return View(model);
                    }
                }

                model.Quiz.OwnerTeacherId = teacherId.Value;

                _context.Quiz.Add(model.Quiz);
                await _context.SaveChangesAsync();

                TempData["WizardSuccess"] = "Sınav oluşturuldu. Şimdi soruları ekleyin.";
                return RedirectToAction(nameof(Create), new { id = model.Quiz.Id });
            }

            if (step == "addQuestion")
            {
                var quizId = model.Quiz?.Id ?? 0;
                if (quizId <= 0)
                {
                    TempData["WizardError"] = "Sınav bulunamadı. Önce sınav oluşturmalısın.";
                    return RedirectToAction(nameof(Create));
                }

                var quiz = await _context.Quiz
                    .Include(q => q.Questions)
                    .FirstOrDefaultAsync(q => q.Id == quizId);

                if (quiz == null) return NotFound();
                if (quiz.OwnerTeacherId != teacherId.Value) return Forbid();

                var q = model.NewQuestion ?? new Question();
                q.QuizId = quiz.Id;

                async Task<IActionResult> ReturnWizardWithError(string msg)
                {
                    TempData["WizardError"] = msg;

                    var freshQuiz = await _context.Quiz
                        .Include(x => x.Questions)
                        .FirstAsync(x => x.Id == quiz.Id);

                    var vm = new CreateQuizViewModel
                    {
                        QuizCreated = true,
                        Quiz = freshQuiz,
                        Questions = freshQuiz.Questions.OrderBy(x => x.QuestionNum).ToList(),
                        NewQuestion = model.NewQuestion ?? new Question { QuizId = freshQuiz.Id }
                    };

                    return View(vm);
                }

                if (q.QuestionNum <= 0)
                    return await ReturnWizardWithError("Soru numarası 1 veya daha büyük olmalı.");

                var expected = quiz.Questions.Any()
                    ? quiz.Questions.Max(x => x.QuestionNum) + 1
                    : 1;

                if (q.QuestionNum != expected)
                    return await ReturnWizardWithError($"Soru numarası sırayla gitmeli. Sıradaki numara: {expected}");

                if (string.IsNullOrWhiteSpace(q.Text))
                    return await ReturnWizardWithError("Soru metni boş olamaz.");

                if (quiz.Questions.Any(x => x.QuestionNum == q.QuestionNum))
                    return await ReturnWizardWithError($"Bu sınavda {q.QuestionNum} numaralı soru zaten var.");

                q.CorrectOption = (q.CorrectOption ?? "").Trim().ToUpperInvariant();
                bool IsFilled(string? s) => !string.IsNullOrWhiteSpace(s);

                var allowed = new HashSet<string>();
                if (IsFilled(q.ChoiceA)) allowed.Add("A");
                if (IsFilled(q.ChoiceB)) allowed.Add("B");
                if (IsFilled(q.ChoiceC)) allowed.Add("C");
                if (IsFilled(q.ChoiceD)) allowed.Add("D");
                if (IsFilled(q.ChoiceE)) allowed.Add("E");

                if (allowed.Count < 2)
                    return await ReturnWizardWithError("En az 2 şık doldurmalısın.");

                if (!allowed.Contains(q.CorrectOption))
                    return await ReturnWizardWithError("Doğru şık, doldurduğun şıklardan biri olmalı.");

                // resim upload
                if (imageFile != null && imageFile.Length > 0)
                {
                    var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                    var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };

                    if (!allowedExt.Contains(ext))
                        return await ReturnWizardWithError("Sadece JPG/PNG/WEBP yükleyebilirsin.");

                    var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsDir))
                        Directory.CreateDirectory(uploadsDir);

                    var fileName = $"{Guid.NewGuid():N}{ext}";
                    var filePath = Path.Combine(uploadsDir, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                        await imageFile.CopyToAsync(stream);

                    q.ImagePath = "/uploads/" + fileName;
                }

                _context.Question.Add(q);
                await _context.SaveChangesAsync();

                TempData["WizardSuccess"] = "Soru eklendi.";
                return RedirectToAction(nameof(Create), new { id = quiz.Id });
            }

            TempData["WizardError"] = "Geçersiz işlem (step).";
            return RedirectToAction(nameof(Create));
        }

        // Detaylar
        [HttpGet]
        [RequireRole("Egitmen")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var teacherId = HttpContext.Session.GetInt32("UserId");
            if (teacherId == null) return RedirectToAction("Index", "Login");

            var quiz = await _context.Quiz.FirstOrDefaultAsync(q => q.Id == id.Value);
            if (quiz == null) return NotFound();
            if (quiz.OwnerTeacherId != teacherId.Value) return Forbid();

            return View(quiz);
        }

        // Editleme
        [HttpGet]
        [RequireRole("Egitmen")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var teacherId = HttpContext.Session.GetInt32("UserId");
            if (teacherId == null) return RedirectToAction("Index", "Login");

            var quiz = await _context.Quiz.FirstOrDefaultAsync(q => q.Id == id.Value);
            if (quiz == null) return NotFound();
            if (quiz.OwnerTeacherId != teacherId.Value) return Forbid();

            return View(quiz);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireRole("Egitmen")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,QuizName,Genre,TimeLimitMinutes,IsPublic,AccessCode")] Quiz incoming)
        {
            if (id != incoming.Id) return NotFound();

            var teacherId = HttpContext.Session.GetInt32("UserId");
            if (teacherId == null) return RedirectToAction("Index", "Login");

            var quizDb = await _context.Quiz.FirstOrDefaultAsync(q => q.Id == id);
            if (quizDb == null) return NotFound();
            if (quizDb.OwnerTeacherId != teacherId.Value) return Forbid();

            incoming.QuizName = (incoming.QuizName ?? "").Trim();
            incoming.Genre = string.IsNullOrWhiteSpace(incoming.Genre) ? null : incoming.Genre.Trim();
            incoming.AccessCode = string.IsNullOrWhiteSpace(incoming.AccessCode) ? null : incoming.AccessCode.Trim();

            if (string.IsNullOrWhiteSpace(incoming.QuizName))
            {
                ModelState.AddModelError(nameof(incoming.QuizName), "Sınav adı boş olamaz.");
                return View(incoming);
            }

            if (incoming.QuizName.Length > QUIZNAME_MAX)
            {
                ModelState.AddModelError(nameof(incoming.QuizName), $"Sınav adı en fazla {QUIZNAME_MAX} karakter olabilir.");
                return View(incoming);
            }

            if (incoming.Genre != null && incoming.Genre.Length > GENRE_MAX)
            {
                ModelState.AddModelError(nameof(incoming.Genre), $"Kategori en fazla {GENRE_MAX} karakter olabilir.");
                return View(incoming);
            }

            if (incoming.IsPublic)
            {
                incoming.AccessCode = null;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(incoming.AccessCode))
                {
                    ModelState.AddModelError(nameof(incoming.AccessCode), "Kodlu sınav için kod girmelisin.");
                    return View(incoming);
                }

                if (incoming.AccessCode!.Length > ACCESSCODE_MAX)
                {
                    ModelState.AddModelError(nameof(incoming.AccessCode), $"Sınav kodu en fazla {ACCESSCODE_MAX} karakter olabilir.");
                    return View(incoming);
                }
            }

            quizDb.QuizName = incoming.QuizName;
            quizDb.Genre = incoming.Genre;
            quizDb.TimeLimitMinutes = incoming.TimeLimitMinutes;
            quizDb.IsPublic = incoming.IsPublic;
            quizDb.AccessCode = incoming.AccessCode;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // Delete(Get ve Post)
        [HttpGet]
        [RequireRole("Egitmen")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var teacherId = HttpContext.Session.GetInt32("UserId");
            if (teacherId == null) return RedirectToAction("Index", "Login");

            var quiz = await _context.Quiz.FirstOrDefaultAsync(q => q.Id == id.Value);
            if (quiz == null) return NotFound();
            if (quiz.OwnerTeacherId != teacherId.Value) return Forbid();

            return View(quiz);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RequireRole("Egitmen")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var teacherId = HttpContext.Session.GetInt32("UserId");
            if (teacherId == null) return RedirectToAction("Index", "Login");

            var quiz = await _context.Quiz.FirstOrDefaultAsync(q => q.Id == id);
            if (quiz == null) return RedirectToAction(nameof(Index));
            if (quiz.OwnerTeacherId != teacherId.Value) return Forbid();

            var questions = await _context.Question.Where(q => q.QuizId == id).ToListAsync();
            foreach (var q in questions)
                TryDeleteUpload(q.ImagePath);

            _context.Question.RemoveRange(questions);

            var attemptIds = await _context.QuizAttempts.Where(a => a.QuizId == id).Select(a => a.Id).ToListAsync();
            if (attemptIds.Count > 0)
            {
                var answers = await _context.AttemptAnswers.Where(x => attemptIds.Contains(x.QuizAttemptId)).ToListAsync();
                _context.AttemptAnswers.RemoveRange(answers);

                var attempts = await _context.QuizAttempts.Where(a => a.QuizId == id).ToListAsync();
                _context.QuizAttempts.RemoveRange(attempts);
            }

            _context.Quiz.Remove(quiz);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ScoreBoard
        [HttpGet]
        [RequireRole("Egitmen")]
        public async Task<IActionResult> Scoreboard(int quizId)
        {
            var teacherId = HttpContext.Session.GetInt32("UserId");
            if (teacherId == null) return RedirectToAction("Index", "Login");

            var quiz = await _context.Quiz.FirstOrDefaultAsync(q => q.Id == quizId);
            if (quiz == null) return NotFound();
            if (quiz.OwnerTeacherId != teacherId.Value) return Forbid();

            var attempts = await _context.QuizAttempts
                .Include(a => a.User)
                .Include(a => a.Answers)
                .Where(a => a.QuizId == quizId && a.FinishedAt != null)
                .OrderByDescending(a => a.CorrectCount)
                .ThenBy(a => a.FinishedAt)
                .ToListAsync();

            ViewBag.Quiz = quiz;
            return View(attempts);
        }

        //Solve sonuc (get)
        [HttpGet]
        [RequireRole("Ogrenci")]
        public async Task<IActionResult> Result(int quizId, int attemptId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Index", "Login");

            var attempt = await _context.QuizAttempts
                .FirstOrDefaultAsync(a => a.Id == attemptId && a.QuizId == quizId && a.UserId == userId.Value);

            if (attempt == null) return RedirectToAction(nameof(Index));

            var quizInfo = await _context.Quiz.FirstOrDefaultAsync(q => q.Id == quizId);
            return await BuildFinishedSolveViewModel(quizId, attemptId, quizInfo?.TimeLimitMinutes);
        }

        // Solve(Get)
        [HttpGet]
        [RequireRole("Ogrenci")]
        public async Task<IActionResult> Solve(int quizId, int order = 0)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Index", "Login");

            var quizInfo = await _context.Quiz.FirstOrDefaultAsync(q => q.Id == quizId);
            if (quizInfo == null) return NotFound();

            var totalQuestions = await _context.Question.CountAsync(q => q.QuizId == quizId);
            if (totalQuestions <= 0)
            {
                TempData["UnlockError"] = "Bu sınav henüz hazırlanmadı (soru eklenmemiş).";
                return RedirectToAction(nameof(Index));
            }

            if (!quizInfo.IsPublic)
            {
                var unlocked = GetUnlockedQuizIds();
                if (!unlocked.Contains(quizId))
                {
                    TempData["UnlockError"] = "Bu sınav kodlu. Çözmek için önce kod girmelisin.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var activeAttemptId = HttpContext.Session.GetInt32(KEY_ACTIVE_ATTEMPT_ID);
            var activeQuizId = HttpContext.Session.GetInt32(KEY_ACTIVE_QUIZID);

            QuizAttempt? attemptDb = null;

            if (activeAttemptId == null || activeQuizId == null || activeQuizId != quizId)
            {
                attemptDb = await _context.QuizAttempts
                    .FirstOrDefaultAsync(a => a.QuizId == quizId && a.UserId == userId.Value && a.FinishedAt == null);

                if (attemptDb != null)
                {
                    HttpContext.Session.SetInt32(KEY_ACTIVE_ATTEMPT_ID, attemptDb.Id);
                    HttpContext.Session.SetInt32(KEY_ACTIVE_QUIZID, quizId);
                    activeAttemptId = attemptDb.Id;
                }
            }

            if (activeAttemptId == null || activeQuizId == null || activeQuizId != quizId)
            {
                var total = await _context.Question.CountAsync(q => q.QuizId == quizId);

                var attempt = new QuizAttempt
                {
                    QuizId = quizId,
                    UserId = userId.Value,
                    StartedAt = DateTime.UtcNow,
                    FinishedAt = null,
                    TotalQuestions = total,
                    CorrectCount = 0
                };

                _context.QuizAttempts.Add(attempt);
                await _context.SaveChangesAsync();

                HttpContext.Session.SetInt32(KEY_ACTIVE_ATTEMPT_ID, attempt.Id);
                HttpContext.Session.SetInt32(KEY_ACTIVE_QUIZID, quizId);

                activeAttemptId = attempt.Id;
            }

            attemptDb = await _context.QuizAttempts
                .Include(a => a.Answers)
                .FirstOrDefaultAsync(a => a.Id == activeAttemptId.Value);

            if (attemptDb == null) return RedirectToAction(nameof(Index));

            int remainingSeconds = 0;
            if (quizInfo.TimeLimitMinutes.HasValue && quizInfo.TimeLimitMinutes.Value > 0)
            {
                var limitSeconds = quizInfo.TimeLimitMinutes.Value * 60;
                var elapsed = (int)Math.Floor((DateTime.UtcNow - attemptDb.StartedAt).TotalSeconds);
                remainingSeconds = Math.Max(0, limitSeconds - elapsed);

                if (remainingSeconds <= 0)
                {
                    await RecalculateAndFinishAttemptAsync(attemptDb.Id);
                    HttpContext.Session.Remove(KEY_ACTIVE_ATTEMPT_ID);
                    HttpContext.Session.Remove(KEY_ACTIVE_QUIZID);

                    return RedirectToAction(nameof(Result), new { quizId, attemptId = attemptDb.Id });
                }
            }

            var question = await _context.Question
                .Where(q => q.QuizId == quizId)
                .OrderBy(q => q.QuestionNum)
                .Skip(order)
                .FirstOrDefaultAsync();

            if (question == null)
            {
                await RecalculateAndFinishAttemptAsync(attemptDb.Id);
                HttpContext.Session.Remove(KEY_ACTIVE_ATTEMPT_ID);
                HttpContext.Session.Remove(KEY_ACTIVE_QUIZID);
                return RedirectToAction(nameof(Result), new { quizId, attemptId = attemptDb.Id });
            }

            var existing = attemptDb.Answers.FirstOrDefault(x => x.QuestionId == question.Id);

            var model = new SolveQuizViewModel
            {
                QuizId = quizId,
                Order = order,
                CurrentQuestion = question,
                IsFinished = false,
                TotalQuestions = attemptDb.TotalQuestions,
                CorrectCount = attemptDb.CorrectCount,
                SelectedOption = existing?.SelectedOption,
                TimeLimitMinutes = quizInfo.TimeLimitMinutes,
                RemainingSeconds = remainingSeconds,
                ReviewMode = false
            };

            return View(model);
        }

        // Solve (POst)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireRole("Ogrenci")]
        public async Task<IActionResult> Solve(int quizId, int order, string selected)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Index", "Login");

            var quizInfo = await _context.Quiz.FirstOrDefaultAsync(q => q.Id == quizId);
            if (quizInfo == null) return NotFound();

            if (!quizInfo.IsPublic)
            {
                var unlocked = GetUnlockedQuizIds();
                if (!unlocked.Contains(quizId))
                {
                    TempData["UnlockError"] = "Bu sınav kodlu. Çözmek için önce kod girmelisin.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var attemptId = HttpContext.Session.GetInt32(KEY_ACTIVE_ATTEMPT_ID);
            var activeQuizId = HttpContext.Session.GetInt32(KEY_ACTIVE_QUIZID);

            if (attemptId == null || activeQuizId == null || activeQuizId != quizId)
                return RedirectToAction(nameof(Solve), new { quizId = quizId, order = 0 });

            var attemptDb = await _context.QuizAttempts.FirstOrDefaultAsync(a => a.Id == attemptId.Value);
            if (attemptDb == null) return RedirectToAction(nameof(Index));

            if (quizInfo.TimeLimitMinutes.HasValue && quizInfo.TimeLimitMinutes.Value > 0)
            {
                var limitSeconds = quizInfo.TimeLimitMinutes.Value * 60;
                var elapsed = (int)Math.Floor((DateTime.UtcNow - attemptDb.StartedAt).TotalSeconds);

                if (elapsed >= limitSeconds)
                {
                    await RecalculateAndFinishAttemptAsync(attemptDb.Id);
                    HttpContext.Session.Remove(KEY_ACTIVE_ATTEMPT_ID);
                    HttpContext.Session.Remove(KEY_ACTIVE_QUIZID);

                    return RedirectToAction(nameof(Result), new { quizId, attemptId = attemptDb.Id });
                }
            }

            var question = await _context.Question
                .Where(q => q.QuizId == quizId)
                .OrderBy(q => q.QuestionNum)
                .Skip(order)
                .FirstOrDefaultAsync();

            if (question == null)
                return RedirectToAction(nameof(Solve), new { quizId = quizId, order = order });

            selected = (selected ?? "").Trim().ToUpperInvariant();

            var valid = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A", "B", "C", "D", "E" };
            if (!valid.Contains(selected))
            {
                TempData["SolveError"] = "Geçersiz seçim.";
                return RedirectToAction(nameof(Solve), new { quizId = quizId, order = order });
            }

            bool IsFilled(string? s) => !string.IsNullOrWhiteSpace(s);
            var allowed = new HashSet<string>();
            if (IsFilled(question.ChoiceA)) allowed.Add("A");
            if (IsFilled(question.ChoiceB)) allowed.Add("B");
            if (IsFilled(question.ChoiceC)) allowed.Add("C");
            if (IsFilled(question.ChoiceD)) allowed.Add("D");
            if (IsFilled(question.ChoiceE)) allowed.Add("E");

            if (!allowed.Contains(selected))
            {
                TempData["SolveError"] = "Bu soru için seçtiğin şık boş. Lütfen geçerli bir şık seç.";
                return RedirectToAction(nameof(Solve), new { quizId = quizId, order = order });
            }

            var existing = await _context.AttemptAnswers
                .FirstOrDefaultAsync(a => a.QuizAttemptId == attemptId.Value && a.QuestionId == question.Id);

            if (existing == null)
            {
                _context.AttemptAnswers.Add(new AttemptAnswer
                {
                    QuizAttemptId = attemptId.Value,
                    QuestionId = question.Id,
                    SelectedOption = selected
                });
            }
            else
            {
                existing.SelectedOption = selected;
                _context.AttemptAnswers.Update(existing);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Solve), new { quizId = quizId, order = order + 1 });
        }

        //Helpers
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireRole("Egitmen")]
        public async Task<IActionResult> DeleteQuestion(int id, int questionId)
        {
            var teacherId = HttpContext.Session.GetInt32("UserId");
            if (teacherId == null) return RedirectToAction("Index", "Login");

            var quiz = await _context.Quiz.FirstOrDefaultAsync(q => q.Id == id);
            if (quiz == null) return NotFound();
            if (quiz.OwnerTeacherId != teacherId.Value) return Forbid();

            var q = await _context.Question.FirstOrDefaultAsync(x => x.Id == questionId && x.QuizId == id);
            if (q == null) return RedirectToAction(nameof(Create), new { id });

            if (!string.IsNullOrWhiteSpace(q.ImagePath))
            {
                TryDeleteUpload(q.ImagePath);
            }

            _context.Question.Remove(q);
            await _context.SaveChangesAsync();

            TempData["WizardSuccess"] = "Soru silindi.";
            return RedirectToAction(nameof(Create), new { id });
        }

        private async Task<IActionResult> BuildFinishedSolveViewModel(int quizId, int attemptId, int? timeLimitMinutes)
        {
            var attempt = await _context.QuizAttempts
                .Include(a => a.Answers)
                .FirstAsync(a => a.Id == attemptId);

            var questions = await _context.Question
                .Where(q => q.QuizId == quizId)
                .OrderBy(q => q.QuestionNum)
                .ToListAsync();

            var answered = attempt.Answers.Select(x => x.QuestionId).Distinct().Count();
            var blank = Math.Max(0, questions.Count - answered);

            var model = new SolveQuizViewModel
            {
                QuizId = quizId,
                Order = 0,
                CurrentQuestion = null,
                IsFinished = true,
                TotalQuestions = attempt.TotalQuestions,
                CorrectCount = attempt.CorrectCount,
                BlankCount = blank,
                SelectedOption = null,
                TimeLimitMinutes = timeLimitMinutes,
                RemainingSeconds = 0,
                ReviewMode = true
            };

            foreach (var q in questions)
            {
                var ans = attempt.Answers.FirstOrDefault(a => a.QuestionId == q.Id);

                model.ReviewItems.Add(new SolveQuizViewModel.ReviewItem
                {
                    QuestionNum = q.QuestionNum,
                    Text = q.Text,
                    ImagePath = q.ImagePath,
                    ChoiceA = q.ChoiceA,
                    ChoiceB = q.ChoiceB,
                    ChoiceC = q.ChoiceC,
                    ChoiceD = q.ChoiceD,
                    ChoiceE = q.ChoiceE,
                    CorrectOption = q.CorrectOption,
                    SelectedOption = ans?.SelectedOption
                });
            }

            return View("Solve", model);
        }

        private async Task RecalculateAndFinishAttemptAsync(int attemptId)
        {
            var attempt = await _context.QuizAttempts
                .Include(a => a.Answers)
                .FirstAsync(a => a.Id == attemptId);

            var questions = await _context.Question
                .Where(q => q.QuizId == attempt.QuizId)
                .ToListAsync();

            int correct = 0;
            foreach (var q in questions)
            {
                var ans = attempt.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
                if (ans != null && ans.SelectedOption == q.CorrectOption)
                    correct++;
            }

            attempt.CorrectCount = correct;
            attempt.FinishedAt = DateTime.UtcNow;

            _context.QuizAttempts.Update(attempt);
            await _context.SaveChangesAsync();
        }

        private HashSet<int> GetUnlockedQuizIds()
        {
            var raw = HttpContext.Session.GetString(KEY_UNLOCKED_QUIZ_IDS);
            if (string.IsNullOrWhiteSpace(raw)) return new HashSet<int>();

            var set = new HashSet<int>();
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part, out var id))
                    set.Add(id);
            }
            return set;
        }

        private void SaveUnlockedQuizIds(HashSet<int> ids)
        {
            var raw = string.Join(",", ids.OrderBy(x => x));
            HttpContext.Session.SetString(KEY_UNLOCKED_QUIZ_IDS, raw);
        }

        private void TryDeleteUpload(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return;
            if (!imagePath.StartsWith("/uploads/")) return;

            try
            {
                var rel = imagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var physical = Path.Combine(_env.WebRootPath, rel);
                if (System.IO.File.Exists(physical))
                    System.IO.File.Delete(physical);
            }
            catch { }
        }
    }
}
