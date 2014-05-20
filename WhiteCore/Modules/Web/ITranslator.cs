namespace WhiteCore.Modules.Web
{
    public interface ITranslator
    {
        string LanguageName { get; }
        string GetTranslatedString(string key);
    }
}