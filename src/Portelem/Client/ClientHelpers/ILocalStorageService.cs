using Microsoft.JSInterop;

namespace UsersManager.Client;

// ============================================================
// גישה ל-localStorage של הדפדפן מתוך קוד C#.
//
// Blazor WebAssembly אינו יכול לגעת ב-localStorage ישירות, ולכן
// כל פעולה עוברת דרך קריאת JavaScript. הממשק מופרד מהמימוש כדי
// שאפשר יהיה להחליף את מקום האחסון בלי לגעת בקוד שמשתמש בו.
//
// כאן נשמר טוקן ההתחברות, וזו הסיבה שהמשתמש נשאר מחובר גם
// אחרי רענון העמוד
// ============================================================
public interface ILocalStorageService
{
    // כתיבת ערך תחת מפתח
    Task SetItemAsync(string key, string value);

    // קריאת ערך. מחזיר null כשהמפתח אינו קיים
    Task<string> GetItemAsync(string key);

    // מחיקת מפתח. נקרא בהתנתקות
    Task RemoveItemAsync(string key);
}

// המימוש בפועל, מעל IJSRuntime
public class LocalStorageService : ILocalStorageService
{
    private readonly IJSRuntime _jsRuntime;

    // IJSRuntime הוא הגשר לקריאות JavaScript מתוך Blazor
    public LocalStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    // כותב ל-localStorage של הדפדפן
    public async Task SetItemAsync(string key, string value)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
    }

    // קורא מ-localStorage. מחזיר null כשאין ערך שמור
    public async Task<string> GetItemAsync(string key)
    {
        return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
    }

    // מוחק מפתח מ-localStorage
    public async Task RemoveItemAsync(string key)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
    }
}