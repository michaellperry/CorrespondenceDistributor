using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Correspondence.Distributor.BinaryHttp;
using UpdateControls.Correspondence;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.Web
{
    public class RequestProcessor
    {
        private DistributorService _service;

        public RequestProcessor(IRepository repository)
        {
            _service = new DistributorService(repository);
        }

        public async Task<byte[]> PostAsync(Stream inputStream)
        {
            var reader = new BinaryReader(inputStream);
            BinaryRequest request = BinaryRequest.Read(reader);

            var task =
                TryHandleAsync<GetManyRequest, GetManyResponse>(request, GetManyAsync) ??
                TryHandleAsync<PostRequest, PostResponse>(request, PostAsync) ??
                TryHandleAsync<InterruptRequest, InterruptResponse>(request, InterruptAsync) ??
                TryHandleAsync<NotifyRequest, NotifyResponse>(request, NotifyAsync) ??
                TryHandleAsync<WindowsPhoneSubscribeRequest, WindowsPhoneSubscribeResponse>(request, WindowsPhoneSubscribeAsync) ??
                TryHandleAsync<WindowsPhoneUnsubscribeRequest, WindowsPhoneUnsubscribeResponse>(request, WindowsPhoneUnsubscribeAsync);
            if (task == null)
                throw new CorrespondenceException(String.Format("Unknown request type {0}.", request));
            BinaryResponse response = await task;

            MemoryStream memory = new MemoryStream();
            using (var writer = new BinaryWriter(memory))
            {
                response.Write(writer);
            }
            return memory.ToArray();
        }

        public async Task<byte[]> GetAsync()
        {
            MemoryStream memory = new MemoryStream();
            using (var writer = new StreamWriter(memory))
            {
                writer.Write("<html><head><title>Correspondence Distributor</title></head><body><h1>Correspondence Distributor</h1></body></html>");
                await writer.FlushAsync();
                return memory.ToArray();
            }
        }

        private Task<BinaryResponse> TryHandleAsync<TRequest, TResponse>(BinaryRequest request, Func<TRequest, Task<TResponse>> method)
            where TRequest : BinaryRequest
            where TResponse : BinaryResponse
        {
            TRequest specificRequest = request as TRequest;
            if (specificRequest != null)
                return method(specificRequest).ContinueWith(t => (BinaryResponse)t.Result);
            else
                return null;
        }

        private async Task<GetManyResponse> GetManyAsync(GetManyRequest request)
        {
            var pivotIds = request.PivotIds
                .ToDictionary(p => p.FactId, p => p.TimestampId);
            var result = await _service.GetManyAsync(
                request.ClientGuid,
                request.Domain,
                request.PivotTree,
                pivotIds,
                request.TimeoutSeconds);
            FactTreeMemento factTree = result.Tree;
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

        private async Task<PostResponse> PostAsync(PostRequest request)
        {
            _service.Post(
                request.ClientGuid,
                request.Domain,
                request.MessageBody,
                request.UnpublishedMessages);
            return new PostResponse();
        }

        private async Task<InterruptResponse> InterruptAsync(InterruptRequest request)
        {
            _service.Interrupt(
                request.ClientGuid,
                request.Domain);
            return new InterruptResponse();
        }

        private async Task<NotifyResponse> NotifyAsync(NotifyRequest request)
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

        private async Task<WindowsPhoneSubscribeResponse> WindowsPhoneSubscribeAsync(WindowsPhoneSubscribeRequest request)
        {
            _service.WindowsPhoneSubscribe(
                request.ClientGuid,
                request.Domain,
                request.PivotTree,
                request.PivotId,
                request.DeviceUri);
            return new WindowsPhoneSubscribeResponse();
        }

        private async Task<WindowsPhoneUnsubscribeResponse> WindowsPhoneUnsubscribeAsync(WindowsPhoneUnsubscribeRequest request)
        {
            _service.WindowsPhoneUnsubscribe(
                request.Domain,
                request.PivotTree,
                request.PivotId,
                request.DeviceUri);
            return new WindowsPhoneUnsubscribeResponse();
        }
    }
}