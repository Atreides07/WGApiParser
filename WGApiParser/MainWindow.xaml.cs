using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using CefSharp;
using WGApiParser.Convertor;
using WGApiParser.Model;

namespace WGApiParser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void MyWebBrowser_OnFrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            //var result=await MyWebBrowser.EvaluateScriptAsync()
            await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () =>
            {
                if (allMethodLinks == null)
                {
                    var allinks = await GetAllMethods();
                    AllMethodItems.Clear();
                    allMethodLinks = allinks;
                    MethodLinkItemIndex = -1;
                    //MethodLinkItemIndex = 130;
                    ParseProgressBar.Minimum = 0;
                    ParseProgressBar.Maximum = allinks.Count-1;
                    ParseProgressBar.Value = 0;
                    ParseProgressLabel.Content = "0%";
                }
                else
                {
                    var methodItem = await ExtractMethodItem();
                    methodItem.MethodLink = allMethodLinks[MethodLinkItemIndex];
                    AllMethodItems.Add(methodItem);
                }


                if (MethodLinkItemIndex<allMethodLinks.Count-1)
                {
                    MethodLinkItemIndex++;
                    ParseProgressBar.Value = MethodLinkItemIndex;
                    ParseProgressLabel.Content = MethodLinkItemIndex*100/ allMethodLinks.Count+"%";

                    var item = allMethodLinks[MethodLinkItemIndex];
                    MyWebBrowser.Address = item.MethodUrl;
                }
                else
                {
                    ParseProgressLabel.Content = "";
                    try
                    {
                        CSConverter csConverter = new CSConverter();
                        ResultTextBox.Text = csConverter.CreateCSModels(AllMethodItems);
                    }
                    catch (Exception exp)
                    {
                        MessageBox.Show(exp.Message);
                    }
                    
                    //ResultTextBox.Text = string.Join("\r\n", AllHashTypes.ToArray());

                }

            }));


        }
        

        private int MethodLinkItemIndex;
        private List<MethodLinkItem> allMethodLinks;
        private List<MethodItem> AllMethodItems=new List<MethodItem>();
        private async Task<List<MethodLinkItem>>  GetAllMethods()
        {
            var allLinks = new List<MethodLinkItem>();

            var projectCount = await GetFromJS<int>("$('body > div > div.b-content.clearfix > div.b-sidebar > ul > li').length");
            for (var i = 1; i <= projectCount; i++)
            {
                var projectName = await GetFromJquerySelector($"body > div > div.b-content.clearfix > div.b-sidebar > ul > li:nth-child({i}) > a ");

                var projectGroupCoung = await GetFromJS<int>($"$('body > div > div.b-content.clearfix > div.b-sidebar > ul > li:nth-child({i}) > ul > li').length");
                for (var g = 1; g <= projectGroupCoung; g++)
                {
                    var groupName = await GetFromJquerySelector($"body > div > div.b-content.clearfix > div.b-sidebar > ul > li:nth-child({i}) > ul > li:nth-child({g}) > a");
                    
                    var methodCount = await GetFromJS<int>($"$('body > div > div.b-content.clearfix > div.b-sidebar > ul > li:nth-child({i}) > ul > li:nth-child({g}) > ul > li').length");
                    for (var m = 1; m <= methodCount; m++)
                    {
                        var methodName = await GetFromJquerySelector($"body > div > div.b-content.clearfix > div.b-sidebar > ul > li:nth-child({i}) > ul > li:nth-child({g}) > ul > li:nth-child({m}) > a");
                        var methodUrl = await GetFromJS<string>($"$('body > div > div.b-content.clearfix > div.b-sidebar > ul > li:nth-child({i}) > ul > li:nth-child({g}) > ul > li:nth-child({m}) > a')[0].href");

                        if (methodName == "Вход по OpenID")
                        {
                            continue;
                        }

                        allLinks.Add(new MethodLinkItem()
                        {
                            ProjectName = projectName,
                            GroupName = groupName,
                            MethodName = methodName,
                            MethodUrl = methodUrl
                        });
                    }
                }
            }
            return allLinks;
        }

        HashSet<string> AllHashTypes=new HashSet<string>();

        public async Task<MethodItem> ExtractMethodItem()
        {
            var methodItem = new MethodItem();
            methodItem.DescriptionUrl = MyWebBrowser.Address;

            methodItem.MethodName = await GetFromJquerySelector("#name");
            methodItem.DescriptionPath = await GetFromJquerySelector("body>div>div.b-content.clearfix>div.b-content-column>div.b-page-header.js-page-header>div>span");
            methodItem.AlertText = await GetFromJquerySelector("body > div > div.b-content.clearfix > div.b-content-column > div.b-maincontent.clearfix.js-maincontent > div.b-alert > div");
            var len = await GetFromJS<int>("$('body > div > div.b-content.clearfix > div.b-content-column > div.b-maincontent.clearfix.js-maincontent > table > tbody>tr').length");
            for (var i = 1; i <= len; i++)
            {
                var key = await GetFromJquerySelector($"body>div>div.b-content.clearfix>div.b-content-column>div.b-maincontent.clearfix.js-maincontent>table>tbody>tr:nth-child({i})>td:nth-child(1)");
                var value = await GetFromJquerySelector($"body>div>div.b-content.clearfix>div.b-content-column>div.b-maincontent.clearfix.js-maincontent>table>tbody>tr:nth-child({i})>td:nth-child(2)");
                if (key == "URI")
                {
                    methodItem.RequestUri = value;
                }
                if (key == "Протокол запроса") methodItem.SupportedProtocol = value;
                if (key == "Метод запроса") methodItem.SupportedHttpMethod = value;
            }

            var requestParamLen = await GetFromJS<int>("$('#parameters_block > table > tbody > tr').length");
            for (var i = 1; i <= requestParamLen; i++)
            {
                var requestItem = new RequestFieldItem();

                requestItem.FieldName = await GetFromJquerySelector($"#parameters_block > table > tbody > tr:nth-child({i}) > td:nth-child(1)");
                requestItem.FieldType = await GetFromJquerySelector($"#parameters_block > table > tbody > tr:nth-child({i}) > td:nth-child(2)");
                requestItem.FieldDescription = await GetFromJquerySelector($"#parameters_block > table > tbody > tr:nth-child({i}) > td:nth-child(3)");
                methodItem.RequestFields.Add(requestItem);

                AllHashTypes.Add("req: "+requestItem.FieldType);
            }

            var responseParamLen = await GetFromJS<int>("$('#response_block > table > tbody > tr').length");
            for (var i = 1; i <= responseParamLen; i++)
            {
                var fieldName = (await GetFromJquerySelector($"#response_block > table > tbody > tr:nth-child({i}) > td:nth-child(1)")).Trim();
                var fieldType = (await GetFromJquerySelector($"#response_block > table > tbody > tr:nth-child({i}) > td:nth-child(2)")).Trim();
                var fieldDescription = (await GetFromJquerySelector($"#response_block > table > tbody > tr:nth-child({i}) > td:nth-child(3)")).Trim();
                if (string.IsNullOrWhiteSpace(fieldType) && string.IsNullOrWhiteSpace(fieldName) && string.IsNullOrWhiteSpace(fieldDescription))
                {
                    continue;
                }

                

                var getKeys = GetClassKeys(fieldName);
                var responseClass = methodItem.RootResponse;
                foreach (var key in getKeys)
                    responseClass = responseClass.ResponseClasses[key];

                if (!string.IsNullOrWhiteSpace(fieldName) &&
                    string.IsNullOrWhiteSpace(fieldDescription))
                {
                    var className = GetClassName(fieldName);
                    responseClass.ResponseClasses[className] = new ResponseClass()
                    {
                        ClassName = className,
                        ClassDescription = fieldType
                    };
                }
                else
                {
                    var responseItem = new ResponseFieldItem()
                    {
                        FieldName = GetClassName(fieldName),
                        FieldType = fieldType,
                        FieldDescription = fieldDescription
                    };
                    responseClass.ResponseFieldItems.Add(responseItem);

                    AllHashTypes.Add("res: " + fieldType);
                }
                
            }

            return methodItem;
        }

        private string[] GetClassKeys(string fieldName)
        {
            var keyArray=fieldName.Split(new[] {'.'}, StringSplitOptions.RemoveEmptyEntries);
            return keyArray.Take(keyArray.Length - 1).ToArray();
        }

        public string GetClassName(string fieldName)
        {
            var keyArray = fieldName.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            return keyArray.Skip(keyArray.Length - 1).First();
        }


        private async Task<string> GetFromJquerySelector(string jquerySelector)
        {
            var result = await GetFromJS<string>($"$('{jquerySelector}').text()");
            return result?.Trim();
        }

        private async Task<T> GetFromJS<T>(string javaScript)
        {
            var selectorResult = await MyWebBrowser.EvaluateScriptAsync(javaScript);
            return (T) selectorResult.Result;
        }


    }
}
