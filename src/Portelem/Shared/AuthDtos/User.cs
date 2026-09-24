using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsersManager.Shared
{
    // ============================================================
    // המשתמש המחובר, כפי שהלקוח מכיר אותו.
    //
    // נוצר בשרת מתוך הטוקן של פורטלם ונשלח ללקוח בהתחברות.
    // המחלקה יושבת ב-Shared כדי ששני הצדדים יעבדו על אותו מבנה
    // בלי להעתיק אותו פעמיים
    // ============================================================
    public class User
    {
        // המזהה בטבלת Users של המחולל, לא המזהה של פורטלם.
        // כל המשחקים נשמרים תחת המזהה הזה
        public int Id { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        // הדוא"ל מפורטלם. משמש לזיהוי משתמש שחזר במחשב אחר,
        // ולכן ההשוואה עליו בשרת מתבצעת בלי רגישות לאותיות גדולות
        public string Email { get; set; }
    }



}
