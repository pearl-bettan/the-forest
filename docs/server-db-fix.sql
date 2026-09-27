-- ============================================================
-- תיקוני בסיס הנתונים שצריך להריץ על השרת (129.159.134.156)
-- על הקובץ FinalProjectDB.db שנמצא לצד קובץ הרצת השרת.
--
-- לפני ההרצה: לעצור את השרת ולהעתיק את קובץ ה-db לגיבוי.
-- להרצה:  sqlite3 FinalProjectDB.db < server-db-fix.sql
-- ============================================================


-- ============================================================
-- 1. מחיקת עמודת Topic מטבלת השלבים
--
-- נוסח השאלה ("TopicText") הוסר מהמשחק ומהמחולל, והעמודה אינה
-- בשימוש. ALTER TABLE DROP COLUMN היה משאיר את הערות התיעוד
-- של הטבלה מעורבבות, ולכן הטבלה נבנית מחדש.
--
-- ההזדמנות משמשת גם לתיקון שתי הערות שהיו הפוכות: LeftTag היא
-- סוף הסדר ו-RightTag היא תחילתו, ולא להפך
-- ============================================================

PRAGMA foreign_keys = OFF;

BEGIN TRANSACTION;

CREATE TABLE "Stages_new"
(
    "Id"         INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,

    -- מפתח זר לטבלת המשחקים
    "GameId"     INTEGER NOT NULL,

    -- התגית שמשמאל לאגם - מסמנת את סוף הסדר
    "LeftTag"    TEXT    NOT NULL,

    -- התגית שמימין לאגם - מסמנת את תחילת הסדר
    "RightTag"   TEXT    NOT NULL,

    -- זמן השלב בשניות. 0 = ללא הגבלת זמן
    "StageTime"  INTEGER NOT NULL DEFAULT 60,

    -- סדר השלב בתוך המשחק, לתצוגה ולעריכה במחולל
    "StageOrder" INTEGER NOT NULL DEFAULT 1,

    -- מחיקת משחק תמחק את כל השלבים שלו
    CONSTRAINT "fk_Stages_Games" FOREIGN KEY ("GameId")
        REFERENCES "Games" ("Id")
        ON DELETE CASCADE
        ON UPDATE CASCADE
);

INSERT INTO "Stages_new" (Id, GameId, LeftTag, RightTag, StageTime, StageOrder)
SELECT Id, GameId, LeftTag, RightTag, StageTime, StageOrder FROM "Stages";

DROP TABLE "Stages";

ALTER TABLE "Stages_new" RENAME TO "Stages";

COMMIT;

-- החזרת מונה המפתח האוטומטי למקום שבו היה
UPDATE sqlite_sequence
SET    seq = (SELECT COALESCE(MAX(Id), 0) FROM Stages)
WHERE  name = 'Stages'
       AND seq < (SELECT COALESCE(MAX(Id), 0) FROM Stages);

PRAGMA foreign_keys = ON;


-- ============================================================
-- 2. סדר שאלות כפול
--
-- במשחקים ישנים נשארו שתי שאלות עם אותו StageOrder (למשל
-- משחק 19, שתי שאלות עם StageOrder = 3), תוצאה של מחיקת שאלה
-- לפני התיקון בקוד. הקוד כבר ממיין משנית לפי Id כדי שהסדר
-- יהיה יציב, וכאן מסדרים את הנתונים עצמם מחדש: 1, 2, 3...
-- לכל משחק, לפי הסדר הקיים
-- ============================================================

-- הסדר החדש מחושב תחילה לטבלה זמנית. חישוב ישירות בתוך ה-UPDATE
-- היה קורא את הטבלה בזמן שהיא משתנה, והדירוג היה יוצא שגוי
CREATE TEMP TABLE FixOrder AS
SELECT Id,
       ROW_NUMBER() OVER (PARTITION BY GameId ORDER BY StageOrder, Id) AS NewOrder
FROM   Stages;

UPDATE Stages
SET    StageOrder = (SELECT NewOrder FROM FixOrder WHERE FixOrder.Id = Stages.Id);

DROP TABLE FixOrder;

-- בדיקה: השאילתה הזאת צריכה לחזור ריקה
SELECT GameId, StageOrder, COUNT(*) AS Duplicates
FROM   Stages
GROUP  BY GameId, StageOrder
HAVING COUNT(*) > 1;


-- ============================================================
-- 3. שם משחק פגום
--
-- שם המשחק 1001 נשמר כ-"פריטיםכענמיעכ" ואינו קריא. אין דרך
-- לשחזר את השם המקורי, ולכן צריך להזין שם חדש במקום הסוגריים
-- שלמטה, או לשנות אותו מתוך המחולל
-- ============================================================

-- UPDATE Games SET GameName = 'שם חדש' WHERE GameCode = 1001;

SELECT Id, GameCode, GameName FROM Games WHERE GameCode = 1001;
