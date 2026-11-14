using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Mubsir.Core.Speech;

namespace Mubsir.App.Speech;

internal static unsafe partial class NvdaNative
{
    private const string Dll = "nvdaControllerClient";

    [LibraryImport(Dll)]
    public static partial uint nvdaController_testIfRunning();

    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint nvdaController_speakText(string text);

    [LibraryImport(Dll)]
    public static partial uint nvdaController_cancelSpeech();

    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint nvdaController_brailleMessage(string message);

    [LibraryImport(Dll)]
    public static partial uint nvdaController_getProcessId(out uint pid);

    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint nvdaController_speakSsml(string ssml, int symbolLevel, int priority, [MarshalAs(UnmanagedType.U1)] bool asynchronous);

    [LibraryImport(Dll)]
    public static partial uint nvdaController_setOnSsmlMarkReachedCallback(delegate* unmanaged[Stdcall]<char*, uint> callback);
}

internal sealed unsafe class NvdaController
{
    public const uint ErrorUnknownInterface = 1717;

    public const uint ErrorDllUnavailable = 126;

    private static SpeechHistory? s_markTarget;

    private volatile bool _dllMissing;
    private bool _callbackSet;

    public bool DllAvailable => !_dllMissing;

    public static void SetMarkTarget(SpeechHistory? history) => Volatile.Write(ref s_markTarget, history);

    public uint TestIfRunning() => Call(static () => NvdaNative.nvdaController_testIfRunning());

    public bool IsRunning() => TestIfRunning() == 0;

    public uint SpeakText(string text) => Call(() => NvdaNative.nvdaController_speakText(text));

    public uint CancelSpeech() => Call(static () => NvdaNative.nvdaController_cancelSpeech());

    public uint BrailleMessage(string message) => Call(() => NvdaNative.nvdaController_brailleMessage(message));

    public uint SpeakSsml(string ssml, int symbolLevel = -1, int priority = 0, bool asynchronous = false)
    {
        EnsureMarkCallback();
        return Call(() => NvdaNative.nvdaController_speakSsml(ssml, symbolLevel, priority, asynchronous));
    }

    public uint GetProcessId()
    {
        uint pid = 0;
        uint rc = Call(() => NvdaNative.nvdaController_getProcessId(out pid));
        return rc == 0 ? pid : 0;
    }

    private void EnsureMarkCallback()
    {
        if (_callbackSet || _dllMissing)
        {
            return;
        }

        uint rc = Call(static () => NvdaNative.nvdaController_setOnSsmlMarkReachedCallback(&OnMarkReached));
        _callbackSet = rc == 0;
    }

    private uint Call(Func<uint> call)
    {
        if (_dllMissing)
        {
            return ErrorDllUnavailable;
        }

        try
        {
            return call();
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            _dllMissing = true;
            return ErrorDllUnavailable;
        }
        catch (SEHException)
        {
            return ErrorDllUnavailable;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint OnMarkReached(char* mark)
    {
        try
        {
            if (mark is not null
                && Volatile.Read(ref s_markTarget) is SpeechHistory history
                && SpeechComposer.TryParseMark(new string(mark), out int index))
            {
                history.MarkSpoken(index);
            }
        }
        catch
        {
        }

        return 0;
    }
}
