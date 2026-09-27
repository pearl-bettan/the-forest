using Microsoft.AspNetCore.Mvc;
using Data;
using ForestGame.Shared.UnityDtos;

namespace AuthTemplate.Server.Controllers
{
    // נקודת הכניסה  של משחק היוניטי אל השרת
   
    [Route("api/[controller]")]
    [ApiController]
    public class UnityController : ControllerBase
    {
        // החיבור לבסיס הנתונים
        private readonly DbRepository _db;

        //מספר הפסילות הקבוע של המשחק
        private const int FixedLives = 3;

        public UnityController(DbRepository db)
        {
            _db = db;
        }

        
        // מחזיר משחק  לפי קוד המשחק:
        [HttpGet("{gameCode}")]
        public async Task<ActionResult<GameForUnityDto>> GetGameByCode(string gameCode)
        {
            // בודק שהתקבל קוד
            if (string.IsNullOrWhiteSpace(gameCode))
            {
                return BadRequest("לא התקבל קוד משחק");
            }

            // בודק שהקוד הוא מספר חיובי
            int codeNumber;

            if (int.TryParse(gameCode, out codeNumber) == false || codeNumber <= 0)
            {
                return BadRequest("קוד המשחק חייב להיות מספר חיובי");
            }

            // בודק שאכן קיים משחק עם הקוד 
            string gameQuery = @"SELECT Id, GameName, IsPublish FROM Games WHERE GameCode = @GameCode";
            var games = await _db.GetRecordsAsync<GameRow>(gameQuery, new { GameCode = codeNumber });
            // אם השאילתה נכשלה, מוחזרת הודעת שגיאה
            if (games == null)
            {
                return StatusCode(500, "שגיאה בגישה לבסיס הנתונים");
            }
            
            GameRow game = games.FirstOrDefault();
            if (game == null)
            {
                return NotFound("משחק לא קיים");
            }

            //בדיקה האם המשחק פורסם
            if (game.IsPublish == false)
            {
                return BadRequest("המשחק אינו מפורסם");
            }

            //שליפת השלבים של המשחק
            string stagesQuery = @"SELECT Id, LeftTag, RightTag, StageTime FROM Stages WHERE GameId = @GameId ORDER BY StageOrder";
            var stageRows = await _db.GetRecordsAsync<StageRow>(stagesQuery, new { GameId = game.Id });

            // בדיקה שקיים תוכן במשחק
            if (stageRows == null || stageRows.Count() == 0)
            {
                return BadRequest("אין שלבים במשחק");
            }

            // בניית ה-DTO 
            GameForUnityDto gameDto = new GameForUnityDto();
            gameDto.GameName = game.GameName;
            //מספר הפסילות קבוע על 3 ואינו ניתן לשינוי 
            gameDto.StartingLives = FixedLives;
            gameDto.Stages = new List<StageForUnityDto>();

            foreach (StageRow stageRow in stageRows)
            {
                StageForUnityDto stageDto = new StageForUnityDto();
                stageDto.LeftTag = stageRow.LeftTag;
                stageDto.RightTag = stageRow.RightTag;
                stageDto.StageTime = stageRow.StageTime;
                stageDto.Answers = new List<AnswerForUnityDto>();

                // התשובות ממויינות לפי המקום הנכון
                string answersQuery = @"SELECT Content, IsImage FROM Answers WHERE StageId = @StageId ORDER BY CorrectPlace";

                var answerRows = await _db.GetRecordsAsync<AnswerForUnityDto>(answersQuery, new { StageId = stageRow.Id });

                if (answerRows != null)
                {
                    foreach (AnswerForUnityDto answer in answerRows)
                    {
                        stageDto.Answers.Add(answer);
                    }
                }

                gameDto.Stages.Add(stageDto);
            }

            return Ok(gameDto);
        }


        // מחלקות עזר
        // מחלקה שמייצגת שורה של משחק מהטבלה 
        private class GameRow
        {
            public int Id { get; set; }
            public string GameName { get; set; }
            public bool IsPublish { get; set; }
        }
        //  מחלקה שמייצגת שורה של שלב מהטבלה
        private class StageRow
        {
            public int Id { get; set; }
            public string LeftTag { get; set; }
            public string RightTag { get; set; }
            public int StageTime { get; set; }
        }
    }
}
