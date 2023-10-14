using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource;

namespace SqPackIndexer
{
    public unsafe class ResourceManagerService
    {
        public ResourceManagerService(IGameInteropProvider interop)
            => interop.InitializeFromAttributes(this);

        /// <summary> The SE Resource Manager as pointer. </summary>
        public ResourceManager* ResourceManager
            => *ResourceManagerAddress;

        /// <summary> A static pointer to the SE Resource Manager. </summary>
        [Signature(Sigs.ResourceManager, ScanType = ScanType.StaticAddress)]
        internal readonly ResourceManager** ResourceManagerAddress = null;
    }
}