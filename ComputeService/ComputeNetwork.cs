using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace WSLAttachSwitch.ComputeService
{
    class ComputeNetwork : SafeHandle
    {
        [DllImport("computenetwork.dll", ExactSpelling = true, PreserveSig = false, CharSet = CharSet.Unicode)]
        private static extern void HcnOpenNetwork(in Guid id, out ComputeNetwork network, [MarshalAs(UnmanagedType.LPWStr)] out string err);

        [DllImport("computenetwork.dll", ExactSpelling = true, PreserveSig = false, CharSet = CharSet.Unicode)]
        private static extern void HcnEnumerateNetworks(string query, [MarshalAs(UnmanagedType.LPWStr)] out string networks, [MarshalAs(UnmanagedType.LPWStr)] out string err);

        [DllImport("computenetwork.dll", ExactSpelling = true, PreserveSig = false, CharSet = CharSet.Unicode)]
        private static extern void HcnQueryNetworkProperties(ComputeNetwork network, string query, [MarshalAs(UnmanagedType.LPWStr)] out string properties, [MarshalAs(UnmanagedType.LPWStr)] out string err);

        [DllImport("computenetwork.dll", ExactSpelling = true, PreserveSig = false, CharSet = CharSet.Unicode)]
        private static extern void HcnCloseNetwork(IntPtr network);


        private ComputeNetwork() : base(IntPtr.Zero, true) { }


        public static Guid[] Enumerate()
        {
            HcnEnumerateNetworks("", out var networks, out _);
            var doc = JsonDocument.Parse(networks);
            return doc.RootElement.EnumerateArray().Select(x => new Guid(x.GetString())).ToArray();
        }


        public static ComputeNetwork Open(in Guid id)
        {
            HcnOpenNetwork(id, out var network, out _);
            return network;
        }
        public JsonElement QueryProperites()
        {
            HcnQueryNetworkProperties(this, "", out var response, out _);
            var doc = JsonDocument.Parse(response);
            return doc.RootElement;
        }

        public override bool IsInvalid => handle == IntPtr.Zero;
        protected override bool ReleaseHandle()
        {
            Debug.WriteLine("ComputeNetwork.ReleaseHandle");
            HcnCloseNetwork(handle);
            handle = IntPtr.Zero;
            return true;
        }
    }
}
