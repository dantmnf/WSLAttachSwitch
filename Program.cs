using System;
using System.Runtime.InteropServices;
using System.Text;
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

        static bool Attach(string networkName)
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
                }
                var netprops = network.QueryProperites();
                netid = new Guid(netprops.GetProperty("ID").GetString());
                var epid = XorGuid(netid, Encoding.UTF8.GetBytes("WSL2BrdgEp"));
                var eps = ComputeNetworkEndpoint.Enumerate();
                if (Array.Exists(eps, x => x == epid))
                {
                    using var oldendpoint = ComputeNetworkEndpoint.Open(epid);
                    var epprops = oldendpoint.QueryProperites();
                    if (epprops.GetProperty("VirtualMachine").GetString() != systemid)
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
                using var endpoint = ComputeNetworkEndpoint.Create(network, epid, new { VirtualNetwork = netid.ToString() });
                system.Modify("VirtualMachine/Devices/NetworkAdapters/bridge_" + netid.ToString("N"), ModifyRequestType.Add, new { EndpointId = epid.ToString() }, null);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return false;
            }
            return true;
        }

        static void Main(string[] args)
        {
            var status = 0;
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: {0} <network name or GUID>", AppDomain.CurrentDomain.FriendlyName);
                Console.WriteLine("Check Check availiable networks with `hnsdiag list networks`");
                status = 1;
            }
            else
            {
                var result = Attach(args[0]);
                status = result ? 0 : 1;
            }
            Environment.Exit(status);
        }
    }
}
