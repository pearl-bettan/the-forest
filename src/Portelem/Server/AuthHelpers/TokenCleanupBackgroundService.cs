namespace UsersManager.Server
{
    // ============================================================
    // שירות רקע שמנקה מדי יום טוקנים שפג תוקפם מהרשימה השחורה.
    //
    // בלעדיו הרשימה הייתה גדלה בכל התנתקות ולעולם לא מתכווצת,
    // ובדיקת הרשימה בכל בקשה הייתה נעשית איטית יותר עם הזמן.
    // טוקן שפג תוקפו נדחה ממילא באימות, ולכן אין טעם לשמור אותו
    // ============================================================
    public class TokenCleanupBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TokenCleanupBackgroundService> _logger;

        public TokenCleanupBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<TokenCleanupBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        // ============================================================
        // הלולאה הראשית של השירות, רצה כל עוד השרת פועל.
        //
        // ההמתנה הראשונה של דקה נותנת לשרת לסיים לעלות לפני
        // שהוא ניגש לבסיס הנתונים. אחר כך ניקוי כל 24 שעות.
        // שגיאה אינה מפילה את השירות אלא רק דוחה את הניסיון
        // הבא בשעה, כדי שתקלה זמנית לא תשבית את הניקוי לתמיד.
        //
        // הפרמטר stoppingToken מסומן כשהשרת נסגר, וכל המתנה
        // מתבטלת מיד במקום להחזיק את הסגירה
        // ============================================================
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Token cleanup service started");

            // Wait 1 minute before first cleanup (give server time to start)
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var blacklistService = scope.ServiceProvider
                        .GetRequiredService<ITokenBlacklistService>();

                    // הניקוי מוגדר רק במימוש שמבוסס בסיס נתונים.
                    // מימוש אחר של הממשק פשוט ידולג
                    if (blacklistService is DbTokenBlacklistService dbService)
                    {
                        dbService.RemoveExpiredTokens();
                    }

                    // Wait 24 hours until next cleanup
                    await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                }
                // ביטול אינו שגיאה אלא סימן שהשרת נסגר
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during token cleanup");
                    // Wait 1 hour before retrying on error
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }

            _logger.LogInformation("Token cleanup service stopped");
        }
    }
}