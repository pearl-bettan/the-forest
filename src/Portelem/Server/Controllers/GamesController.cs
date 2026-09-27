using Microsoft.AspNetCore.Mvc;
using UsersManager.Server;
using Data;
using AuthTemplate.Shared.Models.Games;

namespace AuthTemplate.Server.Controllers
{
    // המחלקה הראשית - ניהול המשחקים
   
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


        // מחזיר את טבלת המשחקים של המשתמש המחובר
        [HttpGet]
        public async Task<IActionResult> GetUserGames(int authUserId)
        {
            //בדיקה שיש משתמש מחובר
            if (authUserId > 0)
            {
                //שמירת המזהה של המשתמש
                object param = new
                {
                    UserId = authUserId
                };

                //שליפת המשחקים של המשתמש.
                string gameQuery = "SELECT Id, GameName, GameCode, TimePerQuestion, IsPublish, CanPublish, " +
                                   "(SELECT COUNT(*) FROM Stages WHERE Stages.GameId = Games.Id) AS QuestionsCount " +
                                   "FROM Games WHERE UserId = @UserId";

                var gamesRecords = await _db.GetRecordsAsync<GameToTable>(gameQuery, param);
                List<GameToTable> GamesList = gamesRecords.ToList();

                //אם יש משחקים למשתמש  - בודקים כל משחק אם ניתן לפרסום
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

        //שליפת משחק  לפי המזהה שלו
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

        //הוספת משחק חדש
        [HttpPost("addGame")]
        public async Task<IActionResult> AddGames(int authUserId, GameToAdd gameToAdd)
        {
            //בדיקה שיש משתמש מחובר
            if (authUserId > 0)
            {
                //יצירת משחק חדש עם ערכי ברירת מחדל.
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
                // שמירת הזמן בבסיס הנתונים
                string updateStagesQuery = "UPDATE Stages SET StageTime = @TimePerQuestion WHERE GameId = @ID";
                await _db.SaveDataAsync(updateStagesQuery, param);

                if (isUpdate > 0)
                {
                    // החזרת המשחק המעודכן
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

                //החלפת מצב הפרסום
                bool newValue = !game.IsPublish;

                //בדיקת עמידה בתנאי הפרסום.
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


       // מחיקת משחק 
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

                // בדיקה שהמשחק שייך למשתמש שמחובר
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

                //השאלות מוחזרות לפי הסדר 
                string questionsQuery = "SELECT Id, GameId, LeftTag, RightTag, StageOrder AS QuestionOrder " +
                                        "FROM Stages WHERE GameId = @GameId ORDER BY StageOrder, Id";

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

               // בתכנון ועיצוב המשחק יש 10 אבנים 
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

                // השאלה החדשה נכנסת בסוף הרשימה.               
                var orders = await _db.GetRecordsAsync<int>(
                    "SELECT COALESCE(MAX(StageOrder), 0) + 1 FROM Stages WHERE GameId = @GameId",
                    new { GameId = question.GameId });

                int questionOrder = orders == null ? 1 : orders.FirstOrDefault();

                object param = new
                {
                    GameId = question.GameId,
                    LeftTag = string.IsNullOrWhiteSpace(question.LeftTag) ? "אחרון" : question.LeftTag,
                    RightTag = string.IsNullOrWhiteSpace(question.RightTag) ? "ראשון" : question.RightTag,
                    StageTime = game.TimePerQuestion,
                    StageOrder = questionOrder
                };

                string insertQuery = "INSERT INTO Stages (GameId, LeftTag, RightTag, StageTime, StageOrder) " +
                                     "VALUES (@GameId, @LeftTag, @RightTag, @StageTime, @StageOrder)";

                int questionId = await _db.InsertReturnIdAsync(insertQuery, param);

                if (questionId == 0)
                {
                    return BadRequest("Question not created");
                }

                await SaveAnswers(questionId, question.Answers);

                question.ID = questionId;
                question.QuestionOrder = questionOrder;

                //אחרי הוספת שאלה - עדכון מצב הפרסום 
                await SyncPublishState(question.GameId);

                return Ok(question);
            }
            else
            {
                return Unauthorized("user is not authenticated");
            }
        }

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
                    LeftTag = string.IsNullOrWhiteSpace(question.LeftTag) ? "אחרון" : question.LeftTag,
                    RightTag = string.IsNullOrWhiteSpace(question.RightTag) ? "ראשון" : question.RightTag
                };

                string updateQuery = "UPDATE Stages SET LeftTag = @LeftTag, " +
                                     "RightTag = @RightTag WHERE Id = @ID";

                await _db.SaveDataAsync(updateQuery, param);

                //שמות קבצי התמונות שהיו בשאלה לפני העריכה
                List<string> oldImages = await GetQuestionImages(question.ID);

                // שמירה בבסיס הנתונים
                await _db.RunBatchAsync(
                    AnswerStatements(question.ID, question.Answers, true));

                //  מחיקת קובץ התמונה שנמחקה
                DeleteUnusedImages(oldImages, question.Answers);

                // המשחק נשלף מבסיס הנתונים     
                int gameId = await GetQuestionGameId(question.ID);

                if (gameId > 0) await SyncPublishState(gameId);

                return Ok(question);
            }
            else
            {
                return Unauthorized("user is not authenticated");
            }
        }


        //מחיקת שאלה
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

                //אוספים את שמות הקבצים לפני המחיקה
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


        // מעלה תמונה
        [HttpPost("uploadImage")]
        public async Task<IActionResult> UploadImage(int authUserId, ImageToUpload image)
        {
            if (authUserId > 0)
            {
                if (image == null || string.IsNullOrWhiteSpace(image.ImageBase64))
                {
                    return BadRequest("No image");
                }

                string fileName;
                // שמירת התמונה פורמט base64
                try
                {
                    fileName = await _files.SaveFile(image.ImageBase64, image.Extension, "uploadedFiles");
                }
                catch (Exception)
                {
                    return BadRequest("Image not valid");
                }

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


        
        // פונקציות עזר

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

            //שמות הקבצים שבשימוש 
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



        //שמירת פרטי השאלה
        private async Task SaveAnswers(int questionId, List<AnswerToEdit> answers)
        {
            await _db.RunBatchAsync(AnswerStatements(questionId, answers));
        }

        // פונקציה שמחזירה רשימת של שמות פרמטרים לשמירת הפריטים של שאלה
        private List<KeyValuePair<string, object>> AnswerStatements(
            int questionId, List<AnswerToEdit> answers, bool deleteFirst = false)
        {
            var list = new List<KeyValuePair<string, object>>();

            if (deleteFirst == true)
            {
                list.Add(new KeyValuePair<string, object>(
                    "DELETE FROM Answers WHERE StageId = @StageId",
                    new { StageId = questionId }));
            }

            string query = "INSERT INTO Answers (StageId, Content, IsImage, CorrectPlace) " +
                           "VALUES (@StageId, @Content, @IsImage, @CorrectPlace)";

            for (int i = 0; i < answers.Count; i++)
            {
                list.Add(new KeyValuePair<string, object>(query, new
                {
                    StageId = questionId,
                    Content = answers[i].Content,
                    IsImage = answers[i].IsImage,
                    CorrectPlace = i
                }));
            }

            return list;
        }


        //פונקציה למחיקת קובץ שהועלה ולא נשמר 
        [HttpDelete("deleteImage/{fileName}")]
        public async Task<IActionResult> DeleteImage(int authUserId, string fileName)
        {
            if (authUserId <= 0)
            {
                return Unauthorized("user is not authenticated");
            }

            if (string.IsNullOrWhiteSpace(fileName) == true)
            {
                return BadRequest("No file name");
            }

            var used = await _db.GetRecordsAsync<int>(
                "SELECT COUNT(*) FROM Answers WHERE Content = @Content AND IsImage = 1",
                new { Content = fileName });

            if (used != null && used.FirstOrDefault() > 0)
            {
                // הקובץ בשימוש. לא שגיאה - פשוט אין מה למחוק
                return Ok("in use");
            }

            _files.DeleteFile(fileName, "uploadedFiles");

            return Ok("deleted");
        }

        // פונקציה שמחזירה את המשחק שאליו שייכת השאלה, או 0 אם אינה קיימת
        private async Task<int> GetQuestionGameId(int questionId)
        {
            var rows = await _db.GetRecordsAsync<int>(
                "SELECT GameId FROM Stages WHERE Id = @ID",
                new { ID = questionId });

            if (rows == null) return 0;

            return rows.FirstOrDefault();
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

            // חישוב תנאי הפרסום של המשחק 
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


 
        //עדכון מצב הפרסום אחרי שינוי בשאלות
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


       // בדיקת עמידה בתנאי הפרסום 
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
