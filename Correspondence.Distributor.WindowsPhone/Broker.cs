using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
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

        public async Task<List<string>> SendToastNotifications(string text1, string text2, IEnumerable<string> subscriberDeviceUris)
        {
            var payload = GetToastPayload(text1, text2);
            var tasks = subscriberDeviceUris
                .Select(deviceUri => SendToastNotification(payload, deviceUri));
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

        private byte[] GetToastPayload(string text1, string text2)
        {
            XDocument document = new XDocument(
                new XDeclaration("1.0", "utf-8", ""),
                new XElement(XName.Get("Notification", "WPNotification"),
                    new XAttribute(XName.Get("ws", XNamespace.Xmlns.NamespaceName), "WPNotification"),
                    new XElement(XName.Get("Toast", "WPNotification"),
                        new XElement(XName.Get("Text1", "WPNotification"),
                            text1
                        ),
                        new XElement(XName.Get("Text2", "WPNotification"),
                            text2
                        )
                    )
                )
            );
            MemoryStream memory = new MemoryStream();
            using (var writer = XmlWriter.Create(memory))
            {
                document.WriteTo(writer);
            }
            var bytes = memory.ToArray();
            return bytes;
        }

        private async Task<bool> SendRawNotification(
            byte[] payload,
            string deviceUri)
        {
            try
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
            catch (Exception x)
            {
                return false;
            }
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

        private async Task<bool> SendToastNotification(byte[] payload, string deviceUri)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(deviceUri);
                request.Method = WebRequestMethods.Http.Post;
                request.ContentType = "text/xml; charset=utf-8";
                request.ContentLength = payload.Length;
                request.Headers["X-NotificationClass"] = "2";
                request.Headers["X-WindowsPhone-Target"] = "toast";

                using (Stream requestStream = await request.GetRequestStreamAsync())
                {
                    requestStream.Write(payload, 0, payload.Length);
                    requestStream.Close();
                }

                var response = (HttpWebResponse)await request.GetResponseAsync();
                return 200 <= (int)response.StatusCode && (int)response.StatusCode < 300;
            }
            catch (Exception x)
            {
                return false;
            }
        }
    }
}
