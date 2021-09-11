using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace WSLAttachSwitch.ComputeService
{
    class ComputeNetworkEndpoint : SafeHandle
    {
        [DllImport("computenetwork.dll", ExactSpelling = true, PreserveSig = false, CharSet = CharSet.Unicode)]
        private static extern void HcnOpenEndpoint(in Guid id, out ComputeNetworkEndpoint endpoint, [MarshalAs(UnmanagedType.LPWStr)] out string err);

        [DllImport("computenetwork.dll", ExactSpelling = true, PreserveSig = false, CharSet = CharSet.Unicode)]
        private static extern void HcnCreateEndpoint(ComputeNetwork network, in Guid id, string settings, out ComputeNetworkEndpoint endpoint, [MarshalAs(UnmanagedType.LPWStr)] out string err);

        [DllImport("computenetwork.dll", ExactSpelling = true, PreserveSig = false, CharSet = CharSet.Unicode)]
        private static extern void HcnEnumerateEndpoints(string query, [MarshalAs(UnmanagedType.LPWStr)] out string endpoints, [MarshalAs(UnmanagedType.LPWStr)] out string err);

        [DllImport("computenetwork.dll", ExactSpelling = true, PreserveSig = false, CharSet = CharSet.Unicode)]
        private static extern void HcnQueryEndpointProperties(ComputeNetworkEndpoint endpoint, string query, [MarshalAs(UnmanagedType.LPWStr)] out string properties, [MarshalAs(UnmanagedType.LPWStr)] out string err);

        [DllImport("computenetwork.dll", ExactSpelling = true, PreserveSig = false, CharSet = CharSet.Unicode)]
        private static extern void HcnModifyEndpoint(ComputeNetworkEndpoint endpoint, string settings, [MarshalAs(UnmanagedType.LPWStr)] out string err);

        [DllImport("computenetwork.dll", ExactSpelling = true, PreserveSig = false, CharSet = CharSet.Unicode)]
        private static extern void HcnCloseEndpoint(IntPtr endpoint);
        [DllImport("computenetwork.dll", ExactSpelling = true, PreserveSig = false, CharSet = CharSet.Unicode)]
        private static extern void HcnDeleteEndpoint(in Guid id, [MarshalAs(UnmanagedType.LPWStr)] out string err);

        public static Guid[] Enumerate()
        {
            HcnEnumerateEndpoints("", out var endpoints, out _);
            var doc = JsonSerializer.Deserialize<JsonElement>(endpoints);
            return doc.EnumerateArray().Select(x => new Guid(x.GetString())).ToArray();
        }

        public static ComputeNetworkEndpoint Open(in Guid id)
        {
            HcnOpenEndpoint(id, out var endpoint, out _);
            return endpoint;
        }

        public static ComputeNetworkEndpoint Create(ComputeNetwork network, in Guid id, object settings)
        {
            var settingsdoc = JsonSerializer.Serialize(settings);
            string err;
            HcnCreateEndpoint(network, id, settingsdoc, out var ep, out err);
            return ep;
        }

        public static void Delete(in Guid id)
        {
            HcnDeleteEndpoint(id, out _);
        }
        // for P/Invoke marshal
        private ComputeNetworkEndpoint() : base(IntPtr.Zero, true) { }

        public ComputeNetworkEndpoint(IntPtr handle, bool own_handle = false) : base(IntPtr.Zero, own_handle)
        {
            this.handle = handle;
        }

        public JsonElement QueryProperites()
        {
            HcnQueryEndpointProperties(this, "", out var response, out _);
            var doc = JsonSerializer.Deserialize<JsonElement>(response);
            return doc;
        }

        public override bool IsInvalid => handle == IntPtr.Zero;
        protected override bool ReleaseHandle()
        {
            HcnCloseEndpoint(handle);
            return true;
        }
    }
}
