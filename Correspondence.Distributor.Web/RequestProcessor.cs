﻿using System;
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

        public RequestProcessor()
        {
            _service = new DistributorService(new SqlRepository.Repository());
        }

        public async Task<byte[]> PostAsync(Stream inputStream)
        {
            var reader = new BinaryReader(inputStream);
            BinaryRequest request = BinaryRequest.Read(reader);

            var task =
                TryHandle<GetManyRequest, GetManyResponse>(request, GetMany) ??
                TryHandle<PostRequest, PostResponse>(request, Post) ??
                TryHandle<InterruptRequest, InterruptResponse>(request, Interrupt) ??
                TryHandle<NotifyRequest, NotifyResponse>(request, Notify) ??
                TryHandle<WindowsSubscribeRequest, WindowsSubscribeResponse>(request, WindowsSubscribe) ??
                TryHandle<WindowsUnsubscribeRequest, WindowsUnsubscribeResponse>(request, WindowsUnsubscribe);
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

        private Task<BinaryResponse> TryHandle<TRequest, TResponse>(BinaryRequest request, Func<TRequest, Task<TResponse>> method)
            where TRequest : BinaryRequest
            where TResponse : BinaryResponse
        {
            TRequest specificRequest = request as TRequest;
            if (specificRequest != null)
                return method(specificRequest).ContinueWith(t => (BinaryResponse)t.Result);
            else
                return null;
        }

        private async Task<GetManyResponse> GetMany(GetManyRequest request)
        {
            var pivotIds = request.PivotIds
                .ToDictionary(p => p.FactId, p => p.TimestampId);
            var result = await _service.GetMany(
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

        private async Task<PostResponse> Post(PostRequest request)
        {
            _service.Post(
                request.ClientGuid,
                request.Domain,
                request.MessageBody,
                request.UnpublishedMessages);
            return new PostResponse();
        }

        private async Task<InterruptResponse> Interrupt(InterruptRequest request)
        {
            _service.Interrupt(
                request.ClientGuid,
                request.Domain);
            return new InterruptResponse();
        }

        private async Task<NotifyResponse> Notify(NotifyRequest request)
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

        private async Task<WindowsSubscribeResponse> WindowsSubscribe(WindowsSubscribeRequest request)
        {
            _service.WindowsSubscribe(
                request.ClientGuid,
                request.Domain,
                request.PivotTree,
                request.PivotId,
                request.DeviceUri);
            return new WindowsSubscribeResponse();
        }

        private async Task<WindowsUnsubscribeResponse> WindowsUnsubscribe(WindowsUnsubscribeRequest request)
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