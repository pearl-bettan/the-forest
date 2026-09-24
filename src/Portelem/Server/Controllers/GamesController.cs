using Microsoft.AspNetCore.Mvc;
using UsersManager.Server;
using Data;
using AuthTemplate.Shared.Models.Games;

namespace AuthTemplate.Server.Controllers
{
    // ============================================================
    // הבקר המרכזי של המחולל: כל מה שעורכת עושה עם משחקים,
    // שאלות, פריטים ותמונות עובר כאן.
    //
    // מבנה הנתונים בשלוש רמות:
    //   Games   - משחק, ולו קוד שהשחקן מקליד
    //   Stages  - שאלה במשחק. בקוד ובמסכים היא "שאלה",
    //             בבסיס הנתונים וביוניטי היא "שלב"
    //   Answers - פריט בודד שהשחקן מסדר, עם המקום הנכון שלו
    //
    // [ServiceFilter(typeof(AuthCheck))] חל על כל הבקר, ולכן כל
    // פעולה כאן מקבלת את authUserId כפרמטר ראשון. הפרמטר הזה
    // אינו מגיע מהלקוח אלא מוזרק מהטוקן, ולכן אי אפשר לזייף אותו.
    //
    // כלל שחוזר בכל פעולה: לפני כל נגיעה בנתונים נבדק שהמשחק
    // או השאלה אכן שייכים למשתמש המחובר, דרך IsMyGame או
    // IsMyQuestion. בלי זה כל אחד היה יכול לערוך משחק של אחר
    // רק בכך שינחש מזהה
    // ============================================================
    [Route("api/[controller]")]
    [ApiController]
    //בדיקה שהמשתמש מחובר
    [ServiceFilter(typeof(AuthCheck))]

    public class GamesController : ControllerBase
    {
        //החיבור לבסיס הנתונים
        private readonly DbRepository _db;

        //שמירת קבצי תמונה בשרת
        private readonly FilesManage _files;

        public GamesController(DbRepository db, FilesManage files)
        {
            _db = db;
            _files = files;
        }


        // מחזיר את טבלת "המשחקים שלי": GET api/Games
        // רק המשחקים של המשתמש המחובר, עם ספירת השאלות לכל אחד
        //שליפת כל המשחקים של המשתמש המחובר
        [HttpGet]
        public async Task<IActionResult> GetUserGames(int authUserId)
        {
            //בדיקה שיש משתמש מחובר
            if (authUserId > 0)
            {
                //יצירת פרמטר עם המזהה של המשתמש
                object param = new
                {
                    UserId = authUserId
                };

                //שליפת המשחקים של המשתמש.
                //מספר השלבים נספר בתת-שאילתה, כך שגם משחק ללא שלבים יוחזר
                string gameQuery = "SELECT Id, GameName, GameCode, TimePerQuestion, IsPublish, CanPublish, " +
                                   "(SELECT COUNT(*) FROM Stages WHERE Stages.GameId = Games.Id) AS QuestionsCount " +
                                   "FROM Games WHERE UserId = @UserId";

                var gamesRecords = await _db.GetRecordsAsync<GameToTable>(gameQuery, param);
                List<GameToTable> GamesList = gamesRecords.ToList();

                //במידה ויש משחקים - החזרתם
                if (GamesList.Count > 0)
                {
                    //עדכון תנאי הפרסום של כל משחק, כדי שהטבלה תציג מצב מעודכן
                    foreach (GameToTable game in GamesList)
                    {
                        game.CanPublish = await CheckCanPublish(game.ID);
                    }

                    return Ok(GamesList);
                }
                else
                {
                    return BadRequest("No games for this user");
                }
            }
            else
            {
                return Unauthorized("user is not authenticated");
            }
        }


        // מחזיר משחק אחד לעמוד העריכה: GET api/Games/{gameId}
        // מחזיר שגיאה גם כשהמשחק קיים אך שייך למשתמש אחר, כדי
        // לא לחשוף שהמזהה הזה תפוס
        //שליפת משחק אחד לפי המזהה שלו, עבור מסך העורך
        [HttpGet("{gameId}")]
        public async Task<IActionResult> GetOneGame(int authUserId, int gameId)
        {
            //בדיקה שיש משתמש מחובר
            if (authUserId > 0)
            {
                //בדיקה שהמשחק אכן שייך למשתמש המחובר
                bool isMine = await IsMyGame(gameId, authUserId);

                if (isMine == false)
                {
                    return BadRequest("Game not found");
                }

                GameToTable game = await GetGameById(gameId);

                if (game == null)
                {
                    return BadRequest("Game not found");
                }

                return Ok(game);
            }
            else
            {
                return Unauthorized("user is not authenticated");
            }
        }


        // יוצר משחק חדש: POST api/Games/addGame
        // קוד המשחק נקבע בשרת ולא מגיע מהלקוח, כדי שיהיה ייחודי
        //הוספת משחק חדש
        [HttpPost("addGame")]
        public async Task<IActionResult> AddGames(int authUserId, GameToAdd gameToAdd)
        {
            //בדיקה שיש משתמש מחובר
            if (authUserId > 0)
            {
                //יצירת משחק חדש עם ערכי ברירת מחדל.
                //קוד המשחק יהיה 0 בינתיים - נעדכן אותו מיד אחרי שנקבל את המזהה
                object newGameParam = new
                {
                    GameName = gameToAdd.GameName,
                    GameCode = 0,
                    IsPublish = false,
                    TimePerQuestion = gameToAdd.TimePerQuestion,
                    UserId = authUserId,
                    CanPublish = false
                };

                string insertGameQuery = "INSERT INTO Games (GameName, GameCode, IsPublish, TimePerQuestion, UserId, CanPublish) " +
                                         "VALUES (@GameName, @GameCode, @IsPublish, @TimePerQuestion, @UserId, @CanPublish)";

                int newGameId = await _db.InsertReturnIdAsync(insertGameQuery, newGameParam);

                if (newGameId != 0)
                {
                    //יצירת קוד משחק ייחודי בן 4 ספרות על בסיס המזהה של המשחק
                    int gameCode = newGameId + 1000;

                    object updateParam = new
                    {
                        ID = newGameId,
                        GameCode = gameCode
                    };

                    string updateCodeQuery = "UPDATE Games SET GameCode = @GameCode WHERE Id = @ID";
                    int isUpdate = await _db.SaveDataAsync(updateCodeQuery, updateParam);

                    if (isUpdate > 0)
                    {
                        //החזרת פרטי המשחק המלאים כפי שנשמרו בבסיס הנתונים
                        GameToTable newGame = await GetGameById(newGameId);
                        return Ok(newGame);
                    }

                    return BadRequest("Game code not created");
                }

                return BadRequest("Game not created");
            }
            else
            {
                return Unauthorized("user is not authenticated");
            }
        }


        //עריכת הגדרות של משחק קיים
        [HttpPut("editGame")]
        public async Task<IActionResult> EditGame(int authUserId, GameToEdit gameToEdit)
        {
            //בדיקה שיש משתמש מחובר
            if (authUserId > 0)
            {
                //בדיקה שהמשחק אכן שייך למשתמש המחובר
                bool isMine = await IsMyGame(gameToEdit.ID, authUserId);

                if (isMine == false)
                {
                    return BadRequest("Game not found");
                }

                object param = new
                {
                    ID = gameToEdit.ID,
                    GameName = gameToEdit.GameName,
                    TimePerQuestion = gameToEdit.TimePerQuestion
                };

                string updateQuery = "UPDATE Games SET GameName = @GameName, TimePerQuestion = @TimePerQuestion WHERE Id = @ID";
                int isUpdate = await _db.SaveDataAsync(updateQuery, param);

                // הזמן נשמר גם על כל שאלה בטבלת Stages, וזה מה שהמשחק ביוניטי
                // קורא. בלי העדכון הזה שינוי הזמן חל רק על שאלות חדשות,
                // והשאלות הקיימות ממשיכות לרוץ עם הזמן הישן
                string updateStagesQuery = "UPDATE Stages SET StageTime = @TimePerQuestion WHERE GameId = @ID";
                await _db.SaveDataAsync(updateStagesQuery, param);

                if (isUpdate > 0)
                {
                    //החזרת המשחק המעודכן, כדי שהטבלה תציג את הערכים החדשים
                    GameToTable updatedGame = await GetGameById(gameToEdit.ID);
                    return Ok(updatedGame);
                }

                return BadRequest("Game not updated");
            }
            else
            {
                return Unauthorized("user is not authenticated");
            }
        }


        // ============================================================
        // מחליף את מצב הפרסום: PUT api/Games/publishGame/{gameId}
        //
        // פרסום הוא מה שהופך משחק לזמין לשחקנים - UnityController
        // מגיש רק משחקים מפורסמים. לכן תנאי הפרסום נבדקים כאן
        // בשרת ולא רק במסך, כדי שקריאה ישירה ל-API לא תעקוף אותם
        // ============================================================
        //שינוי מצב הפרסום של משחק
        [HttpPut("publishGame/{gameId}")]
        public async Task<IActionResult> PublishGame(int authUserId, int gameId)
        {
            //בדיקה שיש משתמש מחובר
            if (authUserId > 0)
            {
                //בדיקה שהמשחק אכן שייך למשתמש המחובר
                bool isMine = await IsMyGame(gameId, authUserId);

                if (isMine == false)
                {
                    return BadRequest("Game not found");
                }

                //שליפת המצב הנוכחי של המשחק
                GameToTable game = await GetGameById(gameId);

                //המצב החדש הוא ההפך מהמצב הנוכחי
                bool newValue = !game.IsPublish;

                //בדיקת עמידה בתנאי הפרסום.
                //כיבוי הפרסום מותר תמיד, הדלקה רק אם המשחק תקין
                bool canPublish = await CheckCanPublish(gameId);

                if (newValue == true && canPublish == false)
                {
                    return BadRequest("Game cannot be published");
                }

                object param = new
                {
                    ID = gameId,
                    IsPublish = newValue,
                    CanPublish = canPublish
                };

                string updateQuery = "UPDATE Games SET IsPublish = @IsPublish, CanPublish = @CanPublish WHERE Id = @ID";
                int isUpdate = await _db.SaveDataAsync(updateQuery, param);

                if (isUpdate > 0)
                {
                    GameToTable updatedGame = await GetGameById(gameId);
                    return Ok(updatedGame);
                }

                return BadRequest("Publish not changed");
            }
            else
            {
                return Unauthorized("user is not authenticated");
            }
        }


        // ============================================================
        // מוחק משחק: DELETE api/Games/deleteGame/{gameId}
        //
        // השאלות והפריטים נמחקים מאליהם בזכות מחיקה מדורגת -
        // ולכן חשוב ש-PRAGMA foreign_keys נדלק ב-DbRepository.
        // קבצי התמונות אינם חלק מבסיס הנתונים ולכן נמחקים כאן
        // במפורש, אחרת היו נשארים יתומים בתיקייה לנצח
        // ============================================================
        //מחיקת משחק. השלבים והתשובות נמחקים אוטומטית בזכות ה-CASCADE
        [HttpDelete("deleteGame/{gameId}")]
        public async Task<IActionResult> DeleteGame(int authUserId, int gameId)
        {
            //בדיקה שיש משתמש מחובר
            if (authUserId > 0)
            {
                object param = new
                {
                    ID = gameId,
                    UserId = authUserId
                };

                //אוספים את שמות הקבצים לפני המחיקה, אחריה כבר אי אפשר לשלוף אותם
                List<string> images = await GetGameImages(gameId);

                //התנאי על UserId מוודא שעורך לא יוכל למחוק משחק של עורך אחר
                string deleteQuery = "DELETE FROM Games WHERE Id = @ID AND UserId = @UserId";
                int isDelete = await _db.SaveDataAsync(deleteQuery, param);

                if (isDelete > 0)
                {
                    //מחיקת המשחק מוחקת גם את כל קבצי התמונות ששייכים לו
                    _files.DeleteFiles(images, "uploadedFiles");

                    return Ok(gameId);
                }

                return BadRequest("Game not deleted");
            }
            else
            {
                return Unauthorized("user is not authenticated");
            }
        }


        // ==========================================================
        // שאלות
        // ==========================================================

        //שליפת כל השאלות של משחק, כולל הפריטים של כל שאלה
        [HttpGet("{gameId}/questions")]
        public async Task<IActionResult> GetQuestions(int authUserId, int gameId)
        {
            if (authUserId > 0)
            {
                bool isMine = await IsMyGame(gameId, authUserId);

                if (isMine == false)
                {
                    return BadRequest("Game not found");
                }

                //השאלות מוחזרות לפי הסדר שקבע העורך
                string questionsQuery = "SELECT Id, GameId, Topic, LeftTag, RightTag, StageOrder AS QuestionOrder " +
                                        "FROM Stages WHERE GameId = @GameId ORDER BY StageOrder";

                var records = await _db.GetRecordsAsync<QuestionToEdit>(questionsQuery, new { GameId = gameId });

                if (records == null)
                {
                    return StatusCode(500, "שגיאה בשליפת השאלות");
                }

                List<QuestionToEdit> questions = records.ToList();

                //לכל שאלה מצרפים את הפריטים שלה, לפי הסדר הנכון
                foreach (QuestionToEdit question in questions)
                {
                    question.Answers = await GetAnswers(question.ID);
                }

                return Ok(questions);
            }
            else
            {
                return Unauthorized("user is not authenticated");
            }
        }


        // ============================================================
        // מוסיף שאלה למשחק: POST api/Games/addQuestion
        //
        // השאלה והפריטים שלה נשמרים יחד: קודם השאלה, כדי לקבל
        // את המזהה שלה, ואז הפריטים שמצביעים עליו.
        // בסיום נקרא SyncPublishState, כי הוספת שאלה חסרה עלולה
        // להוציא משחק מפורסם ממצב שניתן לפרסום
        // ============================================================
        //הוספת שאלה חדשה
        [HttpPost("addQuestion")]
        public async Task<IActionResult> AddQuestion(int authUserId, QuestionToEdit question)
        {
            if (authUserId > 0)
            {
                bool isMine = await IsMyGame(question.GameId, authUserId);

                if (isMine == false)
                {
                    return BadRequest("Game not found");
                }

                if (question.Answers == null)
                {
                    question.Answers = new List<AnswerToEdit>();
                }

                //בסצנת היוניטי יש 10 אבנים, ולכן זו המגבלה
                if (question.Answers.Count > 10)
                {
                    return BadRequest("Too many answers");
                }

                //מגבלה של עד 10 שאלות במשחק
                var count = await _db.GetRecordsAsync<int>(
                    "SELECT COUNT(*) FROM Stages WHERE GameId = @GameId",
                    new { GameId = question.GameId });

                if (count != null && count.FirstOrDefault() >= 10)
                {
                    return BadRequest("Too many questions");
                }

                //זמן השאלה נלקח מההגדרות הכלליות של המשחק
                GameToTable game = await GetGameById(question.GameId);

                //השאלה החדשה נכנסת בסוף הרשימה
                var orders = await _db.GetRecordsAsync<int>(
                    "SELECT COUNT(*) + 1 FROM Stages WHERE GameId = @GameId",
                    new { GameId = question.GameId });

                int questionOrder = orders == null ? 1 : orders.FirstOrDefault();

                object param = new
                {
                    GameId = question.GameId,
                    //העמודה אינה מקבלת null, ולכן שדה ריק נשמר כמחרוזת ריקה
                    Topic = question.Topic ?? "",
                    LeftTag = string.IsNullOrWhiteSpace(question.LeftTag) ? "אחרון" : question.LeftTag,
                    RightTag = string.IsNullOrWhiteSpace(question.RightTag) ? "ראשון" : question.RightTag,
                    StageTime = game.TimePerQuestion,
                    StageOrder = questionOrder
                };

                string insertQuery = "INSERT INTO Stages (GameId, Topic, LeftTag, RightTag, StageTime, StageOrder) " +
                                     "VALUES (@GameId, @Topic, @LeftTag, @RightTag, @StageTime, @StageOrder)";

                int questionId = await _db.InsertReturnIdAsync(insertQuery, param);

                if (questionId == 0)
                {
                    return BadRequest("Question not created");
                }

                await SaveAnswers(questionId, question.Answers);

                question.ID = questionId;
                question.QuestionOrder = questionOrder;

                //אחרי הוספת שאלה - עדכון מצב הפרסום (טיוטה מורידה מפרסום)
                await SyncPublishState(question.GameId);

                return Ok(question);
            }
            else
            {
                return Unauthorized("user is not authenticated");
            }
        }


        // ============================================================
        // מעדכן שאלה קיימת: PUT api/Games/editQuestion
        //
        // הפריטים אינם מעודכנים אחד-אחד אלא נמחקים ונכתבים מחדש
        // מהרשימה שהגיעה. זה פשוט יותר מהשוואת רשימות, ומבטיח
        // שהמקום הנכון של כל פריט תואם לסדר שבמסך.
        // לפני הכתיבה נמחקים קבצי התמונות שכבר אינם בשימוש
        // ============================================================
        //עדכון שאלה קיימת והפריטים שלה
        [HttpPut("editQuestion")]
        public async Task<IActionResult> EditQuestion(int authUserId, QuestionToEdit question)
        {
            if (authUserId > 0)
            {
                bool isMine = await IsMyQuestion(question.ID, authUserId);

                if (isMine == false)
                {
                    return BadRequest("Question not found");
                }

                if (question.Answers == null)
                {
                    question.Answers = new List<AnswerToEdit>();
                }

                if (question.Answers.Count > 10)
                {
                    return BadRequest("Too many answers");
                }

                object param = new
                {
                    ID = question.ID,
                    Topic = question.Topic ?? "",
                    LeftTag = string.IsNullOrWhiteSpace(question.LeftTag) ? "אחרון" : question.LeftTag,
                    RightTag = string.IsNullOrWhiteSpace(question.RightTag) ? "ראשון" : question.RightTag
                };

                string updateQuery = "UPDATE Stages SET Topic = @Topic, LeftTag = @LeftTag, " +
                                     "RightTag = @RightTag WHERE Id = @ID";

                await _db.SaveDataAsync(updateQuery, param);

                //שמות קבצי התמונות שהיו בשאלה לפני העריכה
                List<string> oldImages = await GetQuestionImages(question.ID);

                //הפריטים נכתבים מחדש. כך המקומות נשארים רציפים ולפי הסדר שבמסך
                await _db.SaveDataAsync("DELETE FROM Answers WHERE StageId = @StageId",
                                        new { StageId = question.ID });

                await SaveAnswers(question.ID, question.Answers);

                //תמונה שהוחלפה או שהפריט שלה נמחק - הקובץ שלה מיותר עכשיו.
                //מוחקים אותו כדי שתיקיית התמונות תישאר מסונכרנת עם בסיס הנתונים
                DeleteUnusedImages(oldImages, question.Answers);

                //אחרי עריכת שאלה - עדכון מצב הפרסום (טיוטה מורידה מפרסום)
                await SyncPublishState(question.GameId);

                return Ok(question);
            }
            else
            {
                return Unauthorized("user is not authenticated");
            }
        }


        //מחיקת שאלה. הפריטים נמחקים אוטומטית בזכות ה-CASCADE
        [HttpDelete("deleteQuestion/{questionId}")]
        public async Task<IActionResult> DeleteQuestion(int authUserId, int questionId)
        {
            if (authUserId > 0)
            {
                bool isMine = await IsMyQuestion(questionId, authUserId);

                if (isMine == false)
                {
                    return BadRequest("Question not found");
                }

                //אוספים את שמות הקבצים לפני המחיקה, אחריה כבר אי אפשר לשלוף אותם
                List<string> images = await GetQuestionImages(questionId);

                //שומרים את מזהה המשחק לפני המחיקה, לצורך עדכון מצב הפרסום
                var gameIds = await _db.GetRecordsAsync<int>(
                    "SELECT GameId FROM Stages WHERE Id = @ID", new { ID = questionId });
                int ownerGameId = gameIds == null ? 0 : gameIds.FirstOrDefault();

                await _db.SaveDataAsync("DELETE FROM Stages WHERE Id = @ID", new { ID = questionId });

                //מחיקת השאלה מוחקת גם את קבצי התמונות שלה
                _files.DeleteFiles(images, "uploadedFiles");

                //אחרי מחיקת שאלה - עדכון מצב הפרסום
                if (ownerGameId > 0)
                {
                    await SyncPublishState(ownerGameId);
                }

                return Ok(questionId);
            }
            else
            {
                return Unauthorized("user is not authenticated");
            }
        }


        // מעלה תמונה לפריט: POST api/Games/uploadImage
        // מוחזר שם הקובץ בלבד, והוא זה שנשמר בעמודת התוכן של
        // הפריט. התמונה עצמה יושבת כקובץ תחת wwwroot
        //העלאת תמונה עבור פריט תשובה.
        //הקובץ נשמר בתיקיית uploadedFiles, ומוחזר שמו כדי שיישמר בבסיס הנתונים
        [HttpPost("uploadImage")]
        public async Task<IActionResult> UploadImage(int authUserId, ImageToUpload image)
        {
            if (authUserId > 0)
            {
                if (image == null || string.IsNullOrWhiteSpace(image.ImageBase64))
                {
                    return BadRequest("No image");
                }

                string fileName = await _files.SaveFile(image.ImageBase64, image.Extension, "uploadedFiles");

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    return BadRequest("Image not saved");
                }

                return Ok(fileName);
            }
            else
            {
                return Unauthorized("user is not authenticated");
            }
        }


        // ==========================================================
        // שיטות עזר
        // ==========================================================

        // ==========================================================
        // ניהול קבצי התמונות
        // תיקיית uploadedFiles חייבת להישאר מסונכרנת מול בסיס הנתונים,
        // ולכן כל מחיקה או החלפה של תמונה מוחקת גם את הקובץ שכבר לא בשימוש
        // ==========================================================

        //שמות קבצי התמונות של שאלה אחת
        private async Task<List<string>> GetQuestionImages(int questionId)
        {
            string query = "SELECT Content FROM Answers " +
                           "WHERE StageId = @StageId AND IsImage = 1";

            var records = await _db.GetRecordsAsync<string>(query, new { StageId = questionId });

            if (records == null)
            {
                return new List<string>();
            }

            return records.ToList();
        }


        //שמות קבצי התמונות של כל השאלות במשחק
        private async Task<List<string>> GetGameImages(int gameId)
        {
            string query = "SELECT Answers.Content FROM Answers " +
                           "INNER JOIN Stages ON Stages.Id = Answers.StageId " +
                           "WHERE Stages.GameId = @GameId AND Answers.IsImage = 1";

            var records = await _db.GetRecordsAsync<string>(query, new { GameId = gameId });

            if (records == null)
            {
                return new List<string>();
            }

            return records.ToList();
        }


        //מוחק את הקבצים שהיו בשאלה ואינם מופיעים בה יותר
        private void DeleteUnusedImages(List<string> oldImages, List<AnswerToEdit> newAnswers)
        {
            if (oldImages == null || oldImages.Count == 0)
            {
                return;
            }

            //שמות הקבצים שנשארו בשימוש אחרי העריכה
            HashSet<string> stillUsed = new HashSet<string>();

            if (newAnswers != null)
            {
                foreach (AnswerToEdit answer in newAnswers)
                {
                    if (answer.IsImage == true && string.IsNullOrWhiteSpace(answer.Content) == false)
                    {
                        stillUsed.Add(answer.Content);
                    }
                }
            }

            List<string> toDelete = new List<string>();

            foreach (string image in oldImages)
            {
                if (stillUsed.Contains(image) == false)
                {
                    toDelete.Add(image);
                }
            }

            _files.DeleteFiles(toDelete, "uploadedFiles");
        }


        //שליפת הפריטים של שאלה, לפי הסדר הנכון
        private async Task<List<AnswerToEdit>> GetAnswers(int questionId)
        {
            string query = "SELECT Id, Content, IsImage FROM Answers " +
                           "WHERE StageId = @StageId ORDER BY CorrectPlace";

            var records = await _db.GetRecordsAsync<AnswerToEdit>(query, new { StageId = questionId });

            if (records == null)
            {
                return new List<AnswerToEdit>();
            }

            return records.ToList();
        }


        // ============================================================
        // כותבת את פריטי השאלה.
        //
        // המקום הנכון אינו שדה שהעורכת מזינה אלא נגזר מהמיקום
        // ברשימה: הפריט הראשון מקבל מקום ראשון וכן הלאה. לכן
        // שינוי סדר במסך הוא כל מה שצריך כדי לשנות את התשובה
        // הנכונה, ואין שדה נפרד שעלול לצאת מסנכרון
        // ============================================================
        //שמירת הפריטים של שאלה.
        //המקום הנכון נקבע לפי הסדר ברשימה שהגיעה מהעורך
        private async Task SaveAnswers(int questionId, List<AnswerToEdit> answers)
        {
            string query = "INSERT INTO Answers (StageId, Content, IsImage, CorrectPlace) " +
                           "VALUES (@StageId, @Content, @IsImage, @CorrectPlace)";

            for (int i = 0; i < answers.Count; i++)
            {
                await _db.SaveDataAsync(query, new
                {
                    StageId = questionId,
                    Content = answers[i].Content,
                    IsImage = answers[i].IsImage,
                    CorrectPlace = i
                });
            }
        }


        //בדיקה שהשאלה שייכת למשחק של המשתמש המחובר
        private async Task<bool> IsMyQuestion(int questionId, int userId)
        {
            string query = "SELECT COUNT(*) FROM Stages " +
                           "INNER JOIN Games ON Games.Id = Stages.GameId " +
                           "WHERE Stages.Id = @QuestionId AND Games.UserId = @UserId";

            var counts = await _db.GetRecordsAsync<int>(query,
                new { QuestionId = questionId, UserId = userId });

            if (counts == null)
            {
                return false;
            }

            return counts.FirstOrDefault() > 0;
        }


        //שליפת משחק אחד לפי המזהה שלו
        private async Task<GameToTable> GetGameById(int gameId)
        {
            object param = new
            {
                ID = gameId
            };

            string gameQuery = "SELECT Id, GameName, GameCode, TimePerQuestion, IsPublish, CanPublish, " +
                               "(SELECT COUNT(*) FROM Stages WHERE Stages.GameId = Games.Id) AS QuestionsCount " +
                               "FROM Games WHERE Id = @ID";

            var gameRecord = await _db.GetRecordsAsync<GameToTable>(gameQuery, param);
            GameToTable game = gameRecord.FirstOrDefault();

            //תנאי הפרסום מחושבים ולא נשלפים, כדי שהערך תמיד יהיה מעודכן
            if (game != null)
            {
                game.CanPublish = await CheckCanPublish(gameId);
            }

            return game;
        }


        //בדיקה שהמשחק שייך למשתמש המחובר
        private async Task<bool> IsMyGame(int gameId, int userId)
        {
            object param = new
            {
                ID = gameId,
                UserId = userId
            };

            string query = "SELECT COUNT(*) FROM Games WHERE Id = @ID AND UserId = @UserId";
            var counts = await _db.GetRecordsAsync<int>(query, param);

            if (counts == null)
            {
                return false;
            }

            return counts.FirstOrDefault() > 0;
        }


        // ============================================================
        // מעדכנת את שני דגלי הפרסום אחרי כל שינוי בשאלות.
        //
        // CanPublish - האם המשחק עומד בתנאים
        // IsPublish  - האם הוא מפורסם בפועל
        //
        // המעבר הוא חד-כיווני בכוונה: משחק שחדל לעמוד בתנאים
        // יורד מפרסום מיד, אבל משחק שחזר לעמוד בהם אינו עולה
        // לפרסום מאליו - העורכת צריכה לפרסם אותו שוב.
        //
        // בלי הקריאה הזו אחרי כל הוספה, עריכה ומחיקה של שאלה,
        // משחק מפורסם היה יכול להישאר מוגש לשחקנים במצב שבור
        // ============================================================
        //סנכרון מצב הפרסום אחרי שינוי בשאלות.
        //אם משחק מפורסם כבר לא עומד בתנאים (למשל נוצרה טיוטה) - מורידים אותו מפרסום
        private async Task SyncPublishState(int gameId)
        {
            bool canPublish = await CheckCanPublish(gameId);
            GameToTable game = await GetGameById(gameId);

            //ברירת מחדל - נשארים במצב הנוכחי, אלא אם אי אפשר לפרסם יותר
            bool isPublish = game.IsPublish;

            if (canPublish == false)
            {
                isPublish = false;
            }

            object param = new
            {
                ID = gameId,
                IsPublish = isPublish,
                CanPublish = canPublish
            };

            await _db.SaveDataAsync(
                "UPDATE Games SET IsPublish = @IsPublish, CanPublish = @CanPublish WHERE Id = @ID",
                param);
        }


        // ============================================================
        // בודקת אם המשחק עומד בתנאי הפרסום שבאפיון:
        //   1. לפחות שלוש שאלות במשחק
        //   2. אין אף שאלה עם פחות משלושה פריטים
        //
        // התנאי השני נבדק בשאילתה אחת עם תת-שאילתה, ולא בלולאה
        // על השאלות, כדי לא לפנות לבסיס הנתונים פעם לכל שאלה.
        //
        // שים לב שההערה הישנה מתחת מדברת על שלב אחד ושתי תשובות -
        // הקוד מחמיר ממנה. הקוד הוא הקובע
        // ============================================================
        //בדיקת עמידה בתנאי הפרסום.
        //משחק ניתן לפרסום אם יש בו לפחות שלב אחד, ובכל שלב לפחות שתי תשובות
        private async Task<bool> CheckCanPublish(int gameId)
        {
            object param = new
            {
                GameId = gameId
            };

            //תנאי ראשון - יש במשחק לפחות שלוש שאלות
            string stagesQuery = "SELECT COUNT(*) FROM Stages WHERE GameId = @GameId";
            var stagesCount = await _db.GetRecordsAsync<int>(stagesQuery, param);

            if (stagesCount == null || stagesCount.FirstOrDefault() < 3)
            {
                return false;
            }

            //תנאי שני - אין שאלה עם פחות משלושה פריטים
            string badStagesQuery = "SELECT COUNT(*) FROM Stages WHERE GameId = @GameId " +
                                    "AND (SELECT COUNT(*) FROM Answers WHERE Answers.StageId = Stages.Id) < 3";

            var badStages = await _db.GetRecordsAsync<int>(badStagesQuery, param);

            if (badStages == null)
            {
                return false;
            }

            return badStages.FirstOrDefault() == 0;
        }
    }
}
