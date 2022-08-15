using Microsoft.CodeAnalysis.Sarif;
using System.Text.RegularExpressions;

internal class Program
{
    private static void Main(string[] args)
    {
        String fileName = args[0];
        String xmllintErrorOutput = string.Empty;

        if (File.Exists(fileName))
        {
            xmllintErrorOutput = File.ReadAllText(fileName);
        }
        else
        {
            throw new FileNotFoundException(
                "Argument 1 must be the output of an xmllint error stream output.  Example: xmllint sample.xml 2> errors.txt");
        }

        var sarifLog = new SarifLog
        {
            SchemaUri = new Uri("https://json.schemastore.org/sarif-2.1.0.json"),
            Version = SarifVersion.Current,
            Runs =
            {
                new Run
                {
                    Tool = new Tool { Driver = identityGovernanceXmlScanDriver() },
                    Results = identityGovernanceXmlScanResults(xmllintErrorOutput)
                }
            }
        };
    }



    public static IList<Result> identityGovernanceXmlScanResults(String xmllintErrorOutput)
    {
        var results = new List<Result>();
        String pattern = @"^(.*):(\d+): (.*): (.*):(.*)\n(.*)\n(.*)\^$";

        // For each Match
        // Group 1: Filename
        // Group 2: Line Number
        // Group 3: Error Message
        // Group 4: Line of Offending Code
        // Group 5: Column marker of where line of code ends
        foreach (Match match in Regex.Matches(xmllintErrorOutput, pattern))
        {
            var fileName = match.Groups[0].Value;
            var lineNumber = match.Groups[1].Value;
            var errorNode = match.Groups[2].Value;
            var errorType = match.Groups[3].Value;
            var message = match.Groups[4].Value;
            var offendingCodeLine = match.Groups[5].Value;
            var endingColumnMarker = match.Groups[6].Value;

            String ruleId = string.Empty;

            switch (errorType.Trim())
            {
                case "parser error":
                    ruleId = "R01";
                    break;
                case "validity error":
                    ruleId = "R02";
                    break;
                default:
                    ruleId = "R99";
                    break;
            }

            var result = new Result
            {
                RuleId = ruleId,
                Message = { Text = message },
                Locations =
                {
                    new Location
                    {
                        PhysicalLocation =
                        {
                            ArtifactLocation =
                            {
                                Uri = new Uri(fileName),
                                UriBaseId = "%SRCROOT%"
                            },
                        Region =
                            {
                                StartLine = Int32.Parse(lineNumber),
                                StartColumn =  offendingCodeLine.TakeWhile(c => char.IsWhiteSpace(c)).Count(),
                                EndColumn = endingColumnMarker.Length
                            }
                        }
                    }
                }
            };

            results.Add(result);
        }

        return results;
    }

    private static ToolComponent identityGovernanceXmlScanDriver()
    {
        var reportingDescriptors = new List<ReportingDescriptor>
        {
            parseErrorDescriptor(),
            validityErrorDescriptor(),
            missingScriptErrorDescriptor()
        };

        var toolComponent = new ToolComponent
        {
            Name = "identityGovernance-form-xml-scan",
            SemanticVersion = "2.0.0",
            Rules = reportingDescriptors,
        };

        return toolComponent;
    }

    private static ReportingDescriptor parseErrorDescriptor()
    {
        var reportingDescriptor = new ReportingDescriptor
        {
            Id = "R01",
            ShortDescription = new MultiformatMessageString { Text = "Parser error" },
            FullDescription = new MultiformatMessageString { Text = "XML document has a parser error" },
            DefaultConfiguration = new ReportingConfiguration { Level = FailureLevel.Error },
        };

        return reportingDescriptor;
    }

    private static ReportingDescriptor validityErrorDescriptor()
    {
        var reportingDescriptor = new ReportingDescriptor
        {
            Id = "R02",
            ShortDescription = new MultiformatMessageString { Text = "Validity error" },
            FullDescription = new MultiformatMessageString { Text = "XML element does not follow the DTD" },
            DefaultConfiguration = new ReportingConfiguration { Level = FailureLevel.Error },
        };

        return reportingDescriptor;
    }

    private static ReportingDescriptor missingScriptErrorDescriptor()
    {
        var reportingDescriptor = new ReportingDescriptor
        {
            Id = "R03",
            ShortDescription = new MultiformatMessageString { Text = "Missing script error" },
            FullDescription = new MultiformatMessageString { Text = "Beanshell script is missing in the Scripts folder" },
            DefaultConfiguration = new ReportingConfiguration { Level = FailureLevel.Error },
        };

        return reportingDescriptor;
    }

}