namespace UsersManager.Server
{    
    public interface ITokenBlacklistService
    {
        void AddToBlacklist(string token);

        bool IsBlacklisted(string token);

        void RemoveExpiredTokens();
    }
}
