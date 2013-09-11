using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Correspondence.Distributor.BinaryHttp;
using UpdateControls.Correspondence.FieldSerializer;
using UpdateControls.Correspondence.Mementos;

namespace Correspondence.Distributor.WindowsPhone
{
    public class Broker : IBroker
    {
 	    private const int RawNotificationSize = 1024;

        public async Task<List<string>> SendPushNotifications(FactTreeMemento factTree, IEnumerable<string> subscriberDeviceUris)
        {
            var payload = GetRawPayload(factTree);
            var tasks = subscriberDeviceUris
                .Select(deviceUri => SendRawNotification(payload, deviceUri));
            bool[] succeses = await Task.WhenAll(tasks);
            var failedDeviceUris = subscriberDeviceUris
                .Zip(succeses, (deviceUri, success) => new { deviceUri, success })
                .Where(pair => !pair.success)
                .Select(pair => pair.deviceUri)
                .ToList();
            return failedDeviceUris;
        }

        private byte[] GetRawPayload(FactTreeMemento factTree)
        {
            byte[] bytes = SerializeRawMessage(factTree);

            // If the message is too big, just send an empty tree.
            if (bytes.Length > RawNotificationSize)
            {
                bytes = SerializeRawMessage(new FactTreeMemento(factTree.DatabaseId));
            }
            return bytes;
        }

        private async Task<bool> SendRawNotification(
            byte[] payload,
            string deviceUri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(deviceUri);
            request.Method = WebRequestMethods.Http.Post;
            request.ContentType = "text/xml; charset=utf-8";
            request.ContentLength = payload.Length;
            request.Headers["X-NotificationClass"] = "3";

            using (Stream requestStream = await request.GetRequestStreamAsync())
            {
                requestStream.Write(payload, 0, payload.Length);
                requestStream.Close();
            }

            var response = (HttpWebResponse)await request.GetResponseAsync();
            return 200 <= (int)response.StatusCode && (int)response.StatusCode < 300;
        }

        private static byte[] SerializeRawMessage(FactTreeMemento factTree)
        {
            MemoryStream memory = new MemoryStream();
            using (var factWriter = new BinaryWriter(memory))
            {
                byte version = 1;
                BinaryHelper.WriteByte(version, factWriter);
                new FactTreeSerializer().SerlializeFactTree(factTree, factWriter);
                factWriter.Flush();
            }
            var bytes = memory.ToArray();
            return bytes;
        }
    }
}
