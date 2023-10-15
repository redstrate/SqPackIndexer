using System;
using System.IO.Enumeration;
using System.Net.Http;
using System.Text;
using System.Threading;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using Newtonsoft.Json;

namespace SqPackIndexer;

public class ApiRequest
{
    public string filename;
}

public class HttpHelper
{
    private static HttpClient sharedClient = new()
    {
        BaseAddress = new Uri("http://127.0.0.1:3500"),
    };

    public async void sendFilename(String filename)
    {
        using StringContent jsonContent = new(JsonConvert.SerializeObject(new
                ApiRequest {
                    filename = filename
                }),
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response = await sharedClient.PostAsync(
            "add_hash",
            jsonContent);
    }
}

public unsafe class FileReadService : IDisposable
{
    public FileReadService(ResourceManagerService resourceManager, IGameInteropProvider interop)
    {
        _resourceManager = resourceManager;
        interop.InitializeFromAttributes(this);
        _readSqPackHook.Enable();
        _httpHelper = new HttpHelper();
    }

    /// <summary> Invoked when a file is supposed to be read from SqPack. </summary>
    /// <param name="fileDescriptor">The file descriptor containing what file to read.</param>
    /// <param name="priority">The games priority. Should not generally be changed.</param>
    /// <param name="isSync">Whether the file needs to be loaded synchronously. Should not generally be changed.</param>
    /// <param name="returnValue">The return value. If this is set, original will not be called.</param>
    public delegate void ReadSqPackDelegate(SeFileDescriptor* fileDescriptor, ref int priority, ref bool isSync, ref byte? returnValue);

    /// <summary>
    /// <inheritdoc cref="ReadSqPackDelegate"/> <para/>
    /// Subscribers should be exception-safe.
    /// </summary>
    public event ReadSqPackDelegate? ReadSqPack;

    /// <summary>
    /// Use the games ReadFile function to read a file from the hard drive instead of an SqPack.
    /// </summary>
    /// <param name="fileDescriptor">The file to load.</param>
    /// <param name="priority">The games priority.</param>
    /// <param name="isSync">Whether the file needs to be loaded synchronously.</param>
    /// <returns>Unknown, not directly success/failure.</returns>
    public byte ReadFile(SeFileDescriptor* fileDescriptor, int priority, bool isSync)
        => _readFile.Invoke(GetResourceManager(), fileDescriptor, priority, isSync);

    public byte ReadDefaultSqPack(SeFileDescriptor* fileDescriptor, int priority, bool isSync)
        => _readSqPackHook.Original(GetResourceManager(), fileDescriptor, priority, isSync);

    public void Dispose()
    {
        _readSqPackHook.Dispose();
    }

    private readonly ResourceManagerService _resourceManager;

    private delegate byte ReadSqPackPrototype(nint resourceManager, SeFileDescriptor* pFileDesc, int priority, bool isSync);

    [Signature(Sigs.ReadSqPack, DetourName = nameof(ReadSqPackDetour))]
    private readonly Hook<ReadSqPackPrototype> _readSqPackHook = null!;

    private readonly HttpHelper _httpHelper;
    
    private byte ReadSqPackDetour(nint resourceManager, SeFileDescriptor* fileDescriptor, int priority, bool isSync)
    {
        Dalamud.Logging.PluginLog.Log("Got detour: " + fileDescriptor->ResourceHandle->FileName);

        _httpHelper.sendFilename(fileDescriptor->ResourceHandle->FileName.ToString());
        
        byte?     ret         = null;
        _lastFileThreadResourceManager.Value = resourceManager;
        ReadSqPack?.Invoke(fileDescriptor, ref priority, ref isSync, ref ret);
        _lastFileThreadResourceManager.Value = IntPtr.Zero;
        return ret ?? _readSqPackHook.Original(resourceManager, fileDescriptor, priority, isSync);
    }

    private delegate byte ReadFileDelegate(nint resourceManager, SeFileDescriptor* fileDescriptor, int priority,
        bool isSync);

    /// We need to use the ReadFile function to load local, uncompressed files instead of loading them from the SqPacks.
    [Signature(Sigs.ReadFile)]
    private readonly ReadFileDelegate _readFile = null!;

    private readonly ThreadLocal<nint> _lastFileThreadResourceManager = new(true);

    /// <summary>
    /// Usually files are loaded using the resource manager as a first pointer, but it seems some rare cases are using something else.
    /// So we keep track of them per thread and use them.
    /// </summary>
    private nint GetResourceManager()
        => !_lastFileThreadResourceManager.IsValueCreated || _lastFileThreadResourceManager.Value == IntPtr.Zero
            ? (nint)_resourceManager.ResourceManager
            : _lastFileThreadResourceManager.Value;
}
