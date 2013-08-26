using System.IO;
using System.Threading.Tasks;
using System.Web;
using Correspondence.Distributor.SqlRepository;

namespace Correspondence.Distributor.Web
{
    public class BinaryHttpHandler : HttpTaskAsyncHandler
    {
        private class HttpResponseData
        {
            public string ContentType { get; set; }
            public byte[] Data { get; set; }
        }

        private RequestProcessor _requestProcessor;

        public BinaryHttpHandler()
        {
            _requestProcessor = new RequestProcessor(new Repository("Correspondence"));
        }

        public override async Task ProcessRequestAsync(HttpContext context)
        {
            var responseData = await GetResponseAsync(context.Request);
            context.Response.ContentType = responseData.ContentType;
            context.Response.OutputStream.Write(responseData.Data, 0, responseData.Data.Length);
        }

        private async Task<HttpResponseData> GetResponseAsync(HttpRequest request)
        {
            if (request.HttpMethod == "POST")
                return new HttpResponseData
                {
                    ContentType = "application/octet-stream",
                    Data = await _requestProcessor.PostAsync(request.InputStream)
                };
            else if (request.HttpMethod == "GET")
                return new HttpResponseData
                {
                    ContentType = "text/html",
                    Data = await _requestProcessor.GetAsync()
                };
            else
                return new HttpResponseData
                {
                    ContentType = "text/html",
                    Data = ReturnError()
                };
        }

        private byte[] ReturnError()
        {
            MemoryStream memory = new MemoryStream();
            using (var writer = new StreamWriter(memory))
            {
                writer.Write("<html><head><title>Correspondence Distributor</title></head><body><h1>Correspondence Distributor</h1>I expected GET or POST.</body></html>");
            }
            return memory.ToArray();
        }
    }
}