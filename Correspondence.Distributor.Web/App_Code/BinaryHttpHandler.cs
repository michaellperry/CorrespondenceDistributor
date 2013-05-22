using System;
using System.IO;
using System.Linq;
using System.Web;
using Correspondence.Distributor.BinaryHttp;
using UpdateControls.Correspondence;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.Web
{
    public class BinaryHttpHandler : IHttpHandler
    {
        private DistributorService _service;

        public BinaryHttpHandler()
        {
            _service = new DistributorService();
        }

        public bool IsReusable
        {
            get { return true; }
        }

        public void ProcessRequest(HttpContext context)
        {
            if (context.Request.HttpMethod == "POST")
                Post(context.Request, context.Response);
            else if (context.Request.HttpMethod == "GET")
                Get(context.Response);
        }

        private void Post(HttpRequest httpRequest, HttpResponse httpResponse)
        {
            var reader = new BinaryReader(httpRequest.InputStream);
            BinaryRequest request = BinaryRequest.Read(reader);

            BinaryResponse response =
                TryHandle<GetManyRequest>(request, GetMany) ??
                TryHandle<PostRequest>(request, Post) ??
                TryHandle<InterruptRequest>(request, Interrupt) ??
                TryHandle<NotifyRequest>(request, Notify) ??
                TryHandle<WindowsSubscribeRequest>(request, WindowsSubscribe) ??
                TryHandle<WindowsUnsubscribeRequest>(request, WindowsUnsubscribe);
            if (response == null)
                throw new CorrespondenceException(String.Format("Unknown request type {0}.", request));
            using (var writer = new BinaryWriter(httpResponse.OutputStream))
            {
                response.Write(writer);
            }
        }

        private void Get(HttpResponse httpResponse)
        {
            using (var writer = new StreamWriter(httpResponse.OutputStream))
            {
                writer.Write("<html><head><title>Correspondence Distributor</title></head><body><h1>Correspondence Distributor</h1></body></html>");
            }
        }

        private BinaryResponse TryHandle<TRequest>(BinaryRequest request, Func<TRequest, BinaryResponse> method)
            where TRequest : BinaryRequest
        {
            TRequest specificRequest = request as TRequest;
            if (specificRequest != null)
                return method(specificRequest);
            else
                return null;
        }

        private GetManyResponse GetMany(GetManyRequest request)
        {
            var pivotIds = request.PivotIds
                .ToDictionary(p => p.FactId, p => p.TimestampId);
            FactTreeMemento factTree = _service.GetMany(
                request.ClientGuid, 
                request.Domain, 
                request.PivotTree, 
                pivotIds, 
                request.TimeoutSeconds);
            return new GetManyResponse
            {
                FactTree = factTree,
                PivotIds = pivotIds
                    .Select(pair => new FactTimestamp
                    {
                        FactId = pair.Key,
                        TimestampId = pair.Value
                    })
                    .ToList()
            };
        }

        private PostResponse Post(PostRequest request)
        {
            _service.Post(
                request.ClientGuid,
                request.Domain,
                request.MessageBody,
                request.UnpublishedMessages);
            return new PostResponse();
        }

        private InterruptResponse Interrupt(InterruptRequest request)
        {
            _service.Interrupt(
                request.ClientGuid,
                request.Domain);
            return new InterruptResponse();
        }

        private NotifyResponse Notify(NotifyRequest request)
        {
            _service.Notify(
                request.ClientGuid,
                request.Domain,
                request.PivotTree,
                request.PivotId,
                request.Text1,
                request.Text2);
            return new NotifyResponse();
        }

        private WindowsSubscribeResponse WindowsSubscribe(WindowsSubscribeRequest request)
        {
            _service.WindowsSubscribe(
                request.ClientGuid,
                request.Domain,
                request.PivotTree,
                request.PivotId,
                request.DeviceUri);
            return new WindowsSubscribeResponse();
        }

        private WindowsUnsubscribeResponse WindowsUnsubscribe(WindowsUnsubscribeRequest request)
        {
            _service.WindowsUnsubscribe(
                request.Domain,
                request.PivotTree,
                request.PivotId,
                request.DeviceUri);
            return new WindowsUnsubscribeResponse();
        }
    }
}