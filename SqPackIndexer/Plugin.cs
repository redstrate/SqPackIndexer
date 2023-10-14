using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace SqPackIndexer
{
    public sealed class Plugin : IDalamudPlugin
    {
        private DalamudPluginInterface PluginInterface { get; init; }
        private readonly FileReadService _fileReadService;
        private readonly ResourceManagerService _resourceManager;
        
        [PluginService] internal static IGameInteropProvider Hooking { get; private set; } = null!;

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
        {
            PluginInterface = pluginInterface;
            _resourceManager = new ResourceManagerService(Hooking);
            _fileReadService = new FileReadService(_resourceManager, Hooking);
        }

        public void Dispose()
        {
        }
    }
}
