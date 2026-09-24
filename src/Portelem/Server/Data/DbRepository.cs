using System;
using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Data
{
    // ============================================================
    // שכבת הגישה לבסיס הנתונים. כל שאילתה במערכת עוברת כאן.
    //
    // בסיס הנתונים הוא SQLite - קובץ אחד על השרת, בלי שרת
    // בסיס נתונים נפרד. הגישה נעשית דרך Dapper, שממפה את
    // תוצאות השאילתה לאובייקטים לפי שמות העמודות.
    //
    // המחלקה אינה יודעת דבר על משחקים או משתמשים. היא רק
    // מריצה שאילתות ומחזירה תוצאות, וכל הלוגיקה יושבת מעליה
    // ============================================================
	public class DbRepository
	{
        private IDbConnection _dbConnection;

        // מחרוזת החיבור נקראת מ-appsettings ומצביעה על קובץ
        // ה-db. החיבור נוצר כאן אבל אינו נפתח - הפתיחה נעשית
        // לפי שאילתה
        public DbRepository(IConfiguration config)
        {
            _dbConnection = new SqliteConnection(config.GetConnectionString("DefaultConnection"));
        }


        // ============================================================
        // פותח את החיבור אם הוא סגור.
        //
        // ב-SQLite מחיקה מדורגת אינה פעילה כברירת מחדל, וצריך
        // להדליק אותה בכל חיבור מחדש. בלי השורה הזו מחיקת משחק
        // הייתה משאירה את השאלות והפריטים שלו יתומים בבסיס הנתונים
        // ============================================================
        public void OpenConnection()
        {
            //if there is no connection to the DB
            if (_dbConnection.State != ConnectionState.Open)
            {
                //Create new connection to the db
                _dbConnection.Open();

                //enable OnDelete cascade
                using (var command = _dbConnection.CreateCommand())
                {
                    command.CommandText = "PRAGMA foreign_keys = ON;";
                    command.ExecuteNonQuery();
                }
            }
        }



        // סוגר את החיבור. נקרא בסוף כל פעולה, גם כשהיא נכשלה,
        // כדי שקובץ ה-db לא יישאר נעול
        public void CloseConnection()
        {
            _dbConnection.Close();
        }

        // ============================================================
        // מריץ שאילתת שליפה ומחזיר את השורות כאובייקטים מטיפוס T.
        //
        // parameters הוא אובייקט אנונימי שכל מאפיין בו נקשר
        // לפרמטר בשאילתה לפי שמו. כך הערכים אף פעם אינם
        // משורשרים לתוך הטקסט, וזו ההגנה מפני SQL Injection.
        //
        // שים לב: בשגיאה מוחזר null ולא רשימה ריקה, ולכן כל קורא
        // חייב לבדוק null לפני שהוא עובר על התוצאה
        // ============================================================
        //parameters = query parameters
        public async Task<IEnumerable<T>> GetRecordsAsync<T>(string query, object parameters = null)
        {
            try
            {
                OpenConnection();

                if (parameters == null) parameters = new { };

                //Query - Method from dapper
                //Query<RETURN_TYPE>();
                IEnumerable<T> records = await _dbConnection.QueryAsync<T>(query, parameters, commandType: CommandType.Text);

                CloseConnection();
                return records;
            }
            catch (Exception ex)
            {
                CloseConnection();
                return null;
                throw;
            }
        }

        // ============================================================
        // מריץ שאילתת כתיבה - הוספה, עדכון או מחיקה - ומחזיר
        // את מספר השורות שהושפעו.
        //
        // 0 שורות אינו בהכרח שגיאה: הוא גם התשובה כשתנאי ה-WHERE
        // לא התאים לאף שורה, למשל בעדכון משחק של משתמש אחר.
        // חריגה נזרקת הלאה כדי שהקורא יבחין בין כישלון אמיתי
        // לבין עדכון שלא תפס
        // ============================================================
        public async Task<int> SaveDataAsync(string query, object parameters = null)
        {
            try
            {
                OpenConnection();

                if (parameters == null) parameters = new { };

                //Return the amount of saved records 
                int records = await _dbConnection.ExecuteAsync(query, parameters, commandType: CommandType.Text);

                CloseConnection();

                //if one records or more updated - return true. else return false
                return records;
            }
            catch (Exception ex)
            {
                CloseConnection();
                //return 0;
                throw;
                //throw;
            }
        }

        // ============================================================
        // מוסיף שורה ומחזיר את המזהה שנוצר עבורה, או 0 אם ההוספה
        // לא בוצעה.
        //
        // last_insert_rowid של SQLite מחזיר את המזהה האחרון
        // שנוצר באותו חיבור, ולכן חשוב שהשליפה תתבצע לפני
        // הסגירה ועל אותו חיבור בדיוק.
        //
        // בשימוש בהוספת משחק ובהוספת שאלה, ששם צריך את המזהה
        // כדי לקשר אליו את הפריטים
        // ============================================================
        public async Task<int> InsertReturnIdAsync(string query, object parameters = null)
        {
            try
            {
                OpenConnection();

                if (parameters == null) parameters = new { };

                int results = await _dbConnection.ExecuteAsync(sql: query, param: parameters, commandType: CommandType.Text);

                if (results > 0)
                {
                    int Id = _dbConnection.Query<int>("SELECT last_insert_rowid()").FirstOrDefault();
                    CloseConnection();
                    return Id;
                }
                CloseConnection();
                return 0;
            }
            catch (System.Exception)
            {
                CloseConnection();
                //return null;
                throw;

            }
        }

    }
}

