using Microsoft.CodeAnalysis.Sarif;
using System.Xml;
using System.Xml.Schema;
using System.Text.Encodings;
using System.Text;

namespace IdentityGovernanceCodeScanning
{
    public class XmlValidationRun : Run
    {
        private IList<string> _xmlFileLocations { get; set; }

        public XmlValidationRun(IList<string> xmlFileLocations)
        {
            _xmlFileLocations = xmlFileLocations;
            this.Results = new List<Result>();
            this.Tool = new Tool();
            this.Tool.Driver = new ToolComponent
            {
                Name = "Xml Validation",
                Rules = new List<ReportingDescriptor>
                {
                    new ReportingDescriptor
                    {
                        Id = "X01",
                        ShortDescription = new MultiformatMessageString { Text = "Invalid XML" },
                        FullDescription = new MultiformatMessageString { Text = "XML document is not well formed or valid" },
                        DefaultConfiguration = new ReportingConfiguration { Level = FailureLevel.Error }
                    }
                }
            };

            StartTasks();
        }

        private void StartTasks()
        {
            var tasks = new List<Task>();

            foreach (var xmlFileLocation in _xmlFileLocations)
            {
                tasks.Add(Task.Run(async () => { await validateXml(xmlFileLocation); }));
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

            if (t.Status == TaskStatus.RanToCompletion)
            {
                Console.WriteLine("All validations completed");
            }

            if (t.Status == TaskStatus.Faulted)
            {
                Console.WriteLine($"At least one task failed");
            }
        }

        private async Task validateXml(string xmlFileLocation)
        {
            var xmlReaderSettings = new XmlReaderSettings();
            xmlReaderSettings.CheckCharacters = true;
            xmlReaderSettings.ConformanceLevel = ConformanceLevel.Auto;
            xmlReaderSettings.DtdProcessing = DtdProcessing.Parse;
            //xmlReaderSettings.ValidationEventHandler += new ValidationEventHandler(validationCallBack);
            xmlReaderSettings.ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings;
            xmlReaderSettings.ValidationType = ValidationType.DTD;
            xmlReaderSettings.Async = true;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding.GetEncoding("windows-1254");


            using (var fileStream = File.Open(xmlFileLocation, FileMode.Open, FileAccess.Read))
            {
                Console.WriteLine($"Validating {xmlFileLocation}");
                using (var reader = XmlReader.Create(fileStream, xmlReaderSettings))
                {
                    try
                    {
                        await reader.ReadAsync();
                    }
                    catch (XmlException exception)
                    {
                        AddXmlException(exception, xmlFileLocation);
                    }
                }
            }
        }

        void AddXmlException(XmlException exception, String xmlFileLocation)
        {
            var artifactionLocation = new ArtifactLocation();

            if (exception.SourceUri != null)
            {
                artifactionLocation.Uri = new Uri(xmlFileLocation, UriKind.Relative);
            }

            this.Results.Add(new Result
            {
                RuleId = "X01",
                Message = new Message { Text = exception.Message },
                Level = FailureLevel.Error,
                Locations = new List<Location>
                {
                    new Location
                    {
                        PhysicalLocation = new PhysicalLocation
                        {
                            ArtifactLocation = artifactionLocation,
                            Region = new Region
                            {
                                StartLine = exception.LineNumber,
                                StartColumn = exception.LinePosition
                            }
                        }
                    }
                }

            });
        }
    }
}