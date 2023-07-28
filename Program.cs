using System;
using System.CommandLine;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
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
            throw Marshal.GetExceptionForHR(unchecked((int)0x80070037));
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

        static bool Attach(string networkName, string macAddress = null, int vlanIsolationId = -1)
        {
            try
            {
                var systems = ComputeSystem.Enumerate(new { Owners = new[] { "WSL" } });
                if (systems.Length != 1)
                {
                    Console.Error.WriteLine("Can't find unique WSL VM. Is WSL2 running?");
                    return false;
                }
                var systemid = systems[0].GetProperty("Id").GetString();
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
                    var netprops = network.QueryProperites();
                    netid = new Guid(netprops.GetProperty("ID").GetString());
                }
                var epid = XorGuid(netid, Encoding.UTF8.GetBytes("WSL2BrdgEp"));
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
                object policies = null;
                if (vlanIsolationId >= 0)
                {
                    policies = new object[] {
                        new
                        {
                            Type = "VLAN",
                            Settings = new { IsolationId = (uint) vlanIsolationId },
                        }
                    };
                }
                using var endpoint = ComputeNetworkEndpoint.Create(network, epid, new {
                    VirtualNetwork = netid.ToString(),
                    MacAddress = macAddress,
                    Policies = policies
                });
                system.Modify(
                    "VirtualMachine/Devices/NetworkAdapters/bridge_" + netid.ToString("N"),
                    ModifyRequestType.Add,
                    new { EndpointId = epid.ToString(), MacAddress = macAddress },
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

        static string ParseMacAddress(string input)
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
            var command = new RootCommand("Attach a Hyper-V virtual switch to the WSL2 virtual machine");
            var macAddressOption = new Option<string>(
                name: "--mac",
                description: "If specified, use this physical address for the virtual interface instead of random one.",
                parseArgument: static result =>
                {
                    if (result.Tokens.Count == 0) return null;
                    var raw = result.Tokens.Single().Value;
                    var mac = Program.ParseMacAddress(raw);
                    if (mac == null) result.ErrorMessage = "Invalid MAC address";
                    return mac;
                });
            var vlanIdOption = new Option<int?>("--vlan", "If specified, enable VLAN filtering with this VLAN ID for the virtual interface.");
            vlanIdOption.AddValidator(static result =>
            {
                var vlanid = result.GetValueOrDefault<int?>();
                if (vlanid != null && (vlanid < 0 || vlanid > 4095))
                {
                    result.ErrorMessage = "VLAN ID must be between 0 and 4095";
                }
            });
            var networkArg = new Argument<string>("network name or GUID", "Name or GUID of the virtual switch to attach to the WSL2 virtual machine. Check availiable networks with `hnsdiag list networks`");

            command.AddOption(macAddressOption);
            command.AddOption(vlanIdOption);
            command.AddArgument(networkArg);
            var status = 0;

            command.SetHandler((network, macAddress, vlanId) =>
            {
                status = Attach(network, macAddress, vlanId ?? -1) ? 0 : 1;

            }, networkArg, macAddressOption, vlanIdOption);

            command.Invoke(args);
            return status;
        }
    }
}
