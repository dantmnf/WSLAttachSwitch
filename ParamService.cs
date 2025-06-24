using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WSLAttachSwitch
{
    //This is required due to AOT compilation and assembly trimming:
    [JsonSerializable(typeof(Params))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    public partial class JsonContext : JsonSerializerContext
    {
    }

    public record Params(string Network, string? MacAddress, int? Vlan)
    {
        public override string ToString()
        {
            return $"Network: {Network}, MAC: {MacAddress}, VLAN: {Vlan}";
        }

        public bool AreValid()
        {
            return !string.IsNullOrEmpty(Network);
        }
    }

    internal class ParamService
    {
        private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"WSLAttachSwitch");
        private static readonly string ParamsFile = Path.Combine(AppFolder, "params.json");

        public static bool Save(Params parameters)
        {
            try
            {
                Directory.CreateDirectory(AppFolder);
                string json = JsonSerializer.Serialize(parameters, JsonContext.Default.Params);
                File.WriteAllText(ParamsFile, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryLoad(out Params parameters)
        {
            try
            {
                if (File.Exists(ParamsFile))
                {
                    var json = File.ReadAllText(ParamsFile);
                    parameters = JsonSerializer.Deserialize<Params>(json, JsonContext.Default.Params) ?? new Params(string.Empty, null, null);
                    Console.WriteLine($"Loaded parameters: {parameters}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error while loading saved parameters: {ex.Message}");
            }

            parameters = new Params(string.Empty, null, null);
            return false;
        }

        public static bool GetOrLoadParams(string? network, string? macAddress, int? vlanId, out Params paramsToBeUsed)
        {
            Params passedParams = new(network ?? string.Empty, macAddress, vlanId);
            if (passedParams.AreValid())
            {
                paramsToBeUsed = passedParams;
                return true;
            }
            else
            {
                if (TryLoad(out Params? savedParams) && savedParams.AreValid())
                {
                    paramsToBeUsed = savedParams;
                    return true;
                }
                else
                {
                    Console.Error.WriteLine("No network specified and no saved parameters found. Use --network option to specify a network.");
                    paramsToBeUsed = passedParams;
                    return false;
                }
            }
        }
    }
}
