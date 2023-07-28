using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WSLAttachSwitch.ComputeService
{
    public enum ModifyRequestType
    {
        Add,
        Remove,
        Update,
    }

    class ComputeSystem : SafeHandle
    {
        [DllImport("computecore.dll", ExactSpelling = true, PreserveSig = false, CharSet = CharSet.Unicode)]
        private static extern void HcsEnumerateComputeSystems(string query, HcsOperation operation);

        [DllImport("computecore.dll", ExactSpelling = true, PreserveSig = false, CharSet = CharSet.Unicode)]
        private static extern void HcsOpenComputeSystem(string id, int access, out ComputeSystem computeSystem);

        [DllImport("computecore.dll", ExactSpelling = true)]
        private static extern void HcsCloseComputeSystem(IntPtr computeSystem);

        [DllImport("computecore.dll", ExactSpelling = true, PreserveSig = false, CharSet = CharSet.Unicode)]
        private static extern void HcsGetComputeSystemProperties(ComputeSystem computeSystem, HcsOperation operation, string propertyQuery);

        [DllImport("computecore.dll", ExactSpelling = true, PreserveSig = false, CharSet = CharSet.Unicode)]
        private static extern void HcsModifyComputeSystem(ComputeSystem computeSystem, HcsOperation operation, string configuration, IntPtr identity);


        public static JsonElement[] Enumerate(JsonObject query = null)
        {
            using var op = new HcsOperation();
            var querydoc = query?.ToJsonString();
            HcsEnumerateComputeSystems(querydoc, op);
            var resultdoc = op.Result;
            var doc = JsonDocument.Parse(resultdoc);
            return doc.RootElement.EnumerateArray().ToArray();
        }

        public static ComputeSystem Open(string id)
        {
            HcsOpenComputeSystem(id, 0x10000000, out var handle);
            return handle;
        }

        private IntPtr handleValue;
        public IntPtr Handle => handleValue;
        private bool own_handle;
        public ComputeSystem() : base(IntPtr.Zero, true) { }

        public JsonElement QueryProperites()
        {
            using var op = new HcsOperation();
            HcsGetComputeSystemProperties(this, op, null);
            var resultdoc = op.Result;
            var doc = JsonDocument.Parse(resultdoc);
            return doc.RootElement;
        }

        public JsonElement QueryProperites(JsonObject query)
        {
            using var op = new HcsOperation();
            var querydoc = query?.ToJsonString();
            HcsGetComputeSystemProperties(this, op, querydoc);
            var resultdoc = op.Result;
            var doc = JsonDocument.Parse(resultdoc);
            return doc.RootElement;
        }

        public string Modify(string resourcePath, ModifyRequestType requestType, JsonObject settings, JsonObject guestRequest)
        {
            var request = new JsonObject
            {
                ["ResourcePath"] = resourcePath,
                ["RequestType"] = requestType.ToString(),
                ["Settings"] = settings,
                ["GuestRequest"] = guestRequest,
            };
            using var op = new HcsOperation();
            var querydoc = request.ToJsonString();
            HcsModifyComputeSystem(this, op, querydoc, IntPtr.Zero);
            var resultdoc = op.Result;
            return resultdoc;
        }

        public override bool IsInvalid => handle == IntPtr.Zero;
        protected override bool ReleaseHandle()
        {
            HcsCloseComputeSystem(handle);
            return true;
        }
    }
}
