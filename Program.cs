using System;
using System.CommandLine;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using WSLAttachSwitch.ComputeService;

namespace WSLAttachSwitch
{
    class Program
    {

        static ComputeNetwork FindNetworkByName(string name)
        {
            var networks = ComputeNetwork.Enumerate();
            foreach (var id in networks)
            {
                var network = ComputeNetwork.Open(id);
                if (name.Equals(network.QueryProperites().GetProperty("Name").GetString(), StringComparison.OrdinalIgnoreCase))
                {
                    return network;
                }
                network.Close();
            }

            Exception? marshalException = Marshal.GetExceptionForHR(unchecked((int)0x80070037));
            throw marshalException ?? new Exception("Unknown error while finding network: " + name);
        }

        static Guid XorGuid(in Guid input, ReadOnlySpan<byte> xorWith)
        {
            var guidbytes = input.ToByteArray();
            var minlen = Math.Min(xorWith.Length, guidbytes.Length);
            for (int i = 0; i < minlen; i++)
            {
                guidbytes[i] ^= xorWith[i];
            }
            return new Guid(guidbytes);
        }

        static bool Attach(string networkName, string? macAddress = null, int? vlanIsolationId = null)
        {
            try
            {
                var systems = ComputeSystem.Enumerate(new JsonObject { ["Owners"] = new JsonArray("WSL") });
                if (systems.Length != 1)
                {
                    Console.Error.WriteLine("Can't find unique WSL VM. Is WSL2 running?");
                    return false;
                }
                string? systemid = systems[0].GetProperty("Id").GetString();
                if (string.IsNullOrEmpty(systemid))
                {
                    Console.Error.WriteLine("Can't detect ID of WSL2 VM.");
                    return false;
                }
                using var system = ComputeSystem.Open(systemid);
                var props = system.QueryProperites();
                ComputeNetwork network;
                if (Guid.TryParse(networkName, out var netid))
                {
                    network = ComputeNetwork.Open(netid);
                }
                else
                {
                    network = FindNetworkByName(networkName);
                    JsonElement netprops = network.QueryProperites();
                    string? networkId = netprops.GetProperty("Id").GetString();
                    if (string.IsNullOrEmpty(networkId))
                    {
                        Console.Error.WriteLine("Can't detect network ID.");
                        return false;
                    }
                    netid = new Guid(networkId);
                }
                var epid = XorGuid(netid, "WSL2BrdgEp"u8);
                var eps = ComputeNetworkEndpoint.Enumerate();
                if (Array.Exists(eps, x => x == epid))
                {
                    using var oldendpoint = ComputeNetworkEndpoint.Open(epid);
                    var epprops = oldendpoint.QueryProperites();
                    if (!epprops.TryGetProperty("VirtualMachine", out JsonElement vmJsonElement) || vmJsonElement.GetString() != systemid)
                    {
                        // endpoint not attached to current WSL2 VM, recreate it
                        ComputeNetworkEndpoint.Delete(epid);
                    }
                    else
                    {
                        Console.WriteLine("Endpoint already attached to current WSL2 VM.");
                        return true;
                    }
                }
                JsonNode? policies = null;
                if (vlanIsolationId != null)
                {
                    policies = new JsonArray(
                        new JsonObject
                        {
                            ["Type"] = "VLAN",
                            ["Settings"] = new JsonObject { ["IsolationId"] = (int)vlanIsolationId },
                        }
                    );
                }
                using var endpoint = ComputeNetworkEndpoint.Create(network, epid, new JsonObject
                {
                    ["VirtualNetwork"] = netid.ToString(),
                    ["MacAddress"] = macAddress,
                    ["Policies"] = policies
                });
                system.Modify(
                    "VirtualMachine/Devices/NetworkAdapters/bridge_" + netid.ToString("N"),
                    ModifyRequestType.Add,
                    new JsonObject { ["EndpointId"] = epid.ToString(), ["MacAddress"] = macAddress },
                    null
                );
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return false;
            }
            return true;
        }

        static string? ParseMacAddress(string input)
        {
            if (input.Length == 17)
            {
                // XX-XX-XX-XX-XX-XX
                var sep = input[2];
                if (sep != ':' && sep != '-' && sep != '.') return null;
                for (int i = 0; i < 17; i += 1)
                {
                    if (i == 2 || i == 5 || i == 8 || i == 11 || i == 14)
                    {
                        if (input[i] != sep) return null;
                    }
                    else if (!Uri.IsHexDigit(input[i]))
                    {
                        return null;
                    }
                }
                return input.Replace(sep, '-').ToUpperInvariant();
            }
            else if (input.Length == 14)
            {
                // XXXX-XXXX-XXXX
                var sep = input[4];
                if (sep != ':' && sep != '-' && sep != '.') return null;
                for (int i = 0; i < 14; i += 1)
                {
                    if (i == 4 || i == 9)
                    {
                        if (input[i] != sep) return null;
                    }
                    else if (!Uri.IsHexDigit(input[i]))
                    {
                        return null;
                    }
                }
                return string.Format("{0}-{1}-{2}-{3}-{4}-{5}",
                    input.Substring(0, 2),
                    input.Substring(2, 2),
                    input.Substring(5, 2),
                    input.Substring(7, 2),
                    input.Substring(10, 2),
                    input.Substring(12, 2)
                ).ToUpperInvariant();
            }
            else if (input.Length == 12)
            {
                // XXXXXXXXXXXX
                for (int i = 0; i < 12; i += 1)
                {
                    if (!Uri.IsHexDigit(input[i]))
                    {
                        return null;
                    }
                }
                return string.Format("{0}-{1}-{2}-{3}-{4}-{5}",
                    input.Substring(0, 2),
                    input.Substring(2, 2),
                    input.Substring(4, 2),
                    input.Substring(6, 2),
                    input.Substring(8, 2),
                    input.Substring(10, 2)
                ).ToUpperInvariant();
            }
            return null;
        }

        static int Main(string[] args)
        {
            RootCommand command = new("Attach a Hyper-V virtual switch to the WSL2 virtual machine");

            Option<string?> macAddressOption = new("--mac") 
            {
                Description = "If specified, use this physical address for the virtual interface instead of random one.",
                Arity = ArgumentArity.ExactlyOne,
                Required = false,
                CustomParser = static result =>
                {
                    if (result.Tokens.Count == 0) 
                        return null;

                    string raw = result.Tokens.Single().Value;
                    string? mac = Program.ParseMacAddress(raw);

                    if (mac == null)
                            result.AddError("Invalid MAC address");

                    return mac;
                }
            };

            Option<int?> vlanIdOption = new ("--vlan") 
            {
                Description = "If specified, enable VLAN filtering with this VLAN ID for the virtual interface.",
                Required = false,
                Arity = ArgumentArity.ExactlyOne,
                Validators = { 
                    static result => 
                    {
                        int? vlanid = result.GetValueOrDefault<int?>();
                        if (vlanid != null && (vlanid < 0 || vlanid > 4095))
                        {
                            result.AddError("VLAN ID must be between 0 and 4095");
                        }
                    }
                }
            };

            Argument<string> networkArg = new("network name or GUID") 
            {
                Description = "Name or GUID of the virtual switch to attach to the WSL2 virtual machine. Check availiable networks with `hnsdiag list networks`",
                Arity = ArgumentArity.ExactlyOne
            };

            command.Add(macAddressOption);
            command.Add(vlanIdOption);
            command.Add(networkArg);

            int exitCode = 0;

            command.SetAction(parseResult =>
            {
                exitCode = Attach(parseResult.GetRequiredValue<string>(networkArg), parseResult.GetValue<string?>(macAddressOption), parseResult.GetValue<int?>(vlanIdOption)) ? 0 : 1;

            });

            ParseResult parseResult = command.Parse(args);
            parseResult.Invoke();

            return exitCode;
        }
    }
}
