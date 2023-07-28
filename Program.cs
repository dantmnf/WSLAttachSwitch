using System;
using System.CommandLine;
using System.Net.Mail;
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

        /// <summary>
        /// Attach a Hyper-V virtual switch to the WSL2 virtual machine 
        /// </summary>
        /// <param name="network">Network name or GUID. Example: Ethernet</param>
        /// <param name="macAddress">Optional. Fix physical address of network interface to this mac address if specificated. Example: 00-11-45-14-19-19</param>
        static void Main(string network, string macAddress = null, int vlanIsolationId = -1)
        {
            var status = 0;
            if (network == null)
            {
                Console.WriteLine("Usage: {0} --network <network name or GUID> [--mac-address <addr>] [--]", AppDomain.CurrentDomain.FriendlyName);
                Console.WriteLine("Check availiable networks with `hnsdiag list networks`");
                Console.WriteLine("See full help message with {0} -h", AppDomain.CurrentDomain.FriendlyName);
                status = 1;
            }
            else
            {
                if (macAddress != null)
                {
                    macAddress = macAddress.Trim().Replace(':', '-').ToLowerInvariant();
                }
                var result = Attach(network, macAddress, vlanIsolationId);
                status = result ? 0 : 1;
            }

            Environment.Exit(status);
        }
    }
}
