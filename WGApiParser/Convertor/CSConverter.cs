using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGApiParser.Model;

namespace WGApiParser.Convertor
{
    public class CSConverter
    {
        public string CreateCSModels(IEnumerable<MethodItem> methodItems)
        {

            var sb = new StringBuilder();
            sb.AppendLine(@"using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WGClient.Attributes;

namespace WGClient.Attributes
{
    public class MethodAttribute : Attribute
    {
        public string Url { get; set; }
    }

    public class FieldIsMandatoryAttribute : Attribute
    {
    }

    public class DescriptionUrlAttribute : Attribute
    {
        public string Url { get; set; }
    }
}

namespace WGClient
{
    public interface IRequest
    {

    }

    public interface IResponse
    {

    }

    public class Client
    {
        public Task<Dictionary<string,TResponse>> SendRequestDictionary<TResponse>(IRequest request) where TResponse : IResponse
        {
            return BaseSendRequest<Dictionary<string,TResponse>>(request);
        }

        public Task<List<TResponse>> SendRequestArray<TResponse>(IRequest request) where TResponse : IResponse
        {
            return BaseSendRequest<List<TResponse>>(request);
        }

        public Task<TResponse> SendRequest<TResponse>(IRequest request) where TResponse : IResponse
        {
            return BaseSendRequest<TResponse>(request);
        }

        private async Task<TResponse> BaseSendRequest<TResponse>(IRequest request)
        {
            var client = new HttpClient();
            var requestUrl =""http://""+request.GetType().GetTypeInfo().GetCustomAttribute<MethodAttribute>().Url;
            var requestBody =GetBody(request);
            var httpContent = new StringContent(requestBody,Encoding.UTF8, ""application/x-www-form-urlencoded"");
            
            var requestMessage=new HttpRequestMessage(HttpMethod.Post, requestUrl);
            requestMessage.Content = httpContent;
            var responseMessage=await client.SendAsync(requestMessage);
            var response= await responseMessage.Content.ReadAsStringAsync();

            var responseBody=JsonConvert.DeserializeObject<ResponseBody<TResponse>>(response);

            if (responseBody.Status == ""ok"")
            {
                return responseBody.Data;
            }
            if (responseBody.Status == ""error"")
            {
                var error = responseBody.Error;
                var message = $""Field:{error.Field}  Message:{error.Message}  Value:{error.Value}  Code:{error.Code}"";
                throw new ResponseException(message)
                {
                    Error = error,
                    
                };
            }
            return default(TResponse);
        }

        private string GetBody(IRequest request)
        {
            var list = new List<string>();
            
            foreach (var propertyInfo in request.GetType().GetRuntimeProperties())
            {
                var propertyName = propertyInfo.Name;
                var jsonPropertyAttribute = propertyInfo.GetCustomAttribute<JsonPropertyAttribute>();
                if (jsonPropertyAttribute != null)
                {
                    propertyName = jsonPropertyAttribute.PropertyName;
                }
                var value =((string) propertyInfo.GetValue(request))??string.Empty;
                
                list.Add($""{propertyName}={value}"");
                
                if (string.IsNullOrEmpty(value))
                {
                    var isMandatory = propertyInfo.GetCustomAttribute<FieldIsMandatoryAttribute>();
                    if (isMandatory != null)
                    {
                        var helpUrl = request.GetType().GetTypeInfo().GetCustomAttribute<DescriptionUrlAttribute>().Url;
                        throw new ArgumentException($""Обязательное поле {propertyInfo.Name} не указано. Смотрите подробности {helpUrl}"");
                    }
                }
                
            }
            return string.Join(""&"",list);
        }

        public class ResponseException : Exception
        {
            public ResponseException(string message) : base(message)
            {
            }

            public Error Error { get; set; }
        }

        public class ResponseBody<T>
        {
            [JsonProperty(""status"")]
            public string Status { get; set; }

            [JsonProperty(""meta"")]
            public Meta Meta { get; set; }

            [JsonProperty(""data"")]
            public T Data { get; set; }

            [JsonProperty(""error"")]
            public Error Error { get; set; }
        }

        public class Meta
        {
            [JsonProperty(""count"")]
            public int Count { get; set; }
        }

        public class Error
        {
            [JsonProperty(""field"")]
            public string Field { get; set; }

            [JsonProperty(""message"")]
            public string Message { get; set; }

            [JsonProperty(""code"")]
            public string Code { get; set; }

            [JsonProperty(""value"")]
            public string Value { get; set; }
        }
    }
}");
            var methodItemGroups=new Dictionary<string, List<MethodItem>>();
            foreach (var methodItem in methodItems)
            {
                var ns="WGClient."+GetNormalizedName(methodItem.MethodLink.ProjectName.Trim().Split(new[] {' '},StringSplitOptions.RemoveEmptyEntries));
                if (!methodItemGroups.ContainsKey(ns))
                {
                    methodItemGroups[ns]=new List<MethodItem>();
                }
                methodItemGroups[ns].Add(methodItem);
            }


            foreach (var group in methodItemGroups)
            {
                var csharpClass = GetNamespaceModel(group.Key,group.Value);
                sb.Append(csharpClass);
            }
            return sb.ToString();
        }
        
        private string GetNamespaceModel(string clientNamespace, List<MethodItem> methods)
        {
            int tab = 0;
            var sb = new StringBuilder();
            AppendLine(sb, tab, "namespace "+clientNamespace);
            AppendLine(sb, tab, "{");
            
            foreach (var methodItem in methods)
            {
                var rootModel = GetRootModel(tab,methodItem);
                AppendLine(sb, tab, rootModel);
            }
            
            AppendLine(sb, tab, "}");
            sb.AppendLine("");
            return sb.ToString();
        }

        
        private string GetRootModel(int tab,MethodItem methodItem)
        {
            var sb = new StringBuilder();
            tab++;
            var className = methodItem.RequestUri.Split('/').Skip(1).ToList();
            
            AppendLine(sb, tab, "///<summary>");
            if (!string.IsNullOrWhiteSpace(methodItem.AlertText))
            {
                AppendLine(sb, tab, "/// " + methodItem.AlertText);
            }
            AppendLine(sb, tab, "/// " + methodItem.MethodName);
            AppendLine(sb, tab, "/// "+methodItem.DescriptionUrl);
            
            AppendLine(sb, tab, "///</summary>");
            AppendLine(sb, tab, $@"[Method(Url=""{methodItem.RequestUri}/"")]");
            AppendLine(sb, tab, $@"[DescriptionUrl(Url= ""{methodItem.DescriptionUrl}"")]");

            if (methodItem.AlertText?.Trim() == "Внимание! Метод будет отключён.")
            {
                AppendLine(sb, tab, $@"[Obsolete]");
            }
            AppendLine(sb, tab, "public class Request" + GetNormalizedName(className)+ ":IRequest");
            AppendLine(sb, tab, "{");
            tab++;
            foreach (var requestField in methodItem.RequestFields)
            {
                if (requestField.FieldDescription.Contains("Внимание! Поле будет отключено."))
                {
                    continue;
                }
                var fieldName = GetNormalizedName(requestField.FieldName.Split(new[] { ',', '_' }, StringSplitOptions.RemoveEmptyEntries));
                bool isRequered = false;
                sb.AppendLine();
                AppendLine(sb, tab, "///<summary>");
                if (fieldName.StartsWith("*"))
                {
                    AppendLine(sb, tab, "///Обязательный параметер");
                    fieldName = fieldName.Substring(1);
                    isRequered = true;
                }
                AppendLine(sb, tab, "///" + requestField.FieldDescription.Trim().Replace("\r\n", "\r\n///").Replace("\n", "\n///"));
                AppendLine(sb, tab, "///" + requestField.FieldType);
                AppendLine(sb, tab, "///</summary>");

                var jsonFieldName = requestField.FieldName;
                if (jsonFieldName.StartsWith("*"))
                {
                    jsonFieldName = jsonFieldName.Substring(1);
                }

                AppendLine(sb, tab, $@"[JsonProperty(""{jsonFieldName}"")]");
                if (isRequered)
                {
                    AppendLine(sb,tab, "[FieldIsMandatory]");
                }
                Append(sb, tab, "public ");
                sb.Append("string");
                sb.Append(" ");
                sb.Append(GetNormalizedName(new[] { fieldName}));
                sb.AppendLine(" {get; set;}");
            }
            tab--;
            AppendLine(sb, tab, "}");
            AppendLine(sb, tab, "");
            
            AppendLine(sb,tab,"///<summary>");
            AppendLine(sb,tab,"///" + methodItem.MethodName);
            AppendLine(sb,tab,"///</summary>");
            AppendLine(sb,tab,"public class Response" + GetNormalizedName(className)+ ":IResponse");
            AppendLine(sb,tab,"{");
            tab++;
            foreach (var responseField in methodItem.RootResponse.ResponseFieldItems)
            {
                if (responseField.FieldDescription.Contains("Внимание! Поле будет отключено."))
                {
                    continue;
                }
                var fieldName = GetNormalizedName(responseField.FieldName.Split(new[] { ',', '_' }, StringSplitOptions.RemoveEmptyEntries));

                sb.AppendLine();
                AppendLine(sb,tab,"///<summary>");
               
                AppendLine(sb,tab,"///" + responseField.FieldDescription.Trim().Replace("\r\n", "\r\n///").Replace("\n", "\n///"));
                AppendLine(sb,tab,"///</summary>");
                AppendLine(sb,tab,$@"[JsonProperty(""{responseField.FieldName}"")]");
                Append(sb,tab,"public ");
                sb.Append(GetTypeString(responseField.FieldType));
                sb.Append(" ");
                
                sb.Append(fieldName);
                sb.AppendLine(" {get; set;}");
            }

            var subClass = new StringBuilder();

            foreach (var chieldClass in methodItem.RootResponse.ResponseClasses.Values)
            {
                if (chieldClass.ClassDescription.Contains("Внимание! Поле будет отключено."))
                {
                    continue;
                }

                var createClassModel = GetClass(GetNormalizedName(className),chieldClass);
                var typeName = createClassModel.Item1;
                var classModel = createClassModel.Item2;
                subClass.Append(classModel);


                var fieldName = GetNormalizedName(chieldClass.ClassName.Split(new[] { ',', '_' }, StringSplitOptions.RemoveEmptyEntries));

                sb.AppendLine();
                AppendLine(sb, tab, "///<summary>");

                AppendLine(sb, tab, "///" + chieldClass.ClassDescription.Trim().Replace("\r\n", "\r\n///").Replace("\n", "\n///"));
                AppendLine(sb, tab, "///</summary>");
                AppendLine(sb, tab, $@"[JsonProperty(""{chieldClass.ClassName}"")]");
                Append(sb, tab, "public ");
                sb.Append(typeName);
                sb.Append(" ");

                sb.Append(fieldName);
                sb.AppendLine(" {get; set;}");
            }

            tab--;
            AppendLine(sb,tab,"}");
            tab--;
            AppendLine(sb,tab,subClass.ToString());

            return sb.ToString();
        }

        private Tuple<string, string> GetClass(string prefix,ResponseClass classItem)
        {
            var modelTypeName = prefix+GetNormalizedName(new[] { classItem.ClassName});

            int tab = 1;
            var sb=new StringBuilder();

            AppendLine(sb, tab, "public class " + modelTypeName);
            AppendLine(sb, tab, "{");
            tab++;
            foreach (var responseField in classItem.ResponseFieldItems)
            {
                if (responseField.FieldDescription.Contains("Внимание! Поле будет отключено."))
                {
                    continue;
                }

                var fieldName = GetNormalizedName(responseField.FieldName.Split(new[] { ',', '_' }, StringSplitOptions.RemoveEmptyEntries));

                sb.AppendLine();
                AppendLine(sb, tab, "///<summary>");

                AppendLine(sb, tab, "///" + responseField.FieldDescription.Trim().Replace("\r\n", "\r\n///").Replace("\n", "\n///"));
                AppendLine(sb, tab, "///</summary>");
                AppendLine(sb, tab, $@"[JsonProperty(""{responseField.FieldName}"")]");
                Append(sb, tab, "public ");
                sb.Append(GetTypeString(responseField.FieldType));
                sb.Append(" ");

                sb.Append(fieldName);
                sb.AppendLine(" {get; set;}");
            }

            var subClass = new StringBuilder();

            foreach (var chieldClass in classItem.ResponseClasses.Values)
            {
                if (chieldClass.ClassDescription.Contains("Внимание! Поле будет отключено."))
                {
                    continue;
                }

                var createClassModel = GetClass(modelTypeName, chieldClass);
                var typeName = createClassModel.Item1;
                var classModel = createClassModel.Item2;
                subClass.Append(classModel);


                var fieldName = GetNormalizedName(chieldClass.ClassName.Split(new[] { ',', '_' }, StringSplitOptions.RemoveEmptyEntries));

                sb.AppendLine();
                AppendLine(sb, tab, "///<summary>");

                AppendLine(sb, tab, "///" + chieldClass.ClassDescription.Trim().Replace("\r\n", "\r\n///").Replace("\n", "\n///"));
                AppendLine(sb, tab, "///</summary>");
                AppendLine(sb, tab, $@"[JsonProperty(""{chieldClass.ClassName}"")]");
                Append(sb, tab, "public ");
                sb.Append(typeName);
                sb.Append(" ");

                sb.Append(fieldName);
                sb.AppendLine(" {get; set;}");
            }

            tab--;
            AppendLine(sb, tab, "}");
            tab--;
            AppendLine(sb, tab, subClass.ToString());
            
            return new Tuple<string, string>(modelTypeName, sb.ToString());
        }

        private void AppendLine(StringBuilder sb, int tab, string content)
        {
            for (var i = 0; i < 4*tab; i++)
            {
                sb.Append(" ");
            }
            sb.AppendLine(content);
        }

        private void Append(StringBuilder sb, int tab, string content)
        {
            for (var i = 0; i < 4 * tab; i++)
            {
                sb.Append(" ");
            }
            sb.Append(content);
        }

        private string GetTypeString(string fieldType)
        {
            if (!WGTypeToCSType.ContainsKey(fieldType))
            {
                throw new ArgumentOutOfRangeException("Неизвестный тип данных: "+fieldType);
            }
            return WGTypeToCSType[fieldType];
        }

        public CSConverter()
        {
            //req: string
            //req: string, list
            //req: numeric
            //res: numeric
            //res: string
            //req: numeric, list
            //res: timestamp
            //res: list of integers
            //res: boolean
            //res: associative array
            //res: float
            //res: list of strings
            //res: list of timestamps
            //req: timestamp / date
            //res: список словарей
            WGTypeToCSType.Add("string", "string");
            WGTypeToCSType.Add("string, list", "string[]");
            WGTypeToCSType.Add("numeric", "Int64?");
            WGTypeToCSType.Add("numeric, list", "Int64[]");
            WGTypeToCSType.Add("timestamp", "int?");
            WGTypeToCSType.Add("list of integers", "int[]");
            WGTypeToCSType.Add("boolean", "bool");
            WGTypeToCSType.Add("associative array", "Dictionary<string,string>");
            WGTypeToCSType.Add("float", "double");
            WGTypeToCSType.Add("list of strings", "string[]");
            WGTypeToCSType.Add("list of timestamps", "int[]");
            WGTypeToCSType.Add("timestamp/date", "int?");
            WGTypeToCSType.Add("список словарей", "Dictionary<string,string>");
            
        }

        Dictionary<string,string> WGTypeToCSType=new Dictionary<string, string>();

        private string GetNormalizedName(IEnumerable<string> nameItems)
        {
            var sb = new StringBuilder();
            foreach (var name in nameItems)
            {
                if (!String.IsNullOrWhiteSpace(name))
                {
                    sb.Append(Char.ToUpper(name[0]));
                    sb.Append(name.Substring(1));
                }
            }
            return sb.ToString();
        }
    }
}
