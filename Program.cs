using Microsoft.CodeAnalysis.Sarif;

namespace IdentityGovernanceCodeScanning
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var xmlValidationRun = new XmlValidationRun();

            var sarifLog = new SarifLog();
            sarifLog.SchemaUri = new Uri("https://json.schemastore.org/sarif-2.1.0.json");
            sarifLog.Version = SarifVersion.Current;
            sarifLog.Runs = new List<Run> { xmlValidationRun };

            using (var streamWriter = new StreamWriter("identity-governance.sarif", false, System.Text.Encoding.UTF8))
            {
                sarifLog.Save(streamWriter);
            }
        }
    }
}
