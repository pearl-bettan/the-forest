using Microsoft.JSInterop;

namespace UsersManager.Client;

// ============================================================
// גישה ל-localStorage של הדפדפן מתוך קוד C#.
//
// Blazor WebAssembly אינו יכול לגעת ב-localStorage ישירות, ולכן
// כל פעולה עוברת דרך קריאת JavaScript. הממשק מופרד מהמימוש כדי
// שאפשר יהיה להחליף את מקום האחסון בלי לגעת בקוד שמשתמש בו.
//
// כאן נשמר טוקן ההתחברות, וזו הסיבה שהמשתמש נשאר מחובר
// גם אחרי רענון העמוד
// ============================================================
public interface ILocalStorageService
{
    Task SetItemAsync(string key, string value);
    Task<string> GetItemAsync(string key);
    Task RemoveItemAsync(string key);
}

// המימוש בפועל, מעל IJSRuntime
public class LocalStorageService : ILocalStorageService
{
    private readonly IJSRuntime _jsRuntime;

    public LocalStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task SetItemAsync(string key, string value)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
    }

    public async Task<string> GetItemAsync(string key)
    {
        return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
    }

    public async Task RemoveItemAsync(string key)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
    }
}