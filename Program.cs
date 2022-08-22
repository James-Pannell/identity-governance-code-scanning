using Microsoft.CodeAnalysis.Sarif;

namespace IdentityGovernanceCodeScanning
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string[] files = Directory.GetFiles(".", "*.xml", SearchOption.AllDirectories);
            var xmlValidationRun = new XmlValidationRun(files);
            var sarifLog = new SarifLog();

            sarifLog.SchemaUri = new Uri("https://json.schemastore.org/sarif-2.1.0.json");
            sarifLog.Version = SarifVersion.Current;
            sarifLog.Runs = new List<Run> { xmlValidationRun };

            sarifLog.Save("./identity-governance.sarif");
        }
    }
}
