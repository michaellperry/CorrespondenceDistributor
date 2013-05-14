using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Correspondence.Distributor.Web
{
    public class BinaryHttpHandler : IHttpHandler
    {
        public bool IsReusable
        {
            get { return true; }
        }

        public void ProcessRequest(HttpContext context)
        {
            context.Response.Write("I'm here!");
        }
    }
}