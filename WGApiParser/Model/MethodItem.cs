using System.Collections.Generic;

namespace WGApiParser.Model
{
    public class MethodItem
    {
        public MethodItem()
        {
            RequestFields=new List<RequestFieldItem>();
            RootResponse=new ResponseClass();
        }

        public string MethodName { get; set; }
        public string DescriptionPath { get; set; }
        public string AlertText { get; set; }
        public string DescriptionUrl { get; set; }
        public string RequestUri { get; set; }
        public string SupportedProtocol { get; set; }
        public string SupportedHttpMethod { get; set; }

        public List<RequestFieldItem> RequestFields { get; set; }
        
        public ResponseClass RootResponse { get; set; }
        
        public MethodLinkItem MethodLink { get; set; }
    }
}