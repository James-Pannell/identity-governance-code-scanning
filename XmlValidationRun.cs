using System.Xml;
using System.Xml.Schema;
using Microsoft.CodeAnalysis.Sarif;

namespace IdentityGovernanceCodeScanning
{
    public class XmlValidationRun : Run
    {
        private IList<Uri> _xmlUris { get; set; }
        private IList<Uri> _beanshellUris { get; set; }

        public XmlValidationRun()
        {
            _xmlUris = GetUrisByExtension("xml");
            _beanshellUris = GetUrisByExtension("bsh");

            this.Results = new List<Result>();
            this.Tool = new Tool { Driver = Driver() };
            StartTasks();
        }

        private IList<Uri> GetUrisByExtension(string extension)
        {
            string[] files = Directory.GetFiles(".", $"*.{extension}", SearchOption.AllDirectories);
            var uris = new List<Uri>();

            foreach (var file in files)
            {
                uris.Add(new Uri(Path.GetRelativePath(Environment.CurrentDirectory, file), UriKind.Relative));
            }

            return uris;
        }

        private ToolComponent Driver()
        {
            return new ToolComponent
            {
                Name = "Xml Validation",
                InformationUri = new Uri("https://github.com/James-Pannell/identity-governance-code-scanning"),
                SemanticVersion = "1.0.0",
                Rules = RuleDescriptors()
            };
        }

        private IList<ReportingDescriptor> RuleDescriptors()
        {
            return new List<ReportingDescriptor>
            {
                new ReportingDescriptor
                {
                    Id = "XML01",
                    Name = "XmlNotvalid",
                    HelpUri = new Uri("https://www.w3schools.com/xml/xml_syntax.asp"),
                    ShortDescription = new MultiformatMessageString  { Text = "XML not valid" },
                    FullDescription = new MultiformatMessageString { Text = "XML document is not valid" },
                    DefaultConfiguration = new ReportingConfiguration { Level = FailureLevel.Error }
                },
                new ReportingDescriptor
                {
                    Id = "IDG01",
                    Name = "MissingScriptAttribute",
                    HelpUri = new Uri("https://github.com/HCAIdentityDev/esaf-forms"),
                    ShortDescription = new MultiformatMessageString { Text = "Application is missing a script attribute" },
                    FullDescription = new MultiformatMessageString { Text = "Application XML should have a prescript, postscript, and provisiong script defined." },
                    DefaultConfiguration = new ReportingConfiguration { Level = FailureLevel.Error }
                },
                new ReportingDescriptor
                {
                    Id = "IDG02",
                    Name = "InconsistentScriptCasing",
                    HelpUri = new Uri("https://github.com/HCAIdentityDev/esaf-forms"),
                    ShortDescription = new MultiformatMessageString { Text = "Script attribute casing does not match script name" },
                    FullDescription = new MultiformatMessageString { Text = "The script attirbute should have casing as the file it references." },
                    DefaultConfiguration = new ReportingConfiguration { Level = FailureLevel.Error }
                }
            };
        }

        private void StartTasks()
        {
            var tasks = new List<Task>();

            foreach (var uri in _xmlUris)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await validateXml(uri);
                }));
            }

            var t = Task.WhenAll(tasks);

            try
            {
                t.Wait();
            }
            catch (XmlSchemaValidationException exception)
            {
                Console.WriteLine(exception.FormatMessage());
            }
        }

        private async Task validateXml(Uri uri)
        {
            using (var streamReader = new StreamReader(uri.GetFilePath()))
            {
                Console.WriteLine($"Validating {uri}");
                var xmlReaderSettings = new XmlReaderSettings();
                xmlReaderSettings.Async = true;
                xmlReaderSettings.DtdProcessing = DtdProcessing.Parse;

                using (var xmlReader = XmlReader.Create(streamReader, xmlReaderSettings, Environment.CurrentDirectory))
                {
                    try
                    {
                        while (await xmlReader.ReadAsync())
                        {
                            switch (xmlReader.NodeType)
                            {
                                case XmlNodeType.DocumentType:
                                    var documentType = xmlReader.Name;
                                    Console.WriteLine($"{uri}: {documentType}");
                                    if (documentType == "Application") { validateApplication(uri, xmlReader); }
                                    // if (documentType == "Applications") { ValidateApplications(uri, xmlReader); }
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    catch (XmlException exception)
                    {
                        AddResult(uri, "XML01", exception.LineNumber, exception.LinePosition, exception.Message);
                    }
                }
            }
        }

        private void validateApplication(Uri uri, XmlReader xmlReader)
        {
            xmlReader.ReadToFollowing("Application");
            var xmlInfo = (IXmlLineInfo)xmlReader;
            string id = string.Empty;
            string preScript = string.Empty;
            string postScript = string.Empty;
            string provisioningScript = string.Empty;
            int applicationAttributeLineNumber = xmlInfo.LineNumber;
            int applicationAttributeLinePosition = xmlInfo.LinePosition;


            while (xmlReader.MoveToNextAttribute())
            {
                switch (xmlReader.Name)
                {
                    case "id":
                        id = xmlReader.Value;
                        break;
                    case "preScript":
                        preScript = xmlReader.Value;
                        ValidateApplicationScript(uri, "preScript", preScript, xmlInfo.LineNumber, xmlInfo.LinePosition);
                        break;
                    case "postScript":
                        postScript = xmlReader.Value;
                        ValidateApplicationScript(uri, "postScript", postScript, xmlInfo.LineNumber, xmlInfo.LinePosition);
                        break;
                    case "provisioningScript":
                        provisioningScript = xmlReader.Value;
                        ValidateApplicationScript(uri, "provisioningScript", provisioningScript, xmlInfo.LineNumber, xmlInfo.LinePosition);
                        break;
                    default:
                        break;
                }
                    
            }

            if (preScript == string.Empty)
            {
                string message = $"{uri.GetFileName()} does not have preScript defined.";
                AddResult(uri, "IDG01", applicationAttributeLineNumber, applicationAttributeLinePosition, message);
            }

            if (postScript == string.Empty)
            {
                string message = $"{uri.GetFileName()} does not have postScript defined.";
                AddResult(uri, "IDG01", applicationAttributeLineNumber, applicationAttributeLinePosition, message);
            }

            if (provisioningScript == string.Empty)
            {
                string message = $"{uri.GetFileName()} does not have provisioningScript defined.";
                AddResult(uri, "IDG01", applicationAttributeLineNumber, applicationAttributeLinePosition, message);
            }
        }

        private void ValidateApplicationScript(Uri uri, string scriptType, string scriptName,int lineNumber, int LinePosition )
        {
            var beanshellUri = _beanshellUris.Where(x => x.GetFileName().Equals(scriptName, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();

            if (beanshellUri == null)
            {
                string message = $"{scriptType} attribute references a script that does not exist. {scriptName}";
                AddResult(uri, "IDG03" , lineNumber, LinePosition, message);
            }
            if (beanshellUri.GetFileName() != scriptName)
            {
                string message = $"{scriptType} attribute casing is not consistent with the target script's filename. {scriptName} : {beanshellUri.GetFileName()}";
                AddResult(uri, "IDG02" , lineNumber, LinePosition, message);
            }
        }

        private void AddResult(Uri uri, string ruleId, int lineNumber, int LinePosition, string message)
        {
            this.Results.Add(new Result
            {
                RuleId = ruleId,
                Message = new Message { Text = message },
                Level = FailureLevel.Error,
                Locations = new List<Location>
                {
                    new Location
                    {
                        PhysicalLocation = new PhysicalLocation
                        {
                            ArtifactLocation = new ArtifactLocation
                            {
                                Uri = uri,
                                UriBaseId = new DirectoryInfo(Environment.CurrentDirectory).Name
                            },
                            Region = new Region
                            {
                                StartLine = lineNumber,
                                StartColumn = LinePosition
                            }
                        }
                    }
                }
            });
        }
    }

}
