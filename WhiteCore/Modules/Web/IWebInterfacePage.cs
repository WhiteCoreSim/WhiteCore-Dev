using WhiteCore.Framework.Servers.HttpServer;
using System.Collections.Generic;
using WhiteCore.Framework.Servers.HttpServer.Implementation;

namespace WhiteCore.Modules.Web
{
    public interface IWebInterfacePage
    {
        string[] FilePath { get; }
        bool RequiresAuthentication { get; }
        bool RequiresAdminAuthentication { get; }

        Dictionary<string, object> Fill(WebInterface webInterface, string filename, OSHttpRequest request,
                                        OSHttpResponse httpResponse, Dictionary<string, object> requestParameters,
                                        ITranslator translation, out string response);

        bool AttemptFindPage(string filename, ref OSHttpResponse httpResponse, out string text);
    }
}