using System;
using System.Collections.Generic;

namespace WGApiParser.Model
{
    public class ResponseClass
    {
        public ResponseClass()
        {
            ResponseFieldItems=new List<ResponseFieldItem>();
            ResponseClasses=new Dictionary<string, ResponseClass>();
        }

        public string ClassName { get; set; }
        public string ClassDescription { get; set; }
        public List<ResponseFieldItem> ResponseFieldItems { get; set; }

        public Dictionary<string, ResponseClass> ResponseClasses { get; set; }
    }
}