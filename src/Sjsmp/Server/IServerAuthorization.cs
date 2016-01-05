namespace Sjsmp.Server
{
    public interface IServerAuthorization
    {
        bool CheckAccess(string username, string password);
    }
}
