using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Correspondence.Distributor.Test
{
    [TestClass]
    public class XmlTest
    {
        [TestMethod]
        public void CanSerializeNotificationMessage()
        {
            var text1 = "This is a notification message.";
            var text2 = "It's recipients will <3 it.";
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

            var str = document.ToString();
            Assert.AreEqual(
                @"<ws:Notification xmlns:ws=""WPNotification"">" +
                Environment.NewLine +
                @"  <ws:Toast>" +
                Environment.NewLine +
                @"    <ws:Text1>This is a notification message.</ws:Text1>" +
                Environment.NewLine +
                @"    <ws:Text2>It's recipients will &lt;3 it.</ws:Text2>" +
                Environment.NewLine +
                @"  </ws:Toast>" +
                Environment.NewLine +
                @"</ws:Notification>", str);
        }
    }
}
