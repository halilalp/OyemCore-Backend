using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OyemCore.BusinessLayer.Dtos;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Helpers;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    // referans: WebServiceGames (oyna + liderlik alt kümesi) — .NET 7 / EF Core portu.
    // Günlük limit: 1 oyun. Feedback: G=doğru yer, Y=var-yanlış yer, X=yok. Skor = 11 - deneme.
    public class GameService : IGameService
    {
        private readonly IYbsDbContext _context;
        private static readonly CultureInfo TrCulture = new CultureInfo("tr-TR");
        private const int DailyLimit = 1;
        private const int MaxAttempts = 10;

        public GameService(IYbsDbContext context)
        {
            _context = context;
        }

        private tb_Kullanici GetUser(int kullaniciID)
            => _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);

        private string DepartmanAdi(tb_Kullanici u)
        {
            if (u == null) return "Genel";
            var d = _context.tb_Departman.FirstOrDefault(x => x.Kod == u.DepartmanKod);
            return d != null ? d.DepartmanAdi : (u.DepartmanKod ?? "Genel");
        }

        // Tahmin geri bildirimi: her pozisyon için G/Y/X. referans: CalculateFeedbackString (birebir).
        private string CalculateFeedbackString(string guess, string target)
        {
            int len = target.Length;
            char[] feedback = new char[len];
            bool[] targetUsed = new bool[len];
            bool[] guessUsed = new bool[len];

            for (int i = 0; i < len; i++)
            {
                if (i < guess.Length && guess[i] == target[i])
                {
                    feedback[i] = 'G';
                    targetUsed[i] = true;
                    guessUsed[i] = true;
                }
            }
            for (int i = 0; i < len; i++)
            {
                if (guessUsed[i]) continue;
                feedback[i] = 'X';
                for (int j = 0; j < len; j++)
                {
                    if (!targetUsed[j] && i < guess.Length && guess[i] == target[j])
                    {
                        feedback[i] = 'Y';
                        targetUsed[j] = true;
                        break;
                    }
                }
            }
            return new string(feedback);
        }

        // Havuzdan (oynanmamış) rastgele hedef kelime ata. referans: AssignRandomWordToGame.
        private void AssignRandomWordToGame(tb_GameScore game, List<tb_GameWord> todayWords, string currentSicilNo)
        {
            int currentWordIdx = Math.Min(game.PlayNumber - 1, todayWords.Count - 1);
            var templateWord = todayWords[currentWordIdx];
            int L = templateWord.WordLength ?? 5;

            var playedWords = _context.tb_GameScore
                .Where(s => s.SicilNo == currentSicilNo && s.TargetWord != null && s.TargetWord != "")
                .Select(s => s.TargetWord)
                .ToList()
                .Select(w => (ClsEncryption.Decrypt(w) ?? "").Trim().ToLower(TrCulture))
                .Distinct().ToList();

            List<tb_GameWordPool> DecryptPool(List<tb_GameWordPool> raw) => raw
                .Select(w => new tb_GameWordPool
                {
                    Id = w.Id,
                    WordLength = w.WordLength,
                    IsActive = w.IsActive,
                    Word = (ClsEncryption.Decrypt(w.Word) ?? "").Trim(),
                    Meaning = ClsEncryption.Decrypt(w.Meaning) ?? ""
                })
                .Where(w => !playedWords.Contains(w.Word.ToLower(TrCulture)))
                .ToList();

            var available = DecryptPool(_context.tb_GameWordPool.Where(w => w.WordLength == L && w.IsActive).ToList());
            if (available.Count == 0)
                available = DecryptPool(_context.tb_GameWordPool.Where(w => w.IsActive).ToList());

            if (available.Count > 0)
            {
                var sel = available[new Random().Next(available.Count)];
                game.TargetWord = sel.Word;       // referansla aynı: düz metin saklanır, okumada Decrypt no-op
                game.TargetMeaning = sel.Meaning;
            }
        }

        // ── Oyun durumu ──
        public object GetGameState(int kullaniciID)
        {
            var user = GetUser(kullaniciID);
            if (user == null) return new { requireRegistration = true, message = "Lütfen önce giriş yapınız." };

            string currentSicilNo = (user.SicilNo ?? "").Trim();
            string adSoyad = user.AdSoyad;
            string departman = DepartmanAdi(user);

            DateTime today = DateTime.Today;
            var todayWords = _context.tb_GameWord.Where(w => w.GameDate == today).OrderBy(w => w.Id).ToList();
            if (todayWords.Count == 0)
                return new { error = true, noWordToday = true, message = "Bugün için henüz bir kelime belirlenmemiştir.", adSoyad, departman, isLoggedIn = true };

            var scores = _context.tb_GameScore
                .Where(s => s.GameDate == today && s.SicilNo == currentSicilNo)
                .OrderBy(s => s.PlayNumber).ToList();

            var activeGame = scores.FirstOrDefault(s => !s.IsCompleted);

            if (activeGame != null)
            {
                string target = (ClsEncryption.Decrypt(activeGame.TargetWord) ?? "").Trim().ToUpper(TrCulture);
                int wordLength = target.Length > 0 ? target.Length : (todayWords[0].WordLength ?? 5);
                var (g, f) = BuildGuessFeedback(activeGame.Guesses, target);
                return new
                {
                    success = true, adSoyad, departman, isLoggedIn = true,
                    wordLength, isCompleted = false, attemptsCount = activeGame.AttemptsCount,
                    guesses = g, feedbacks = f, playNumber = activeGame.PlayNumber, dailyLimit = DailyLimit
                };
            }

            int completedCount = scores.Count(s => s.IsCompleted);

            if (completedCount < DailyLimit)
            {
                var newGame = new tb_GameScore
                {
                    SicilNo = currentSicilNo, AdSoyad = adSoyad, Departman = departman,
                    GameDate = today, PlayNumber = completedCount + 1, Score = 0, AttemptsCount = 0,
                    Guesses = "", IsCompleted = false, PlayDate = DateTime.Now
                };
                AssignRandomWordToGame(newGame, todayWords, currentSicilNo);
                _context.tb_GameScore.Add(newGame);
                _context.SaveChanges();

                string target = (ClsEncryption.Decrypt(newGame.TargetWord) ?? "").Trim().ToUpper(TrCulture);
                int wordLength = target.Length > 0 ? target.Length : (todayWords[0].WordLength ?? 5);
                return new
                {
                    success = true, adSoyad, departman, isLoggedIn = true,
                    wordLength, isCompleted = false, attemptsCount = 0,
                    guesses = new List<string>(), feedbacks = new List<string>(), playNumber = newGame.PlayNumber, dailyLimit = DailyLimit
                };
            }

            // Bugünkü hakkı bitmiş → son oyunu kilitli, hedef + anlam açık göster.
            var lastGame = scores.LastOrDefault();
            string lastTarget = (ClsEncryption.Decrypt(lastGame?.TargetWord) ?? "").Trim().ToUpper(TrCulture);
            int lastLen = lastTarget.Length > 0 ? lastTarget.Length : (todayWords.Last().WordLength ?? 5);
            var (lg, lf) = BuildGuessFeedback(lastGame?.Guesses, lastTarget);
            return new
            {
                success = true, adSoyad, departman, isLoggedIn = true,
                wordLength = lastLen, isCompleted = true, attemptsCount = lastGame?.AttemptsCount ?? 0,
                guesses = lg, feedbacks = lf, score = lastGame?.Score ?? 0,
                targetWord = lastTarget, meaning = lastGame?.TargetMeaning ?? "",
                dailyLimit = DailyLimit, completedCount, canPlayNew = false
            };
        }

        private (List<string> guesses, List<string> feedbacks) BuildGuessFeedback(string guessesCsv, string targetUpper)
        {
            var g = new List<string>();
            var f = new List<string>();
            if (!string.IsNullOrEmpty(guessesCsv))
            {
                foreach (var item in guessesCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string clean = item.Trim().ToUpper(TrCulture);
                    g.Add(clean);
                    f.Add(CalculateFeedbackString(clean, targetUpper));
                }
            }
            return (g, f);
        }

        // ── Tahmin gönder ──
        public object SubmitGuess(int kullaniciID, string guess)
        {
            if (string.IsNullOrEmpty(guess)) return new { error = true, message = "Geçersiz tahmin!" };

            var user = GetUser(kullaniciID);
            if (user == null) return new { error = true, message = "Lütfen önce giriş yapınız." };

            string currentSicilNo = (user.SicilNo ?? "").Trim();
            string adSoyad = user.AdSoyad;
            string departman = DepartmanAdi(user);

            DateTime today = DateTime.Today;
            var todayWords = _context.tb_GameWord.Where(w => w.GameDate == today).OrderBy(w => w.Id).ToList();
            if (todayWords.Count == 0) return new { error = true, message = "Bugün için bir kelime bulunamadı." };

            var activeGame = _context.tb_GameScore.FirstOrDefault(s => s.GameDate == today && s.SicilNo == currentSicilNo && !s.IsCompleted);
            if (activeGame == null)
            {
                int completedCount = _context.tb_GameScore.Count(s => s.GameDate == today && s.SicilNo == currentSicilNo && s.IsCompleted);
                if (completedCount >= DailyLimit)
                    return new { error = true, isCompleted = true, message = "Bugünkü tahmin hakkınızı tamamladınız." };

                activeGame = new tb_GameScore
                {
                    SicilNo = currentSicilNo, AdSoyad = adSoyad, Departman = departman,
                    GameDate = today, PlayNumber = completedCount + 1, Score = 0, AttemptsCount = 0,
                    Guesses = "", IsCompleted = false, PlayDate = DateTime.Now
                };
                AssignRandomWordToGame(activeGame, todayWords, currentSicilNo);
                _context.tb_GameScore.Add(activeGame);
                _context.SaveChanges();
            }

            string targetUpper = (ClsEncryption.Decrypt(activeGame.TargetWord) ?? "").Trim().ToUpper(TrCulture);
            string guessUpper = guess.Trim().ToUpper(TrCulture);
            if (guessUpper.Length != targetUpper.Length)
                return new { error = true, message = "Tahmin kelimesi harf sayısı uyumsuz!" };

            var currentGuesses = new List<string>();
            if (!string.IsNullOrEmpty(activeGame.Guesses))
                currentGuesses.AddRange(activeGame.Guesses.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

            if (currentGuesses.Count >= MaxAttempts)
                return new { error = true, message = "Maksimum " + MaxAttempts + " tahmin hakkınız bulunmaktadır." };

            currentGuesses.Add(guessUpper);
            activeGame.Guesses = string.Join(",", currentGuesses);
            activeGame.AttemptsCount = currentGuesses.Count;
            activeGame.PlayDate = DateTime.Now;

            string feedback = CalculateFeedbackString(guessUpper, targetUpper);
            bool isCorrect = guessUpper == targetUpper;
            if (isCorrect)
            {
                activeGame.Score = 11 - activeGame.AttemptsCount;
                activeGame.IsCompleted = true;
            }
            else if (activeGame.AttemptsCount >= MaxAttempts)
            {
                activeGame.Score = 0;
                activeGame.IsCompleted = true;
            }
            _context.SaveChanges();

            var feedbacks = currentGuesses.Select(g => CalculateFeedbackString(g, targetUpper)).ToList();
            return new
            {
                success = true,
                isCompleted = activeGame.IsCompleted,
                attemptsCount = activeGame.AttemptsCount,
                guesses = currentGuesses,
                feedbacks,
                score = activeGame.Score,
                latestFeedback = feedback,
                isCorrect,
                targetWord = activeGame.IsCompleted ? targetUpper : "",
                meaning = activeGame.IsCompleted ? (activeGame.TargetMeaning ?? "") : ""
            };
        }

        // ── Liderlik tabloları ──
        public object GetLeaderboards()
        {
            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);
            DateTime firstOfMonth = new DateTime(today.Year, today.Month, 1);
            DateTime firstOfNextMonth = firstOfMonth.AddMonths(1);

            // Tüm tamamlanmış skorları belleğe çekip gruplama (küçük veri; EF GroupBy sorunlarından kaçınılır).
            var all = _context.tb_GameScore
                .Where(s => s.IsCompleted)
                .Select(s => new { s.AdSoyad, s.Departman, s.SicilNo, s.Score, s.GameDate })
                .ToList();

            List<LeaderboardEntryDto> Board(Func<DateTime, bool> dateFilter, bool average = false)
            {
                var grouped = all.Where(s => dateFilter(s.GameDate))
                    .GroupBy(s => new { s.AdSoyad, s.Departman, s.SicilNo })
                    .Select(g =>
                    {
                        int total = g.Sum(x => x.Score);
                        int played = g.Count();
                        int wins = g.Count(x => x.Score > 0);
                        return new LeaderboardEntryDto
                        {
                            AdSoyad = g.Key.AdSoyad,
                            Departman = g.Key.Departman,
                            SicilNo = (g.Key.SicilNo ?? "").Trim(),
                            TotalScore = total,
                            GamesPlayed = played,
                            SuccessRate = played > 0 ? Math.Round((double)wins / played * 100, 1) : 0,
                            AverageScore = played > 0 ? Math.Round((double)total / played, 2) : 0
                        };
                    })
                    .Where(x => x.TotalScore > 0);

                if (average)
                    return grouped.Where(x => x.GamesPlayed >= 3)
                        .OrderByDescending(x => x.AverageScore).ThenByDescending(x => x.TotalScore).Take(50).ToList();

                return grouped.OrderByDescending(x => x.TotalScore).ThenByDescending(x => x.SuccessRate).Take(50).ToList();
            }

            return new
            {
                success = true,
                daily = Board(d => d >= today && d < tomorrow),
                monthly = Board(d => d >= firstOfMonth && d < firstOfNextMonth),
                general = Board(d => true),
                average = Board(d => true, average: true)
            };
        }
    }
}
